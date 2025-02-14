﻿# Ground Station

## 2.4.1.0 - 2024-10-07
* Fixed an issue with *Send HTTP Request* where device-related tokens in POST bodies were not processed due to a missing metadata object

## 2.4.0.0 - 2024-03-01
* Fixed parsing of `FORMAT_DATETIME` tokens when used on the same line as another token
* Small optimizations for all token regular expressions
* Changed instruction/trigger logging to use actual name instead of class name
* Migrated plugin to use `CommunityToolkit.Mvvm.Input` for button controls
* Converted plugin to .NET 8
* Removed unused package references
* Updated `System.Speech` to 8.0.0
* Oldest supported NINA version is now 3.0 RC1

## 2.3.0.0 - 2023-09-25
* Expanded the *Send HTTP Request* instruction:
  * Moved its various fields to a new window that is opened when a button on the instruction is pressed
  * Included the ability specify a `Content-Type` header for HTTP POSTs.
  * Added a new Description field that allows a more meaninful description of the HTTP request to be displayed
* Oldest supported NINA version is now 3.0 NIGHTLY build 32 

## 2.2.2.0 - 2023-07-13
* Fixed possible null reference when resolving the selected filter name and position. Thanks to @linuxkidd for the fix

## 2.2.1.0 - 2023-04-01
* Fixed handing of IPv6 addresses in *Send UDP*

## 2.2.0.0 - 2023-03-31
* New instruction: *Send UDP* - Send a single UDP packet to a specified address and port containing an ASCII or binary payload
    - Packet contents are truncated to 65507 bytes
    - ASCII text may contain valid message tokens
    - Line terminations in ASCII text may be one of the following:
        - Carriage-return (CR) `\r`
        - Newline/Linefeed (LF) `\n`
        - Carriage-return+Linefeed (CRLF) `\r\n`
        - None, where line terminations are stripped from the text
    - Binary text is expected as space-separated bytes in hexadecimal representation, case-insensitive. Example: `a1 ff E3 0c`
<!-- -->
* New instruction and failure trigger: *Play Sound* - Plays a specified WAV, AIFF, or MP3 file
    - Sounds may be played such that the sequence moves on to the next instruction while the sound is playing, or the sequence waits until the sound finishes playing before proceeding to the next instruction
<!-- -->
* *Send HTTP Request* improvements:
    - Disabled sending of the `Expect: 100-continue` header in `POST` requests
    - The user-provided URL is now validated for format errors
    - The HTTP method and URL are now logged when the instruction runs
    - The raw HTTP response body is now logged
    - Removed use of `System.Web` for better .NET Core compatibility
<!-- -->
* Fixed *Send To TTS* not cloning the message during template creation
* Fixed an occasional hard crash caused by attempting to reuse a disposed background worker cancellation token source
* Small code refactors and cleanups
* Updated MailKit to 3.6.0

## 2.1.0.0 - 2022-11-26
* Pushover client library replaced with internal one (Credit: Stefan Berg)
* Replacing the internal Pushover client allows the implementation of Emergency priority messages for Pushover:
    - Emergency priority messages are the most critical class of Pushover message
    - Emergency priority messages will trigger an alert in the Pushover client, and this alert will repeat until acknowledged (not just viewed!) by the user
    - The repeat interval and maximum repeat time are adjustable settings. The minimum is 30 seconds and the maximum is 86400 seconds (24 hours). The default in Ground Station is 60 and 600 seconds respectively
* Fixed: Clicking on URLs in the Options page didn not work because .NET7 requires `UseShellExec=true`

## 2.0.0.1 - 2022-11-13
* Ported Test-to-speech to support .NET 7
* Compiler warning cleanups

## 2.0.0.0 - 2022-11-12
* Updated plugin to Microsoft .NET 7 for compatibility with NINA 3.0. The version of Ground Station that is compatible with NINA 2.x will remain under the 1.x versioning scheme, and Ground Station 2.x and later is relvant only to NINA 3.x and later.

## 1.12.0.0 - 2022-09-05
* Thanks to Stefan Berg for contributing the following:
    - *Ground Station* now uses the new *FailedItem* facility, introduced in NINA 2.0.1. This simplifies detection of runtime errors in sequences and makes alerting on them more reliable
    - Send to the Windows Text To Speech (TTS) facility. Listen to your errors in addition to reading them!
    - All Failures To... instructions will attempt to resend these critical messages 3 times before giving up
    - A test button is now on each transport's configuration tab so that you can easily test if your settings work
<!-- -->
* Added message tokens for equipment information. The list of new tokens is too large to list here. Please refer to the **Message Token Help** tab for the full list with descriptions
* Fixed spacing in `$$FORMAT_DATETIME$$` descriptions
* Various UI adjustments to the **Send to ...** instructions
* MQTTnet: Updated to 3.1.2
* MailKit: Updated to 3.4.0
* Telegram.Bot: Updated to 18.0.0
* Minimum supported NINA version is now 2.0.1 (2.0 HF1)

## 1.11.0.0 - 2022-03-13
* Updated to support changes to DSO containers in NINA 2.0 beta 50
* Minimum supported NINA version is now 2.0 Beta 50

## 1.10.6.0 - 2022-01-30
* Fixed tripping over a null reference if the failing entity's Name or Category are not available
* Minimum supported NINA version is now 2.0 Beta 37

## 1.10.5.0 - 2022-01-10
* Pushover: Reinstate the copying of the message title when cloning a **Send to Pushover** instruction
* MQTT: Implement retry attempt limit to prevent limitless retry attempts

## 1.10.0.0 - 2022-01-09
* Two new message tokens for failures:
    - `$$FAILED_ITEM_DESC$$` - A description of the failed instruction
    - `$$FAILED_ITEM_CATEGORY$$` - The category name that the failed instruction belongs to
<!-- -->
* Instructions inside of **Parallel Instruction Sets** are now detected and alerted on by the **Failures To...** triggers. Alerts are emitted upon completion of the Parallel Instruction Set
* Pushover: Fixed failure to observe new Pushover user key by already-instantiated Pushover instructions/triggers in a sequence
* The **Failures to...** triggers now give themselves more time to get the word out in the specific case of a failed instruction that has its On Error property set to skip-to-end
* Password fields under options no longer display the set password in visible text
* Removed unnecessary property copies when cloning instructions
* Removed unnecessary WPF data context bindings that just love to hold on to memory when they really should not be doing that
* Package now includes a PDB file for debugging purposes
* Updated MimeKit to 3.0.0
* Minimum supported NINA version is now 2.0 Beta 21
* Have a happy and healthy new year! 🎆🍾🧧🎆
 
## 1.9.5.0 - 2021-12-17
* MQTT: Fixed exception in **Failures to MQTT broker** when trigger is exectuted with no target object in the context
* MQTT: Fixed including the QoS parameter when the failures trigger or send to instruction is duplicated in a sequence
* MQTT: Added ISO 8601-formatted `date_local`, `date_utc` and UNIX epoch `date_unix` members to the **Failures to MQTT broker** trigger. An example JSON object:
```json
{
  "version": 2,
  "name": "Failed Validation Instruction",
  "description": "This instruction will always fail the validation",
  "date_local": "2021-12-17T10:40:38.3478158-05:00",
  "date_utc": "2021-12-17T15:40:38.3478158Z",
  "date_unix": 1639755638,
  "attempts": 1,
  "target_info": [
    {
      "target_name": "Great Star Cluster in Hercules",
      "target_ra": "16:41:41",
      "target_dec": "36° 27' 41\""
    }
  ],
  "error_list": [
    {
      "reason": "Validation for this instruction has failed"
    }
  ]
}
```

## 1.9.0.0 - 2021-12-10
* Added new message token: `$$UNIX_EPOCH$$` - yields the number of seconds elapsed since 00:00:00 January 1, 1970 UTC
<!-- -->
* MQTT: Added [Last Will & Testament](https://www.hivemq.com/blog/mqtt-essentials-part-9-last-will-and-testament/) (LWT) support. When configured and enabled, Ground Station will start a session with the MQTT broker when NINA launches and loads the plugin. The session contains configurable birth, last will &amp; testament, and close payloads that will be sent to the specified topic. The LWT payload can be used to trigger further automation to warn of or react to a *potentially* crashed system. The LWT session will always attempt to reconnect to the broker if the network connection with the broker is interrupted.
    - The **birth** payload is published immediately to the topic upon the initial connection, which is made when NINA starts and loads Ground Station, or when the LWT feature is enabled under the Ground Station options
    - The **last will &amp; testament** payload is sent with the intial connection. The broker will publish this payload to topic subscribers if the session drops without being gracefully closed (ie, TCP reset due to dead client or network connection)
    - The **close** payload is published when NINA closes or the LWT feature is disabled.
<!-- -->
* MQTT: Client ID field now defaults to empty and is renamed to "Client ID prefix". If left blank, the client ID will be auto-generated by the broker. If a client ID prefix is configured, it will be used as a prefix for a randomized alphanumeric string provided to the broker in the format of `[prefix text].[random string]`, where the randomized string is padded out to the 23 byte maximum size of a client ID.
<!-- -->
* Minimum supported NINA version is now 2.0 beta 13

## 1.8.0.0 - 2021-12-02
* MQTT: Added configurable QoS levels
* Reorganized plugin source code and put Ground Station into its own repository
* MQTTnet updated to 3.1.1
* Telegram.Bot updated to 17.0.0
* Minimum supported NINA version is now 2.0 beta 11

## 1.7.5.0 - 2021-11-01
* Fixed issue with URL encoding

## 1.7.0.0 - 2021-10-27
* Updated the settings save routine to use the new safe multi-instance method in 1.11 build 172

## 1.7.0.0 - 2021-10-25
* Fixed corner case that could result in failed error notifications
* Minimum supported NINA version is now 1.11 build 170

## 1.6.0.0 - 2021-10-5
* Added new "Send HTTP Request" instruction for making a generic HTTP GET/POST request to a URL. Message token substitution is supported in the URL and POST body
* Minimum supported NINA version is now 1.11 build 141

## 1.5.5.0 - 2021-9-1
* Fix some corner cases when Failures to... triggers are ran as a Global Trigger

## 1.5.0.0 - 2021-8-13
* Fix null reference when running trigger or action in the root container

## 1.4.2.0 - 2021-8-13
* Small fixes and adjustments to Help tab text formatting

## 1.4.1.0 - 2021-8-9
* Fix missing token substitution for IFTTT failure messages

## 1.4.0.0 - 2021-8-6
* This release reorganizes the Message Tokens Help tab and adds new tokens that may be used:
  * `$$SYSTEM_NAME$$` - The name of the computer
  * `$$USER_NAME$$` - The name of the user running N.I.N.A.
  * `$$NINA_VERSION$$` - The version of N.I.N.A.
  * `$$GS_VERSION$$` - The version of Ground Station
  * `$$FORMAT_DATETIME <custom date and time format>$$` - Custom local date and time string
  * `$$FORMAT_DATETIME_UTC <custom date and time format>$$` - Custom UTC date and time string

 The `$$FORMAT_DATETIME <custom date and time format>$$` token takes additional options in the form of [format specifiers](https://docs.microsoft.com/dotnet/standard/base-types/custom-date-and-time-format-strings). This allows you to insert a custom date and time string into your messages. Simply specify the format specifiers in the indicated area of the token. Your system's cultural settings are observed. For example, `$$FORMAT_DATETIME ddd d MMM$$` will display "Thu 14 Aug" for systems set to US English, and "ven. 14 août" for French locales. Local time is used. To create custom times in UTC, use `$$FORMAT_DATETIME_UTC ...$$` token instead.

## 1.3.0.0 - 2021-8-4
* Fix message failure when a DSO container is not present

## 1.2.0.0 - 2021-8-3
* Added message tokens and customizable failure message text for each service. Please refer to the Message Token Help tab for a list of supported tokens
* MQTT: added a `version` field to the failure JSON object. We start with version `1`
* Uses new `ShouldTriggerAfter()` method to evaluate failure conditions
* Minimum supported NINA version is now 1.11 build 120

## 1.1.0.0 - 2021-8-1
* MQTT: Added instruction and failure trigger for [MQTT](https://mqtt.org/) brokers
* Fixed failure triggers running twice for failed items when used as a Global Trigger
* Moved configuration options to a more compact tabbed layout
* Large refactoring of the Ground Station code to reduce duplication and make it more efficient
* Reordered change log to list the most recent versions first
* Updated MimeKit to 2.14.0
* Minimum supported NINA version is now 1.11 build 116

## 1.0.0.7 - 2021-7-28
* Improved sanitization of configurable inputs
* Added ability to configure a default Pushover sound and message priority for failures and normal messages

## 1.0.0.6 - 2021-7-18
* Added instruction and failure trigger for [Telegram](https://telegram.org/)
* Message text boxes now have vertical and horizontal scrollbars and soft wrapping

## 1.0.0.5 - 2021-7-14
* Added validation issue list to failure messages
  * Pushover, Email: list of any validation issues in the previous instruction is appended to the failure message
  * IFTTT: list of any validation issues in the previous instruction is populated into Value 3 of the Webhooks message
* Fix validation refesh issue in Send to Pushover
* Fix for invalid base64 strings in encrypted credential storage
* Fix instructions displaying as triggers in mini sequencer

## 1.0.0.4 - 2021-7-7 (beta)
* Fix installation issue

## 1.0.0.3 - 2021-7-7 (beta)
* Minimum NINA version: 1.11.0.1106
* Fix the saving of empty encrypted settings
* Ensure that the validator signals an update of any validation issues

## 1.0.0.2 - 2021-7-7 (pre-release)
* Store all passwords and API key settings using the `ProtectedData` class

## 1.0.0.1 - 2021-7-5 (pre-release)
* Initial release
* Added Instructions:
  - Send email
  - Send to Pushover
  - Send to IFTTT
* Added Triggers:
  - Failures to email
  - Failures to Pushover
  - Failures to IFTTT
