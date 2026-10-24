using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private CHR0EntryNode _curveTarget;
        private NativeControlSnapshot _curveBefore;
        private bool _curvePending,_restoringCurve;
        private void ObserveControlCurve(ResourceNode target)
        {
            if(_restoringCurve)return;
            if(ReferenceEquals(target,_curveTarget))return;
            FinishControlCurveEdit();_curveTarget=target as CHR0EntryNode;
            _curveBefore=_curveTarget?.Parent is CHR0Node clip?NativeControlSnapshot.Capture(clip,new[]{_curveTarget.Name}):null;
        }
        private void QueueControlCurveEdit(ResourceNode target)
        {
            if(_restoringCurve||!(target is CHR0EntryNode entry)||!(entry.Parent is CHR0Node)||!IsHandleCreated||IsDisposed)return;
            if(!ReferenceEquals(entry,_curveTarget)||_curveBefore==null)throw new InvalidOperationException("The curve target changed before its edit was captured.");
            MarkFighterChanged();if(_curvePending)return;_curvePending=true;
            // Native curve callbacks can precede their synchronized end-key write.
            // Finish after the complete UI message, using the captured owner.
            BeginInvoke(new Action(FinishControlCurveEdit));
        }
        private void FinishControlCurveEdit()
        {
            if(!_curvePending)return;_curvePending=false;
            if(_curveTarget==null||!(_curveTarget.Parent is CHR0Node clip)||_curveBefore==null)return;
            string bone=_curveTarget.Name;var before=_curveBefore;var after=NativeControlSnapshot.Capture(clip,new[]{bone});_curveBefore=after;
            if(before.ContentKey==after.ContentKey)return;
            Action<NativeControlSnapshot> restore=snapshot=>
            {
                _restoringCurve=true;
                try{snapshot.Restore(clip);if(ReferenceEquals(modelEditControl1.SelectedCHR0,clip)){modelEditControl1.UpdateModel();modelEditControl1.RefreshControlCurveTarget();modelEditControl1.InterpolationEditor?.RootChanged();modelEditControl1.KeyframePanel?.UpdateKeyframes();}_curveBefore=NativeControlSnapshot.Capture(clip,new[]{bone});MarkFighterChanged();}
                finally{_restoringCurve=false;}
            };
            modelEditControl1.RecordEditorCommand(()=>restore(before),()=>restore(after));
            _loadedControlKey=null;_controlEpoch++;modelEditControl1.ModelPanel.Invalidate();
        }
    }
}
