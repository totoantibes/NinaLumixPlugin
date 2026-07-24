# LumixCamera
Lumix native plugin provides a USB direct method to interface with [compatible Lumix cameras](https://av.jpn.support.panasonic.com/support/global/cs/soft/tool/sdk.html).

This differs from the ASCOM Driver which interfaces over wifi and http. As the ASCOM driver allows a wider set of cameras it also does not provide a liveview and is rather slow in downloading the RAW images to Nina.

# Two modes: Standard and Extended

The plugin runs in one of two modes, selected in the plugin options:

* **Standard mode (default).** Uses the public Lumix SDK DLL that ships with the plugin. Exposures are limited to the camera's discrete shutter-speed list, with a **maximum of 60 s** (the public SDK cannot do Bulb).
* **Extended mode (optional).** Loads the DLL from a local install of Panasonic's free [LUMIX Tether application](https://av.jpn.support.panasonic.com/support/global/cs/soft/download/d_lumixtether.html) — the plugin never bundles it. This unlocks capabilities the public SDK lacks:
  * **Bulb / exposures beyond 60 s** (tested to over a minute; the shutter is held open for the requested time).
  * **Sub-second and full-range shutter speeds**, snapped to the nearest supported value so N.I.N.A.'s Flat Wizard converges.
  * **Image quality** selection (RAW / JPEG), including a *Prefer JPEG* option.
  * **Battery** information and live **camera-mode** reading (including the C1–C3 custom presets).
  * **Save destination**: SD card, directly to the PC (cardless), or both.
  * A **real capture-complete event** instead of waiting on the SD-card file.

To use extended mode: install LUMIX Tether, close it (it can't share the USB with N.I.N.A.), then tick **Use LUMIX Tether extended features** in the plugin options and reconnect. The DLL is auto-detected at the default install path; a custom path can be set in the options.

# Camera recognition

The driver has a list of supported cameras published by [Panasonic](https://av.jpn.support.panasonic.com/support/global/cs/soft/tool/sdk.html). The sensor data is derived from the [Digital Camera Database](https://www.digicamdb.com/).

If the camera is not recognized by the driver 2 options are possible:
* Override the sensor dimensions from the options page.
* Leave them at 0 — the default values of 6000x4000 with 5.9 micron pitch will be used.

# Exposure mode (dial)

The driver cannot **set** the exposure-mode dial (A/S/P/M) — that must be set on the camera body. It **reads** the current mode and warns if the camera is not in a manual-capable mode. Manual (M) and the **C1–C3 custom presets** (assumed to be M-based, e.g. an astro preset) are accepted; other modes raise a warning but do not block.

# Known Limitations
* Lumix RAW data assume a 14-bit depth. Overriding the bit depth is possible from the options page.
* RAW (.RW2) decoding depends on your N.I.N.A. version. **N.I.N.A. 3.3 and later** convert RAW with **LibRaw**, which decodes RW2 directly — no workaround needed. On **3.2 and earlier** the legacy DCRaw converter does not support RW2, so set the RAW decoder to **FreeImage** in the camera advanced settings (Equipment tab). A very recent camera whose RW2 variant your N.I.N.A.'s decoder does not yet recognise (e.g. the GH7) may still show a noisy preview even under LibRaw; enabling **Prefer JPEG** in the plugin options is a fallback for that case (extended mode only).
* In **standard mode** Bulb is unavailable, so a requested exposure is snapped to the nearest supported shutter speed (max 60 s). Bulb / >60 s requires **extended mode**.

# Getting help

Help for this plugin may be found in the **#plugin-discussions** channel on the NINA project [Discord chat server](https://discord.com/invite/rWRbVbw) or by filing an issue report at this plugin's [Github repository](https://github.com/daleghent/nina-plugins/issues).

* The Plugin is provided 'as is' under the terms of the [Mozilla Public License 2.0](https://github.com/totoantibes/NinaLumixPlugin?tab=MPL-2.0-1-ov-file)
* Source code for this plugin is available in my NINA plugins [source code repository](https://github.com/totoantibes/NinaLumixPlugin)
* THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE