using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// [MANDATORY] The following GUID is used as a unique identifier of the plugin. Generate a fresh one for your plugin!
[assembly: Guid("8ced010d-15dd-4d0e-b5c5-acb59e2339bc")]

// [MANDATORY] The assembly versioning
//Should be incremented for each new release build of a plugin
[assembly: AssemblyVersion("1.0.0.11")]
[assembly: AssemblyFileVersion("1.0.0.11")]

// [MANDATORY] The name of your plugin
[assembly: AssemblyTitle("LumixCamera")]
// [MANDATORY] A short description of your plugin
[assembly: AssemblyDescription("Control Lumix Cameras natively")]

// The following attributes are not required for the plugin per se, but are required by the official manifest meta data

// Your name
[assembly: AssemblyCompany("roberthasson")]
// The product name that this plugin is part of
[assembly: AssemblyProduct("LumixCamera")]
[assembly: AssemblyCopyright("Copyright © 2024 roberthasson")]

// The minimum Version of N.I.N.A. that this plugin is compatible with
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.0.0.2017")]

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
[assembly: AssemblyMetadata("Tags", "")]

//[Optional] A link that will show a log of all changes in between your plugin's versions
[assembly: AssemblyMetadata("ChangelogURL", "https://github.com/totoantibes/NinaLumixPlugin")]

//[Optional] The url to a featured logo that will be displayed in the plugin list next to the name
[assembly: AssemblyMetadata("FeaturedImageURL", "")]
//[Optional] A url to an example screenshot of your plugin in action
[assembly: AssemblyMetadata("ScreenshotURL", "")]
//[Optional] An additional url to an example example screenshot of your plugin in action
[assembly: AssemblyMetadata("AltScreenshotURL", "")]
//[Optional] An in-depth description of your plugin
[assembly: AssemblyMetadata("LongDescription", @"Lumix native plugin provides a USB direct method to interface with [compatible Lumix cameras](https://av.jpn.support.panasonic.com/support/global/cs/soft/tool/sdk.html).

This differs from the ASCOM Driver which interfaces over wifi and http. As the ASCOM driver allows a wider set of cameras it also does not provide a liveview and is rather slow in downloading the RAW images to Nina.
Ths published Lumix SDK is somewhat limited. It is therefore not possible to use the Bulb mode. The max exposure limit is set to 60 secs.
Also the very first image download will fail.(hoing to fix this in future versions)

The driver has a list of suported cameras published by [Panasonic](https://av.jpn.support.panasonic.com/support/global/cs/soft/tool/sdk.html)
The sensor data is derived from the [Digital Camera Database](https://www.digicamdb.com/)

If the camera is not recognized by the driver 2 options are possible:
* Override the sensor dimensions from this page.
* leave them to 0 value in that case the default values of 6000x4000 with 5.9 microns pitch will be used.

The driver cannot set the Shutter Mode (e.g. A, S, P, or more importantly M). This has to be done on the camera body itself. It will however detect the current Mode and inform the user if incorrect mode is detected.

# Known Limitations
* As mentioned above, the published SDK only exposes a subset of the functions. in fact the [Lumix tether applications](https://av.jpn.support.panasonic.com/support/global/cs/soft/download/d_lumixtether.html) provides much finer capabilities like battery information or Bulb operations. After my request Panasonic acknowledged the different capabilities with no plan to publish the more advanced features via an SDK.
* Lumix RAW data assume a 14 bit depth. Overriding the bitdepth is also possible from the settings page.
* The RW2 format is not supported by the DCRaw implementation included in N.I.N.A. Hence you need to set the raw decoded to FreeImage in the camera advanced settings in the equipment settings tab. Hopefully libraw will be supported by N.I.N.A. which is more current than DCRaw.
* Since Bulb is not supported the driver will set the exposure to a supported shutter speed. In case the speed is not supported by the camera it will fail setting it up and will default to the previous known value.
* N.I.N.A. will know about the exposure termination when the camera writes the image to the SD card.

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