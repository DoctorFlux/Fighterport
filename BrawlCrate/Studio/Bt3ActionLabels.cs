using System.Collections.Generic;

namespace BrawlCrate.Studio
{
    // Display-only motion IDs from Tenkaichi3Decomp 1c4d9060d859f5bc53a0843ad5937c04f43aa6e5.
    // These are common runtime roles, not form-specific move names or retarget bindings.
    internal static class Bt3ActionLabels
    {
        private sealed class Role
        {
            internal string Name, Evidence;
            internal Role(string name,string evidence){Name=name;Evidence=evidence;}
        }
        private static readonly Dictionary<int,Role> Roles=new Dictionary<int,Role>();
        static Bt3ActionLabels()
        {
            void Add(int id,string name,string evidence)=>Roles.Add(id,new Role(name,evidence));
            const string idle="btl_act_d.c / BtlAct_GetIdleMotion";
            const string move="btl_act_d.c / BtlAct_MoveHandler";
            const string close="btl_act_d.c / BtlAct_CloseMoveHandler";
            const string dash="btl_act_d.c / BtlAct_DashMoveHandler";
            const string flight="btl_act_d.c / flight handlers";
            Add(0,"Ground idle · normal HP",idle);Add(1,"Ground idle · low HP",idle);
            Add(2,"Move entry",move);Add(3,"Move loop",move);
            Add(4,"Move side A entry",move);Add(5,"Move side A loop",move);
            Add(6,"Move side B entry",move);Add(7,"Move side B loop",move);
            Add(8,"Close move · toward",close);Add(9,"Close move · away",close);
            Add(10,"Close move side layer A",close);Add(11,"Close move side layer B",close);
            Add(12,"Dash entry",dash);Add(13,"Dash loop",dash);Add(14,"Dash stop",dash);
            for(int id=15;id<=20;id++)Add(id,"Dash side layer "+(id-14),dash);
            Add(21,"Dash turn",dash);Add(22,"Dash side layer 7",dash);Add(23,"Dash side layer 8",dash);
            Add(24,"Homing dash entry","btl_act_d.c / Action 0x19");
            Add(25,"Homing dash loop","btl_act_d.c / Action 0x19");
            Add(26,"Homing dash stop","btl_act_d.c / Action 0x19");
            Add(28,"Back step","btl_act_e.c / Actions 0x1B..0x1D");
            Add(29,"Side step A","btl_act_e.c / Actions 0x1B..0x1D");
            Add(30,"Side step B","btl_act_e.c / Actions 0x1B..0x1D");
            Add(31,"Jump takeoff","btl_act_d.c / Actions 0x10..0x13");
            Add(32,"Jump rise","btl_act_d.c / Actions 0x11 and 0x13");
            Add(33,"Jump fall","btl_act_d.c / Actions 0x11 and 0x13");
            Add(34,"Jump land","btl_act_d.c / Actions 0x11 and 0x13");
            Add(35,"Rush dash entry","btl_act_e.c / Action 0x34");
            Add(36,"Rush dash loop","btl_act_e.c / Action 0x34");
            Add(37,"Rush dash stop","btl_act_e.c / Action 0x34");
            Add(38,"Flight idle · normal HP",idle);Add(39,"Flight idle · low HP",idle);
            Add(40,"Hop back","btl_act_e.c / Action 0x32");
            Add(43,"Ascend",flight);Add(44,"Descend",flight);
            Add(45,"Ascend entry / land",flight);Add(46,"Fast ascend entry",flight);
            Add(47,"Fast ascend loop",flight);Add(48,"Fast ascend stop",flight);
            Add(49,"Fast descend entry",flight);Add(50,"Fast descend loop",flight);
            Add(51,"Fast descend stop",flight);
            for(int id=0x37;id<=0x40;id++)Add(id,"Rush combo step "+(id-0x36),"btl_act_a.c / Action 0x44");
            for(int id=0x4A;id<=0x5B;id++)Add(id,"Charged smash sequence "+(id-0x49),"btl_act_a.c / Actions 0x47..0x4C");
            Add(0x6B,"Flying kick wind-up","btl_act_a.c / Action 0x58");
            Add(0x6C,"Flying kick hold","btl_act_a.c / Action 0x58");
            Add(0x6D,"Flying kick strike","btl_act_a.c / Action 0x58");
            Add(0x6E,"Rising attack wind-up","btl_act_a.c / Action 0x59");
            Add(0x6F,"Rising attack hold","btl_act_a.c / Action 0x59");
            Add(0x70,"Rising attack strike","btl_act_a.c / Action 0x59");
            Add(0x71,"Ki blast volley A","btl_act_d.c / Action 0x95");
            Add(0x72,"Ki blast volley B","btl_act_d.c / Action 0x95");
            Add(0x7A,"Downward ki blast","btl_act_d.c / Action 0xB2");
            Add(0x8D,"Charged blast wind-up","btl_act_d.c / Action 0xB3");
            Add(0x8E,"Charged blast hold","btl_act_d.c / Action 0xB3");
            Add(0x8F,"Charged blast release","btl_act_d.c / Action 0xB3");
            Add(0x94,"Grab dash entry","btl_act_h.c / Actions 0xB4..0xB6");
            Add(0x95,"Grab dash loop","btl_act_h.c / Actions 0xB4..0xB6");
            Add(0xF6,"Vanish step","btl_act_e.c / Actions 0x20..0x24");
            Add(0xF7,"Ki-spending hold","btl_act_a.c / Actions 0x67..0x69");
            Add(0xF8,"Ki-spending lunge","btl_act_a.c / Action 0x6A");
            Add(0x17F,"Search for opponent","btl_act_c.c / Action 0x36");
            Add(0x18B,"Non-flyer air",idle);
            Add(0x18C,"Short rise","btl_act_d.c / Action 0x16");
            Add(0x197,"Attack dash approach","btl_act_a.c / Actions 0x92, 0x9B, 0xAB");
            Add(0x198,"Attack dash contact","btl_act_a.c / Actions 0x92, 0x9B, 0xAB");
        }
        internal static string Describe(int action,string alias=null)
        {
            string prefix=(action<0x19E?"Action ":"Bank 0 member ")+action.ToString("D3")+" · ";
            if(!string.IsNullOrWhiteSpace(alias))return prefix+alias;
            return prefix+(Roles.TryGetValue(action,out var role)?role.Name:"Unlabeled");
        }
        internal static string Detail(int action,string alias=null)
        {
            string identity="Native motion bank 0, member "+action.ToString("D3")+". ";
            if(!string.IsNullOrWhiteSpace(alias))identity+="Library label: "+alias+". ";
            return Roles.TryGetValue(action,out var role)
                ?identity+"Common runtime role from Tenkaichi3Decomp 1c4d9060, "+role.Evidence+". The role does not verify this form's pose or costume."
                :identity+"No verified common gameplay role. Right-click this library action and choose Label BT3 action after reviewing the motion; its native identity stays exact.";
        }
    }
}
