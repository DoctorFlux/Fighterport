using BrawlCrate.Studio.Retargeting;
using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private bool _retargetBusy;
        private RetargetKeyframeMode _retargetKeyframeMode;
        internal event Action<Form> RetargetDialogReady;
        internal Action<Form> RetargetKeyframeReviewReady;
        private RetargetResult BakeWithProgress(MDL0Node source,MDL0Node target,CHR0Node clip)=>BakeWithProgressMode(source,target,clip,null);
        private RetargetResult BakeWithProgressMode(MDL0Node source,MDL0Node target,CHR0Node clip,RetargetKeyframeMode? forcedMode)
        {
            if(_controlBusy||_retargetBusy||_workspaceBusy||_pinGesture!=null)throw new InvalidOperationException("Finish the current animation-control or workspace edit before retargeting.");
            _retargetBusy=true;
            UpdateSimpleWorkspace();
            var restore=CaptureWorkspaceFocus();
            try{return BakeNextSnapshots(source,target,clip,forcedMode);}
            finally{_retargetBusy=false;UpdateSimpleWorkspace();if(_controlReloadRequested)QueueControlSetup();if((forcedMode??_retargetKeyframeMode)!=RetargetKeyframeMode.Dense)restore();}
        }
        private RetargetResult BakeNextSnapshots(MDL0Node source,MDL0Node target,CHR0Node clip,RetargetKeyframeMode? forcedMode)
        {
            modelEditControl1.StopAnim();
            // Keep Next's solver unchanged. The worker reads owned immutable
            // native snapshots, never the viewer's live resources.
            var sourceReview=CaptureFighterRig(source);var targetReview=CaptureFighterRig(target);
            Directory.CreateDirectory(_controlStage);
            string sourcePath=Path.Combine(_controlStage,Guid.NewGuid().ToString("N")+".mdl0"),targetPath=Path.Combine(_controlStage,Guid.NewGuid().ToString("N")+".mdl0"),clipPath=Path.Combine(_controlStage,Guid.NewGuid().ToString("N")+".chr0");
            string previewPath=null;var keyMode=forcedMode??_retargetKeyframeMode;
            try
            {
            string sourceClipContent=keyMode==RetargetKeyframeMode.Dense?null:NativeAnimationOptimization.SourceContentKey(clip);
            source.Export(sourcePath);target.Export(targetPath);
            if(keyMode==RetargetKeyframeMode.Dense)clip.Export(clipPath);
            else
            {
                // Preserve the live authored knots before the legacy native
                // writer can choose a quantized or per-frame codec. Only the
                // independently owned precise copy is rebuilt for this mode.
                using(var frozen=NativeAnimationOptimization.CopySource(clip))frozen.ExportChecked(clipPath);
                using(var decoded=NodeFactory.FromFile(null,clipPath) as CHR0Node)
                    if(decoded==null||NativeAnimationOptimization.SourceContentKey(decoded)!=sourceClipContent)
                        throw new InvalidDataException("The independent source-key snapshot did not preserve the live authored channels exactly.");
            }
            string sourceHash=FighterProject.HashFile(sourcePath),targetHash=FighterProject.HashFile(targetPath),clipHash=FighterProject.HashFile(clipPath);
            var project=_fighterProject;var selected=modelEditControl1.TargetAnimation;var groundMode=project.GroundMode;
            if(keyMode!=RetargetKeyframeMode.Dense)
            {
                string extension=Path.GetExtension(target.RootNode.FilePath);if(string.IsNullOrEmpty(extension))extension=".brres";
                previewPath=Path.Combine(_controlStage,Guid.NewGuid().ToString("N")+extension);target.RootNode.Export(previewPath);
            }
            using(var sourceCopy=NodeFactory.FromFile(null,sourcePath) as MDL0Node)
            using(var targetCopy=NodeFactory.FromFile(null,targetPath) as MDL0Node)
            using(var clipCopy=NodeFactory.FromFile(null,clipPath) as CHR0Node)
            {
            BrawlRig sourceRig=BrawlRetargeter.Capture(sourceCopy,sourceReview.Rig.Roles.ToDictionary(p=>p.Key,p=>sourceReview.Bones[p.Value].Name)),targetRig=BrawlRetargeter.Capture(targetCopy,targetReview.Rig.Roles.ToDictionary(p=>p.Key,p=>targetReview.Bones[p.Value].Name));
            foreach(var entry in clipCopy.Children.OfType<CHR0EntryNode>())
                for(int channel=0;channel<9;channel++){int populated=entry.Keyframes[channel].KeyCount;}
            RetargetResult result=null;Exception failure=null;bool completed=false;
            using(var cancellation=new CancellationTokenSource())
            using(var elapsedTimer=new System.Windows.Forms.Timer{Interval=250})
            using(var dialog=new Form{Text="Retargeting Animation",ClientSize=new Size(440,118),FormBorderStyle=FormBorderStyle.FixedDialog,
                StartPosition=FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false,ShowInTaskbar=false,UseWaitCursor=false})
            {
                var label=new Label{Text="Preparing "+clip.Name+"...",AutoSize=false,Bounds=new Rectangle(14,12,412,22)};
                var bar=new ProgressBar{Minimum=0,Maximum=100,Bounds=new Rectangle(14,38,412,20)};
                var cancel=new Button{Text="Cancel",Bounds=new Rectangle(340,74,86,28)};
                var elapsed=new Label{Name="retargetElapsed",Text="Elapsed 0.0 s",Bounds=new Rectangle(14,78,312,24)};
                var timer=System.Diagnostics.Stopwatch.StartNew();
                elapsedTimer.Tick+=(s,e)=>elapsed.Text="Elapsed "+timer.Elapsed.TotalSeconds.ToString("0.0")+" s";
                dialog.Controls.AddRange(new Control[]{label,bar,elapsed,cancel});
                DarkTheme.Apply(dialog);
                cancel.Click+=(s,e)=>{cancellation.Cancel();cancel.Enabled=false;label.Text="Stopping retarget...";};
                dialog.FormClosing+=(s,e)=>{if(!completed){e.Cancel=true;cancellation.Cancel();cancel.Enabled=false;label.Text="Stopping retarget...";}};
                var progress=new Progress<Tuple<int,string>>(update=>
                {
                    if(dialog.IsDisposed)return;
                    bar.Value=Math.Max(0,Math.Min(100,update.Item1));
                    if(!cancellation.IsCancellationRequested)label.Text=update.Item2;
                });
                dialog.Shown+=(s,e)=>
                {
                    RetargetDialogReady?.Invoke(dialog);
                    elapsedTimer.Start();
                    var scheduler=TaskScheduler.FromCurrentSynchronizationContext();
                    Task.Run(()=>BrawlRetargeter.Bake(sourceRig,targetRig,clipCopy,true,cancellation.Token,
                        (percent,message)=>((IProgress<Tuple<int,string>>)progress).Report(Tuple.Create(percent,message)),groundMode,keyMode),cancellation.Token)
                    .ContinueWith(task=>
                    {
                        completed=true;
                        elapsedTimer.Stop();
                        if(task.IsFaulted)failure=task.Exception.GetBaseException();
                        else if(!task.IsCanceled){if(cancellation.IsCancellationRequested)task.Result.Dispose();else result=task.Result;}
                        dialog.Close();
                    },scheduler);
                };
                dialog.ShowDialog(this);
            }
            if(failure!=null && !(failure is OperationCanceledException))throw new InvalidOperationException("The retarget did not finish: "+failure.Message,failure);
            if(result==null)return null;
            try
            {
                void Current()
                {
                    if(IsDisposed||_fighterProject!=project||modelEditControl1.TargetModel!=target||modelEditControl1.TargetAnimation!=selected||project.GroundMode!=groundMode||
                        !forcedMode.HasValue&&_retargetKeyframeMode!=keyMode||NativeControlHash(source,".mdl0")!=sourceHash||NativeControlHash(target,".mdl0")!=targetHash||
                        (keyMode==RetargetKeyframeMode.Dense?NativeControlHash(clip,".chr0")!=clipHash:NativeAnimationOptimization.SourceContentKey(clip)!=sourceClipContent))
                        throw new InvalidOperationException("Retarget inputs or key options changed. The stale result was discarded; retarget the current source and target again.");
                }
                Current();
                if(keyMode!=RetargetKeyframeMode.Dense)
                {
                    using(var beforeRoot=NodeFactory.FromFile(null,previewPath))
                    using(var afterRoot=NodeFactory.FromFile(null,previewPath))
                    using(var review=new RetargetKeyframeReviewDialog(beforeRoot,RetargetPreviewModel(beforeRoot,targetHash),afterRoot,RetargetPreviewModel(afterRoot,targetHash),
                        result,modelEditControl1.ModelPanel.CurrentViewport,modelEditControl1.CurrentFrame,project.Name))
                    {
                        review.Shown+=(s,e)=>RetargetKeyframeReviewReady?.Invoke(review);
                        if(review.ShowDialog(this)!=DialogResult.OK){result.Dispose();return null;}
                    }
                    Current();
                }
                return result;
            }
            catch{result.Dispose();throw;}
            }
            }
            finally{foreach(string path in new[]{sourcePath,targetPath,clipPath,previewPath})if(path!=null&&File.Exists(path))File.Delete(path);}
        }
        private MDL0Node RetargetPreviewModel(ResourceNode root,string content)
        {
            if(root==null)throw new InvalidDataException("The independent retarget review snapshot could not be opened.");
            return BrawlLib.Internal.Windows.Controls.Model_Panel.ModelPanel.CollectModels(root).OfType<MDL0Node>().Single(m=>NativeControlHash(m,".mdl0")==content);
        }
    }
}
