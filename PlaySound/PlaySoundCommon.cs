#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using NetCoreAudio;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.PlaySound {

    public class PlaySoundCommon {
        public static string FileTypeFilter { get; } = "Audio files|*.wav;*.aiff;*.aif;*.mp3|All files|*.*";

        public string SoundFile { get; set; } = string.Empty;

        public bool WaitUntilFinished { get; set; } = true;

        public async Task<bool> PlaySound(CancellationToken ct) {
            if (string.IsNullOrEmpty(SoundFile)) {
                throw new ArgumentException("Audio file not specified");
            }

            if (!File.Exists(SoundFile)) {
                throw new FileNotFoundException($"{SoundFile} not found");
            }

            var player = new Player();

            if (WaitUntilFinished) {
                await player.Play(SoundFile);

                do {
                    await Task.Delay(250, ct);
                } while (player.Playing);
            } else {
                await player.Play(SoundFile);
            }

            return true;
        }
    }
}