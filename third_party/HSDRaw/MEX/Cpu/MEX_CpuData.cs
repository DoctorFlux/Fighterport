using HSDRaw.Melee;
using System.ComponentModel;

namespace HSDRaw.MEX.Cpu
{
    public class MEX_CpuData : HSDAccessor
    {
        public override int TrimmedSize => 0x24;

        [Description("This pointer is set at runtime by the fighter's code.")]
        public uint ThinkFuncPointer { get => (uint)_s.GetUInt32(0x00); }

        [Description("a general distance from self to target fighter used in decision making")]
        public float AttackRadius { get => _s.GetFloat(0x04); set => _s.SetFloat(0x04, value); }

        [Description("Used when fighter is grounded with no special conditions.")]
        public HSDArrayAccessor<SBM_CPUChance> General { get => _s.GetReference<HSDArrayAccessor<SBM_CPUChance>>(0x08); set => _s.SetReference(0x08, value); }

        [Description("Used when fighter is airborne with no special conditions.")]
        public HSDArrayAccessor<SBM_CPUChance> Airborne { get => _s.GetReference<HSDArrayAccessor<SBM_CPUChance>>(0x0C); set => _s.SetReference(0x0C, value); }

        [Description("Used when there is distance between target fighter and self.")]
        public HSDArrayAccessor<SBM_CPUChance> Projectile { get => _s.GetReference<HSDArrayAccessor<SBM_CPUChance>>(0x10); set => _s.SetReference(0x10, value); }

        [Description("Used when target fighter is shielding.")]
        public HSDArrayAccessor<SBM_CPUChance> Grab { get => _s.GetReference<HSDArrayAccessor<SBM_CPUChance>>(0x14); set => _s.SetReference(0x14, value); }

        [Description("Used when fighter has a item.")]
        public HSDArrayAccessor<SBM_CPUChance> Item { get => _s.GetReference<HSDArrayAccessor<SBM_CPUChance>>(0x18); set => _s.SetReference(0x18, value); }

        [Description("Used when fighter has a battering item.")]
        public HSDArrayAccessor<SBM_CPUChance> BatteringItem { get => _s.GetReference<HSDArrayAccessor<SBM_CPUChance>>(0x1C); set => _s.SetReference(0x1C, value); }

        [Description("Used when target fighter is off stage.")]
        public HSDArrayAccessor<SBM_CPUChance> OffStage { get => _s.GetReference<HSDArrayAccessor<SBM_CPUChance>>(0x20); set => _s.SetReference(0x20, value); }
    }
}
