using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// [MANDATORY] The following GUID is used as a unique identifier of the plugin. Generate a fresh one for your plugin!
[assembly: Guid("8ced010d-15dd-4d0e-b5c5-acb59e2339bc")]

// [MANDATORY] The assembly versioning
//Should be incremented for each new release build of a plugin
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

// [MANDATORY] The name of your plugin
[assembly: AssemblyTitle("LumixCamera")]
// [MANDATORY] A short description of your plugin
[assembly: AssemblyDescription("Control Lumix Cameras natively - nightly")]

// The following attributes are not required for the plugin per se, but are required by the official manifest meta data

// Your name
[assembly: AssemblyCompany("roberthasson")]
// The product name that this plugin is part of
[assembly: AssemblyProduct("LumixCamera")]
[assembly: AssemblyCopyright("Copyright © 2024 roberthasson")]

// The minimum Version of N.I.N.A. that this plugin is compatible with
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.2.0.1067")]

// The license your plugin code is using
[assembly: AssemblyMetadata("License", "MPL-2.0")]
// The url to the license
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
// The repository where your pluggin is hosted
[assembly: AssemblyMetadata("Repository", "https://github.com/totoantibes/NinaLumixPlugin")]

// The following attributes are optional for the official manifest meta data

//[Optional] Your plugin homepage URL - omit if not applicaple
[assembly: AssemblyMetadata("Homepage", "https://github.com/totoantibes/NinaLumixPlugin")]

//[Optional] Common tags that quickly describe your plugin
[assembly: AssemblyMetadata("Tags", "camera lumix native")]

//[Optional] A link that will show a log of all changes in between your plugin's versions
[assembly: AssemblyMetadata("ChangelogURL", "https://github.com/totoantibes/NinaLumixPlugin")]

//[Optional] The url to a featured logo that will be displayed in the plugin list next to the name
[assembly: AssemblyMetadata("FeaturedImageURL", "https://photorumors.com/wp-content/uploads/2016/07/Panasonic-Lumix-G-logo.jpg")]
//[Optional] A url to an example screenshot of your plugin in action
[assembly: AssemblyMetadata("ScreenshotURL", "")]
//[Optional] An additional url to an example example screenshot of your plugin in action
[assembly: AssemblyMetadata("AltScreenshotURL", "")]
//[Optional] An in-depth description of your plugin
[assembly: AssemblyMetadata("LongDescription", @"Lumix native plugin provides a USB direct method to interface with [compatible Lumix cameras](https://av.jpn.support.panasonic.com/support/global/cs/soft/tool/sdk.html).

This differs from the ASCOM Driver which interfaces over wifi and http. As the ASCOM driver allows a wider set of cameras it also does not provide a liveview and is rather slow in downloading the RAW images to Nina.

The plugin runs in one of two modes, selected in the plugin options:

* Standard mode (default): uses the public Lumix SDK DLL that ships with the plugin. Exposures are limited to the camera's discrete shutter-speed list, max 60 s (the public SDK cannot do Bulb).
* Extended mode (optional): loads the DLL from a local install of Panasonic's free [LUMIX Tether application](https://av.jpn.support.panasonic.com/support/global/cs/soft/download/d_lumixtether.html) (never bundled) to unlock capabilities the public SDK lacks: Bulb / exposures beyond 60 s, sub-second and full-range shutter speeds (snapped to the nearest supported value so the Flat Wizard converges), RAW/JPEG image-quality selection, battery info, live camera-mode reading (incl. the C1-C3 custom presets), save destination (SD / PC-cardless / both), and a real capture-complete event. Install LUMIX Tether, close it, then tick 'Use LUMIX Tether extended features' and reconnect; the DLL is auto-detected or a custom path can be set.

The driver has a list of suported cameras published by [Panasonic](https://av.jpn.support.panasonic.com/support/global/cs/soft/tool/sdk.html)
The sensor data is derived from the [Digital Camera Database](https://www.digicamdb.com/)

If the camera is not recognized by the driver 2 options are possible:
* Override the sensor dimensions from this page.
* leave them to 0 value in that case the default values of 6000x4000 with 5.9 microns pitch will be used.

The driver cannot set the exposure-mode dial (A/S/P/M) - that must be set on the camera body. It reads the current mode and warns if not in a manual-capable mode; Manual (M) and the C1-C3 custom presets (assumed M-based) are accepted, other modes warn but do not block.

# Known Limitations
* Lumix RAW data assume a 14 bit depth. Overriding the bitdepth is also possible from the settings page.
* RAW (.RW2) decoding depends on your N.I.N.A. version. N.I.N.A. 3.3 and later convert RAW with LibRaw, which decodes RW2 directly - no workaround needed. On 3.2 and earlier the legacy DCRaw converter does not support RW2, so set the RAW decoder to FreeImage in the camera advanced settings (Equipment tab). A very recent camera whose RW2 variant your N.I.N.A.'s decoder does not yet recognise (e.g. the GH7) may still show a noisy preview even under LibRaw; enabling Prefer JPEG in the plugin options is a fallback for that case (extended mode only).
* In standard mode Bulb is unavailable, so a requested exposure is snapped to the nearest supported shutter speed (max 60 s). Bulb / >60 s requires extended mode.

# Getting help

Help for this plugin may be found in the **#plugin-discussions** channel on the NINA project [Discord chat server](https://discord.com/invite/rWRbVbw) or by filing an issue report at this plugin's [Github repository](https://github.com/daleghent/nina-plugins/issues).

* The Plugin is provided 'as is' under the terms of the [Mozilla Public License 2.0](https://github.com/totoantibes/NinaLumixPlugin?tab=MPL-2.0-1-ov-file)
* Source code for this plugin is available in my NINA plugins [source code repository](https://github.com/totoantibes/NinaLumixPlugin)
* THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]
// [Unused]
[assembly: AssemblyConfiguration("")]
// [Unused]
[assembly: AssemblyTrademark("")]
// [Unused]
[assembly: AssemblyCulture("")]