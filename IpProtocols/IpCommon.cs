using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.IpProtocols
{
    public class IpCommon {

        public enum PayloadType {
            ASCII,
            Binary,
        }

        public enum LineTermination {
            None,
            CR,
            LF,
            CRLF,
        }
    }
}
