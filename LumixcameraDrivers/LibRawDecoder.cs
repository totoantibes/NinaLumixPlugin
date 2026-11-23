using NINA.Core.Enum;
using NINA.Core.Utility;
using NINA.Image.ImageData;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Roberthasson.NINA.Lumixcamera.LumixcameraDrivers {

    /// <summary>
    /// Optional in-plugin RW2 decoder backed by a bundled LibRaw 0.22 DLL (<c>libraw.dll</c>, official
    /// libraw.org Win64 build, LGPL 2.1 / CDDL 1.0).
    ///
    /// Why: N.I.N.A. 3.2 and earlier convert DSLR RAW with DCRaw / FreeImage, whose embedded LibRaw is
    /// years old and does not know newer Lumix bodies (GH7, S5 II, S9, G9 II ...) — the preview is pure
    /// noise (issue #1). N.I.N.A. 3.3 ships its own LibRaw 0.22 and needs none of this. So this path is
    /// OFF by default and only used when the user enables "Use built-in LibRaw decoder" in the options.
    ///
    /// The unpacked CFA frame is handed to N.I.N.A. as a bayered 16-bit array with the Bayer pattern
    /// read from LibRaw, so it flows through N.I.N.A.'s normal debayer pipeline like any other RAW.
    /// Any failure (DLL missing, unsupported file, layout mismatch) is reported to the caller, which
    /// falls back to N.I.N.A.'s own converter — enabling the option can never make things worse.
    ///
    /// Modelled on N.I.N.A.'s LibRawConverter (NINA.Image/RawConverter, MPL-2.0, © Stefan Berg and the
    /// N.I.N.A. contributors). LibRaw C API: https://www.libraw.org/docs/API-C.html
    /// </summary>
    public static class LibRawDecoder {
        public const string DllName = "libraw.dll";

        // Struct offsets for LibRaw 0.22.x on x64 (verified with offsetof() against libraw_types.h of
        // 0.22.1 and 0.22.2 — identical — and matching N.I.N.A.'s constants). Not every needed field has
        // a C getter (raw_image, margins), hence the direct reads. See VerifyLayout() for the runtime guard.
        private const int ImageSizesOffset = 8;            // libraw_data_t.sizes
        private const int RawDataOffset = 193768;          // libraw_data_t.rawdata
        private const int RawImageOffset = 8;              // libraw_rawdata_t.raw_image
        private const int RawDataSizesOffset = 512;        // libraw_rawdata_t.sizes (a copy of the above)
        private const int ImageParamsColorsOffset = 340;   // libraw_iparams_t.colors
        private const int ImageParamsFiltersOffset = 344;  // libraw_iparams_t.filters
        private const int ImageParamsColorDescOffset = 420;// libraw_iparams_t.cdesc[5]
        private const uint LeafCatchlightFilters = 1;
        private const uint XTransFilters = 9;

        private static readonly object _loadLock = new object();
        private static bool _loaded;
        private static string _loadError;

        /// <summary>Result of a successful decode: a cropped, unpacked CFA frame.</summary>
        public sealed class DecodedFrame {
            public ushort[] Pixels;
            public int Width;
            public int Height;
            /// <summary>Bit depth to report to N.I.N.A. (16 when bit-scaled, else the effective RAW depth).</summary>
            public int BitDepth;
            /// <summary>Bayer pattern of pixel (0,0) of the cropped frame, or Monochrome if LibRaw gave none.</summary>
            public SensorType Pattern;
            public ushort MaxPixelValue;
            public string LibRawVersion;
        }

        /// <summary>Full path of the bundled DLL next to the plugin assembly (or in its Library sub-folder).</summary>
        public static string DllPath {
            get {
                string dir;
                try {
                    var loc = Assembly.GetExecutingAssembly().Location;
                    dir = string.IsNullOrEmpty(loc) ? AppContext.BaseDirectory : Path.GetDirectoryName(loc);
                } catch { dir = AppContext.BaseDirectory; }
                foreach (var candidate in new[] { Path.Combine(dir, DllName), Path.Combine(dir, "Library", DllName) }) {
                    if (File.Exists(candidate)) return candidate;
                }
                return Path.Combine(dir, DllName);
            }
        }

        /// <summary>Load the DLL once. Returns false (with a reason) when it is missing or won't load.</summary>
        public static bool EnsureLoaded(out string error) {
            lock (_loadLock) {
                if (_loaded) { error = null; return true; }
                if (_loadError != null) { error = _loadError; return false; }
                var path = DllPath;
                if (!File.Exists(path)) {
                    _loadError = $"{DllName} not found at '{path}'";
                } else if (!NativeLibrary.TryLoad(path, out _)) {
                    _loadError = $"{DllName} at '{path}' could not be loaded (missing VC++ runtime?)";
                } else {
                    try {
                        var v = Marshal.PtrToStringAnsi(Native.Version());
                        Logger.Info($"[LibRaw] loaded {path} (LibRaw {v})");
                        _loaded = true;
                    } catch (Exception ex) {
                        _loadError = $"{DllName} loaded but libraw_version() failed: {ex.Message}";
                    }
                }
                error = _loadError;
                return _loaded;
            }
        }

        /// <summary>
        /// Decode a RAW buffer. <paramref name="configuredBitDepth"/> is the driver's bit depth setting
        /// (used as the starting point; raised if the data needs more bits). <paramref name="bitScaling"/>
        /// mirrors N.I.N.A.'s camera "bit scaling" profile option (shift up to 16 bits).
        /// </summary>
        public static bool TryDecode(byte[] rawBytes, int configuredBitDepth, bool bitScaling, out DecodedFrame frame, out string error) {
            frame = null;
            if (!EnsureLoaded(out error)) { return false; }
            if (rawBytes == null || rawBytes.Length < 16) { error = "empty RAW buffer"; return false; }

            var handle = GCHandle.Alloc(rawBytes, GCHandleType.Pinned);
            var processor = IntPtr.Zero;
            try {
                processor = Native.Init(0);
                if (processor == IntPtr.Zero) { error = "libraw_init failed"; return false; }

                if (!Ok(Native.OpenBuffer(processor, handle.AddrOfPinnedObject(), (UIntPtr)rawBytes.Length), "libraw_open_buffer", out error)) { return false; }
                if (!Ok(Native.Unpack(processor), "libraw_unpack", out error)) { return false; }

                var sizes = Marshal.PtrToStructure<ImageSizes>(IntPtr.Add(processor, ImageSizesOffset));
                if (!VerifyLayout(processor, sizes, out error)) { return false; }

                var rawImage = Marshal.ReadIntPtr(processor, RawDataOffset + RawImageOffset);
                if (rawImage == IntPtr.Zero) { error = "LibRaw did not produce an unpacked CFA (ushort) image — not a Bayer RAW?"; return false; }

                var active = GetActiveFrame(sizes, out error);
                if (active == null) { return false; }

                var result = CopyFrame(rawImage, active, configuredBitDepth, bitScaling);
                result.Pattern = ReadBayerPattern(processor);
                result.LibRawVersion = Marshal.PtrToStringAnsi(Native.Version());
                frame = result;
                error = null;
                return true;
            } catch (Exception ex) {
                error = ex.Message;
                return false;
            } finally {
                if (processor != IntPtr.Zero) { try { Native.Close(processor); } catch { } }
                if (handle.IsAllocated) { handle.Free(); }
            }
        }

        /// <summary>
        /// Guard against a DLL whose struct layout differs from the offsets above: after unpack, LibRaw
        /// keeps a copy of the image sizes inside rawdata. If the copy at our RawDataOffset does not match
        /// the primary sizes, the offsets are wrong for this DLL and reading raw_image would be garbage.
        /// </summary>
        private static bool VerifyLayout(IntPtr processor, ImageSizes sizes, out string error) {
            error = null;
            if (sizes.RawWidth == 0 || sizes.RawHeight == 0) { error = "LibRaw reported zero RAW dimensions"; return false; }
            var copy = Marshal.PtrToStructure<ImageSizes>(IntPtr.Add(processor, RawDataOffset + RawDataSizesOffset));
            bool same = copy.RawHeight == sizes.RawHeight && copy.RawWidth == sizes.RawWidth
                     && copy.Height == sizes.Height && copy.Width == sizes.Width
                     && copy.TopMargin == sizes.TopMargin && copy.LeftMargin == sizes.LeftMargin
                     && copy.RawPitch == sizes.RawPitch;
            if (!same) {
                error = $"LibRaw struct layout mismatch (bundled DLL is not the 0.22.x x64 build these offsets were written for) — LibRaw {Marshal.PtrToStringAnsi(Native.Version())}";
                return false;
            }
            return true;
        }

        private sealed class ActiveFrame { public int Left, Top, Width, Height, RowStride; }

        private static ActiveFrame GetActiveFrame(ImageSizes s, out string error) {
            error = null;
            int sourceWidth = s.RawWidth, sourceHeight = s.RawHeight;
            int rowStride = s.RawPitch > 0 ? Math.Max(sourceWidth, (int)(s.RawPitch / sizeof(ushort))) : sourceWidth;
            int width = FirstPositive(s.Width, s.IWidth, s.RawWidth);
            int height = FirstPositive(s.Height, s.IHeight, s.RawHeight);
            int left = Math.Min((int)s.LeftMargin, rowStride);
            int top = Math.Min((int)s.TopMargin, sourceHeight);
            width = Math.Min(width, rowStride - left);
            height = Math.Min(height, sourceHeight - top);
            if (width <= 0 || height <= 0 || rowStride <= 0) { error = "LibRaw returned invalid RAW image dimensions"; return null; }
            return new ActiveFrame { Left = left, Top = top, Width = width, Height = height, RowStride = rowStride };
        }

        private static int FirstPositive(params ushort[] values) {
            foreach (var v in values) { if (v > 0) return v; }
            return 0;
        }

        /// <summary>Copy the visible area out of LibRaw's buffer; two passes keep the bit-depth logic simple.</summary>
        private static unsafe DecodedFrame CopyFrame(IntPtr image, ActiveFrame f, int configuredBitDepth, bool bitScaling) {
            var pixels = new ushort[f.Width * f.Height];
            var src = (ushort*)image.ToPointer();
            ushort max = 0;
            fixed (ushort* dst = pixels) {
                for (int y = 0; y < f.Height; y++) {
                    ushort* srow = src + ((f.Top + y) * f.RowStride) + f.Left;
                    ushort* drow = dst + (y * f.Width);
                    for (int x = 0; x < f.Width; x++) {
                        ushort v = srow[x];
                        if (v > max) max = v;
                        drow[x] = v;
                    }
                }
            }

            // The configured depth is a floor; the data itself is a hard lower bound (a 14-bit setting must
            // not truncate a body that really delivers 16-bit values).
            int effective = (configuredBitDepth >= 1 && configuredBitDepth <= 16) ? configuredBitDepth : 16;
            int required = RequiredBits(max);
            if (required > effective) { effective = required; }

            int shift = bitScaling ? 16 - effective : 0;
            if (shift > 0) {
                for (int i = 0; i < pixels.Length; i++) { pixels[i] = (ushort)(pixels[i] << shift); }
            }

            return new DecodedFrame {
                Pixels = pixels, Width = f.Width, Height = f.Height,
                BitDepth = bitScaling ? 16 : effective, MaxPixelValue = max
            };
        }

        private static int RequiredBits(ushort value) {
            int bits = 0;
            do { bits++; value >>= 1; } while (value > 0);
            return bits;
        }

        /// <summary>
        /// Bayer pattern of the cropped frame's pixel (0,0). LibRaw's COLOR(row,col) is defined relative to
        /// the visible area (after top/left margins), which is exactly what CopyFrame produced.
        /// </summary>
        private static SensorType ReadBayerPattern(IntPtr processor) {
            var ip = Native.GetImageParams(processor);
            if (ip == IntPtr.Zero) return SensorType.Monochrome;
            int colors = Marshal.ReadInt32(ip, ImageParamsColorsOffset);
            uint filters = (uint)Marshal.ReadInt32(ip, ImageParamsFiltersOffset);
            if (colors < 3 || filters == 0 || filters == LeafCatchlightFilters || filters == XTransFilters) return SensorType.Monochrome;

            var cdesc = new byte[5];
            Marshal.Copy(IntPtr.Add(ip, ImageParamsColorDescOffset), cdesc, 0, cdesc.Length);
            var pattern = new char[4];
            int i = 0;
            for (int row = 0; row < 2; row++) {
                for (int col = 0; col < 2; col++) {
                    int ci = Native.Color(processor, row, col);
                    if (ci < 0 || ci >= cdesc.Length || cdesc[ci] == 0) return SensorType.Monochrome;
                    pattern[i++] = char.ToUpperInvariant((char)cdesc[ci]);
                }
            }
            switch (new string(pattern)) {
                case "RGGB": return SensorType.RGGB;
                case "BGGR": return SensorType.BGGR;
                case "GBRG": return SensorType.GBRG;
                case "GRBG": return SensorType.GRBG;
                case "GRGB": return SensorType.GRGB;
                case "GBGR": return SensorType.GBGR;
                case "RGBG": return SensorType.RGBG;
                case "BGRG": return SensorType.BGRG;
                default: return SensorType.Monochrome;
            }
        }

        private static bool Ok(int rc, string op, out string error) {
            if (rc == 0) { error = null; return true; }
            string msg = null;
            try { msg = Marshal.PtrToStringAnsi(Native.StrError(rc)); } catch { }
            error = $"{op} failed: {(string.IsNullOrWhiteSpace(msg) ? "LibRaw error " + rc : msg)}";
            return false;
        }

        [StructLayout(LayoutKind.Explicit, Size = 184)]
        private struct ImageSizes {
            [FieldOffset(0)] public ushort RawHeight;
            [FieldOffset(2)] public ushort RawWidth;
            [FieldOffset(4)] public ushort Height;
            [FieldOffset(6)] public ushort Width;
            [FieldOffset(8)] public ushort TopMargin;
            [FieldOffset(10)] public ushort LeftMargin;
            [FieldOffset(12)] public ushort IHeight;
            [FieldOffset(14)] public ushort IWidth;
            [FieldOffset(16)] public uint RawPitch;
        }

        private static class Native {
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libraw_init")]
            public static extern IntPtr Init(uint flags);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libraw_open_buffer")]
            public static extern int OpenBuffer(IntPtr processor, IntPtr buffer, UIntPtr size);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libraw_unpack")]
            public static extern int Unpack(IntPtr processor);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libraw_get_iparams")]
            public static extern IntPtr GetImageParams(IntPtr processor);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libraw_COLOR")]
            public static extern int Color(IntPtr processor, int row, int col);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libraw_close")]
            public static extern void Close(IntPtr processor);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libraw_strerror")]
            public static extern IntPtr StrError(int errorCode);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libraw_version")]
            public static extern IntPtr Version();
        }
    }
}
