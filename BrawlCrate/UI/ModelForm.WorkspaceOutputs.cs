using System;
using System.IO;
using System.Linq;
using BrawlCrate.Studio;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private void ProtectCharacterWorkspaceOutput(string destination)
        {
            var info=_characterWorkspace.Info;if(info?.Path==null)return;
            string path=Path.GetFullPath(destination),root=Path.GetDirectoryName(Path.GetFullPath(info.Path));
            bool Inside(string folder)=>string.Equals(path,folder,StringComparison.OrdinalIgnoreCase)||path.StartsWith(folder+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
            if(string.Equals(path,Path.GetFullPath(info.Path),StringComparison.OrdinalIgnoreCase)||Inside(Path.Combine(root,"Sources"))||Inside(Path.Combine(root,".fighterport")))throw new IOException("Native output overlaps the character descriptor or preserved workspace inputs/revisions.");
            string active=info.Scenes.FirstOrDefault(s=>s.Id==_workspaceSceneId)?.Target;
            foreach(var target in info.Targets.Where(t=>t.Id!=active))
            {
                string id=Guid.Parse(target.Id).ToString("N");if(Inside(Path.Combine(root,"Targets",id))||Inside(Path.Combine(root,"Build",id)))throw new IOException("Choose an output within this target's scope. This path belongs to another target.");
            }
        }
    }
}
