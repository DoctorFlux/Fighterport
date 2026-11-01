using System.ComponentModel;
using System.Drawing;

namespace HSDRaw.Melee.Pl
{
    public class SBM_PhysicsGroup : HSDAccessor
    {
        public override int TrimmedSize => 0x14;

        public int DynamicDescCount { get => _s.GetInt32(0x00); set => _s.SetInt32(0x00, value); }

        public HSDArrayAccessor<SBM_DynamicDesc> DynamicDesc
        {
            get => _s.GetReference<HSDArrayAccessor<SBM_DynamicDesc>>(0x04);
            set => _s.SetReference(0x04, value);
        }

        public int DynamicHitBubbleCount { get => _s.GetInt32(0x08); set => _s.SetInt32(0x08, value); }

        public HSDArrayAccessor<SBM_DynamicHitBubble> Hitbubbles
        {
            get => _s.GetReference<HSDArrayAccessor<SBM_DynamicHitBubble>>(0x0C);
            set => _s.SetReference(0x0C, value);
        }

        public HSDFixedLengthPointerArrayAccessor<HSDIntArray> BoneApplyTable
        {
            get => _s.GetReference<HSDFixedLengthPointerArrayAccessor<HSDIntArray>>(0x10);
            set => _s.SetReference(0x10, value);
        }
    }

    public class ItemDynamics : HSDAccessor
    {
        public int DynamicsNum { get => _s.GetInt32(0x00); set => _s.SetInt32(0x00, value); }
        public SBM_DynamicDesc[] DynamicsDesc
        {
            get
            {
                var re = _s.GetReference<HSDArrayAccessor<SBM_DynamicDesc>>(0x04);
                if (re == null)
                    return null;
                return re.Array;
            }
            set
            {
                if (value == null)
                {
                    _s.SetInt32(0x00, 0);
                    _s.SetReference(0x04, null);
                }
                else
                {
                    _s.SetInt32(0x00, value.Length);
                    var re = _s.GetCreateReference<HSDArrayAccessor<SBM_DynamicDesc>>(0x04);
                    re.Array = value;
                }
            }
        }

    }


    public class SBM_DynamicHitBubble : HSDAccessor
    {
        public override int TrimmedSize => 0x14;

        public int BoneIndex { get => _s.GetInt32(0x00); set => _s.SetInt32(0x00, value); }
        public float Z { get => _s.GetFloat(0x04); set => _s.SetFloat(0x04, value); }
        public float Y { get => _s.GetFloat(0x08); set => _s.SetFloat(0x08, value); }
        public float X { get => _s.GetFloat(0x0C); set => _s.SetFloat(0x0C, value); }
        public float Size { get => _s.GetFloat(0x10); set => _s.SetFloat(0x10, value); }

        public override string ToString()
        {
            return $"B: {BoneIndex} S: {Size} P: ({X}, {Y}, {Z})";
        }
    }

    public class SBM_DynamicDesc : HSDAccessor
    {
        public override int TrimmedSize => 0x18;

        public int BoneIndex { get => _s.GetInt32(0x00); set => _s.SetInt32(0x00, value); }

        public float DragMultiplier { get => _s.GetFloat(0x0C); set => _s.SetFloat(0x0C, value); }
        public float StiffnessMultiplier { get => _s.GetFloat(0x10); set => _s.SetFloat(0x10, value); }
        public float GravityLengthCompensation { get => _s.GetFloat(0x14); set => _s.SetFloat(0x14, value); }

        public SBM_DynamicParams[] Parameters
        {
            get
            {
                var re = _s.GetReference<HSDArrayAccessor<SBM_DynamicParams>>(0x04);
                if (re == null)
                    return null;
                return re.Array;
            }
            set
            {
                if (value == null)
                {
                    _s.SetInt32(0x08, 0);
                    _s.SetReference(0x04, null);
                }
                else
                {
                    _s.SetInt32(0x08, value.Length);
                    var re = _s.GetCreateReference<HSDArrayAccessor<SBM_DynamicParams>>(0x04);
                    re.Array = value;
                }
            }
        }
    }

    public class SBM_DynamicParams : HSDAccessor
    {
        public override int TrimmedSize => 0x3C;

        [DisplayName("Follow Damping")]
        [Description("Controls how strongly the bone resists following motion from its parent. Higher values create more lag and softer movement.")]
        public float FollowDamping
        {
            get => _s.GetFloat(0x00);
            set => _s.SetFloat(0x00, value);
        }

        [DisplayName("Stiffness")]
        [Description("Controls how strongly the bone returns toward its rest pose. Higher values make the bone appear stiffer and less flexible.")]
        public float Stiffness
        {
            get => _s.GetFloat(0x04);
            set => _s.SetFloat(0x04, value);
        }

        [DisplayName("Rest Rotation X")]
        [Description("Default/rest rotation around the X axis, in radians.")]
        public float RotX
        {
            get => _s.GetFloat(0x08);
            set => _s.SetFloat(0x08, value);
        }

        [DisplayName("Rest Rotation Y")]
        [Description("Default/rest rotation around the Y axis, in radians.")]
        public float RotY
        {
            get => _s.GetFloat(0x0C);
            set => _s.SetFloat(0x0C, value);
        }

        [DisplayName("Rest Rotation Z")]
        [Description("Default/rest rotation around the Z axis, in radians.")]
        public float RotZ
        {
            get => _s.GetFloat(0x10);
            set => _s.SetFloat(0x10, value);
        }

        [DisplayName("Unknown Rotation Parameter")]
        [Description("Unknown rotation-related parameter. Purpose has not yet been identified.")]
        public float RotW
        {
            get => _s.GetFloat(0x14);
            set => _s.SetFloat(0x14, value);
        }

        [DisplayName("Rotation Limit")]
        [Description("Maximum allowed angular deviation from the rest pose, in radians.")]
        public float RotationLimit
        {
            get => _s.GetFloat(0x18);
            set => _s.SetFloat(0x18, value);
        }

        [Browsable(false)]
        public float PARAM8
        {
            get => _s.GetFloat(0x1C);
            set => _s.SetFloat(0x1C, value);
        }

        [Browsable(false)]
        public float PARAM9
        {
            get => _s.GetFloat(0x20);
            set => _s.SetFloat(0x20, value);
        }

        [Browsable(false)]
        public float PARAM10
        {
            get => _s.GetFloat(0x24);
            set => _s.SetFloat(0x24, value);
        }

        [Browsable(false)]
        public float PARAM11
        {
            get => _s.GetFloat(0x28);
            set => _s.SetFloat(0x28, value);
        }

        [Browsable(false)]
        public float PARAM12
        {
            get => _s.GetFloat(0x2C);
            set => _s.SetFloat(0x2C, value);
        }

        [Browsable(false)]
        public float PARAM13
        {
            get => _s.GetFloat(0x30);
            set => _s.SetFloat(0x30, value);
        }

        [DisplayName("Inertia Damping")]
        [Description("Controls how quickly secondary motion loses momentum over time, in radians per frame. Higher values stop swinging motion more quickly.")]
        public float InertiaDamping
        {
            get => _s.GetFloat(0x34);
            set => _s.SetFloat(0x34, value);
        }

        [DisplayName("Resistance")]
        [Description("Controls resistance to sudden directional changes, in radians. Higher values reduce responsiveness and create heavier motion.")]
        public float Resistance
        {
            get => _s.GetFloat(0x38);
            set => _s.SetFloat(0x38, value);
        }

        public override string ToString()
        {
            return $"Joint Param";
        }
    }
}
