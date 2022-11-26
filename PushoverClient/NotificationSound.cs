#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org> and contributors

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

namespace DaleGhent.NINA.GroundStation.PushoverClient {

    /// <summary>
    /// https://pushover.net/api#sounds
    /// </summary>
    public enum NotificationSound {
        NotSet, // NotSet will not send the sound and the user default sound will play
        Pushover,
        Bike,
        Bugle,
        CashRegister,
        Classical,
        Cosmic,
        Falling,
        Gamelan,
        Incoming,
        Intermission,
        Magic,
        Mechanical,
        Pianobar,
        Siren,
        SpaceAlarm,
        Tugboat,
        Alien,
        Climb,
        Persistent,
        Echo,
        Updown,
        None
    }
}