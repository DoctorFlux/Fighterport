namespace BrawlCrate.Studio
{
    // Shared game action semantics, not character or costume heuristics.
    // BtlAct_GetIdleMotion chooses 0/1 on ground and 0x26/0x27 in flight;
    // model variant (+4 costume pack) does not select a separate motion bank.
    internal static class Bt3ActionLabels
    {
        internal static string Describe(int action)
        {
            switch(action)
            {
                case 0:return "Ground idle · normal · Action 000";
                case 1:return "Ground idle · alternate · Action 001";
                case 0x26:return "Flight idle · normal · Action 038";
                case 0x27:return "Flight idle · alternate · Action 039";
                default:return "Action "+action.ToString("D3");
            }
        }
    }
}
