#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Config;
using DaleGhent.NINA.GroundStation.DiscordWebhook;
using DaleGhent.NINA.GroundStation.Images;
using DaleGhent.NINA.GroundStation.Mqtt;
using NINA.Core.Utility;
using NINA.Image.Interfaces;
using NINA.Plugin.Interfaces;
using NINA.Plugin;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using System.Threading;

namespace DaleGhent.NINA.GroundStation {

    [Export(typeof(IPluginManifest))]
    public class GroundStation : PluginBase {
        private readonly ImageEventHandler imageEventHandler;
        private static LastWillAndTestament lwtSession;
        private readonly IProfileService profileService;
        private readonly IImageSaveMediator imageSaveMediator;
        private readonly IImageDataFactory imageDataFactory;


        [ImportingConstructor]
        public GroundStation(IProfileService profileService, IImageSaveMediator imageSaveMediator, IImageDataFactory imageDataFactory) {
            if (Properties.Settings.Default.UpgradeSettings) {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeSettings = false;
                CoreUtil.SaveSettings(Properties.Settings.Default);
            }

            this.profileService = profileService;
            this.imageSaveMediator = imageSaveMediator;
            this.imageDataFactory = imageDataFactory;

            GroundStationConfig ??= new GroundStationConfig(profileService);
            imageEventHandler = new ImageEventHandler(this.profileService, this.imageSaveMediator, this.imageDataFactory);

            lwtSession = new LastWillAndTestament(GroundStationConfig);
        }

        public static GroundStationConfig GroundStationConfig { get; private set; }

        public override async Task Initialize() {
            ImageService.Instance.Image = new ImageData();

            await lwtSession.StartLwtSession(CancellationToken.None);

            imageEventHandler.Start();
            DiscordWebhookEvents.Start();
            MqttEvents.Start();

            Logger.Debug("Init completed");
            return;
        }

        public override async Task Teardown() {
            DiscordWebhookEvents.Stop();
            MqttEvents.Stop();
            imageEventHandler.Stop();
            GroundStationConfig.Dispose();

            await lwtSession.StopLwtSession(CancellationToken.None);
            return;
        }

        public static string GetVersion() {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
        }

        public static async void StartLwtSession() {
            await lwtSession.StartLwtSession(CancellationToken.None);
        }

        public static async void StopLwtSession() {
            await lwtSession.StopLwtSession(CancellationToken.None);
        }

        public static bool LwtIsConnected => lwtSession.IsConnected;
    }
}