using BrawlLib.SSBB.ResourceNodes;
using System;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private Action CaptureNativeBoneAnimationUndo(CHR0Node clip, string[] bones)
        {
            var snapshot = NativeControlSnapshot.Capture(clip, bones);
            return () =>
            {
                if (!OwnsFighterRoot(clip.RootNode)) return;
                _restoringCurve = true;
                try
                {
                    snapshot.Restore(clip);
                    if (ReferenceEquals(modelEditControl1.SelectedCHR0, clip))
                    {
                        modelEditControl1.UpdateModel();
                        modelEditControl1.RefreshControlCurveTarget();
                        modelEditControl1.InterpolationEditor?.RootChanged();
                        modelEditControl1.KeyframePanel?.UpdateKeyframes();
                    }
                    if (_curveTarget?.Parent == clip)
                        _curveBefore = NativeControlSnapshot.Capture(clip, new[] { _curveTarget.Name });
                    _loadedControlKey = null;
                    _controlEpoch++;
                    MarkFighterChanged();
                    QueueControlSetup();
                }
                finally { _restoringCurve = false; }
            };
        }
    }
}
