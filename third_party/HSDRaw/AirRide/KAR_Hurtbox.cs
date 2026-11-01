namespace HSDRaw.AirRide
{
    /// <summary>
    /// 
    /// </summary>
    public class KAR_HurtCollision : HSDAccessor
    {
        public override int TrimmedSize => 0x0C;

        public HSDArrayAccessor<KAR_Hurtbox> Entries
        {
            get => _s.GetReference<HSDArrayAccessor<KAR_Hurtbox>>(0x00);
            set
            {
                if (value != null)
                    Count = value.Length;
                else
                    Count = 0;

                _s.SetReference(0x00, value); 
            }
        }

        public int Count { get => _s.GetInt32(0x04); set => _s.SetInt32(0x04, value); }
    }

    /// <summary>
    /// 
    /// </summary>
    public class KAR_Hurtbox : HSDAccessor
    {
        public override int TrimmedSize => 0x18;

        public int BoneIndex { get => _s.GetInt32(0x00); set => _s.SetInt32(0x00, value); }

        public float X { get => _s.GetFloat(0x04); set => _s.SetFloat(0x04, value); }

        public float Y { get => _s.GetFloat(0x08); set => _s.SetFloat(0x08, value); }

        public float Z { get => _s.GetFloat(0x0C); set => _s.SetFloat(0x0C, value); }

        public float Size { get => _s.GetFloat(0x10); set => _s.SetFloat(0x10, value); }

        public float x14 { get => _s.GetFloat(0x14); set => _s.SetFloat(0x14, value); }
    }
}
