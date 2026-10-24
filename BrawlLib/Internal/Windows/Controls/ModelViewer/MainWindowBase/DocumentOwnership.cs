using BrawlLib.SSBB.ResourceNodes;

namespace BrawlLib.Internal.Windows.Controls.ModelViewer.MainWindowBase
{
    public partial class ModelEditorBase
    {
        // A hosting document may own native resources without transferring their lifetime to this panel.
        public virtual bool CanCloseExternalFile(ResourceNode node) => true;

        public virtual bool TrySaveExternalFile(ResourceNode node, bool saveAs, out bool result)
        {
            result = false;
            return false;
        }
    }
}
