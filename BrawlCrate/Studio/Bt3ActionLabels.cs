namespace BrawlCrate.Studio
{
    // Shared game action semantics, not character or costume heuristics.
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
                case 0:return "Action 000 · ground motion · normal-health selection";
                case 1:return "Action 001 · ground motion · low-health selection";
                case 0x26:return "Action 038 · flight motion · normal-health selection";
                case 0x27:return "Action 039 · flight motion · low-health selection";
                default:return "Action "+action.ToString("D3");
            }
        }
    }
}
