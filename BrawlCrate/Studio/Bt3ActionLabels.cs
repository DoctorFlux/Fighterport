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
                case 0:return "Ground idle · normal health · Action 000";
                case 1:return "Ground idle · low health · Action 001";
                case 0x26:return "Flight idle · normal health · Action 038";
                case 0x27:return "Flight idle · low health · Action 039";
                default:return "Action "+action.ToString("D3");
            }
        }
    }
}
