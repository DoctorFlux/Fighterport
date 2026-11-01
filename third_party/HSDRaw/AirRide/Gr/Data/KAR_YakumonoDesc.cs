using HSDRaw.Common.Animation;

namespace HSDRaw.AirRide.Gr.Data
{
    public class KAR_YakumonoDesc : HSDAccessor
    {
        public override int TrimmedSize => 0x18;

        public HSDAccessor Params { get => _s.GetReference<HSDAccessor>(0x00); set => _s.SetReference(0x00, value); }

        public HSDFixedLengthPointerArrayAccessor<KAR_grMainModel> Model { get => _s.GetReference<HSDFixedLengthPointerArrayAccessor<KAR_grMainModel>>(0x04); set => _s.SetReference(0x04, value); }

        public HSDArrayAccessor<KAR_YakumonoState> Animation { get => _s.GetReference<HSDArrayAccessor<KAR_YakumonoState>>(0x08); set => _s.SetReference(0x08, value); }

        public KAR_grCollisionNode Collision { get => _s.GetReference<KAR_grCollisionNode>(0x0C); set => _s.SetReference(0x0C, value); }

        public KAR_HurtCollision HurtData { get => _s.GetReference<KAR_HurtCollision>(0x10); set => _s.SetReference(0x10, value); }

        public KAR_YakumonoAudio Audio { get => _s.GetReference<KAR_YakumonoAudio>(0x14); set => _s.SetReference(0x14, value); }
    }

    public class KAR_YakumonoState : HSDAccessor
    {
        public override int TrimmedSize => 0x14;

        public HSD_AnimJoint JointAnimation { get => _s.GetReference<HSD_AnimJoint>(0x00); set => _s.SetReference(0x00, value); }

        public HSD_MatAnimJoint MaterialAnimation { get => _s.GetReference<HSD_MatAnimJoint>(0x04); set => _s.SetReference(0x04, value); }

        public HSD_ShapeAnimJoint ShapeAnimation { get => _s.GetReference<HSD_ShapeAnimJoint>(0x08); set => _s.SetReference(0x08, value); }

        // 0x0C - script

        public int Flags { get => _s.GetInt32(0x10); set => _s.SetInt32(0x10, value); }
    }

    public class KAR_YakumonoAudio : HSDAccessor
    {
        public enum TrackKind
        {
            Single,
            Double,
            Terminate,
        }
        public enum ProxKind
        {
            Type1, // NoPosition,
            Type2, // SetPosition,
            Type3, // Unknown
        }

        public override int TrimmedSize => 0x18;

        public HSDArrayAccessor<KAR_SfxId> Sounds { get => _s.GetReference<HSDArrayAccessor<KAR_SfxId>>(0x00); set => _s.SetReference(0x00, value); }

        public uint SoundNum { get => _s.GetUInt32(0x04); set => _s.SetUInt32(0x04, value); }

        public TrackKind Kind { get => (TrackKind)_s.GetInt32(0x08); set => _s.SetInt32(0x08, (int)value); }

        public ProxKind ProximetyKind { get => (ProxKind)_s.GetInt32(0x0C); set => _s.SetInt32(0x0C, (int)value); }

        public float Volume { get => _s.GetFloat(0x10); set => _s.SetFloat(0x10, value); }

        public uint x14 { get => _s.GetUInt32(0x14); set => _s.SetUInt32(0x14, value); }
    }

    public class KAR_SfxId : HSDAccessor
    {
        public override int TrimmedSize => 0x8;

        public ushort BankID { get => _s.GetUInt16(0x00); set => _s.SetUInt16(0x00, value); }

        public ushort SoundID { get => _s.GetUInt16(0x02); set => _s.SetUInt16(0x02, value); }

        public uint x04 { get => _s.GetUInt32(0x04); set => _s.SetUInt32(0x04, value); }
    }
}
