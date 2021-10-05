using System.Reflection;
using System.Runtime.InteropServices;

// [MANDATORY] The following GUID is used as a unique identifier of the plugin
[assembly: Guid("2737AFDF-A1AA-48C3-BE17-0F5F03282AEB")]

// [MANDATORY] The assembly versioning
//Should be incremented for each new release build of a plugin
[assembly: AssemblyVersion("1.6.0.0")]
[assembly: AssemblyFileVersion("1.6.0.0")]

// [MANDATORY] The name of your plugin
[assembly: AssemblyTitle("Ground Station")]
// [MANDATORY] A short description of your plugin
[assembly: AssemblyDescription("Send failure events and free-form messages to a variety of messaging or automation services")]


// The following attributes are not required for the plugin per se, but are required by the official manifest meta data

// Your name
[assembly: AssemblyCompany("Dale Ghent")]
// The product name that this plugin is part of
[assembly: AssemblyProduct("Ground Station")]
[assembly: AssemblyCopyright("Copyright © 2021 Dale Ghent")]

// The minimum Version of N.I.N.A. that this plugin is compatible with
[assembly: AssemblyMetadata("MinimumApplicationVersion", "1.11.0.1141")]

// The license your plugin code is using
[assembly: AssemblyMetadata("License", "MPL-2.0")]
// The url to the license
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
// The repository where your pluggin is hosted
[assembly: AssemblyMetadata("Repository", "https://github.com/daleghent/nina-plugins")]


// The following attributes are optional for the official manifest meta data

//[Optional] Your plugin homepage - omit if not applicaple
[assembly: AssemblyMetadata("Homepage", "https://daleghent.com/ground-station")]

//[Optional] Common tags that quickly describe your plugin
[assembly: AssemblyMetadata("Tags", "notifications,alerts,ifttt,email,pushover,mqtt")]

//[Optional] A link that will show a log of all changes in between your plugin's versions
[assembly: AssemblyMetadata("ChangelogURL", "https://github.com/daleghent/nina-plugins/blob/main/GroundStation/CHANGELOG.md")]

//[Optional] The url to a featured logo that will be displayed in the plugin list next to the name
[assembly: AssemblyMetadata("FeaturedImageURL", "https://daleghent.github.io/nina-plugins/assets/images/ground-station-logo1.png")]
//[Optional] A url to an example screenshot of your plugin in action
[assembly: AssemblyMetadata("ScreenshotURL", "https://daleghent.github.io/nina-plugins/assets/images/gs-example1.jpg")]
//[Optional] An additional url to an example example screenshot of your plugin in action
[assembly: AssemblyMetadata("AltScreenshotURL", "https://daleghent.github.io/nina-plugins/assets/images/gs-example2.png")]
//[Optional] An in-depth description of your plugin
[assembly: AssemblyMetadata("LongDescription", @"Ground Station provides remote messaging instructions and triggers for use in NINA's Advanced Sequencer. This allows you to send alerts to messaging platforms and orchestrate IoT devices and services directly from Advanced Sequencer.

The provided instructions and triggers can be broken down in to two general categories:

* Instructions for sending free form messages to a variety of services
* Triggers for sending messages to services when something in the sequence experiences a failure

# Supported services #

* Pushover — Simple and reliable desktop and iOS/Android push messaging. Pushover charges a one-time $5 per-device fee to support its service
* Telegram — Utilize the [Telegram bot API](https://core.telegram.org/bots/api) to send messages a Telegram channel
* Email — Who doesn't love plain old email? Plain SMTP with user auth and SSL/TLS support
* HTTP - Send a generic HTTP GET or POST request to a URL
* IFTTT Webhooks — If This Then That. An easy to use configurable webhooks-based gateway to control a wide variety of messaging platforms and IoT devices. Taking full advantage requires an IFTTT Pro account
* MQTT — Publish free-form payloads to a topic on a MQTT broker. Failures are published to a specified topic with information packaged in a JSON object

Information about your session or any failures may be inserted into the messages by the use of tokens. These tokens are described on the **Message Token Help** tab.

# Getting help #

Help for this plugin may be found in the **#plugin-discussions** channel on the NINA project [Discord chat server](https://discord.com/invite/rWRbVbw) or by filing an issue report at this plugin's [Github repository](https://github.com/daleghent/nina-plugins/issues).

* Some services charge a subscription or one-time fee to utilize them
* Most services require an Internet connection in order to function
* I do not provide in-depth technical support or training for the services themselves
* Ground Station is provided 'as is' under the terms of the [Mozilla Public License 2.0](https://github.com/daleghent/nina-plugins/blob/main/LICENSE.txt)
* Source code for this plugin is available in my NINA plugins [source code repository](https://github.com/daleghent/nina-plugins)")]

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