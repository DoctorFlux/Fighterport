using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static void TimingFitOracle()
    {
        using(var source=new CHR0Node{Name="Original",FrameCount=9,Loop=true})
        {
            var entry=new CHR0EntryNode{Name="ExistingJoint",ScaleCompensateApply=true,ClassicScaleOff=true};source.AddChild(entry);entry.SetKeyframe(6,0,2,true,true);entry.SetKeyframe(6,8,10,true,true);entry.GetKeyframe(6,0)._tangent=1.5f;entry.GetKeyframe(6,8)._tangent=-.5f;
            var fit=new NativeTimingFit{Start=1,End=7,Frames=13};
            using(var output=fit.Resample(source))
            {
                var target=(CHR0EntryNode)output.Children[0];float maximum=0;
                for(int frame=0;frame<13;frame++){double t=(1+frame*.5)/8;double expected=(2*t*t*t-3*t*t+1)*2+(t*t*t-2*t*t+t)*8*1.5+(-2*t*t*t+3*t*t)*10+(t*t*t-t*t)*8*(-.5);maximum=Math.Max(maximum,(float)Math.Abs(target.GetFrameValue(6,frame)-expected));}
                Check(maximum<.00001,"Offline trim/frame fitting samples an independent Hermite oracle at fractional source times");
                Check(output.FrameCount==13&&!output.Loop&&target.Name==entry.Name,"Timing fit retains native bone identity and explicitly produces the requested non-looping range");
                Check(target.ScaleCompensateApply&&target.ClassicScaleOff&&!target.ScaleCompensateParent,"Timing fitting retains native transform interpretation flags");
            }
            Check(source.FrameCount==9&&source.Loop&&entry.GetKeyframe(6,0)._tangent==1.5f&&entry.KeyArrays[6].KeyCount==2,"Timing fitting leaves authentic source keys, tangents, duration and loop unchanged");
            bool refused=false;try{new NativeTimingFit{Start=-1,End=7,Frames=8}.Resample(source).Dispose();}catch(InvalidDataException){refused=true;}Check(refused,"Invalid timing ranges reject before output publication");
        }
    }
    static async Task LibraryBatch(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Batch acceptance requires a private desktop.");TimingFitOracle();
        string donor=args.Contains("--retarget-source")?args[Array.IndexOf(args,"--retarget-source")+1]:args[0];string motion=args[1];
        string modelHash=FighterProject.HashFile(donor),motionHash=FighterProject.HashFile(motion),targetHash=FighterProject.HashFile(args[0]);
        if(args.Contains("--workspace-batch-review")||args.Contains("--workspace-set-export"))
        {
            string ownedDonor=Path.Combine(output,"owned-source-model.pac"),ownedMotion=Path.Combine(output,"owned-source-motion.pac");
            File.Copy(donor,ownedDonor);File.Copy(motion,ownedMotion);donor=ownedDonor;motion=ownedMotion;
        }
        var library=new AnimationLibrary(Path.Combine(output,"library"));var inspected=BrawlLibrarySource.InspectModels(donor).First();
        library.Register(BrawlLibrarySource.Prepare(new LibraryRegistration{ModelPath=donor,ModelKey=inspected.ResourceKey,MotionPath=motion,Character="Supplied donor",GameVersion="Supplied fixture",BuildEvidence="Local user supplied fixtures; native workflow acceptance only",ApproveAssociation=true}));
        var project=FighterProject.Create("Native batch fitting");var form=new ModelForm();form.InitializeFighterProject(project);form.Show();Call(form,"ImportFighterModel",args[0]);
        await Task.Delay(150);await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        Call(form,"BuildLibraryPane");form.GetType().GetField("_library",Private).SetValue(form,library);((TextBox)Field(form,"_librarySearch")).Text="Wait";((Timer)Field(form,"_librarySearchTimer")).Stop();
        if(args.Contains("--workspace-batch-review")||args.Contains("--workspace-set-export"))
        {
            foreach(var item in library.Browse("Wait").Where(i=>i.Clip.Name=="Wait1"||i.Clip.Name=="Wait2").Where(i=>i.Model!=null))library.SetFavorite(item.Clip.Id,true);
            SimpleControl<CheckBox>(form,"libraryFavoritesOnly").Checked=true;((Timer)Field(form,"_librarySearchTimer")).Stop();
        }
        Exception failure=null;bool finished=false;int before=project.Animations.Count;
        form.LibraryBatchReady=async window=>
        {
            try
            {
                var grid=(DataGridView)window.Controls.Find("batchClips",true).Single();var prepare=(Button)window.Controls.Find("prepareBatch",true).Single();var apply=(Button)window.Controls.Find("applyBatch",true).Single();var review=(CheckBox)window.Controls.Find("reviewBatch",true).Single();var shared=(NumericUpDown)window.Controls.Find("sharedFrames",true).Single();
                var rows=grid.Rows.Cast<DataGridViewRow>().Where(r=>((LibrarySearchItem)r.Tag).State==LibrarySourceState.Available&&((LibrarySearchItem)r.Tag).Clip.FrameCount>=5).OrderBy(r=>((LibrarySearchItem)r.Tag).Clip.Name=="Wait1"?0:((LibrarySearchItem)r.Tag).Clip.Name=="Wait2"?1:2).Take(2).ToArray();Check(rows.Length==2,"Batch window lists verified source-bound clips from the shared library");
                if(args.Contains("--workspace-batch-review")||args.Contains("--workspace-set-export"))Check(grid.Rows.Count==2&&SimpleControl<Label>(window,"batchHeading").Text.Contains("2 of 2 matches"),"Favorites/current filters apply before batch counts and the 200-row display boundary is explicit");
                shared.Value=9;foreach(var row in rows){row.Cells["Use"].Value=true;row.Cells["Start"].Value=2;row.Cells["End"].Value=5;}rows[1].Cells["Frames"].Value=7;
                prepare.PerformClick();Check(rows.All(r=>Convert.ToString(r.Cells["Result"].Value).StartsWith("Review:")),"Both rows run the original Next retargeter with shared timing and a per-row override");
                grid.CurrentCell=rows[0].Cells["Clip"];var details=(TextBox)window.Controls.Find("batchDetails",true).Single();Check(details.Text.Contains(Convert.ToString(rows[0].Cells["Result"].Value)),"The selected native batch report exposes the full untruncated residual and channel-loss list");
                Check(project.Animations.Count==before&&!apply.Enabled,"Batch preparation keeps results separate until explicit channel-loss review");
                review.Checked=true;Check(apply.Enabled,"Reviewing batch warnings enables adding the prepared results");
                await CaptureWindow(window,Path.Combine(output,"batch-fitting-review.png"));
                var originalMode=project.GroundMode;project.GroundMode=originalMode==GroundContactMode.Auto?GroundContactMode.Airborne:GroundContactMode.Auto;apply.PerformClick();
                Check(project.Animations.Count==before&&!apply.Enabled,"Changed contact dependencies reject and discard stale batch results atomically");project.GroundMode=originalMode;
                prepare.PerformClick();review.Checked=true;apply.PerformClick();
                Check(project.Animations.Count==before+2,"Reviewed batch adds two independent target revisions in one import transaction");
            }
            catch(Exception e){failure=e;window.Close();}
            finally{finished=true;}
        };
        Call(form,"ShowLibraryBatch");await Wait(()=>finished);if(failure!=null)throw failure;
        var assets=project.Animations.Skip(before).ToArray();Check(assets.All(a=>a.Retarget.Algorithm==HumanoidRetargeter.Algorithm&&a.Retarget.Library!=null&&a.Retarget.Timing!=null),"Batch saves original Next provenance, authentic source ownership and explicit timing recipes");
        Check(assets.Select(a=>a.Retarget.Timing.Frames).OrderBy(i=>i).SequenceEqual(new[]{7,9}),"Shared fitting and per-row timing remain distinct in persisted recipes");
        if(args.Contains("--workspace-set-export")) await SetExportCases(form,assets,output);
        if(args.Contains("--workspace-batch-review")) { await ReviewBatchCases(form,assets,output); await ReviewBatchFailureCases(form,assets,output,motion); }
        string save=Path.Combine(output,"batch.fpstudio");form.SaveFighterProject(save);using(var fresh=FighterProject.Open(save))
        {
            Check(fresh.Animations.Skip(before).All(a=>a.Retarget.Timing.Start==1&&a.Retarget.Timing.End==4),"Fresh project reopen retains exact source trim boundaries");
            foreach(var asset in fresh.Animations.Skip(before))using(var native=NodeFactory.FromFile(null,asset.WorkingPath) as CHR0Node)Check(native!=null&&native.FrameCount==asset.Retarget.Timing.Frames&&!native.Loop,"Fresh native batch animation decodes with requested duration and explicit non-looping policy");
        }
        await Task.Delay(150);await CaptureWindow(form,Path.Combine(output,"batch-result-viewer.png"));
        Check(FighterProject.HashFile(donor)==modelHash&&FighterProject.HashFile(motion)==motionHash&&FighterProject.HashFile(args[0])==targetHash,"Both source containers and the supplied target remain immutable after batch fitting");
        Call(form,"UndoFighterImport");Check(project.Animations.Count==before,"One native import Undo removes the complete batch while saved independent outputs remain available");
        ((AnimationControlClient)Field(form,"_controlEngine")).Dispose();project.Dispose();BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.Instances.Remove(form.FighterEditor);form.Dispose();
    }
}
