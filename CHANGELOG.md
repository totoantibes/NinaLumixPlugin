# LumixCamera

## 1.3.1.2
Four fixes, all of them found or proven on a camera rather than reasoned about. Two came from
review of 1.3.1.1 by the plugin-manifest maintainer; the other two were found while verifying
those on hardware.

- **A quick reconnect could leave N.I.N.A. connected to a camera session that no longer existed.**
  Disconnect returns as soon as it has started its teardown, so the interface never freezes, but
  nothing waited for that teardown to finish. Reconnect fast enough and the old teardown closed
  the session the new connect had just opened - N.I.N.A. then reported the camera connected, and
  the next exposure died with nothing in the log to explain it, showing up only as "camera did not
  set image as ready". Reconnecting by hand is slow enough to be safe by luck; anything driving
  N.I.N.A. through its API is not. A reconnect now waits for the previous teardown.
- **Memory held for image transfers no longer grows through a session.** The pinned transfer buffer
  was replaced whenever a larger frame arrived, and because the camera's SDK can still write into
  a retired buffer, none of them could ever be released - about 170 MB held after a 131-frame
  session. The size is now fixed once, when the camera connects, from that body's sensor.
- **A frame that never arrives now fails the exposure instead of waiting.** Some bodies announce a
  placeholder before the real frame, which the plugin has always ignored and waited past. If the
  real frame never came, the exposure only ended on N.I.N.A.'s own timeout. It now fails after ten
  seconds, so a sequence moves on to the next frame.
- **Choosing the SD card as the save destination no longer loses the session when no card is in.**
  With the save destination set to SD only and the slot empty, the camera sends nothing at all and
  every exposure ran to a timeout - a night could produce no frames. If the card is missing or full
  when N.I.N.A. starts, the session now saves to the PC and says so. The setting itself is left
  alone, and a restart is needed once the card is sorted out.

Also:

- **Switching between extended and standard mode requires restarting N.I.N.A.**, which was always
  true and is now stated in the plugin options and the description, instead of presenting as
  exposures that time out for no visible reason. The plugin warns when the setting and the mode in
  use disagree.
- A spurious "there is no card in the camera" warning on reconnect is gone. The camera's answer
  about the card cannot be trusted after a reconnect - it is wrong in both directions and only
  sometimes - so it is now believed only on the first connection after N.I.N.A. starts.

`docs/crash-analysis-2026-11-24.md` carries the measurements behind all of this, including what
could not be tested and why.

## 1.3.1.1
Metadata only - no code changes, and functionally identical to 1.3.1.0.

- **Contributor credit now appears in the plugin description** that N.I.N.A. shows, rather than
  only in the changelog. Colour live view and the optional built-in LibRaw decoder are the work
  of [flexo-112](https://github.com/flexo-112), contributed in
  [#2](https://github.com/totoantibes/NinaLumixPlugin/pull/2), along with the sensor-table
  corrections and the release workflow. Those features first shipped in 1.3.0.0, which was
  withdrawn before it reached the plugin manager, so 1.3.1.0 was the first release in which they
  actually reached users - and it credited nobody where users would see it.
- The plugin's one-line description no longer says "nightly". It had said so since 1.0.0.30.

## 1.3.1.0
Includes everything from 1.3.0.0, which was withdrawn before it reached the plugin manager while
the crash below was investigated - so for anyone updating from 1.2.0.0, the colour live view and
the optional built-in LibRaw decoder arrive here. Those, the sensor-table corrections and the
release workflow are the work of [flexo-112](https://github.com/flexo-112), contributed in
[#2](https://github.com/totoantibes/NinaLumixPlugin/pull/2). Thank you.

- **The crash that took N.I.N.A. down mid-session is fixed, and the cause is now proven rather
  than guessed.** Panasonic's Tether DLL is not thread-safe on one session context: two
  overlapping calls deadlock it, and a single call landing inside an image transfer makes that
  transfer fail while leaving it outstanding. The DLL then completes the abandoned transfer one
  to two minutes later, writing a full frame into memory it has already released - which
  corrupts whatever now owns it and kills the process somewhere unrelated. Five sessions died
  this way on 23-24 November. Every native call now goes through one gate, so the plugin can no
  longer provoke it. The mechanism is reproducible on demand with the probes in `tools/`; the
  full analysis is in `docs/crash-analysis-2026-11-24.md`.
- **The battery reading is back.** 1.3.0.0 removed it because a refresh from N.I.N.A.'s
  2-second device poll was landing inside image transfers. It is now refreshed at an idle point
  in the capture cycle instead - at most once every five minutes, never during live view, and
  skipped if the SDK is busy - so the reading is at most five minutes stale instead of frozen at
  its connect-time value. Extended (LUMIX Tether) mode only: the public SDK has no battery
  function to call.
- **Buffers handed to the SDK are no longer collectible.** The object transfer and all four
  live-view destinations now come from permanently pinned native memory rather than fresh
  managed arrays, because `ref array[0]` pins only for the duration of the call and this SDK
  writes to buffers after the call has returned.
- **Live view shuts down without racing itself.** Stopping live view now waits for an in-flight
  read to come back out of the DLL before asking for the teardown, and disconnecting stops live
  view first and settles before touching the session. Start and stop are logged with their
  result, which they were not.
- **SDK error codes are decoded in the log.** `err=262147` was unreadable and is what made the
  23 November failure hard to follow; the log now names the condition.

## 1.3.0.0
- **A frame the camera cannot deliver no longer takes N.I.N.A. down with it.** An object
  reported with no data used to complete the exposure as a success with nothing in it, and
  nothing guarded the SDK's capture callback, so the process died outright. The frame now
  fails as a capture error and the session carries on.
- **Built-in LibRaw decoder (optional, off by default).** For N.I.N.A. 3.2 and earlier, whose
  DCRaw/FreeImage converters show the RW2 of newer bodies (GH7, S5 II, S9, G9 II ...) as noise
  (issue #1), the plugin can now decode the RW2 itself with a bundled LibRaw 0.22.2 (official
  libraw.org build, LGPL/CDDL) and hand N.I.N.A. a bayered frame with the pattern read from LibRaw.
  Enable **Use built-in LibRaw decoder** in the options. On any failure it falls back to N.I.N.A.'s
  converter. Not needed on N.I.N.A. 3.3+, which ships its own LibRaw.
- **Colour live view.** The live-view JPEG from the camera is re-mosaiced into a synthetic RGGB
  Bayer frame and handed to N.I.N.A. as bayered, so its normal debayer step produces a colour
  preview instead of the previous 16-bit grayscale conversion.
- The **Prefer JPEG** capture fallback uses the same path, so JPEG captures are also shown in colour.
- Live view no longer stops when the camera has no frame ready. The frame is trimmed to the size the
  camera reports and an empty poll waits briefly, instead of handing the JPEG decoder an empty
  buffer - which threw an error out of N.I.N.A.'s live-view loop and ended the preview.
- **The SD card is checked at connect.** When the save target writes to the card, the plugin
  warns if the card is full, nearly full, missing, or cannot be read - and says which. On a
  PC-only save target nothing is raised, because the card is not used.
- **Battery level is reported for real** instead of always showing 100%. Unknown is reported
  as unknown rather than guessed at.
- Sensor table: **DC-S9** corrected to its real 24 MP full-frame sensor (6000x4000, 5.94 um; was a
  bogus 8192x5464 / 2.1 um entry that skewed the FOV) and **DC-G9M2** to the GH7's 25 MP MFT sensor
  (5776x4336, 3 um; was a copy of the G9's).
- Fixed the release workflow: package the renamed `LumixPlugin.dll` (the 1.2.0.0 build packaged the
  old assembly name and failed), take the release notes from this file's section matching the tag,
  and drop the hardcoded runner path.
- Build: the post-build copy to the local N.I.N.A. plugin folder only runs on Windows, so the project
  also builds on Linux/CI with `EnableWindowsTargeting`.

Colour live view, the optional built-in LibRaw decoder, the sensor-table corrections and the
release-workflow fixes in this release are the work of [flexo-112](https://github.com/flexo-112),
contributed in [#2](https://github.com/totoantibes/NinaLumixPlugin/pull/2). Thank you.

## 1.2.0.0
- Optional **LUMIX Tether extended mode**: when enabled in the plugin options the driver
  loads the DLL from a local install of Panasonic's free LUMIX Tether application (never
  bundled) to unlock features the public SDK does not expose.
- Extended mode enables **BULB / exposures beyond the 60 s public-SDK cap**, RAW/JPEG image-quality
  selection, battery information, and live camera-state reporting.
- **Sub-second and full-range shutter speeds**: exposures snap to the nearest supported speed so
  N.I.N.A.'s Flat Wizard converges (previously non-listed and sub-second times failed or stuck).
- **Camera mode** is read (including the C1-C3 custom presets); the driver warns — but never
  blocks — when the dial is not in a manual-capable mode.
- **Save destination** option: SD card, PC (cardless), or both.
- Real capture-completion event replaces the previous SD-card-file workaround; fixes the
  first-capture error.
- **Prefer JPEG** option: forces JPEG capture as a workaround for cameras whose RAW (.RW2)
  N.I.N.A. cannot decode (e.g. GH7).
- Standard (public-SDK) mode is unchanged and remains the default; the public DLL now deploys
  alongside the plugin assembly.

## 1.0.0.1
- Initial release