# LumixCamera

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