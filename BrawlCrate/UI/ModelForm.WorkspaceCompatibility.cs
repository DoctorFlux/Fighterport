using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private static FighterProject CreateLegacyWorkspaceProject(ControlReply reply,string name)
        {
            var project=FighterProject.Create(name);
            try
            {
                project.Model=project.StageAsset(reply.Model,"model");
                using(var root=NodeFactory.FromFile(null,project.Model.WorkingPath))project.ModelNodePath=BrawlLib.Internal.Windows.Controls.Model_Panel.ModelPanel.CollectModels(root).Cast<ResourceNode>().Single(n=>WorkspaceNativeIdentity(n)==reply.ModelKey).TreePath;
                string prefix=project.Model.Id+"|"+project.ModelNodePath+"|";
                if(reply.PoseState!=null)project.AnimationControls.Add(new AnimationControlState{Key=prefix+"pose",Json=reply.PoseState});
                foreach(string path in reply.MotionPaths??new string[0])
                {
                    var asset=project.StageAsset(path,"animation");project.Animations.Add(asset);
                    using(var root=NodeFactory.FromFile(null,asset.WorkingPath))
                    {
                        foreach(var animation in DiscoverFighterAnimations(root).OfType<CHR0Node>())
                        {
                            string entry=WorkspaceNativeIdentity(animation);var state=reply.WorkspaceControls?.SingleOrDefault(c=>string.Equals(c.Path,path,StringComparison.OrdinalIgnoreCase)&&c.Entry==entry);
                            if(state!=null)
                            {
                                project.AnimationControls.Add(new AnimationControlState{Key=prefix+FighterAnimationKey(asset,animation),Json=state.State});
                                project.WorkspaceClips.Add(new WorkspaceClipLink{Key=FighterAnimationKey(asset,animation),Id=state.Id});
                            }
                            if(string.Equals(reply.SelectedMotion,path,StringComparison.OrdinalIgnoreCase)&&entry==reply.SelectedEntry){project.SelectedAnimation=FighterAnimationKey(asset,animation);project.Frame=reply.DisplayFrame;}
                        }
                    }
                }
                project.Warnings.AddRange(reply.Diagnostics??new string[0]);return project;
            }
            catch{project.Dispose();throw;}
        }
    }
}
