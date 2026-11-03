using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private void UseBt3OnTarget()
        {
            if(!(_libraryList?.SelectedNode?.Tag is Bt3BrowseChoice selected)||
                !(modelEditControl1.TargetModel is MDL0Node target)||_fighterProject?.TargetGame!="Brawl"||
                _fighterProject.NativeTarget!=null||_fighterProject.Model==null)
                return;
            if(SimpleWorkspaceBusy||_retargetBusy||_controlBusy||_pinGesture!=null)
                throw new InvalidOperationException("Finish the current editor or workspace operation before transferring this BT3 action.");
            var exact=ResolveBt3Choice(selected);
            if(exact==null)throw new InvalidOperationException("Choose an available BT3 transformation, costume and action.");
            CancelLibraryAutoPreview();CancelBt3PreviewLoad();_libraryTimer?.Stop();
            if(_libraryPlay!=null)_libraryPlay.Text="Play";
            modelEditControl1.StopAnim();
            var project=_fighterProject;
            var targetReview=CaptureFighterRig(target,true);
            string coordinates=ReviewedFighterCoordinateKey(target);
            string originalTarget=NativeControlHash(target,".mdl0");
            string targetPath=null;
            _retargetBusy=true;UpdateSimpleWorkspace();
            try
            {
                Directory.CreateDirectory(_controlStage);
                targetPath=Path.Combine(_controlStage,Guid.NewGuid().ToString("N")+".mdl0");
                target.Export(targetPath);
                using(var targetCopy=NodeFactory.FromFile(null,targetPath) as MDL0Node)
                {
                    if(targetCopy==null)throw new InvalidDataException("The current target model snapshot did not reopen.");
                    var roleNames=targetReview.Rig.Roles.ToDictionary(p=>p.Key,p=>targetReview.Bones[p.Value].Name);
                    var preparedTarget=BrawlRetargeter.Capture(targetCopy,roleNames,
                        coordinateFrame:ReviewedFighterCoordinateFrame(target));
                    Bt3SourceLease source=null;
                    using(var result=LibraryWork.Run(this,"Preparing BT3 transfer",(cancel,progress)=>
                    {
                        progress.Report("Verifying the exact BT3 costume and action...");
                        source=Bt3SourceLease.Open(exact.Inventory,exact.Form,exact.Costume,exact.Motion,null,cancel);
                        return BrawlRetargeter.BakeBt3(source,preparedTarget,cancel,
                            (percent,message)=>progress.Report(message));
                    }))
                    {
                        if(result==null)return;
                        void Current()
                        {
                            if(IsDisposed||Disposing||_fighterProject!=project||
                                modelEditControl1.TargetModel!=target||
                                !ReferenceEquals(_libraryList?.SelectedNode?.Tag,selected)||
                                !ReferenceEquals(ResolveBt3Choice(selected)?.Costume,exact.Costume)||
                                !ReferenceEquals(ResolveBt3Choice(selected)?.Motion,exact.Motion)||
                                ReviewedFighterCoordinateKey(target)!=coordinates||
                                NativeControlHash(target,".mdl0")!=originalTarget)
                                throw new InvalidOperationException("The selected BT3 source or target changed. This transfer was discarded.");
                            source.VerifyUnchanged();
                        }
                        Current();
                        string sourceRoles=string.Join(", ",result.Plan.Mapping
                            .Where(m=>!m.Role.StartsWith("trajectory.",StringComparison.Ordinal))
                            .Take(8).Select(m=>m.Role+" "+m.Source+" → "+m.Target));
                        using(var review=new Form{Name="bt3TransferReview",Text="Review BT3 transfer",ClientSize=new Size(690,300),
                            FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,
                            MinimizeBox=false,MaximizeBox=false,ShowInTaskbar=false})
                        {
                            var details=new TextBox{Name="bt3TransferDetails",ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,
                                TabStop=false,Bounds=new Rectangle(14,14,662,225),Text=
                                "Source: "+source.Identity+Environment.NewLine+"Target: "+target.Name+Environment.NewLine+
                                result.Animation.FrameCount+" native frames · "+result.Plan.Mapping.Length+" mapped joints"+Environment.NewLine+
                                result.Plan.Mapping.Count(m=>m.Role=="detail.finger")+" skinned finger joints matched by hand structure; extra thigh segments are carried through leg-chain motion."+Environment.NewLine+
                                result.IgnoredSourceTracks+" animated source joints have no target binding. "+
                                source.Source.Motion.EventCount+" gameplay events are outside this skeletal animation."+Environment.NewLine+
                                "Body mapping: "+sourceRoles+Environment.NewLine+
                                "Animated controls: "+string.Join(", ",result.Plan.Mapping.Where(m=>m.Role.StartsWith("trajectory.",StringComparison.Ordinal))
                                    .Select(m=>m.Source+" → "+m.Target))+". TransN stays at bind when its native source control is unanimated."+
                                Environment.NewLine+Environment.NewLine+
                                "The native Brawl animation reopened and passed sampled fractional pose checks. This is a review draft: contact quality, loop behavior and in-game playback remain unverified. Review and edit it in the Model Previewer before final use."};
                            var add=new Button{Name="bt3TransferAdd",Text="Add review draft",DialogResult=DialogResult.OK,
                                Bounds=new Rectangle(384,256,158,29)};
                            var cancel=new Button{Name="bt3TransferCancel",Text="Cancel",DialogResult=DialogResult.Cancel,
                                Bounds=new Rectangle(552,256,124,29)};
                            review.Controls.AddRange(new Control[]{details,add,cancel});review.AcceptButton=add;review.CancelButton=cancel;
                            review.Shown+=(s,e)=>{details.SelectionStart=0;details.SelectionLength=0;add.Focus();};
                            DarkTheme.Apply(review);
                            if(review.ShowDialog(this)!=DialogResult.OK)return;
                        }
                        Current();
                        var targetRoot=FighterRoot(project.Model);
                        if(targetRoot.IsDirty){targetRoot.Merge();targetRoot.ExportChecked(project.Model.WorkingPath);}
                        var asset=project.CreateBt3Revision(source,result);
                        var decoded=FighterRoot(asset);
                        RememberFighterImport();project.Animations.Add(asset);
                        project.SelectedAnimation=FighterAnimationKey(asset,decoded);project.Frame=1;
                        RebuildFighterScene();modelEditControl1.QueueFrameFighterModel();
                        MarkFighterChanged();UpdateFighterStatus();
                        _fighterStatus.Text="Added BT3 review draft · "+source.Form.Display+" · action "+source.Action.ToString("D3")+
                            ". Source motion retained; contacts and game playback require review.";
                    }
                }
            }
            finally
            {
                if(targetPath!=null&&File.Exists(targetPath))File.Delete(targetPath);
                _retargetBusy=false;UpdateSimpleWorkspace();
                if(!IsDisposed)QueueBt3Preview();
            }
        }
    }
}
