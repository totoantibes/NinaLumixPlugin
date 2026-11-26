using NINA.Core.Enum;
using NINA.ViewModel;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Model;
using NINA.Image.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using NINA.Profile.Interfaces;
using NINA.Image.ImageData;
using NINA.Profile;
using LumixWrapper;
using static LumixWrapper.LumixCam;
using NINA.Equipment.Equipment.MyGuider;
using Accord.Math;
using NINA.Core.Utility.Notification;
using Google.Protobuf.WellKnownTypes;
using Dasync.Collections;
using Newtonsoft.Json.Linq;
using NINA.Core.Model;
using NINA.Equipment.Utility;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting;
using System.Windows.Media.Imaging;
using System.Reflection;

namespace Roberthasson.NINA.Lumixcamera.LumixcameraDrivers {

    /// <summary>
    /// This Class implements a camera driver plugin instance for the native lumix SDK
    /// </summary>
    public class LumixcameraDriver : BaseINPC, ISettings, ICamera {
        private IProfileService _profileService;
        private readonly IExposureDataFactory _exposureDataFactory;
        private LMX_DEVINFO _lmxDevInfo = new LMX_DEVINFO();
        private LMX_CONNECT_DEVICE_INFO _lmxConnectDeviceInfo = new LMX_CONNECT_DEVICE_INFO();
        private uint retError, deviceConnectVer, returnV;
        private byte ret;
        private uint _index;
        private uint _curIsoValue = 0;
        private int _curSsValue = 0;
        private bool _connected = false;
        private byte[] buffer;
        private uint _lastFormat;   // object format of the most recent capture (RAW vs JPEG) for download decode
        private AsyncObservableCollection<BinningMode> _binningModes;

        private LMX_CALLBACK_FUNC callback;

        private LMX_STRUCT_ISO_CAPA_INFO Iso_CapaInfo = new LMX_STRUCT_ISO_CAPA_INFO();
        private LMX_STRUCT_SS_CAPA_INFO SS_CapaInfo = new LMX_STRUCT_SS_CAPA_INFO();
        private TaskCompletionSource<object> _downloadExposure;
        private CameraSpecs _currentCameraspecs = new CameraSpecs();

        public LumixcameraDriver(IProfileService profileService, IExposureDataFactory exposureDataFactory, LumixCam.LMX_DEVINFO lmxDevInfo, LumixCam.LMX_CONNECT_DEVICE_INFO lmxConnetDeviceInfo, int index) {
            _profileService = profileService;
            _exposureDataFactory = exposureDataFactory;
            _lmxDevInfo = lmxDevInfo;
            _lmxConnectDeviceInfo = lmxConnetDeviceInfo;
            _index = ((uint)index);
            callback = new LMX_CALLBACK_FUNC(NotifyCallbackFunction);
        }

        // Largest object we will accept as a still frame. The biggest Lumix RW2 is well under this (18 MB on
        // the bodies used here, under 100 MB on the highest-resolution ones); a size beyond it is a garbage or
        // uninitialised out-param from an object the camera cannot serve, not a photograph.
        private const uint MAX_OBJECT_BYTES = 256u * 1024u * 1024u;

        // An object the camera offers that turns out not to be an image is normally harmless: on the first
        // frame of a session the body can announce a placeholder, and the real frame follows on the next
        // event. The driver ignored those and kept waiting - but if the real frame never came it kept waiting
        // for ever, and the exposure only ended when N.I.N.A. gave up on its own timeout, exposure + 20 s.
        // That is what the 2026-11-24 09:55:05 "Camera did not set image as ready" line was. Reported by the
        // plugin-manifest maintainer on review.
        //
        // So the wait is now bounded: the first unusable object starts a clock and the frame is failed if no
        // usable one arrives inside it, or if too many arrive. Ten seconds is far longer than a transfer needs
        // (observed 130-200 ms) and far shorter than N.I.N.A.'s own timeout, so the sequence gets a clean
        // capture error and moves on instead of stalling.
        private static readonly TimeSpan UNUSABLE_OBJECT_GRACE = TimeSpan.FromSeconds(10);
        private const int UNUSABLE_OBJECT_LIMIT = 4;
        private DateTime _firstUnusableUtc = DateTime.MinValue;
        private int _unusableCount;

        // The native SDK calls this from its own thread: it is a reverse P/Invoke, so a managed exception must
        // NEVER leave this method. Unwinding into the SDK's native frame is undefined behaviour and takes the
        // whole NINA process down — Windows reports it as an access violation in the C runtime, with no managed
        // exception text and the log cut off mid-frame (observed 2026-11-23 08:32:57, caused by a full SD card).
        // Catch everything here, fail the frame NINA is waiting on, and let the capture be the casualty instead
        // of the process.
        public int NotifyCallbackFunction(uint cb_event_type, uint cb_event_param) {
            try {
                return DispatchCallbackEvent(cb_event_type, cb_event_param);
            } catch (Exception ex) {
                Logger.Error($"[LumixCapture] handling camera event 0x{cb_event_type:X} (param 0x{cb_event_param:X}) threw; failing this frame: {ex}");
                FailExposure($"The Lumix driver failed while handling camera event 0x{cb_event_type:X}: {ex.Message}");
                return 0;   // event consumed — never let an exception reach native code
            }
        }

        private int DispatchCallbackEvent(uint cb_event_type, uint cb_event_param) {
            switch ((Lmx_event_id)cb_event_type) {
                case Lmx_event_id.LMX_DEF_LIB_EVENT_ID_REC_CTRL_RELEASE:
                    ExposureFinished(cb_event_param);
                    // Notification.ShowInformation("LMX_DEF_LIB_EVENT_ID_REC_CTRL_RELEASE?");
                    break;

                case Lmx_event_id.LMX_DEF_LIB_EVENT_ID_OBJCT_REQ_TRNSFER:
                    // Host/PC transfer request (save target PC-only or PC+SD). cb_event_param is the cardless
                    // handle (0x12345678). A pending transfer MUST be consumed — pulled or skipped — or the
                    // camera keeps a pending-write state and won't power off. If the frame isn't downloaded yet,
                    // pull it to the PC; if it already completed via the card write (PC+SD), release it.
                    if (_downloadExposure != null && !_downloadExposure.Task.IsCompleted) {
                        ExposureFinished(cb_event_param);
                    } else {
                        ReleasePendingTransfer(cb_event_param);
                    }
                    break;

                case Lmx_event_id.LMX_DEF_LIB_EVENT_ID_SHUTTER:
                    //Notification.ShowInformation("LMX_DEF_LIB_EVENT_ID_SHUTTER");

                    break;

                case Lmx_event_id.LMX_DEF_LIB_EVENT_ID_OBJCT_ADD:
                    //Notification.ShowInformation("LMX_DEF_LIB_EVENT_ID_OBJCT_ADD");
                    ExposureFinished(cb_event_param);
                    break;

                case Lmx_event_id.LMX_DEF_LIB_EVENT_ID_ISO:
                    //Notification.ShowInformation("LMX_DEF_LIB_EVENT_ID_ISO");

                    break;

                default:
                    return -1;
            }

            return 0;
        }

        private Dictionary<string, CameraSpecs> lumixCameras;

        // ---- Card capacity ----
        // A full card is invisible from NINA's side: the camera just stops producing frames, and with the save
        // target on PC+SD it produces an empty object instead. These two Tether exports are the only way to see
        // it coming, so the count is read at connect and re-read after a frame arrives empty.
        private uint? _lastKnownCardFreeFrames;
        private uint? _lastKnownCardStatus;
        private bool _recheckCardCapacity;

        // The save target actually in force for this session, which is not always the one the user asked
        // for - see ResolveSaveTarget. Anything that reasons about where frames are going must read this,
        // not Settings.Default.SaveTarget, or it will warn about a card problem that has been worked around.
        private ushort _effectiveSaveTarget = LumixCam.SAVE_TARGET_PC;

        // Has this PROCESS connected once already? The card reading is only trustworthy on the first
        // connect; see CardReadingIsTrustworthy. Static, because the reading is a property of the loaded
        // DLL and not of a driver instance.
        private static bool _processHasConnectedBefore;

        // A flat set is 21 frames and a night of subs is hundreds, so fewer than this is worth saying out loud
        // before the session starts rather than discovering at 3 a.m.
        private const uint LOW_CARD_FRAMES = 50;

        private static bool SaveTargetUsesCard(ushort saveTarget) =>
            saveTarget == LumixCam.SAVE_TARGET_SD || saveTarget == LumixCam.SAVE_TARGET_PC_AND_SD;

        // Frames the camera says still fit on the card, or null when it will not say. Both calls are
        // undocumented Tether exports, so the raw bytes are logged either way: this line is what will pin their
        // layout down on a real body, and it is also why they are only ever called from NINA's own thread —
        // never from inside the SDK's capture callback.
        // No SD card holds this many frames, so a bigger number is not a count. Measured: with the slot empty
        // the body answers 0xFFFFFFFF, which read as "room for 4294967295 more frame(s)" until this check
        // existed. With a card in, the same call returned 2576 — a real count, whatever the save target.
        private const uint CARD_FRAMES_IMPLAUSIBLE = 1000000;

        // THIS READING CANNOT BE BELIEVED AFTER A RECONNECT. Measured on a GH5S on 2026-11-26, physical card
        // state known at each point:
        //
        //   12:08:21  reconnect          card OUT   status 0x10002, 1404 frames   WRONG, stale present
        //   12:16:40  first of process   card OUT   status 0x2, 0xFFFFFFFF        correct
        //   14:15:38  first of process   card IN    status 0x10002, 1404 frames   correct
        //   14:20:27  reconnect          card IN    status 0x2, 0xFFFFFFFF        WRONG, reads as empty
        //   14:38:49  reconnect          card IN    status 0x10002, 1402 frames   correct
        //
        // So it is wrong in BOTH directions, and only intermittently - a reconnect is sometimes right. The
        // 14:20 answer is byte-identical to the genuine empty slot at 12:16, so "there is no card" and "this
        // session has just reopened and cannot say" cannot be told apart. Anything that acts on an apparently
        // empty slot must gate on CardReadingIsTrustworthy; a fallback that did not do so overrode a working
        // card at 14:20 and silently stopped writing to it.
        //
        // The free COUNT is live, not cached - it read 1404, 1403, 1402 as frames were written to the card. An
        // earlier version of this comment claimed the whole reading was cached at process scope. That was one
        // measurement generalised too far. What fails is the read itself, not the freshness of the number.
        //
        // DescribeCardForFailure is worded "was last reported" because of all this - do not promote it to the
        // present tense.

        private uint? ReadCardFreeFrames() {
            bool ok = LumixCam.Ext_TryGetFreeSpaceInImages(out uint images, out uint spaceErr, out string spaceRaw);
            bool statusOk = LumixCam.Ext_TryGetStatusOfCards(out uint status, out uint statusErr, out string statusRaw);
            _lastKnownCardStatus = statusOk ? status : (uint?)null;
            if (!ok) {
                Logger.Info($"[LumixCard] the camera did not report card capacity (err={LumixError.Describe(spaceErr)}, bytes={spaceRaw}; status err={LumixError.Describe(statusErr)}, bytes={statusRaw}).");
                _lastKnownCardFreeFrames = null;
                return null;
            }
            if (images == LumixCam.CARD_FRAMES_NO_NUMBER || images >= CARD_FRAMES_IMPLAUSIBLE) {
                Logger.Info($"[LumixCard] the camera gave no usable frame count (0x{images:X8}); card status=0x{status:X} (space bytes={spaceRaw}, status bytes={statusRaw})." +
                        (CardReadingIsTrustworthy ? "" : " This is a reconnect, so this reading is NOT evidence about the card - a session that has just reopened answers exactly like an empty slot."));
                _lastKnownCardFreeFrames = null;
                return null;
            }
            Logger.Info($"[LumixCard] room for {images} more frame(s); card status=0x{status:X} (space bytes={spaceRaw}, status bytes={statusRaw}).");
            _lastKnownCardFreeFrames = images;
            return images;
        }

        // Read the card and say so where NINA shows it.
        private void ReportCardCapacity(ushort saveTarget) {
            WarnAboutCard(saveTarget, ReadCardFreeFrames());
        }

        // Can the card reading be believed? Only on the first connect of the process.
        //
        // Measured on a GH5S on 2026-11-26, one N.I.N.A. process, the card physically in the body the whole
        // time:
        //   14:15:38  first connect after a restart   status bytes 02-00-01-00 -> 0x10002, 1404 frames  CORRECT
        //   14:20:27  after a plain disconnect/connect status bytes 02-00-00-00 -> 0x2, 0xFFFFFFFF      WRONG
        // and earlier the same day, in the other direction, with the card physically OUT:
        //   12:08:21  after a reconnect                status 0x10002, 1404 frames                      WRONG
        //   12:16:40  first connect after a restart    status 0x2, 0xFFFFFFFF                           CORRECT
        //
        // The 14:20 reading is byte-identical to the genuine empty-slot reading at 12:16, so "there is no card"
        // and "this session is too young to say" CANNOT BE TOLD APART. Any code that acts on an apparently
        // empty slot must therefore refuse to act on a reconnect, or it will override a perfectly good card -
        // which is exactly what happened at 14:20 before this check existed.
        //
        // This also covers a false warning that predates the fallback: ReportCardCapacity would announce "there
        // is no card in the camera - captures will fail" on that same reconnect.
        private static bool CardReadingIsTrustworthy => !_processHasConnectedBefore;

        // Decide the save target for this session, reading the card once.
        //
        // When the user asked for a target that writes to the card and the camera says, unambiguously, that it
        // cannot be written, fall back to PC so the capture works instead of failing. With SD only and an empty
        // slot the camera raises NO object event at all - measured on a GH5S on 2026-11-26 - so every exposure
        // runs to NINA's own exposure+20s timeout and the session produces nothing at all. With PC+SD the frame
        // reaches the PC regardless, so there the fallback only avoids a card write that would fail.
        //
        // Only on UNAMBIGUOUS evidence: a status word that says the slot is empty, or a free count the camera
        // gives as exactly zero. "The camera would not say" is NOT evidence of absence and must never trigger
        // this - a reading the body withholds is the one case where guessing would do harm.
        //
        // The stored setting is deliberately NOT changed: the user's choice survives, and the override lasts
        // only for this session. It cannot do better than that, because the Tether DLL caches the card reading
        // for the life of the process - see ReadCardFreeFrames - so a card inserted after NINA started still
        // reads as absent and the fallback would stay in force. That is why the warning names the restart.
        private ushort ResolveSaveTarget(ushort wanted) {
            uint? free = ReadCardFreeFrames();
            bool slotEmpty = CardReadingIsTrustworthy
                             && _lastKnownCardStatus.HasValue
                             && !LumixCam.CardPresent(_lastKnownCardStatus.Value);
            bool cardFull = free.HasValue && free.Value == 0;
            if (!SaveTargetUsesCard(wanted) || (!slotEmpty && !cardFull)) {
                WarnAboutCard(wanted, free);
                return wanted;
            }
            string why = slotEmpty ? "there is no card in the camera" : "the camera's SD card is full";
            Logger.Warning($"[LumixCard] the save target asks to write to the SD card, but {why}. Falling back to " +
                           "PC for this session so captures still work. The plugin setting is unchanged; restart " +
                           "N.I.N.A. after fixing the card to use the card again.");
            try {
                Notification.ShowWarning($"Lumix: {why}, so this session saves to the PC only." + Environment.NewLine +
                                         "Your save-target setting is unchanged. Restart N.I.N.A. after fixing the card.");
            } catch { }
            return LumixCam.SAVE_TARGET_PC;
        }

        // Say where NINA shows it what the card reading means. Only warns when the target in force actually
        // writes to the card - on PC-only the card's state does not matter.
        private void WarnAboutCard(ushort saveTarget, uint? free) {
            if (!SaveTargetUsesCard(saveTarget)) { return; }
            if (free == null) {
                // No usable count AND the save target writes to the card: the camera has somewhere it is meant
                // to write and cannot tell us there is room. The status word separates the two causes — an empty
                // slot reads 0x00000002 and a card in the slot 0x00010002 — so name which one it is, falling
                // back to the vaguer wording when the status word is missing or in some third state.
                bool slotEmpty = CardReadingIsTrustworthy
                                 && _lastKnownCardStatus.HasValue
                                 && !LumixCam.CardPresent(_lastKnownCardStatus.Value);
                string reason = slotEmpty
                    ? "there is no card in the camera"
                    : "the camera reports no usable capacity for the card";
                Logger.Warning($"[LumixCard] the save target writes to the SD card, but {reason} — captures will fail until a writable card is in the body or the save target is set to PC only.");
                Notification.ShowWarning($"The save target writes to the camera's SD card, but {reason}. Put in a writable card, or set the plugin's save target to PC only.");
                return;
            }
            if (free.Value == 0) {
                Logger.Error("[LumixCard] the SD card is full and the save target writes to it — captures will fail until the card is cleared or the save target is set to PC only.");
                Notification.ShowError("The camera's SD card is full and the save target writes to it. Clear the card, or set the plugin's save target to PC only.");
            } else if (free.Value < LOW_CARD_FRAMES) {
                Logger.Warning($"[LumixCard] only {free.Value} frame(s) of room left on the SD card.");
                Notification.ShowWarning($"The camera's SD card has room for only {free.Value} more frame(s).");
            }
        }

        // What to tell NINA about the card when a frame arrives empty. Uses the last reading rather than calling
        // the SDK from the capture callback, and asks for a fresh reading on the next exposure.
        private string DescribeCardForFailure() {
            _recheckCardCapacity = true;
            if (_lastKnownCardFreeFrames == 0) {
                return " The SD card was last reported full.";
            }
            if (_lastKnownCardFreeFrames.HasValue) {
                return $" The SD card was last reported as having room for {_lastKnownCardFreeFrames.Value} frame(s).";
            }
            return " If the save target includes the SD card, check that a card is present and not full.";
        }

        // Fail the exposure NINA is awaiting, so a frame that cannot be delivered ends as a failed capture.
        // TrySetException, never TrySetResult: completing the task successfully hands NINA an exposure whose
        // raw bytes are null, because `buffer` was cleared by the previous frame's download.
        private void FailExposure(string reason) {
            var tcs = _downloadExposure;
            if (tcs == null || tcs.Task.IsCompleted) { return; }
            tcs.TrySetException(new Exception(reason));
        }

        // A cardless PC-transfer request must be consumed — pulled or skipped — or the camera keeps a pending
        // write and will not power off. Card objects (OBJCT_ADD) carry no pending transfer to release.
        private void ReleasePendingTransfer(uint cb_event_param) {
            if (cb_event_param != CARDLESS_TRNSFER_HDL) { return; }
            LMX_func_api_Skip_Object_Transfer(cb_event_param, out uint _skipErr);
        }

        private void ExposureFinished(uint cb_event_param) {
            // With PC+SD, one frame fires both OBJCT_REQ_TRNSFER (cardless handle) and OBJCT_ADD (card handle);
            // process only the first so we don't download twice.
            if (_downloadExposure == null || _downloadExposure.Task.IsCompleted) { return; }
            uint formatType;
            // 64-bit, because that is what the SDK writes through this pointer. It stays 64-bit
            // until the size has been bounded below, so a garbage high half is caught rather than
            // silently truncated into a plausible-looking 32-bit length.
            ulong dataSize64;
            LMX_STRUCT_PTP_ARRAY_STRING fileNameArrStr = new LMX_STRUCT_PTP_ARRAY_STRING();

            LMX_func_api_Get_Object_FormatType(cb_event_param, out formatType, out retError);
            LMX_func_api_Get_Object_DataSize(cb_event_param, out dataSize64, out retError);
            LMX_func_api_Get_Object_FileName(cb_event_param, ref fileNameArrStr, out retError);
            _lastFormat = formatType;
            Logger.Info($"[LumixCapture] object=0x{cb_event_param:X} format=0x{formatType:X} size={dataSize64}");
            if (dataSize64 == 0) {
                // The camera raised a transfer event for an object that holds no data. Seen when the SD card is
                // full and the save target includes SD: the card write has nowhere to go, so the frame never
                // exists. This used to call TrySetResult(null) — completing the exposure as a SUCCESS with no
                // data — which handed NINA an exposure whose raw bytes were null. Fail the frame instead.
                Logger.Warning("[LumixCapture] object data size is 0 — nothing to download; failing this frame. If the save target includes the SD card, check that the card is not full.");
                ReleasePendingTransfer(cb_event_param);
                FailExposure("The camera reported a frame with no data." + DescribeCardForFailure());
                return;
            }
            if (dataSize64 > MAX_OBJECT_BYTES) {
                // Allocating a garbage size would throw OutOfMemory inside this native callback — the one place
                // an exception must not be raised. Refuse the object instead of trying to hold it.
                Logger.Warning($"[LumixCapture] object size {dataSize64} exceeds the {MAX_OBJECT_BYTES} byte limit for a still frame — refusing to allocate; failing this frame.");
                ReleasePendingTransfer(cb_event_param);
                FailExposure($"The camera reported an implausible frame size of {dataSize64} bytes.");
                return;
            }
            // Safe: bounded by MAX_OBJECT_BYTES (256 MB) immediately above.
            uint dataSize = (uint)dataSize64;

            // Transfer into the session-lived pinned buffer, never into a fresh managed array. `ref array[0]`
            // pins only for the duration of the call, and this SDK writes to buffers after the call returns —
            // see TryGetObjectBuffer for the evidence. A collectible destination here would be the capture-path
            // twin of the live-view use-after-free.
            //
            // `buffer` is deliberately NOT allocated here any more. It is allocated below, after the header
            // check, and only for a frame that proved usable — see the copy-out comment there for why NINA
            // cannot be handed the shared transfer buffer itself.
            if (!LumixCam.TryGetObjectBuffer(dataSize, out byte[] xfer)) {
                // Either the allocation failed, or the frame is larger than the capacity fixed at connect. The
                // buffer is deliberately never grown - see TryGetObjectBuffer - so this is a failed frame
                // rather than a bigger allocation.
                Logger.Warning($"[LumixCapture] could not obtain a {dataSize}-byte transfer buffer " +
                               $"(session capacity {LumixCam.ObjectBufferCapacity} bytes); failing this frame.");
                ReleasePendingTransfer(cb_event_param);
                FailExposure($"Could not allocate a {dataSize}-byte buffer for the frame.");
                return;
            }
            byte gr = 0;
            try { gr = LMX_func_api_Get_Object(cb_event_param, ref xfer[0], dataSize, out retError); } catch (Exception ex) { Logger.Error("[LumixCapture] Get_Object threw: " + ex); }
            if (dataSize >= 4) {
                Logger.Info($"[LumixCapture] Get_Object ret={gr} err={retError}; magic={xfer[0]:X2} {xfer[1]:X2} {xfer[2]:X2} {xfer[3]:X2}");
            }

            // Guard against a bogus object. On the first frame of a session the camera can fire an OBJCT event
            // for a placeholder/association object whose Get_Object fails (ret=0) and returns a zero-filled buffer
            // (observed as format 0x3, 128 KB). Forwarding that to NINA makes LibRaw throw "not a RAW file". Only
            // complete the download once we've actually pulled a recognisable image; otherwise ignore this object
            // and keep awaiting the real frame — the valid RW2/JPEG arrives on the next OBJCT event.
            if (gr != LMX_BOOL_TRUE || !IsKnownImageHeader(xfer, dataSize)) {
                ReleasePendingTransfer(cb_event_param);

                _unusableCount++;
                if (_firstUnusableUtc == DateTime.MinValue) { _firstUnusableUtc = DateTime.UtcNow; }
                TimeSpan waited = DateTime.UtcNow - _firstUnusableUtc;

                if (waited > UNUSABLE_OBJECT_GRACE || _unusableCount >= UNUSABLE_OBJECT_LIMIT) {
                    // Terminal: stop waiting for a frame that is not coming and let the sequence see a capture
                    // error now, rather than stalling until N.I.N.A.'s exposure+20s timeout fires.
                    Logger.Error($"[LumixCapture] giving up on this frame: {_unusableCount} unusable object(s) " +
                                 $"in {waited.TotalSeconds:0.0}s, the last one ret={gr}, format=0x{formatType:X}, size={dataSize}. " +
                                 "Failing the exposure so the sequence can continue.");
                    FailExposure("The camera did not deliver a usable image for this exposure." + DescribeCardForFailure());
                    return;
                }

                Logger.Warning($"[LumixCapture] object 0x{cb_event_param:X} is not a usable image (ret={gr}, format=0x{formatType:X}, size={dataSize}) — ignoring, waiting for the real frame " +
                               $"({_unusableCount} so far, {(UNUSABLE_OBJECT_GRACE - waited).TotalSeconds:0.0}s left before this frame is failed).");
                return;   // do NOT complete the exposure with junk data
            }

            // Only now that the frame is known good, copy it out of the shared transfer buffer. NINA keeps these
            // bytes well past this method — it decodes them and writes the .RW2 from them on its own threads —
            // so it cannot be handed the buffer itself: the next frame would overwrite it mid-save. The copy is
            // a few milliseconds against a download of a few hundred, and the array it produces is never seen
            // by native code, so it is free to move or be collected.
            buffer = new byte[dataSize];
            Buffer.BlockCopy(xfer, 0, buffer, 0, (int)dataSize);

            _downloadExposure.TrySetResult(null);
            return;
        }

        // A recognisable still-image header: Panasonic RW2 ("IIU\0"), TIFF (RW2 is TIFF-based), or JPEG.
        private static bool IsKnownImageHeader(byte[] b, uint size) {
            if (b == null || size < 4) { return false; }
            if (b[0] == 0x49 && b[1] == 0x49 && b[2] == 0x55 && b[3] == 0x00) { return true; } // IIU\0  RW2
            if (b[0] == 0x49 && b[1] == 0x49 && b[2] == 0x2A && b[3] == 0x00) { return true; } // II*\0  TIFF LE
            if (b[0] == 0x4D && b[1] == 0x4D && b[2] == 0x00 && b[3] == 0x2A) { return true; } // MM\0*  TIFF BE
            if (b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) { return true; }                 // JPEG
            return false;
        }

        // ---- Extended-mode helpers: manual-mode whitelist + shutter-speed snapping ----

        private ushort _lastNotifiedModePos = 0xFFFF;
        private bool _warnedJpeg = false;   // warn once per connect if the camera is capturing JPEG, not RAW
        private bool _warnedLibRaw = false; // warn once per connect if the built-in LibRaw decoder failed and we fell back
        private List<(double seconds, int raw)> _ssTable;

        private static bool IsManualMode(ushort modePos) =>
            modePos == (ushort)Lmx_def_lib_Camera_Mode_Info_Mode_Pos.LMX_DEF_CAMERA_MODE_INFO_MODE_POS_M;

        // C1-C3 custom presets. The Tether SDK reports these dial positions but NOT the base program (M/P/A/S)
        // the preset encodes, so we can't be sure a custom set is Manual. We give the user the benefit of the
        // doubt: assume it is M-based, proceed, and only warn.
        private static bool IsCustomMode(ushort modePos) =>
            modePos == (ushort)Lmx_def_lib_Camera_Mode_Info_Mode_Pos.LMX_DEF_CAMERA_MODE_INFO_MODE_POS_CUSTOM
            || modePos == (ushort)Lmx_def_lib_Camera_Mode_Info_Mode_Pos.LMX_DEF_CAMERA_MODE_INFO_MODE_POS_CUSTOM2
            || modePos == (ushort)Lmx_def_lib_Camera_Mode_Info_Mode_Pos.LMX_DEF_CAMERA_MODE_INFO_MODE_POS_CUSTOM3;

        // Warn (never block) about the current exposure mode. M is silent; a C1-C3 custom preset gets a soft
        // "assumed M-based" warning; anything else gets the stronger warning. Change-detected so repeated
        // captures in the same mode don't spam the same notification.
        private void CheckExposureMode(ushort modePos) {
            if (IsManualMode(modePos)) { _lastNotifiedModePos = modePos; return; }
            if (modePos == _lastNotifiedModePos) { return; }
            _lastNotifiedModePos = modePos;
            if (IsCustomMode(modePos)) {
                Notification.ShowWarning("Camera is in a custom mode (C1-C3), assumed to be an M-based preset. Proceeding - set the preset to Manual if exposures look wrong.");
            } else {
                Notification.ShowWarning("Camera is not in M or a custom (C1-C3) preset - exposures may be incorrect.");
            }
        }

        // Decode the camera's supported shutter-speed list into (seconds, raw) once. Whole-second speeds
        // carry 0x80000000 (seconds = (v & 0x7fffffff)/1000); fractional speeds are 1/x (seconds = 1000/v).
        // BULB / AUTO / UNKNOWN are excluded.
        private void BuildSsTable() {
            _ssTable = new List<(double, int)>();
            if (_exposures == null) { _exposures = new List<int>(); }
            var support = SS_CapaInfo.Capa_Enum.SupportVal;
            if (support == null) { return; }
            // Iterate only the valid entries (NumOfVal), not the full fixed-size array — the tail is garbage.
            int count = SS_CapaInfo.Capa_Enum.NumOfVal;
            if (count <= 0 || count > support.Length) { count = support.Length; }
            for (int i = 0; i < count; i++) {
                uint v = (uint)support[i];
                if (v == 0) { continue; }
                if (v == 0xFFFFFFFF) { continue; }                                                                  // BULB
                if (v == (uint)Lmx_def_lib_DevpropEx_ShutterSpeed_param.LMX_DEF_PTP_DEVPROP_EXT_LMX_SS_UNKNOWN) { continue; }
                if (v == (uint)Lmx_def_lib_DevpropEx_ShutterSpeed_param.LMX_DEF_PTP_DEVPROP_EXT_LMX_SS_AUTO) { continue; }
                double sec = ((v & 0x80000000) != 0) ? (double)(v & 0x7fffffff) / 1000.0 : 1000.0 / v;
                if (sec <= 0) { continue; }
                _ssTable.Add((sec, unchecked((int)v)));
                if ((v & 0x80000000) != 0) { _exposures.Add((int)(v & 0x7fffffff) / 1000); }   // whole-second fallback list
            }
        }

        // Nearest supported shutter speed (raw value) to the requested seconds; 0 if the list is unknown.
        private int NearestSsRaw(double requestedSeconds, out double actualSeconds) {
            if (_ssTable == null || _ssTable.Count == 0) { BuildSsTable(); }
            actualSeconds = requestedSeconds;
            if (_ssTable == null || _ssTable.Count == 0) { return 0; }
            var best = _ssTable[0];
            double bestErr = Math.Abs(best.seconds - requestedSeconds);
            foreach (var e in _ssTable) {
                double err = Math.Abs(e.seconds - requestedSeconds);
                if (err < bestErr) { bestErr = err; best = e; }
            }
            actualSeconds = best.seconds;
            return best.raw;
        }

        public bool HasShutter => true;

        public double Temperature {
            get => double.NaN;
        }

        public double TemperatureSetPoint {
            get => double.NaN;

            set {
            }
        }

        public short BinX { get => 1; set => throw new NotImplementedException(); }
        public short BinY { get => 1; set => throw new NotImplementedException(); }

        public string SensorName {
            get {
                if (Connected) {
                    return _lmxDevInfo.dev_ModelName;
                } else {
                    return string.Empty;
                }
            }
        }

        public SensorType SensorType { get => SensorType.RGGB; set => throw new NotImplementedException(); }

        public short BayerOffsetX { get => 0; set => throw new NotImplementedException(); }

        public short BayerOffsetY { get => 0; set => throw new NotImplementedException(); }

        public int CameraXSize {
            get {
                if (Connected) {
                    return _currentCameraspecs.Width;
                } else {
                    return 0;
                }
            }
        }

        public int CameraYSize {
            get {
                if (Connected) {
                    return _currentCameraspecs.Height;
                } else {
                    return 0;
                }
            }
        }

        private IList<int> _exposures;

        // Exposure bounds are derived from the supported-shutter LIST (reliable in both DLLs), not from
        // CurVal_Range — the Tether capability buffer's range fields sit at a different offset than the public
        // struct, so reading them via PtrToStructure yielded a bogus ExposureMin that clamped NINA (e.g. the
        // Flat Wizard) to 1s and never let sub-second exposures through.
        public double ExposureMin {
            get {
                if (!Connected) { return 0; }
                if (_ssTable == null || _ssTable.Count == 0) { BuildSsTable(); }
                double min = (_ssTable != null && _ssTable.Count > 0) ? _ssTable.Min(e => e.seconds) : 0;
                Logger.Debug($"[LumixSS] ExposureMin={min:0.######}s (from {(_ssTable?.Count ?? 0)} speeds)");
                return min;
            }
        }

        public double ExposureMax {
            get {
                if (!Connected) { return 0; }
                if (_ssTable == null || _ssTable.Count == 0) { BuildSsTable(); }
                double listMax = (_ssTable != null && _ssTable.Count > 0) ? _ssTable.Max(e => e.seconds) : 60;
                // Extended mode supports Bulb (>60s) via the open/hold/close shutter sequence, so allow long
                // exposures (cap at 1 hour). Standard mode is limited to the discrete list (<=60s).
                return NativeBinding.ExtendedMode ? Math.Max(listMax, 3600) : listMax;
            }
        }

        public short MaxBinX { get => 1; set => throw new NotImplementedException(); }

        public short MaxBinY { get => 1; set => throw new NotImplementedException(); }

        public double PixelSizeX {
            get {
                if (Connected) {
                    return _currentCameraspecs.PixelPitch;
                } else {
                    return 0;
                }
            }
        }

        public double PixelSizeY {
            get {
                if (Connected) {
                    return _currentCameraspecs.PixelPitch;
                } else {
                    return 0;
                }
            }
        }

        public bool CanSetTemperature => false;

        public bool CoolerOn {
            get => false;
            set {
            }
        }

        public double CoolerPower => double.NaN;

        public bool HasDewHeater => false;

        public bool DewHeaterOn {
            get => false;
            set {
            }
        }

        public CameraStates CameraState => CameraStates.NoState; // TODO - lumix does not give info...

        public bool CanSubSample => false;

        public bool EnableSubSample { get; set; }

        public int SubSampleX { get; set; }

        public int SubSampleY { get; set; }

        public int SubSampleWidth { get; set; }

        public int SubSampleHeight { get; set; }

        public bool CanShowLiveView => true; //TO DO test logic

        private bool _liveViewEnabled;

        public bool LiveViewEnabled {
            get => _liveViewEnabled;
            set {
                _liveViewEnabled = value;
                RaisePropertyChanged();
            }
        }

        // Only the Tether DLL can answer this. Checked against both export tables: the bundled public SDK
        // (85 exports) has zero battery-related exports, and LMX_func_api_GetBatteryInfo exists only in the
        // Tether DLL. So in Standard mode there is nothing to ask, and NINA should not show a battery
        // indicator it can never fill. NativeBinding.Initialize settles the mode before the first P/Invoke,
        // so this is stable by the time NINA reads it.
        public bool HasBattery => NativeBinding.ExtendedMode;

        // ---- Battery ----
        // The getter is a plain field read - no native call, ever. The reading behind it is refreshed only
        // from an idle point in the capture cycle, never from NINA's polling.
        //
        // Why the getter cannot touch the SDK: NINA asks for BatteryLevel from its 2s device-update cycle and
        // from metaData.FromCamera(this) on every frame and every live-view frame, so a native call here lands
        // wherever the camera happens to be - including inside an image transfer. That is what killed five
        // sessions on 2026-11-23/24.
        //
        // What the dumps showed (processes 3784 and 13588, both captured): the SDK finishes an ABANDONED
        // transfer 141s and 82s after the plugin's Get_Object for that frame has already returned, with a
        // memcpy of the full frame into a native buffer it has itself freed - on a thread it owns, with no
        // managed or plugin frame anywhere on the stack. The copy dies 7,968 bytes in, when it runs off the
        // short mapped prefix that is left. Everything it wrote before that landed on whatever had taken that
        // address: that is the heap corruption, and the later crashes in unrelated code are its victims.
        //
        // The battery read did not corrupt anything itself. In both dumps it was REFUSED - err=0 with an
        // all-zero scratch - in the same millisecond that Get_Object returned 0 with a zero-filled buffer.
        // Two requests bouncing together is the signature of a channel that already had two users: the read
        // was issued from NINA's timer while the SDK was mid-transfer. The corrupting write is inside
        // Panasonic's DLL and nothing here can make it safe, so keeping a second request off the channel
        // during a transfer is the whole defence.
        //
        // Hence the refresh below, which runs at the top of StartExposure: the previous frame has been
        // downloaded and the next shutter has not fired. Two other reads already live at that point - the
        // exposure-mode poll and the card-capacity read - and neither has ever been implicated, across
        // sessions of up to 234 frames. The interval keeps this to a handful of reads a night instead of one
        // per NINA poll, and the read gives up rather than wait if the gate is busy.
        private static readonly TimeSpan BATTERY_REFRESH_INTERVAL = TimeSpan.FromMinutes(5);
        private DateTime _lastBatteryRefreshUtc = DateTime.MinValue;
        private int _batteryPercent = -1;
        private uint _lastLoggedBatteryWord = uint.MaxValue;

        // -1 is NINA's "unknown", which is what every camera without a battery reports.
        public int BatteryLevel => _batteryPercent;

        // Blocking read, for connect only: there is nothing else on the channel then, and the first reading
        // should not be skipped.
        private int ReadBatteryPercent() {
            // Standard mode has no battery function to call, so report unknown without pretending the camera
            // was asked and declined to answer. See HasBattery.
            if (!NativeBinding.ExtendedMode) { return -1; }
            bool ok = LumixCam.Ext_TryGetBatteryInfo(out uint level, out uint levelAlt, out uint head, out uint err, out string rawHead);
            return InterpretBatteryReading(ok, level, levelAlt, head, err, rawHead);
        }

        // Refresh from an idle point in the capture cycle; see the policy note above BatteryLevel for why it
        // may not be done anywhere else. `idleOnEntry` is captured by the caller BEFORE it cancels an exposure
        // that is still running, because a cancelled exposure can leave a transfer outstanding and this must
        // not put a second request on the channel while one is.
        private void MaybeRefreshBatteryAtIdle(bool idleOnEntry) {
            // Standard mode first: without this the refresh would take the gate and log a failure every five
            // minutes for a function the public SDK does not export. See HasBattery.
            if (!NativeBinding.ExtendedMode) { return; }
            if (!idleOnEntry || _liveViewEnabled) { return; }
            if (DateTime.UtcNow - _lastBatteryRefreshUtc < BATTERY_REFRESH_INTERVAL) { return; }

            bool ok = LumixCam.Ext_TryGetBatteryInfoIfFree(out uint level, out uint levelAlt, out uint head,
                                                           out uint err, out string rawHead, out bool gateBusy);
            if (gateBusy) {
                // Something else holds the SDK gate, so no request was issued and the previous reading stands.
                // The interval is deliberately NOT reset: the next exposure retries instead of waiting out
                // another five minutes for a reading that was never attempted.
                Logger.Debug("[LumixBattery] the SDK was busy; skipping this refresh.");
                return;
            }
            _lastBatteryRefreshUtc = DateTime.UtcNow;
            _batteryPercent = InterpretBatteryReading(ok, level, levelAlt, head, err, rawHead);

            // One line per refresh, unconditionally. The detailed line in InterpretBatteryReading fires only
            // when the value changes, which was right when the getter was called thousands of times a session
            // but hides the mechanism now that reads are throttled: a session on mains, or on a battery that
            // stays at 100, would log one battery line at connect and nothing afterwards, leaving no way to
            // tell from the log whether the refresh ran at all. At this interval it is about a dozen lines a
            // session, which is worth being able to see it working.
            Logger.Info($"[LumixBattery] refreshed: {(_batteryPercent < 0 ? "unknown" : _batteryPercent.ToString() + "%")}" +
                        $"; next no sooner than {BATTERY_REFRESH_INTERVAL.TotalMinutes:0} min from now.");
        }

        private int InterpretBatteryReading(bool ok, uint level, uint levelAlt, uint head, uint err, string rawHead) {
            if (!ok) {
                Logger.Info($"[LumixBattery] the camera did not report battery info (err={LumixError.Describe(err)}, bytes={rawHead}).");
                return -1;
            }
            if (level != _lastLoggedBatteryWord) {
                _lastLoggedBatteryWord = level;
                // Both candidates are logged: offset 8 is the one used, offset 12 held the same 100 on the body
                // this was measured on, and only a reading at some other level can rule it out.
                Logger.Info($"[LumixBattery] level={level} (offset 8), alt={levelAlt} (offset 12), head=0x{head:X8}, bytes={rawHead}.");
            }
            // Zero is treated as unknown, not as an empty battery: a call that returns success without writing
            // leaves the scratch zeroed, and a genuinely flat battery would have switched the body off rather
            // than answered. Anything outside 1-100 cannot honestly be shown as a percentage, so it stays
            // unknown — and the line above keeps the raw struct in the log either way.
            if (level >= 1 && level <= 100) { return (int)level; }
            return -1;
        }

        public int BitDepth {
            get {
                if (Properties.Settings.Default.BitDepth != 0) {
                    return Properties.Settings.Default.BitDepth;
                } else {
                    return 14;
                }
            }
        }

        public bool CanSetOffset => false;

        public int Offset { get => -1; set => throw new NotImplementedException(); }

        public int OffsetMin => 0;

        public int OffsetMax => 0;

        public bool CanSetUSBLimit => false;

        public int USBLimit { get => -1; set => throw new NotImplementedException(); }

        public int USBLimitMin => -1;

        public int USBLimitMax => -1;

        public int USBLimitStep => -1;

        public bool CanGetGain {
            get {
                if (Connected) {
                    return true;
                } else { return false; }
            }
        }

        public bool CanSetGain => CanGetGain;

        // Value mode (matches NINA's own DSLR/ISO cameras): Gain is the ISO VALUE, Gains is the list of ISO
        // values, GainMin/GainMax are the min/max ISO.
        public int GainMax => (Connected && Gains != null && Gains.Count > 0) ? Gains.Max() : 0;

        public int GainMin => (Connected && Gains != null && Gains.Count > 0) ? Gains.Min() : 0;

        // Gain is the ISO VALUE (matches NINA's DSLR cameras). The setter maps the value back to the camera's
        // original raw ISO (preserving extended-ISO markers) so it sends exactly what the camera reported.
        public int Gain {
            get {
                return (int)(Iso_CapaInfo.CurVal & 0x0FFFFFFF);
            }
            set {
                _ = Gains;
                uint raw = (uint)value;
                if (_gains != null && _gainsRaw != null) {
                    int idx = _gains.IndexOf(value);
                    if (idx >= 0) { raw = _gainsRaw[idx]; }
                }
                ret = LMX_func_api_ISO_Set_Param(raw, out retError);
            }
        }

        public double ElectronsPerADU => double.NaN;

        public IList<string> ReadoutModes {
            get {
                List<string> readoutModes = new List<string> { "Default" };
                return readoutModes;
            }
        }

        public short ReadoutMode {
            get => 0;
            set { }
        }

        public short ReadoutModeForSnapImages { get => 0; set { } }
        public short ReadoutModeForNormalImages { get => 0; set { } }

        private IList<int> _gains;
        private List<uint> _gainsRaw;   // original raw ISO values aligned with _gains (for the setter)

        public IList<int> Gains {
            get {
                if (_gains == null) {
                    _gains = new List<int>();
                    _gainsRaw = new List<uint>();
                    // Iterate only the valid entries (NumOfVal), not the fixed 512-slot array — the tail is
                    // zeros and was flooding the gain list (also broke the old plugin). Skip the Auto/i-ISO/
                    // Unknown sentinels; the display ISO is the masked value (0x20019000 -> 102400), but keep the
                    // ORIGINAL raw value (with markers) so the setter sends exactly what the camera reported.
                    var isoSupport = Iso_CapaInfo.Capa_Enum.SupportVal;
                    if (isoSupport != null) {
                        int isoCount = Iso_CapaInfo.Capa_Enum.NumOfVal;
                        if (isoCount <= 0 || isoCount > isoSupport.Length) { isoCount = isoSupport.Length; }
                        var pairs = new List<KeyValuePair<int, uint>>();
                        for (int i = 0; i < isoCount; i++) {
                            uint val = (uint)isoSupport[i];
                            if (val == (uint)Lmx_def_lib_ISO_param.LMX_DEF_ISO_AUTO) { continue; }
                            if (val == (uint)Lmx_def_lib_ISO_param.LMX_DEF_ISO_I_ISO) { continue; }
                            if (val == (uint)Lmx_def_lib_ISO_param.LMX_DEF_ISO_UNKNOWN) { continue; }
                            int iso = (int)(val & 0x0FFFFFFF);
                            if (iso > 0) { pairs.Add(new KeyValuePair<int, uint>(iso, val)); }
                        }
                        pairs.Sort((a, b) => a.Key.CompareTo(b.Key));
                        foreach (var p in pairs) { _gains.Add(p.Key); _gainsRaw.Add(p.Value); }
                    }
                }

                return _gains;
            }
        }

        public AsyncObservableCollection<BinningMode> BinningModes {
            get {
                if (_binningModes == null) {
                    _binningModes = new AsyncObservableCollection<BinningMode>();
                    _binningModes.Add(new BinningMode(1, 1));
                }

                return _binningModes;
            }
        }

        public bool HasSetupDialog => false;

        public string Id => "Lumix";

        // Return the enumerated model name even before connecting, so the camera shows up (named) in NINA's
        // camera picker — the driver isn't Connected yet when the chooser renders it.
        public string Name {
            get {
                var n = _lmxDevInfo.dev_ModelName;
                return string.IsNullOrWhiteSpace(n) ? "Lumix Camera" : n;
            }
        }

        public string DisplayName => Name;

        public string Category { get => "Lumix"; }

        public bool Connected {
            get {
                return _connected;
            }
        }

        private Assembly assembly = Assembly.GetExecutingAssembly();

        public string Description => ((AssemblyDescriptionAttribute)assembly.GetCustomAttribute(typeof(AssemblyDescriptionAttribute))).Description;

        public string DriverInfo => ((AssemblyTitleAttribute)assembly.GetCustomAttribute(typeof(AssemblyTitleAttribute))).Title;

        public string DriverVersion => ((AssemblyFileVersionAttribute)assembly.GetCustomAttribute(typeof(AssemblyFileVersionAttribute))).Version;

        public IList<string> SupportedActions => new List<string>();

        public void SendCommandBlind(string command, bool raw = true) {
            throw new NotImplementedException();
        }

        public bool SendCommandBool(string command, bool raw = true) {
            throw new NotImplementedException();
        }

        public string SendCommandString(string command, bool raw = true) {
            throw new NotImplementedException();
        }

        public void SetupDialog() {
            throw new NotImplementedException();
        }

        public string Action(string actionName, string actionParameters) {
            throw new NotImplementedException();
        }

        public void SetBinning(short x, short y) {
            // ignore throw new NotImplementedException();
        }

        // this is for nightly 3.2.067
        public void UpdateSubSampleArea() {
            throw new NotImplementedException();
        }

        private CancellationTokenSource bulbCompletionCTS = null;

        private Task bulbCompletionTask = null;

        // The teardown Disconnect() starts in the background, kept so that the next Connect can wait for it.
        // Static on purpose: the SDK has ONE process-wide session, so a teardown left running by one driver
        // instance must still hold up the next instance's Connect.
        private static Task _teardownTask = null;

        // How long Connect will wait for that teardown. The teardown's own waits are bounded - a 3 s wait on
        // the bulb completion, a 400 ms settle, then the SDK closes - so this only has to be comfortably
        // larger than that, and bounded so a stalled SDK call cannot wedge Connect for ever.
        private const int TEARDOWN_WAIT_MS = 15000;

        public void StartExposure(CaptureSequence sequence) {
            if (Connected) {
                // Captured before the cancellation below, and used only by the battery refresh: an exposure
                // that gets cancelled here can leave a transfer outstanding, and that is exactly when a second
                // request must not go on the channel.
                bool idleOnEntry = _downloadExposure == null || _downloadExposure.Task.Status > TaskStatus.Running;

                // Per-exposure, not per-session: a placeholder on one frame must not count against the next.
                _firstUnusableUtc = DateTime.MinValue;
                _unusableCount = 0;
                if (_recheckCardCapacity) {
                    // The previous frame came back empty. Re-read the card here, on NINA's thread, so the next
                    // failure message carries a current number instead of the one from connect.
                    _recheckCardCapacity = false;
                    ReportCardCapacity(_effectiveSaveTarget);
                }
                if (_downloadExposure != null && _downloadExposure.Task.Status <= TaskStatus.Running) {
                    Notification.ShowWarning("Another exposure still in progress. Cancelling it to start another.");
                    Logger.Warning("An exposure was still in progress. Cancelling it to start another.");
                    try { bulbCompletionCTS?.Cancel(); } catch { }
                    _downloadExposure.TrySetCanceled();
                }
                MaybeRefreshBatteryAtIdle(idleOnEntry);
                LMX_STRUCT_REC_CTRL lmx_rec_ctrl = new LMX_STRUCT_REC_CTRL();
                lmx_rec_ctrl.CtrlID = ((uint)Lmx_TagID_Rec_Ctrl_Release.LMX_DEF_LIB_TAG_REC_CTRL_RELEASE_ONESHOT);
                lmx_rec_ctrl.ParamData.NumOfVal = 0;
                double exposureTime = sequence.ExposureTime;

                // Re-check the exposure mode every capture so a mid-session dial change out of a manual mode is
                // caught (the SDK exposes no mode-change push event, so we poll). Warn once per change to avoid
                // spam. Runs on both DLLs; the Tether DLL additionally recognises the C1-C3 custom presets.
                if (LMX_func_api_CameraMode_Get_Mode_Pos(out uint modePosNow, out uint cmErr) == LMX_BOOL_TRUE) {
                    CheckExposureMode((ushort)modePosNow);
                }

                Logger.Debug("Prepare start of exposure: " + sequence);
                // RunContinuationsAsynchronously is essential: the completion (TrySetResult) happens on the
                // native DLL callback thread, and without this NINA's continuation (LibRaw decode of the frame)
                // would run inline on that native thread and hang the camera pipeline — badly for long bulb subs.
                _downloadExposure = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (_exposures == null) { _ = ExposureMin; }   // lazily build the discrete exposure list (was NRE on 1st shot)

                // Extended mode: use BULB for ANY exposure longer than 1s so arbitrary / unlisted durations
                // (e.g. 7s, or >60s) are timed exactly, instead of snapping to the nearest discrete shutter
                // speed. Sub-second (<=1s) still uses the discrete speeds (bulb has a ~1s practical floor).
                // SS=BULB, open shutter (0x12), hold, then close (0x13) + finalize (0x19). Completion arrives
                // via OBJCT_ADD like a normal frame; the close runs on a cancellable timer so Stop/Abort ends
                // it early.
                if (NativeBinding.ExtendedMode && exposureTime > 1.0) {
                    if (!LumixCam.Ext_EnsureBulb(out retError)) {
                        Notification.ShowWarning("Could not engage BULB mode for the exposure.");
                        _downloadExposure.TrySetCanceled();
                        return;
                    }
                    var bulbOpen = new LMX_STRUCT_REC_CTRL { CtrlID = (uint)Lmx_TagID_Rec_Ctrl_Release.LMX_DEF_LIB_TAG_REC_CTRL_RELEASE_BULB_START };
                    bulbOpen.ParamData.NumOfVal = 0;
                    byte bo = LMX_func_api_Rec_Ctrl_Release(ref bulbOpen, out retError);
                    Logger.Info($"[LumixBulb] open (0x12) ret={bo} err={retError}; holding {exposureTime}s");
                    try { bulbCompletionCTS?.Cancel(); } catch { }
                    bulbCompletionCTS = new CancellationTokenSource();
                    var bulbToken = bulbCompletionCTS.Token;
                    bulbCompletionTask = Task.Run(async () => {
                        try { await CoreUtil.Wait(TimeSpan.FromSeconds(exposureTime), bulbToken); } catch { }
                        try {
                            var bulbClose = new LMX_STRUCT_REC_CTRL { CtrlID = (uint)Lmx_TagID_Rec_Ctrl_Release.LMX_DEF_LIB_TAG_REC_CTRL_RELEASE_BULB_STOP };
                            bulbClose.ParamData.NumOfVal = 0;
                            byte cr = LMX_func_api_Rec_Ctrl_Release(ref bulbClose, out uint ce);
                            var bulbFin = new LMX_STRUCT_REC_CTRL { CtrlID = (uint)Lmx_TagID_Rec_Ctrl_Release.LMX_DEF_LIB_TAG_REC_CTRL_RELEASE_BULB_FINALIZE };
                            bulbFin.ParamData.NumOfVal = 0;
                            byte fr = LMX_func_api_Rec_Ctrl_Release(ref bulbFin, out uint fe);
                            Logger.Info($"[LumixBulb] close (0x13 ret={cr} err={ce}) + finalize (0x19 ret={fr} err={fe}) done; awaiting image.");
                        } catch (Exception bex) {
                            Logger.Error("[LumixBulb] close/finalize threw: " + bex);
                            try { _downloadExposure?.TrySetException(bex); } catch { }
                        }
                    });
                    return;
                }
                if (exposureTime > 60.0) {
                    // Standard mode has no BULB, so it cannot exceed the 60s discrete-list maximum.
                    Notification.ShowWarning("Exposures over 60s need extended (LUMIX Tether) mode. Standard mode is capped at 60s.");
                    _downloadExposure.TrySetCanceled();
                    return;
                }

                // The camera accepts only discrete shutter speeds. NINA (e.g. the Flat Wizard's dichotomy) asks
                // for arbitrary times; snap to the nearest supported speed rather than failing and silently
                // reusing the previous value, which broke exposure-time search. Works on both the public and
                // Tether DLLs (both expose the supported-speed list and the setter). [needs on-camera verify]
                int rawSs = NearestSsRaw(exposureTime, out double actualSec);
                Logger.Info($"[LumixSS] req={exposureTime:0.#####}s ssTable={(_ssTable?.Count ?? -1)} nearest={actualSec:0.#####}s raw=0x{(uint)rawSs:X8}");
                if (rawSs != 0) {
                    if (Math.Abs(actualSec - exposureTime) > 1e-6) {
                        Logger.Info($"Requested {exposureTime:0.####}s is not a supported shutter speed; using nearest {actualSec:0.####}s.");
                    }
                    byte setR = LMX_func_api_SS_Set_Param(rawSs, out uint setErr);
                    LMX_func_api_SS_Get_Param(out int rbVal, out uint rbErr);
                    Logger.Info($"[LumixSS] set raw=0x{(uint)rawSs:X8} ret={setR} err={setErr}; readback=0x{(uint)rbVal:X8}");
                } else if (exposureTime >= 1 && _exposures.Contains((int)exposureTime)) {
                    // Fallback (capability table empty): original exact-match path.
                    LMX_func_api_SS_Set_Param((((int)(exposureTime) * 1000) | 0x80000000), out uint retError);
                } else if (exposureTime < 1) {
                    LMX_func_api_SS_Set_Param((int)(1000 / exposureTime), out uint retError);
                } else {
                    Notification.ShowWarning("Exposure could not be matched to a supported shutter speed; using the last good setting.");
                }
                // for non bulb
                Logger.Debug("Start capture");
                ret = LMX_func_api_Rec_Ctrl_Release(ref lmx_rec_ctrl, out retError);
                Logger.Debug($"Rec_Ctrl_Release ret={ret} retError={retError}");
                if (ret != LMX_BOOL_TRUE) {
                    // Only a genuine failure (function returned not-TRUE). The old code warned whenever
                    // retError==1, which is an error CODE not a bool — that fired a false error on the first shot.
                    // Decoded, because the raw number is what made the 2026-11-23 failure unreadable: err=262147
                    // was LMX_DEF_ERR_COM_DATA_RCV, a transport error - not the body refusing the shot.
                    Logger.Warning($"Single-shot release did not succeed (ret={ret}, err={LumixError.Describe(retError)}).");
                }
            }
        }

        public async Task WaitUntilExposureIsReady(CancellationToken token) {
            using (token.Register(() => AbortExposure())) {
                await _downloadExposure.Task;
            }
        }

        public void StopExposure() {
            // End a bulb exposure early: cancelling the hold timer fires the close (0x13) + finalize (0x19)
            // in the bulb task. For a normal (<=60s) frame there is no timer and nothing to stop.
            try { bulbCompletionCTS?.Cancel(); } catch { }
        }

        public void AbortExposure() {
            if (_downloadExposure != null && _downloadExposure.Task.Status <= TaskStatus.Running) {
                Notification.ShowWarning("Exposure still in progress. Cancelling it.");
                Logger.Warning("Exposure still in progress. Cancelling it.");
                try { bulbCompletionCTS?.Cancel(); } catch { }
                _downloadExposure.TrySetCanceled();
            }
            StopExposure();
        }

        public async Task<IExposureData> DownloadExposure(CancellationToken token) {
            if (_downloadExposure.Task.IsCanceled) { return null; }
            Logger.Debug("Waiting for download of exposure");
            await _downloadExposure.Task;
            Logger.Debug("Downloading of exposure complete. Converting image to internal array");

            try {
                var metaData = new ImageMetaData();
                metaData.FromCamera(this);

                // JPEG path: the camera captured a JPEG (or the user forced it) — decode it directly so NINA
                // can display it even when its RAW converter can't decode this body's .RW2 (e.g. GH7, issue #1).
                bool isJpeg = _lastFormat.IsEqual(Lmx_def_lib_object_format.LMX_DEF_OBJ_FORMAT_JPEG);
                if (isJpeg) {
                    // The camera captured a JPEG. If the user did NOT ask for JPEG, the body is set to a
                    // JPEG quality (not RAW) — warn, because the frame will be shown in grayscale. In standard
                    // mode the plugin cannot change the quality (Tether-only); the user must set RAW on the body.
                    if (!Properties.Settings.Default.PreferJpeg && !_warnedJpeg) {
                        _warnedJpeg = true;
                        Notification.ShowWarning(NativeBinding.ExtendedMode
                            ? "Camera captured JPEG, not RAW. Reconnect — the plugin sets RAW in extended mode."
                            : "Camera captured JPEG, not RAW. Set the camera's image quality to RAW on the body — the plugin can't change it in standard mode.");
                    }
                    Logger.Info($"Captured object is JPEG (PreferJpeg={Properties.Settings.Default.PreferJpeg}) — re-mosaicing to Bayer for colour display.");
                    return DecodeJpegToBayerExposure(buffer, metaData);
                }

                // Optional: decode the RW2 in-plugin with the bundled LibRaw 0.22 (for N.I.N.A. <= 3.2, whose
                // DCRaw/FreeImage converters don't know newer bodies — GH7, S5 II, S9 ...). OFF by default;
                // N.I.N.A. 3.3+ has its own LibRaw. On any failure fall through to N.I.N.A.'s converter.
                if (Properties.Settings.Default.UseBuiltInLibRaw) {
                    bool bitScaling = _profileService.ActiveProfile.CameraSettings.BitScaling;
                    if (LibRawDecoder.TryDecode(buffer, this.BitDepth, bitScaling, out var frame, out var librawError)) {
                        if (frame.Pattern != SensorType.Monochrome && metaData.Camera.BayerPattern != BayerPatternEnum.None) {
                            metaData.Camera.SensorType = frame.Pattern;
                            metaData.Camera.BayerOffsetX = 0;
                            metaData.Camera.BayerOffsetY = 0;
                        }
                        Logger.Info($"[LibRaw] decoded RW2 in-plugin: {frame.Width}x{frame.Height}, pattern={frame.Pattern}, max={frame.MaxPixelValue}, bitDepth={frame.BitDepth} (LibRaw {frame.LibRawVersion}).");
                        return _exposureDataFactory.CreateImageArrayExposureData(
                            input: frame.Pixels,
                            width: frame.Width,
                            height: frame.Height,
                            bitDepth: frame.BitDepth,
                            isBayered: frame.Pattern != SensorType.Monochrome,
                            metaData: metaData);
                    }
                    Logger.Error($"[LibRaw] built-in decode failed, falling back to N.I.N.A.'s RAW converter: {librawError}");
                    if (!_warnedLibRaw) {
                        _warnedLibRaw = true;
                        Notification.ShowWarning("Built-in LibRaw decoder failed (" + librawError + "). Falling back to N.I.N.A.'s RAW converter.");
                    }
                }

                return _exposureDataFactory.CreateRAWExposureData(
                    converter: _profileService.ActiveProfile.CameraSettings.RawConverter,
                    rawBytes: buffer,
                    rawType: "rw2",
                    bitDepth: this.BitDepth,
                    metaData: metaData);
            } finally {
                if (buffer != null) {
                    buffer = null;
                }
            }
        }

        // ---- Live-view stop gate ----
        // The SDK tears its own live-view buffers down inside Ctrl_LiveView_Stop, and the 16:15:33 full dump
        // on 2026-11-23 caught one of its own worker threads memcpy'ing into a 1.4 MB region that was already
        // MEM_FREE / PAGE_NOACCESS and outside every GC segment. We cannot stop the SDK racing itself, but we
        // can stop US adding to the overlap: no read may start once a stop has begun, and a stop waits for the
        // read already inside the DLL to come back out before it asks for the teardown.
        //
        // Latched deliberately: the flag is cleared by StartLiveView, never by StopLiveView, so a poller that
        // was sleeping through the whole stop cannot wake up and call into a torn-down live view.
        private readonly object _lvGate = new object();
        private int _lvReadsInFlight;
        private bool _lvStopping;

        // A live-view read returns in milliseconds (it answers "no frame ready" immediately), so a drain that
        // has not finished in this long means the call is wedged, not slow. Then we stop anyway: a hung stop
        // would freeze whatever asked for it, and the log line says which case happened.
        private const int LIVEVIEW_DRAIN_TIMEOUT_MS = 1500;

        // Breathing room after the stop before the session itself is torn down, for the SDK's own live-view
        // thread to finish whatever frame it was mid-delivery on. It narrows the window; it does not close it,
        // because that thread is not ours to wait on.
        private const int LIVEVIEW_STOP_SETTLE_MS = 250;

        /// <summary>
        /// Claims the right to call the SDK's live-view reader. False means a stop has begun and the caller
        /// must not touch the DLL. Every true must be paired with <see cref="EndLiveViewRead"/>.
        /// </summary>
        private bool TryBeginLiveViewRead() {
            lock (_lvGate) {
                if (_lvStopping) { return false; }
                _lvReadsInFlight++;
                return true;
            }
        }

        private void EndLiveViewRead() {
            lock (_lvGate) {
                _lvReadsInFlight--;
                if (_lvReadsInFlight <= 0) { Monitor.PulseAll(_lvGate); }
            }
        }

        /// <summary>
        /// Latches live view as stopping and waits for in-flight reads to leave the DLL.
        /// Returns false if the wait timed out, i.e. a read is still inside the SDK.
        /// </summary>
        private bool DrainLiveViewReads() {
            lock (_lvGate) {
                _lvStopping = true;
                int deadline = Environment.TickCount + LIVEVIEW_DRAIN_TIMEOUT_MS;
                while (_lvReadsInFlight > 0) {
                    int remaining = deadline - Environment.TickCount;
                    if (remaining <= 0) { return false; }
                    Monitor.Wait(_lvGate, remaining);
                }
                return true;
            }
        }

        public void StartLiveView(CaptureSequence sequence) {
            lock (_lvGate) { _lvStopping = false; }
            byte r = LMX_func_api_Ctrl_LiveView_Start(out uint lvErr);
            // Start and stop were both silent before. The 16:15 log therefore could not say whether a stop had
            // been issued at all, which is exactly what you need to know about a teardown race.
            Logger.Info($"[Lumix] live view start: ret={r} err=0x{lvErr:X8}");
            LiveViewEnabled = true;
        }

        // How long to wait before asking the camera for a live-view frame again when it had none ready. NINA's
        // live-view loop has no delay of its own, so without this the poll is a spin.
        private const int LIVEVIEW_EMPTY_POLL_WAIT_MS = 20;

        public Task<IExposureData> DownloadLiveView(CancellationToken token) {
            return Task.Run<IExposureData>(() => {
                // The camera does not always have a frame ready, and `returnedJpegSize` — written by the SDK on
                // every call — was never read, here or in the grayscale path this replaced. The whole fixed-size
                // buffer went to the JPEG decoder, so an empty poll meant decoding a zero-filled array: that
                // throws WINCODEC_ERR_COMPONENTNOTFOUND, and because NINA's loop has no catch of its own the
                // exception ended live view (observed 2026-11-23 16:15:33). Poll until a frame actually arrives.
                // In extended mode all four destinations come from one permanently pinned block — the JPEG and
                // the three write-only info structs. Handing this SDK a `ref` to a local is what produced the
                // use-after-free we proved from the 16:15 dump. Standard mode is unchanged: that is a different
                // DLL, and nothing has been shown about its buffer lifetime either way.
                bool pinnedPath = NativeBinding.ExtendedMode;
                byte[] lvFallback = pinnedPath ? null : new byte[LMX_DEF_LIVEVIEW_STREAMDATA_SIZE_MAX];
                byte[] block = null;
                int jpegAt = 0;
                uint returnedJpegSize = 0;
                while (true) {
                    token.ThrowIfCancellationRequested();
                    byte r = 0;
                    // Hold the stop off for the duration of the native call, and refuse to make one at all once
                    // a stop has begun. NINA cancels the token and calls StopLiveView, in that order, so the
                    // throw below is the path for a stop that arrives first - which NINA already handles,
                    // because a cancelled download is how live view ends every time.
                    if (!TryBeginLiveViewRead()) {
                        throw new OperationCanceledException("Lumix: live view was stopped while waiting for a frame.");
                    }
                    try {
                        if (pinnedPath) {
                            jpegAt = LumixCam.LiveViewJpegOffset;
                            r = LumixCam.Ext_TryGetLiveViewData(out block, out returnedJpegSize, out retError) ? (byte)LMX_BOOL_TRUE : (byte)0;
                        } else {
                            LMX_STRUCT_LIVEVIEW_INFO_HISTGRAM histgramBuf = new LMX_STRUCT_LIVEVIEW_INFO_HISTGRAM();
                            LMX_STRUCT_LIVEVIEW_INFO_POSTURE postureBuf = new LMX_STRUCT_LIVEVIEW_INFO_POSTURE();
                            LMX_STRUCT_LIVEVIEW_INFO_LEVEL levelBuf = new LMX_STRUCT_LIVEVIEW_INFO_LEVEL();
                            block = lvFallback;
                            jpegAt = 0;
                            r = LMX_func_api_Get_LiveView_data(ref histgramBuf, out _, ref postureBuf, out _, ref levelBuf, out _, ref lvFallback[0], out returnedJpegSize, out retError);
                        }
                    } finally {
                        EndLiveViewRead();
                    }
                    if (r == LMX_BOOL_TRUE && block != null && returnedJpegSize > 4
                        && jpegAt + returnedJpegSize <= block.Length
                        && block[jpegAt] == 0xFF && block[jpegAt + 1] == 0xD8 && block[jpegAt + 2] == 0xFF) {
                        break;
                    }
                    System.Threading.Thread.Sleep(LIVEVIEW_EMPTY_POLL_WAIT_MS);
                }

                // Copy out the bytes the camera actually wrote. Required, not tidiness: on the pinned path the
                // block is shared and the next frame overwrites it.
                byte[] frame = new byte[returnedJpegSize];
                Buffer.BlockCopy(block, jpegAt, frame, 0, (int)returnedJpegSize);

                var metaData = new ImageMetaData();
                metaData.FromCamera(this);
                // The camera's live-view JPEG is already full colour. Re-mosaic it into a synthetic
                // single-plane Bayer (RGGB) array and hand it to NINA as isBayered so NINA's own
                // debayer step (the same one used for real captures) produces a colour preview,
                // instead of flattening the frame to Gray16 as before.
                return DecodeJpegToBayerExposure(frame, metaData);
            });
        }

        /// <summary>
        /// Decodes a JPEG frame that the camera already rendered in full colour (live view, or the
        /// JPEG-capture fallback) and re-mosaics it into a synthetic single-plane Bayer array matching
        /// this driver's declared SensorType (RGGB, offset 0,0), so it flows through NINA's normal
        /// debayer pipeline instead of being collapsed to grayscale. Colour resolution is down-sampled
        /// to sensor-pattern resolution in the process (one channel kept per pixel), which is irrelevant
        /// for live-view framing/focus use but means this is NOT a substitute for real RAW capture data.
        /// </summary>
        private IExposureData DecodeJpegToBayerExposure(byte[] jpeg, ImageMetaData metaData) {
            using (var memStream = new MemoryStream(jpeg)) {
                memStream.Position = 0;

                JpegBitmapDecoder decoder = new JpegBitmapDecoder(memStream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);

                FormatConvertedBitmap bitmap = new FormatConvertedBitmap();
                bitmap.BeginInit();
                bitmap.Source = decoder.Frames[0];
                bitmap.DestinationFormat = System.Windows.Media.PixelFormats.Rgb48; // keep full colour, 16 bit/channel
                bitmap.EndInit();

                int width = bitmap.PixelWidth;
                int height = bitmap.PixelHeight;
                ushort[] rgb = new ushort[width * height * 3];
                bitmap.CopyPixels(rgb, width * 3 * 2, 0);

                ushort[] bayer = new ushort[width * height];
                // RGGB: (evenRow,evenCol)=R  (evenRow,oddCol)=G  (oddRow,evenCol)=G  (oddRow,oddCol)=B
                for (int y = 0; y < height; y++) {
                    bool evenRow = (y % 2 == 0);
                    int rowBase = y * width;
                    int rgbRowBase = rowBase * 3;
                    for (int x = 0; x < width; x++) {
                        bool evenCol = (x % 2 == 0);
                        int rgbIdx = rgbRowBase + x * 3;
                        ushort value;
                        if (evenRow && evenCol) {
                            value = rgb[rgbIdx];       // R
                        } else if (!evenRow && !evenCol) {
                            value = rgb[rgbIdx + 2];   // B
                        } else {
                            value = rgb[rgbIdx + 1];   // G
                        }
                        bayer[rowBase + x] = value;
                    }
                }

                return _exposureDataFactory.CreateImageArrayExposureData(
                        input: bayer,
                        width: width,
                        height: height,
                        bitDepth: 16,
                        isBayered: true,
                        metaData: metaData);
            }
        }

        public void StopLiveView() {
            // Drain before the teardown, never after. Safe to call twice: the second drain returns at once.
            if (!DrainLiveViewReads()) {
                Logger.Warning($"[Lumix] live view stop: a read was still inside the SDK after {LIVEVIEW_DRAIN_TIMEOUT_MS}ms; stopping anyway.");
            }
            byte r = LMX_func_api_Ctrl_LiveView_Stop(out uint lvErr);
            Logger.Info($"[Lumix] live view stop: ret={r} err=0x{lvErr:X8}");
            LiveViewEnabled = false;
        }

        public Task<bool> Connect(CancellationToken token) {
            return Task.Run<bool>(() => {
                try {
                    // Does the options setting still match the mode this process actually loaded? The mode is
                    // bound once, when N.I.N.A. starts: NativeBinding.Initialize runs in the provider's
                    // constructor and the first P/Invoke caches the resolved native handle for the whole
                    // assembly, with no public API to drop it. So toggling the setting and reconnecting cannot
                    // change anything - and on 2026-11-26 that presented as 120 s exposures timing out with
                    // nothing in the log to explain it.
                    //
                    // This check lives HERE, in Connect, and not in the provider's constructor: an earlier
                    // version put it there, where Initialize has just set ExtendedMode from this very setting,
                    // so the two always agreed and the warning could never fire. Connect is what a reconnect
                    // calls, which is when the user is actually waiting to find out.
                    if (Properties.Settings.Default.UseTetherExtended != NativeBinding.ExtendedMode) {
                        string want = Properties.Settings.Default.UseTetherExtended ? "extended (LUMIX Tether)" : "standard (public SDK)";
                        string have = NativeBinding.ExtendedMode ? "extended (LUMIX Tether)" : "standard (public SDK)";
                        Logger.Warning($"Lumix: the plugin options ask for {want} mode but this N.I.N.A. session " +
                                       $"loaded {have} mode. The mode is bound at start-up and cannot be changed " +
                                       "while N.I.N.A. runs, so reconnecting will not apply it. Restart N.I.N.A.");
                        try {
                            Notification.ShowWarning($"Lumix: {want} mode needs a N.I.N.A. restart." + Environment.NewLine +
                                                     $"Still running in {have} mode until then.");
                        } catch { }
                    }

                    // Wait for a teardown left running by a previous Disconnect before touching the SDK.
                    // Disconnect returns as soon as it has STARTED its teardown, so a fast reconnect - and
                    // N.I.N.A.'s Advanced API can issue one within a millisecond - used to open a new session
                    // while the old teardown was still on its way to CloseSession/CloseDevice. Those then closed
                    // the session this Connect had just opened: N.I.N.A. reported the camera connected, and the
                    // next exposure died with no driver log line at all, surfacing only as N.I.N.A.'s own
                    // "did not set image as ready" timeout. Measured on this rig on 2026-11-26: CloseSession at
                    // 11:44:48.3829, Connect reporting Connected=True at .3885, CloseDevice finishing at .5334,
                    // and the 3 s exposure three seconds later dead. One reconnect was enough to do it.
                    Task pendingTeardown = _teardownTask;
                    if (pendingTeardown != null && !pendingTeardown.IsCompleted) {
                        var teardownWait = System.Diagnostics.Stopwatch.StartNew();
                        Logger.Info("[Lumix] Connect: a teardown from the previous disconnect is still running; waiting for it.");
                        bool finished;
                        try {
                            finished = pendingTeardown.Wait(TEARDOWN_WAIT_MS);
                        } catch (Exception tx) {
                            // The teardown faulted. That is its own business - it logs its own errors - and the
                            // only thing that matters here is that it is no longer running.
                            Logger.Info("[Lumix] Connect: the previous teardown ended with an error: " + tx.Message);
                            finished = true;
                        }
                        if (finished) {
                            Logger.Info($"[Lumix] Connect: previous teardown finished after {teardownWait.ElapsedMilliseconds} ms; connecting.");
                        } else {
                            Logger.Warning($"[Lumix] Connect: the previous teardown has not finished after {TEARDOWN_WAIT_MS} ms. " +
                                           "Connecting anyway - an SDK call is stalled, and if this connection misbehaves, restart N.I.N.A.");
                        }
                    }

                    //connect to device
                    if (NativeBinding.ExtendedMode) {
                        _connected = LumixCam.Ext_Connect(_index, out retError);
                    } else {
                        ret = LumixCam.LMX_func_api_Select_PnPDevice(_index, ref _lmxConnectDeviceInfo, out retError);
                        ret = LumixCam.LMX_func_api_Open_Session(0x00010001, out deviceConnectVer, out retError);
                        _connected = true;
                    }

                    // Extended: force JPEG capture (workaround for bodies whose .RW2 the RAW converter can't
                    // read, e.g. GH7). When PreferJpeg is OFF, force RAW back — otherwise a camera left in JPEG
                    // by a previous PreferJpeg session stays in JPEG and every frame comes back grayscale.
                    if (NativeBinding.ExtendedMode) {
                        uint q = Properties.Settings.Default.PreferJpeg ? LumixCam.IMGQ_JPEG_FINE : LumixCam.IMGQ_RAW;
                        LumixCam.Ext_SetImageQuality(q, out retError);
                        Logger.Info($"Image quality set to {(Properties.Settings.Default.PreferJpeg ? "JPEG" : "RAW")} (0x{q:X}, err={retError}).");
                    }

                    ret = LMX_func_api_SS_Get_Capability(ref SS_CapaInfo, out retError);
                    ret = LMX_func_api_ISO_Get_Capability(ref Iso_CapaInfo, out retError);
                    // Rebuild the cached lists from THIS connection's fresh capability data (a reconnect reuses
                    // the driver, and stale/empty caches left gain unpopulated).
                    _gains = null; _gainsRaw = null; _ssTable = null; _exposures = null; _warnedJpeg = false; _warnedLibRaw = false;

                    // Warn (never block) about the exposure mode. Use the dedicated Get_Mode_Pos getter, not the
                    // CameraMode capability struct (whose Tether-buffer layout differs, so CurVal_mode_pos read
                    // wrong and falsely warned "not in M"). Only the Tether DLL reports C1-C3 positions.
                    if (LMX_func_api_CameraMode_Get_Mode_Pos(out uint modePosConnect, out retError) == LMX_BOOL_TRUE) {
                        CheckExposureMode((ushort)modePosConnect);
                    }

                    // Get current parameter values
                    ret = LumixCam.LMX_func_api_ISO_Get_Param(out _curIsoValue, out retError);
                    ret = LumixCam.LMX_func_api_SS_Get_Param(out _curSsValue, out retError);
                    //not useful in nina context
                    //ret = LumixCam.LMX_func_api_Aperture_Get_Param(out _curApetureValue, out retError);
                    //ret = LumixCam.LMX_func_api_WB_Get_Param(out _curWbValue, out retError);
                    //ret = LumixCam.LMX_func_api_Exposure_Get_Param(out _curExposureCompValue, out retError);

                    // Register callback function
                    returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_OBJCT_ADD, callback);
                    returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_OBJCT_REQ_TRNSFER, callback);
                    returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_REC_CTRL_RELEASE, callback);
                    returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_SHUTTER, callback);
                    returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_ISO, callback);

                    // Save destination (extended mode only): SD only / PC only (cardless) / PC+SD. With a PC
                    // target the camera fires OBJCT_REQ_TRNSFER on capture and we pull the frame via Get_Object.
                    if (NativeBinding.ExtendedMode) {
                        // Resolve before setting, not after: ResolveSaveTarget reads the card and may fall back
                        // to PC, and the camera must be given the target that is actually going to be used.
                        ushort wantedTgt = (ushort)Properties.Settings.Default.SaveTarget;
                        ushort saveTgt = ResolveSaveTarget(wantedTgt);
                        _effectiveSaveTarget = saveTgt;
                        // From here on the card reading is a reconnect reading, and not to be believed.
                        _processHasConnectedBefore = true;
                        LumixCam.LMX_func_api_SetupFilesConfig_Set_Target(saveTgt, out retError);
                        Logger.Info($"Save target set to {saveTgt} (0=SD,1=PC,2=PC+SD), err={LumixError.Describe(retError)}." +
                                    (saveTgt != wantedTgt ? $" The setting asked for {wantedTgt}; fell back to PC because the card cannot be written." : ""));
                        // The session's first battery read, taken here because the channel is quiet at connect.
                        // It also starts the refresh interval, so the first exposure does not immediately read
                        // again; after this the only reads are the throttled idle-point refreshes. See the
                        // policy note above BatteryLevel.
                        _batteryPercent = ReadBatteryPercent();
                        _lastBatteryRefreshUtc = DateTime.UtcNow;
                    }

                    lumixCameras = new Dictionary<string, CameraSpecs>
                         {
            { "DC-S1R", new CameraSpecs { Model = "DC-S1R", Width = 8368, Height = 5584, PixelPitch = 4.3 } },
            { "DC-S1RII", new CameraSpecs { Model = "DC-S1RII", Width = 8144, Height = 5434, PixelPitch = 4.39 } },
            { "DC-S1", new CameraSpecs { Model = "DC-S1", Width = 6000, Height = 4000, PixelPitch = 5.94 } },
            { "DC-S1H", new CameraSpecs { Model = "DC-S1H", Width = 6000, Height = 4000, PixelPitch = 5.94 } },
            { "DC-S5", new CameraSpecs { Model = "DC-S5", Width = 6000, Height = 4000, PixelPitch = 5.94 } },
            { "DC-S5M2", new CameraSpecs { Model = "DC-S5M2", Width = 6000, Height = 4000, PixelPitch = 5.94 } },
            { "DC-S5M2X", new CameraSpecs { Model = "DC-S5M2X", Width = 6008, Height = 4008, PixelPitch = 5.94 } },
            { "DC-S9", new CameraSpecs { Model = "DC-S9", Width = 6000, Height = 4000, PixelPitch = 5.94 } },      // 24 MP full frame (same sensor as S5 II)
            { "DC-BGH1", new CameraSpecs { Model = "DC-BGH1", Width = 3680, Height = 2760, PixelPitch = 4.68 } },
            { "DC-BGS1", new CameraSpecs { Model = "DC-BGS1", Width = 6000, Height = 4000, PixelPitch = 5.91 } },
            { "DC-GH6", new CameraSpecs { Model = "DC-GH6", Width = 5776 , Height = 4336, PixelPitch = 3 } },
            { "DC-GH7", new CameraSpecs { Model = "DC-GH7", Width = 5776 , Height = 4336, PixelPitch = 3 } },
            { "DC-GH5", new CameraSpecs { Model = "DC-GH5", Width = 5184, Height = 3888, PixelPitch = 3.33 } },
            { "DC-GH5S", new CameraSpecs { Model = "DC-GH5S", Width = 3680 , Height = 2760, PixelPitch = 4.68 } },
            { "DC-GH5M2", new CameraSpecs { Model = "GH5M2", Width = 5184, Height = 3888, PixelPitch = 3.33} },
            { "DC-G9", new CameraSpecs { Model = "DC-G9", Width = 5184, Height = 3888, PixelPitch = 3.33 } },
            { "DC-G9M2", new CameraSpecs { Model = "DC-G9M2", Width = 5776, Height = 4336, PixelPitch = 3 } },     // 25 MP MFT (same sensor as GH7)
            { "DefaultLumix", new CameraSpecs { Model = "DefaultLumix", Width = 6000, Height = 4000, PixelPitch = 5.9 } }
        };

                    if (!lumixCameras.TryGetValue(_lmxDevInfo.dev_ModelName, out _currentCameraspecs)) {
                        Notification.ShowWarning("Camera is not known. Defaulting to full size sensor values");
                        lumixCameras.TryGetValue("DefaultLumix", out _currentCameraspecs);
                        Notification.ShowWarning("Overriding Height from Plugin Settings");
                    }
                    if (Properties.Settings.Default.SensorHeight != 0) {
                        _currentCameraspecs.Height = Properties.Settings.Default.SensorHeight;
                        Notification.ShowWarning("Overriding Height from Plugin Settings");
                    }
                    if (Properties.Settings.Default.PixelPitch != 0) {
                        _currentCameraspecs.PixelPitch = Properties.Settings.Default.PixelPitch;
                        Notification.ShowWarning("Overriding Pitch from Plugin Settings");
                    }
                    if (Properties.Settings.Default.SensorWidth != 0) {
                        _currentCameraspecs.Width = Properties.Settings.Default.SensorWidth;
                        Notification.ShowWarning("Overriding Width from Plugin Settings");
                    }

                    // Fix the transfer buffer's capacity for the session, now that the sensor is known - this
                    // has to come AFTER the specs table and the user's overrides above, because an earlier
                    // version of this sat before them and sized from 0x0, silently falling back to the
                    // default. 2 bytes per pixel covers 14-bit samples padded to 16, plus 16 MB for the
                    // thumbnail and metadata an RW2 carries. Set once; never grown, because a buffer the SDK
                    // may still be writing into can never be freed.
                    int capPx = _currentCameraspecs.Width * _currentCameraspecs.Height;
                    if (capPx > 0) {
                        long cap = (long)capPx * 2L + 16L * 1024L * 1024L;
                        if (cap > MAX_OBJECT_BYTES) { cap = MAX_OBJECT_BYTES; }
                        LumixCam.SetObjectBufferCapacity((int)cap);
                    }
                    Logger.Info($"Transfer buffer capacity for this session: {LumixCam.ObjectBufferCapacity} bytes " +
                                $"(sensor {_currentCameraspecs.Width}x{_currentCameraspecs.Height}" +
                                (capPx > 0 ? ")." : ", unknown - using the built-in default)."));

                    //not useful in Nina context
                    //returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_APERTURE, callback);
                    //returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_WHITEBALANCE, callback);
                    //returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_EXPOSURE, callback);
                    //returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_AF_CONFIG, callback);
                    //returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_OBJCT_ADD, callback);
                    //returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_REC_CTRL_AFAE, callback);
                    //returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_REC_CTRL_ZOOM, callback);
                    //returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_REC_CTRL_LENS, callback);
                } catch (Exception ex) {
                    Logger.Error(ex);
                }
                Logger.Info($"[Lumix] Connected={_connected} ExtendedMode={NativeBinding.ExtendedMode} DLL='{NativeBinding.ActiveDllPath}' ExposureMin={ExposureMin:0.#####}s ExposureMax={ExposureMax:0}s Gain={Gain} GainMin={GainMin} GainMax={GainMax} Gains={Gains?.Count ?? 0}");
                return _connected;
            });
        }

        public void Disconnect() {
            if (!Connected) { return; }
            Logger.Info("[Lumix] Disconnect: start");
            _connected = false;                 // report disconnected immediately — NINA's Disconnect() never blocks
            try { bulbCompletionCTS?.Cancel(); } catch { }
            // Native teardown is fire-and-forget: some SDK calls (CloseSession after a bulb) can block for a long
            // time, and we must never freeze NINA. If a call stalls it leaks a worker thread, but the UI stays
            // responsive. Each step is logged so the last line before it stops shows which call stalls.
            _teardownTask = Task.Run(() => {
                try {
                    // Live view first. Disconnect used to go straight to Delete_CallBackInfo and CloseSession
                    // with the SDK's live-view thread still running and still writing into session memory the
                    // close was about to release - the same shape as the use-after-free the 16:15 dump caught.
                    if (LiveViewEnabled) {
                        Logger.Info("[Lumix] Disconnect: stopping live view before teardown");
                        StopLiveView();
                        System.Threading.Thread.Sleep(LIVEVIEW_STOP_SETTLE_MS);
                    }
                    Logger.Info("[Lumix] Disconnect: delete callbacks");
                    LumixCam.LMX_func_api_Delete_CallBackInfo((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_OBJCT_ADD);
                    LumixCam.LMX_func_api_Delete_CallBackInfo((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_OBJCT_REQ_TRNSFER);
                    LumixCam.LMX_func_api_Delete_CallBackInfo((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_REC_CTRL_RELEASE);
                    LumixCam.LMX_func_api_Delete_CallBackInfo((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_SHUTTER);
                    LumixCam.LMX_func_api_Delete_CallBackInfo((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_ISO);
                    if (NativeBinding.ExtendedMode) {
                        // Take the camera out of BULB before closing the session — a lingering bulb state appears
                        // to hang CloseSession (a plain, non-bulb disconnect closes fine).
                        try {
                            byte sr = LMX_func_api_SS_Set_Param((1 * 1000) | 0x80000000, out uint sserr);
                            Logger.Info($"[Lumix] Disconnect: exit BULB (SS=1s) ret={sr} err={sserr}");
                        } catch (Exception sx) { Logger.Error("[Lumix] Disconnect: exit-BULB threw: " + sx); }
                        if (Properties.Settings.Default.SaveTarget != LumixCam.SAVE_TARGET_SD) {
                            Logger.Info("[Lumix] Disconnect: restore SD target");
                            LumixCam.LMX_func_api_SetupFilesConfig_Set_Target(LumixCam.SAVE_TARGET_SD, out retError);
                        }
                        // Let any in-flight bulb close/finalize fully settle before tearing down the session,
                        // then step through the close so the log shows which call (Session vs Device) stalls.
                        try { bulbCompletionTask?.Wait(3000); } catch { }
                        System.Threading.Thread.Sleep(400);
                        Logger.Info("[Lumix] Disconnect: CloseSession");
                        LumixCam.Ext_CloseSessionOnly(out retError);
                        Logger.Info("[Lumix] Disconnect: CloseSession done; CloseDevice");
                        LumixCam.Ext_CloseDeviceOnly(out retError);
                        Logger.Info("[Lumix] Disconnect: CloseDevice done");
                    } else {
                        Logger.Info("[Lumix] Disconnect: Close_Session/Device");
                        LumixCam.LMX_func_api_Close_Session(out retError);
                        LumixCam.LMX_func_api_Close_Device(out retError);
                    }
                    Logger.Info("[Lumix] Disconnect: native teardown done");
                } catch (Exception ex) { Logger.Error("[Lumix] Disconnect teardown threw: " + ex); }
            });
            Logger.Info("[Lumix] Disconnect: returned (teardown running async)");
        }
    }
}