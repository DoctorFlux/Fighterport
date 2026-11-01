using System.ComponentModel;

namespace HSDRaw.MEX.Param
{
    public class MEX_AllStarFigther : HSDAccessor
    {
        public override int TrimmedSize => 0x04;

        [Description("(External ID) 50% chance this stage will be selected")]
        public byte Stage1 { get => _s.GetByte(0x00); set => _s.SetByte(0x00, value); }

        [Description("(External ID) 50% chance this stage will be selected")]
        public byte Stage2 { get => _s.GetByte(0x01); set => _s.SetByte(0x01, value); }

        [DisplayName("Fighter Internal ID (CKIND)")]
        [Description("The internal id (CKIND) of the figther. Use 255 for end of list nullifier")]
        public byte FighterID { get => _s.GetByte(0x03); set => _s.SetByte(0x03, value); }
    }
}
