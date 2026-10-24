using BrawlCrate.UI.Model_Previewer.ModelEditControl;
using BrawlLib.Internal;
using System;
using System.Linq;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private void EditControlChannel(int channel,float value)
        {
            if(_controlBusy||_retargetBusy||channel<0||channel>8||modelEditControl1.SelectedControlHandle==null){modelEditControl1.UpdatePropDisplay();return;}
            if(float.IsNaN(value)||float.IsInfinity(value)||channel<3&&value<=0){_fighterStatus.Text="Enter a finite value and positive scale for an animation control.";modelEditControl1.UpdatePropDisplay();return;}
            if(modelEditControl1.SelectedCHR0!=null&&modelEditControl1.CurrentFrame==0)modelEditControl1.SetFrame(1);
            var pin=modelEditControl1.SelectedControlHandle;var frame=modelEditControl1.SelectedControlTransform.Value;frame[channel]=value;
            if(pin.Id.StartsWith("pole:")&&channel<6){_fighterStatus.Text="Pole controls edit position. Move the pole to change the limb bend.";modelEditControl1.UpdatePropDisplay();return;}
            if(pin.Soft&&channel<3)frame._scale=new Vector3(value);
            var goal=pin.Id.StartsWith("fk:")?System.Linq.Enumerable.Range(0,9).Select(i=>frame[i]).ToArray():ModelEditControl.ControlHandleGoal(frame);
            BeginControlGesture(pin);MoveControlGesture(goal);EndControlGesture();
        }
    }
}
