# Ground Station

Ground Station provides remote messaging instructions and triggers for use in NINA's Advanced Sequencer. This allows you to send alerts to messaging platforms and orchestrate IoT devices and services directly from Advanced Sequencer.

The provided instructions and triggers can be broken down in to two general categories:

* Instructions for sending free form messages to a variety of services
* Triggers for sending messages to services when something in the sequence experiences a failure

## Supported services

* Pushover — Simple and reliable desktop and iOS/Android push messaging. Pushover charges a one-time $5 per-device fee to support its service
* Telegram — Utilize the [Telegram bot API](https://core.telegram.org/bots/api) to send messages a Telegram channel
* Email — Who doesn't love plain old email? Plain SMTP with user auth and SSL/TLS support
* HTTP - Send a generic HTTP GET or POST request to a URL
* IFTTT Webhooks — If This Then That. An easy to use configurable webhooks-based gateway to control a wide variety of messaging platforms and IoT devices. Taking full advantage requires an IFTTT Pro account
* MQTT — Publish free-form payloads to a topic on a MQTT broker. Failures are published to a specified topic with information packaged in a JSON object

Information about your session or any failures may be inserted into the messages by the use of tokens. These tokens are described on the **Message Token Help** tab.

## Getting help

Help for this plugin may be found in the **#plugin-discussions** channel on the NINA project [Discord chat server](https://discord.com/invite/rWRbVbw) or by filing an issue report at this plugin's [Github repository](https://github.com/daleghent/nina-plugins/issues).

* Some services charge a subscription or one-time fee to utilize them
* Most services require an Internet connection in order to function
* I do not provide in-depth technical support or training for the services themselves
* Ground Station is provided 'as is' under the terms of the [Mozilla Public License 2.0](https://github.com/daleghent/nina-plugins/blob/main/LICENSE.txt)