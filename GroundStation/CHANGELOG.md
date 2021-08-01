# Ground Station

## 1.1.0.0 - 2021-8-1
* MQTT: Added instruction and failure trigger for [MQTT](https://mqttt.org/) brokers
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