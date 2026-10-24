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
        internal event Action<Form> RetargetDialogReady;
        private RetargetResult BakeWithProgress(MDL0Node source,MDL0Node target,CHR0Node clip)
        {
            if(_controlBusy||_retargetBusy||_workspaceBusy||_pinGesture!=null)throw new InvalidOperationException("Finish the current animation-control or workspace edit before retargeting.");
            _retargetBusy=true;
            UpdateSimpleWorkspace();
            try{return BakeNextSnapshots(source,target,clip);}
            finally{_retargetBusy=false;UpdateSimpleWorkspace();if(_controlReloadRequested)QueueControlSetup();}
        }
        private RetargetResult BakeNextSnapshots(MDL0Node source,MDL0Node target,CHR0Node clip)
        {
            modelEditControl1.StopAnim();
            // Keep Next's solver unchanged. The worker reads owned immutable
            // native snapshots, never the viewer's live resources.
            var sourceReview=CaptureFighterRig(source);var targetReview=CaptureFighterRig(target);
            Directory.CreateDirectory(_controlStage);
            string sourcePath=Path.Combine(_controlStage,Guid.NewGuid().ToString("N")+".mdl0"),targetPath=Path.Combine(_controlStage,Guid.NewGuid().ToString("N")+".mdl0"),clipPath=Path.Combine(_controlStage,Guid.NewGuid().ToString("N")+".chr0");
            try
            {
            source.Export(sourcePath);target.Export(targetPath);clip.Export(clipPath);
            string sourceHash=FighterProject.HashFile(sourcePath),targetHash=FighterProject.HashFile(targetPath),clipHash=FighterProject.HashFile(clipPath);
            var project=_fighterProject;var selected=modelEditControl1.TargetAnimation;var groundMode=project.GroundMode;
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
                        (percent,message)=>((IProgress<Tuple<int,string>>)progress).Report(Tuple.Create(percent,message)),groundMode),cancellation.Token)
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
                if(IsDisposed||_fighterProject!=project||modelEditControl1.TargetModel!=target||modelEditControl1.TargetAnimation!=selected||project.GroundMode!=groundMode||NativeControlHash(source,".mdl0")!=sourceHash||NativeControlHash(target,".mdl0")!=targetHash||NativeControlHash(clip,".chr0")!=clipHash)
                    throw new InvalidOperationException("Retarget inputs changed. The stale result was discarded; retarget the current source and target again.");
                return result;
            }
            catch{result.Dispose();throw;}
            }
            }
            finally{foreach(string path in new[]{sourcePath,targetPath,clipPath})if(File.Exists(path))File.Delete(path);}
        }
    }
}
