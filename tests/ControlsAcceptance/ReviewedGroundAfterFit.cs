using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    static async Task ReviewedGroundAfterFit(ModelForm form,CHR0Node current,CHR0Node wait,string output)
    {
        var editor=form.FighterEditor;var model=(MDL0Node)editor.TargetModel;
        var setup=await Await<ControlReply>(Call(form,"LoadControls"));
        var feet=setup.Chains.Where(c=>c.Reviewed&&c.Role.StartsWith("Foot.",StringComparison.Ordinal)).ToArray();
        var asset=(FighterAsset)Call(form,"SelectedFighterAsset");
        Check(feet.Length==2&&asset.Retarget?.OriginalSupport?.Json!=null,"Ground recovery has complete original support evidence and both reviewed existing foot chains");
        var rig=BrawlRetargeter.Capture(model);var firstWait=BrawlRetargeter.Sample(rig,wait,0);
        // Freeze from the original own Wait1, before contact preparation. Never
        // tune the floor to make an output pass.
        float floor=(float)Math.Round(rig.Rig.Surfaces.Where(p=>p.Key.EndsWith(".foot",StringComparison.Ordinal)).Min(p=>p.Value.Sample(firstWait).Clearance+rig.Rig.GroundHeight),4);
        var json=new JavaScriptSerializer{MaxJsonLength=32*1024*1024,RecursionLimit=100};
        File.WriteAllText(Path.Combine(output,"frozen-ground.json"),json.Serialize(new{Floor=floor,Unit="native units",Reference="Exact target Wait1 first frame, full reviewed native foot geometry; rounded once to the height control's four decimal places",Model=(string)Call(form,"NativeControlHash",model,".mdl0"),SourceSupport=asset.Retarget.OriginalSupport.Sha256}));
        string before=SetNativeContent(current),waitBefore=SetNativeContent(wait);int commands=editor._undoSaves.Count;
        var ends=model.AllBones.Select(b=>new[]{RecoverySample(current,b,0),RecoverySample(current,b,current.FrameCount-1)}).ToArray();
        string[] protectedNames=setup.ProtectedMotionBones.Select(i=>setup.BoneNames[i]).ToArray();string protectedKeys=RecoveryKeys(current,protectedNames);
        Exception failure=null;bool done=false;
        form.ReviewedContactsReady=async window=>
        {
            try
            {
                SimpleControl<NumericUpDown>(window,"reviewedContactFloor").Value=(decimal)floor;SimpleControl<CheckBox>(window,"reviewedContactPlane").Checked=true;
                SimpleControl<Button>(window,"reviewedContactSuggest").PerformClick();await Wait(()=>!window.Busy);
                var grid=SimpleControl<DataGridView>(window,"reviewedContactRanges");
                File.WriteAllText(Path.Combine(output,"contact-suggestions.txt"),SimpleControl<TextBox>(window,"reviewedContactReport").Text);
                while(grid.Rows.Count>0){grid.ClearSelection();grid.Rows[0].Selected=true;SimpleControl<Button>(window,"reviewedContactRemove").PerformClick();}
                // Explicit assisted review, limited to the fitted recovery.
                // The action middle and frozen Wait1 endpoint stay outside.
                foreach(var chain in feet)
                {
                    SimpleControl<ComboBox>(window,"reviewedContactLimb").SelectedItem=chain.Role;SimpleControl<Button>(window,"reviewedContactAdd").PerformClick();
                    // Release reaches the existing frozen final native key.
                    // A release one sample earlier would split sparse fitted
                    // spans and cannot promise bit-exact exterior evaluation.
                    var row=grid.Rows[grid.Rows.Count-1];row.Cells["First"].Value=current.FrameCount-7;row.Cells["Last"].Value=current.FrameCount-2;row.Cells["Blend"].Value=2;row.Cells["Anchor"].Value=current.FrameCount;row.Cells["Reviewed"].Value=true;
                }
                var approach=BrawlRetargeter.Sample(rig,current,current.FrameCount-10);
                bool earlierReview=rig.Rig.Surfaces.Where(p=>p.Key.EndsWith(".foot",StringComparison.Ordinal)).Any(p=>p.Value.Sample(approach).Clearance+rig.Rig.GroundHeight<floor);
                if(earlierReview)
                {
                    // Explicit assisted review authorized for the conflicting
                    // body range. This is a fixture decision from actual foot
                    // geometry, never an algorithmic character/action exception.
                    SimpleControl<Button>(window,"reviewedContactAdvanced").PerformClick();
                    SimpleControl<NumericUpDown>(window,"reviewedContactCorrectionFirst").Value=1;
                    SimpleControl<CheckBox>(window,"reviewedContactEarlier").Checked=true;
                }
                File.WriteAllText(Path.Combine(output,"earlier-body-review.json"),json.Serialize(new{Reviewed=earlierReview,FirstDisplayFrame=earlierReview?(int?)1:null,Reason="Independent original foot geometry at contact entry; first pose and all source-owned motion controls remain exact"}));
                grid.EndEdit();
                // Local immutable replay inputs allow the bounded offline
                // contact stage to be diagnosed without repeating retargeting
                // or interacting with an active desktop. Private assets stay
                // under the ignored acceptance output.
                string replayModel=Path.Combine(output,"contact-master.mdl0"),replayClip=Path.Combine(output,"contact-master.chr0");
                model.Export(replayModel);using(var copy=NativeAnimationOptimization.CopySource(current))copy.ExportChecked(replayClip);
                File.WriteAllText(Path.Combine(output,"contact-prepare-request.json"),json.Serialize(new{Operation="contact-set-prepare",Model=replayModel,Motion=replayClip,
                    State=(string)Call(form,"ControlState",(string)Call(form,"ControlKey")),ReviewedContactsJson=(string)Field(window,"context"),ReviewedRangeEdits=window.ReadEdits(),ReviewedCorrectionStart=window.EarlierCorrectionStart,NativeSupportJson=json.Serialize(asset.Retarget.OriginalSupport)}));
                SimpleControl<Button>(window,"reviewedContactPreview").PerformClick();await GameLibraryWait(()=>!window.Busy,"Reviewed native contact preparation did not finish within its bounded offline test window.",240);
                string report=window.PreparedReport??SimpleControl<TextBox>(window,"reviewedContactReport").Text;File.WriteAllText(Path.Combine(output,"contact-report.txt"),report);
                Check(window.Candidate!=null,"Reviewed simultaneous recovery contacts freshly prepare and reopen: "+report);
                Check(SetNativeContent(current)==before&&SetNativeContent(wait)==waitBefore,"Contact preparation preserves exact fitted master and own Wait1");
                SimpleControl<TrackBar>(window,"reviewedContactFrame").Value=current.FrameCount-7;
                foreach(var size in new[]{new System.Drawing.Size(1050,690),new System.Drawing.Size(1700,950)})foreach(int text in new[]{100,175})
                {
                    Call(form,"SetWorkspaceTextSize",text);window.Font=WorkspaceAppearance.Font;window.ClientSize=size;window.PerformLayout();
                    await CaptureWindow(window,Path.Combine(output,"recovery-contact-"+size.Width+"-"+text+".png"));
                }
                Call(form,"SetWorkspaceTextSize",100);
                using(var copy=NativeAnimationOptimization.CopySource(window.Candidate))copy.ExportChecked(Path.Combine(output,"03-contact-corrected.chr0"));
                SimpleControl<Button>(window,"reviewedContactApply").PerformClick();
            }
            catch(Exception error){failure=error;window.Close();}finally{done=true;}
        };
        await (Task)Call(form,"ShowReviewedContacts");await Wait(()=>done);form.ReviewedContactsReady=null;if(failure!=null)throw failure;
        string after=SetNativeContent(current);Check(editor._undoSaves.Count==commands+1&&after!=before,"Reviewed recovery contact Apply is exactly one native Undo command");
        Check(RecoveryKeys(current,protectedNames)==protectedKeys,"Reviewed recovery keeps every attack-owned protected-control key, tangent and flag exact");
        Check(model.AllBones.Select((b,i)=>RecoverySamePose(RecoverySample(current,b,0),ends[i][0])&&RecoverySamePose(RecoverySample(current,b,current.FrameCount-1),ends[i][1])).All(v=>v),"Contact correction retains original lying/action beginning and the frozen own-Wait1 final pose");
        editor.Undo();Check(SetNativeContent(current)==before,"Authentic fitted recovery contact Undo restores exact finished master");editor.Redo();Check(SetNativeContent(current)==after,"Authentic fitted recovery contact Redo restores exact corrected master");await SimpleControlsReady(form);
        ReviewedNativeContactOracle(model,current,asset.ReviewedContacts,setup,output,"corrected");
        object frozen=Call(form,"FreezeWorkspaceExport");
        try
        {
            await (Task)Call(form,"PrepareOptimizedExportSnapshot",frozen,new NativeExportOptimizationSettings{Policy=NativeExportOptimizationPolicy.ErrorBounded},CancellationToken.None,null);
            Call(form,"ExportWorkspaceSnapshot",frozen,Path.Combine(output,"04-verified-export.chr0"));
            using(var decoded=NodeFactory.FromFile(null,Path.Combine(output,"04-verified-export.chr0")) as CHR0Node)ReviewedNativeContactOracle(model,decoded,asset.ReviewedContacts,setup,output,"decoded");
            File.WriteAllText(Path.Combine(output,"contact-export-report.txt"),(string)Field(frozen,"OptimizationReport"));
            Check(((string)Field(frozen,"OptimizationReport")).Contains("Decoded export independently rechecked"),"Decoded optimized copy rechecks absolute contact acceptance against the finished corrected master");
        }
        finally{((IDisposable)frozen).Dispose();}
        form.FighterProject.Save(Path.Combine(output,"reviewed-ground-recovery.fpstudio"));
    }
}
