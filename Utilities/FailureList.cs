using NINA.Sequencer.SequenceItem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.Utilities {

    internal class FailureList {

        public FailureList() {
        }

        public class FailedItem {
            public SequenceItem Item { get; set; }
            public List<FailureReasons> Reasons { get; set; }
        }

        public class FailureReasons {
            public string Reason { get; set; }
        }
    }
}