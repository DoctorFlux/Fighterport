namespace BrawlCrate.Studio
{
    // Shared game action semantics, not character or costume heuristics.
    // Verified against Tenkaichi3Decomp 1c4d9060: BtlAct_GetIdleMotion
    // and BtlAct_MoveHandler. A label describes the runtime's selection,
    // not proof that every decoded costume matches the game's rendered pose.
    // BtlAct_GetIdleMotion chooses 0/1 on ground and 0x26/0x27 in flight.
    // The second motion is selected by lowHealthIdle, which BtlChar_PostScene
    // derives from health and combat state. Model damage (+4 costume pack)
    // is independent of the idle motion.
    internal static class Bt3ActionLabels
    {
        internal static string Describe(int action)
        {
            switch(action)
            {
                case 0:return "Action 000 · Ground (normal HP)";
                case 1:return "Action 001 · Ground (low HP)";
                case 2:return "Action 002 · Move entry";
                case 3:return "Action 003 · Move loop";
                case 4:return "Action 004 · Side A entry";
                case 5:return "Action 005 · Side A loop";
                case 6:return "Action 006 · Side B entry";
                case 7:return "Action 007 · Side B loop";
                case 0x26:return "Action 038 · Flight (normal HP)";
                case 0x27:return "Action 039 · Flight (low HP)";
                case 0x18B:return "Action 395 · Non-flyer air";
                default:return "Action "+action.ToString("D3");
            }
        }
        internal static string Detail(int action)
        {
            switch(action)
            {
                case 0: return "Native action 000. The game selects this ground motion when the active gauge's low-health state is clear. Costume damage is independent.";
                case 1: return "Native action 001. The game selects this ground motion when the active gauge's low-health state is set. Costume damage is independent.";
                case 2: return "Native action 002. The free-movement handler plays this entry before action 003.";
                case 3: return "Native action 003. The free-movement handler loops this motion after action 002.";
                case 4: return "Native action 004. Side-layer A entry, blended with free movement; it is not a complete standalone move.";
                case 5: return "Native action 005. Side-layer A loop, blended with free movement; it is not a complete standalone move.";
                case 6: return "Native action 006. Side-layer B entry, blended with free movement; it is not a complete standalone move.";
                case 7: return "Native action 007. Side-layer B loop, blended with free movement; it is not a complete standalone move.";
                case 0x26: return "Native action 038. The idle handler selects this flight motion when the active gauge's low-health state is clear.";
                case 0x27: return "Native action 039. The idle handler selects this flight motion when the active gauge's low-health state is set.";
                case 0x18B: return "Native action 395. Selected for a non-flying fighter in the air outside water.";
                default: return "Native action "+action.ToString("D3")+". Its gameplay role is not verified; the numeric member identity is exact.";
            }
        }
    }
}
