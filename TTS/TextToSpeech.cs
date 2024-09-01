#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace DaleGhent.NINA.GroundStation.TTS {

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "NINA is currently Windows-only")]
    public class TextToSpeech : IDisposable {
        private readonly SpeechSynthesizer synthesizer;
        private readonly TaskCompletionSource<bool> tcs;

        public TextToSpeech() {
            synthesizer = new SpeechSynthesizer();
            SpeechApiReflectionHelper.InjectOneCoreVoices(synthesizer);

            tcs = new TaskCompletionSource<bool>();
            synthesizer.SpeakCompleted += SpeakCompletionEvent;
        }

        private string GetVoice() {
            List<VoiceInfo> voices = [];

            foreach (var voice in synthesizer.GetInstalledVoices()) {
                if (voice.Enabled) {
                    voices.Add(voice.VoiceInfo);
                }
            }

            if (voices.Count == 0) {
                return null;
            }

            // Find voice matching the selected UI culture - fallback to first found voice if no match
            var localeVoice = voices.FirstOrDefault(x => x.Culture.Name == Dispatcher.CurrentDispatcher.Thread.CurrentUICulture.Name);

            return localeVoice == null ? voices.First().Name : localeVoice.Name;
        }

        public bool HasVoice() {
            return GetVoice() != null;
        }

        public async Task Speak(string text, CancellationToken token) {
            var voice = GroundStation.GroundStationConfig.TtsVoice;

            if (voice != null) {
                synthesizer.SetOutputToDefaultAudioDevice();
                synthesizer.SelectVoice(voice);
                synthesizer.SpeakAsync(text);

                await tcs.Task;
            }
        }

        public static List<string> GetVoiceNames() {
            using var synthesizer = new SpeechSynthesizer();
            SpeechApiReflectionHelper.InjectOneCoreVoices(synthesizer);
            List<string> voices = [];

            foreach (var voice in synthesizer.GetInstalledVoices()) {
                if (voice.Enabled) {
                    voices.Add(voice.VoiceInfo.Name);
                }
            }

            return voices;
        }

        public void Dispose() {
            synthesizer.SpeakCompleted -= SpeakCompletionEvent;
            synthesizer.Dispose();

            GC.SuppressFinalize(this);
        }

        private void SpeakCompletionEvent(object sender, SpeakCompletedEventArgs e) {
            tcs?.SetResult(true);
        }
    }
}