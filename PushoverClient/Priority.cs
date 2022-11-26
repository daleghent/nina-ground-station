using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.PushoverClient {
    /// <summary>
    /// https://pushover.net/api#priority
    /// </summary>
    public enum Priority {
        Lowest = -2,
        Low = -1,
        Normal = 0,
        High = 1,
        Emergency = 2
    }
}
