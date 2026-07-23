using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using NINA.Core.Utility;

namespace Roberthasson.NINA.Lumixcamera {

    /// <summary>
    /// Decides which <c>Lmxptpif.dll</c> the plugin's P/Invokes bind to at runtime:
    ///   * the bundled PUBLIC Lumix SDK DLL (ships with the plugin, default), or
    ///   * the user's installed LUMIX Tether DLL (extended features), when present and enabled.
    ///
    /// The plugin never redistributes Panasonic's Tether DLL — it loads the user's own installed copy
    /// from disk via a <see cref="NativeLibrary.SetDllImportResolver"/>. Enable it in the plugin options
    /// ("Use LUMIX Tether extended features"); optionally point at a custom DLL path.
    ///
    /// This is the scaffolding stage: the resolver + mode detection are in place. The extended-mode
    /// call bindings (CamelCase entry points + device-context argument) are added in the next step;
    /// until then, extended mode loads the Tether DLL but the driver still uses the public call path.
    /// </summary>
    public static class NativeBinding {
        public const string DllName = "Lmxptpif.dll";

        /// <summary>Standard LUMIX Tether install location.</summary>
        public static readonly string DefaultTetherPath =
            @"C:\Program Files\Panasonic\LUMIX Tether\" + DllName;

        /// <summary>True when the user's LUMIX Tether DLL was found and extended mode is enabled.</summary>
        public static bool ExtendedMode { get; private set; }

        /// <summary>Full path of the DLL the resolver will load (public bundled, or Tether).</summary>
        public static string ActiveDllPath { get; private set; }

        private static bool _resolverInstalled;

        private static string PluginDir {
            get {
                try {
                    var loc = Assembly.GetExecutingAssembly().Location;
                    if (!string.IsNullOrEmpty(loc)) return Path.GetDirectoryName(loc);
                } catch { /* single-file / in-memory */ }
                return AppContext.BaseDirectory;
            }
        }

        /// <summary>The public SDK DLL that ships with the plugin (a couple of copy layouts).</summary>
        private static string PublicDllPath {
            get {
                var dir = PluginDir ?? string.Empty;
                foreach (var candidate in new[] {
                    Path.Combine(dir, DllName),
                    Path.Combine(dir, "Library", DllName)
                }) {
                    if (File.Exists(candidate)) return candidate;
                }
                return Path.Combine(dir, DllName);
            }
        }

        /// <summary>
        /// Choose the mode and install the resolver. Must run before the first P/Invoke into the DLL.
        /// Safe to call more than once (re-evaluates the mode; installs the resolver only once).
        /// </summary>
        public static void Initialize(bool preferExtended, string customTetherPath) {
            var tether = string.IsNullOrWhiteSpace(customTetherPath) ? DefaultTetherPath : customTetherPath.Trim();

            if (preferExtended && File.Exists(tether)) {
                ExtendedMode = true;
                ActiveDllPath = tether;
                Logger.Info($"Lumix: Extended (LUMIX Tether) mode — using {tether}");
            } else {
                ExtendedMode = false;
                ActiveDllPath = PublicDllPath;
                if (preferExtended) {
                    Logger.Warning($"Lumix: extended features requested but LUMIX Tether DLL not found at '{tether}'. " +
                                   "Install LUMIX Tether (or set the path in options). Falling back to the standard SDK.");
                } else {
                    Logger.Info("Lumix: Standard (public SDK) mode.");
                }
            }

            if (!_resolverInstalled) {
                try {
                    NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), Resolve);
                    _resolverInstalled = true;
                } catch (Exception ex) {
                    Logger.Error("Lumix: failed to install native DLL resolver: " + ex);
                }
            }
        }

        private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) {
            if (string.Equals(libraryName, DllName, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(ActiveDllPath)
                && File.Exists(ActiveDllPath)
                && NativeLibrary.TryLoad(ActiveDllPath, out var handle)) {
                return handle;
            }
            return IntPtr.Zero; // fall back to default resolution
        }
    }
}
