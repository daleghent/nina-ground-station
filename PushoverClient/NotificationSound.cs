using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
