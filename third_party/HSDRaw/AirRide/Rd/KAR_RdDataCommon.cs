using System;
using System.Collections.Generic;
using System.Text;

namespace HSDRaw.AirRide.Rd
{
    public class KAR_RdDataCommon : HSDAccessor
    {
        public override int TrimmedSize => 0x1C;

        // 0x00 - parameters
        // 0x04 - color animation scripts
        // 0x08 - unknown param
        // 0x0C - firework cannon
        // 0x10 - shake animation
    }
}
