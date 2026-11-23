# LumixCamera

## 1.2.1.0
- **A frame the camera cannot deliver no longer takes N.I.N.A. down with it.** An object
  reported with no data used to complete the exposure as a success with nothing in it, and
  nothing guarded the SDK's capture callback, so the process died outright. The frame now
  fails as a capture error and the session carries on.
- **The SD card is checked at connect.** When the save target writes to the card, the plugin
  warns if the card is full, nearly full, missing, or cannot be read - and says which. On a
  PC-only save target nothing is raised, because the card is not used.
- **Battery level is reported for real** instead of always showing 100%. Unknown is reported
  as unknown rather than guessed at.

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