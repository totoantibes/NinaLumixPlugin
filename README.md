# LumixCamera

LongDescription", @"Lumix native plugin provides a USB direct method to interface with [compatible Lumix cameras](https://av.jpn.support.panasonic.com/support/global/cs/soft/tool/sdk.html).

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
* Lumix RAW data assume a 14 bit depth. Overriding the bitdepth is also possible from the settings page.The RW2 format is not supported by the DCRaw implementation included in N.I.N.A. Hence you need to set the raw decoded to FreeImage in the camera advanced settings in the equipment settings tab. Hopefully libraw will be supported by N.I.N.A. which is more current than DCRaw.
* Since Bulb is not supported the driver will set the exposure to a supported shutter speed. In case the speed is not supported by the camera it will fail setting it up and will default to the previous known value.
* N.I.N.A. will know about the exposure termination when the camera writes the image to the SD card.

# Getting help

Help for this plugin may be found in the **#plugin-discussions** channel on the NINA project [Discord chat server](https://discord.com/invite/rWRbVbw) or by filing an issue report at this plugin's [Github repository](https://github.com/daleghent/nina-plugins/issues).

* The Plugin is provided 'as is' under the terms of the [Mozilla Public License 2.0](https://github.com/totoantibes/NinaLumixPlugin?tab=MPL-2.0-1-ov-file)
* Source code for this plugin is available in my NINA plugins [source code repository](https://github.com/totoantibes/NinaLumixPlugin)