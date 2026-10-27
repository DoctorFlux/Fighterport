using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    static async Task ExistingTrackNextFit(ModelForm form,TransitionFitDialog window,MDL0Node model,CHR0Node source,string output)
    {
        var json=new JavaScriptSerializer();var original=json.Serialize(window.ReadOptions());
        string before=SetNativeContent(source);int frame=form.FighterEditor.CurrentFrame,undo=form.FighterEditor._undoSaves.Count;
        var keyed=source.Children.OfType<CHR0EntryNode>().Where(t=>t.KeyArrays.Any(c=>c.KeyCount>0)&&model._linker.BoneCache.Any(b=>b.Name==t.Name)).Select(t=>t.Name).ToArray();
        var scope=SimpleControl<CheckBox>(window,"transition_existingTracksOnly");scope.Checked=true;
        var settings=window.ReadOptions();
        Check(settings.ExistingTracksOnly&&settings.Bones.All(n=>keyed.Contains(n)),"Authentic Next fitting freezes its mask to actual keyed bones in the current target animation");
        var controls=SimpleControl<CheckBox>(window,"transition_matchReferenceControls");var controlNames=(string[])Field(window,"referenceControls");
        if(controlNames.Any(n=>!keyed.Contains(n)))Check(!controls.Enabled&&!controls.Checked&&SimpleControl<Label>(window,"transitionExistingTrackHint").Text.Contains("Unkeyed reference controls"),"Scoped Next fitting explains omitted optional controls and never silently adds their tracks");
        await(Task)Call(window,"PrepareFit");
        if(window.Candidate==null)
        {
            string report=SimpleControl<TextBox>(window,"transitionReport").Text;
            Check(report.Contains("Existing animated bones only")&&model._linker.BoneCache.Any(b=>!keyed.Contains(b.Name)&&report.Contains(b.Name)),"An unkeyed mandatory recovery dependency is named and blocks scoped Next fitting");
            await CaptureWindow(window,Path.Combine(output,"existing-track-next-dependency.png"));
        }
        else
        {
            var fitted=window.Candidate;
            Check(fitted.Children.Select(t=>t.Name).SequenceEqual(source.Children.Select(t=>t.Name)),"Scoped authentic Next preparation preserves exact native track membership and order");
            Check(RecoveryRetainedKeysExact(source,fitted,settings.EntrySamples-1,source.FrameCount-settings.ExitSamples),"Scoped authentic Next fitting retains every authored middle/join key, tangent and flag");
            var protectedNames=(string[])Field(window,"protectedMotion");
            if(settings.MatchReferenceControls)protectedNames=protectedNames.Except(controlNames,StringComparer.Ordinal).ToArray();
            Check(RecoveryKeys(source,protectedNames)==RecoveryKeys(fitted,protectedNames),"Scoped authentic Next fitting retains complete source-authored protected-control curves");
            foreach(string side in new[]{"entry","exit"})
            {
                string key=side=="entry"?settings.Entry:settings.Exit;if(key==null)continue;
                int at=side=="entry"?0:source.FrameCount-1;var reference=SimpleControl<ComboBox>(window,"transition_"+side).Items.OfType<TransitionClipChoice>().Single(c=>c.Key==key).Clip;
                Check(model._linker.BoneCache.Where(b=>settings.Bones.Contains(b.Name)&&!protectedNames.Contains(b.Name)).All(b=>RecoverySamePose(RecoverySample(fitted,b,at),RecoverySample(reference,b,0))),"Scoped authentic Next "+side+" reaches the frozen first reference pose on eligible body bones");
            }
            await CaptureWindow(window,Path.Combine(output,"existing-track-next-first.png"));SimpleControl<TrackBar>(window,"transitionFrame").Value=source.FrameCount;
            await CaptureWindow(window,Path.Combine(output,"existing-track-next-last.png"));
        }
        Check(SetNativeContent(source)==before&&form.FighterEditor.CurrentFrame==frame&&form.FighterEditor._undoSaves.Count==undo,"Scoped Next analysis preserves the live animation, authoring playhead and native Undo");
        window.RestoreOptions(json.Deserialize<TransitionFitOptions>(original));
    }
}
