using System.Drawing;

namespace HSDRaw.AirRide.Ext
{
    internal class KAR_HudColor : HSDAccessor
    {
        public override int TrimmedSize => 0x20;

        public Color Main { get => _s.GetColorRGBA(0x00); set => _s.SetColorRGBA(0x00, value); }

        public Color Light { get => _s.GetColorRGBA(0x04); set => _s.SetColorRGBA(0x04, value); }

        public Color Dark { get => _s.GetColorRGBA(0x08); set => _s.SetColorRGBA(0x08, value); }

        public Color Select { get => _s.GetColorRGBA(0x0C); set => _s.SetColorRGBA(0x0C, value); }

        public Color InGameHud { get => _s.GetColorRGBA(0x10); set => _s.SetColorRGBA(0x10, value); }
    }
}
