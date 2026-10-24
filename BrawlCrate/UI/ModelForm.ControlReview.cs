using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Threading.Tasks;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        internal Action<ControlReviewDialog> ControlReviewReady;
        private async Task ReviewControlSetup()
        {
            var loaded=await LoadControls();int epoch=_controlEpoch;string key=ControlKey(),before=ControlState(key);
            var model=(MDL0Node)modelEditControl1.TargetModel;var clip=modelEditControl1.SelectedCHR0;
            string modelHash=NativeControlHash(model,".mdl0"),clipHash=clip==null?null:NativeControlHash(clip,".chr0");
            using(var dialog=new ControlReviewDialog(model,loaded))
            {
                dialog.Shown+=(s,e)=>ControlReviewReady?.Invoke(dialog);
                if(dialog.ShowDialog(this)!=System.Windows.Forms.DialogResult.OK)return;
                var reply=await _controlEngine.Send(new{Operation="review-controls",Review=dialog.Review});
                if(IsDisposed||epoch!=_controlEpoch||key!=ControlKey()||modelEditControl1.TargetModel!=model||modelEditControl1.SelectedCHR0!=clip||NativeControlHash(model,".mdl0")!=modelHash||(clip!=null&&NativeControlHash(clip,".chr0")!=clipHash))throw new InvalidOperationException("The model or animation changed during review. The stale control setup was discarded.");
                StoreControls(key,reply.State);PublishPins(reply);MarkFighterChanged();
                modelEditControl1.RecordEditorCommand(()=>RestoreControlMetadata(key,before),()=>RestoreControlMetadata(key,reply.State));
                _fighterStatus.Text="Reviewed animation controls saved for this rig. Existing bones and animation keys are preserved.";
            }
        }
    }
}
