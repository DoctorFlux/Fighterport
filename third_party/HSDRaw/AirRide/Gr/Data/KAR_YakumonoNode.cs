namespace HSDRaw.AirRide.Gr.Data
{
    public class KAR_YakumonoNode : HSDAccessor
    {
        public override int TrimmedSize => 0x18;

        public HSDNullPointerArrayAccessor<KAR_YakumonoDesc> YakumonoEntries { get => _s.GetReference<HSDNullPointerArrayAccessor<KAR_YakumonoDesc>>(0x00); set => _s.SetReference(0x00, value); }
    
        public int YakumonoCount { get => _s.GetInt32(0x04); set => _s.SetInt32(0x04, value); }

        public HSDAccessor x08 { get => _s.GetReference<HSDAccessor>(0x08); set => _s.SetReference(0x08, value); }

        public int x08_count { get => _s.GetInt32(0x0C); set => _s.SetInt32(0x0C, value); }

        public HSDAccessor x10 { get => _s.GetReference<HSDAccessor>(0x10); set => _s.SetReference(0x10, value); }

        public int x10_count { get => _s.GetInt32(0x14); set => _s.SetInt32(0x14, value); }

    }
}
