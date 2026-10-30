using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    static async Task ReviewedContactsWorkflow(string modelPath,string motionPath,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Reviewed contact acceptance requires the private desktop.");
        var originalFiles=new[]{modelPath,motionPath}.ToDictionary(p=>p,FighterProject.HashFile);
        using(var project=FighterProject.Create("Reviewed contact current-animation qualification"))using(var form=new ModelForm())
        {
            form.InitializeFighterProject(project);form.Show();form.ClientSize=new Size(1700,950);
            Call(form,"ImportFighterModel",modelPath);Call(form,"ImportFighterAnimations",new[]{new[]{motionPath}});await SimpleControlsReady(form);
            var editor=form.FighterEditor;var model=(MDL0Node)editor.TargetModel;
            var reference=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Single(c=>c.Name=="Wait1");
            var rig=BrawlRetargeter.Capture(model);var pose=BrawlRetargeter.Sample(rig,reference,0);
            // Explicitly authored fixture on authentic native geometry. This is
            // not presented as the original character's full Wait1 motion.
            string fixture=Path.Combine(output,"procedural-fixed-first-pose.chr0");
            using(var clip=new CHR0Node{Name="Procedural first-pose contact fixture",Version=4,FrameCount=24,Loop=false})
            {
                for(int i=0;i<rig.Bones.Length;i++)
                {
                    var entry=clip.CreateEntry(rig.Bones[i].Name);var state=RecoverySample(reference,rig.Bones[i],0);
                    for(int c=0;c<9;c++)entry.SetKeyframe(c,0,state[c],true,true)._tangent=0;
                    var original=reference.Children.OfType<CHR0EntryNode>().SingleOrDefault(e=>e.Name==entry.Name);
                    if(original!=null){entry.ScaleCompensateApply=original.ScaleCompensateApply;entry.ScaleCompensateParent=original.ScaleCompensateParent;entry.ClassicScaleOff=original.ClassicScaleOff;}
                }
                clip.Export(fixture);
            }
            Call(form,"ImportFighterAnimations",new[]{new[]{fixture}});await SimpleControlsReady(form);
            var source=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Single(c=>c.Name=="Procedural first-pose contact fixture");
            editor.leftPanel.listAnims.SelectedItems.Clear();foreach(ListViewItem row in editor.leftPanel.listAnims.Items)row.Selected=ReferenceEquals(row.Tag,source);
            editor.SetFrame(7);await SimpleControlsReady(form);var setup=await Await<ControlReply>(Call(form,"LoadControls"));
            var feet=setup.Chains.Where(c=>c.Reviewed&&c.Role.StartsWith("Foot.",StringComparison.Ordinal)).ToArray();Check(feet.Length==2,"Authentic target has two reviewed existing native foot chains");
            var motionRole=setup.Roles.Single(r=>r.Reviewed&&r.Role=="RootMotion"&&r.Bone.HasValue);
            var motionEntry=source.Children.OfType<CHR0EntryNode>().Single(e=>e.Name==setup.BoneNames[motionRole.Bone.Value]);float origin=motionEntry.GetFrameValue(8,0);
            motionEntry.SetKeyframe(8,0,origin,true,true)._tangent=.001f;motionEntry.SetKeyframe(8,23,origin+.023f,true,true)._tangent=.001f;source.SignalPropertyChange();
            Check(motionEntry.GetFrameValue(8,23)!=origin,"Procedural acceptance includes genuine source-authored native root travel");
            form.SaveFighterProject(Path.Combine(output,"before-contact.fpstudio"));await SimpleControlsReady(form);
            Check(!(bool)Field(form,"_fighterDirty")&&!source.IsDirty,"Failed-Apply qualification starts from an actually saved clean native/project document");
            string before=SetNativeContent(source),referenceBefore=SetNativeContent(reference),modelBefore=(string)Call(form,"NativeControlHash",model,".mdl0");
            string camera=CompactCamera(form);var selectedBone=editor.SelectedBone;
            int count=project.Animations.Count,playhead=editor.CurrentFrame;
            float floor=rig.Rig.Surfaces.Where(p=>p.Key.EndsWith(".foot",StringComparison.Ordinal)).Min(p=>p.Value.Sample(pose).Clearance+rig.Rig.GroundHeight);
            Exception failure=null;bool done=false;
            form.ReviewedContactsReady=async window=>
            {
                try
                {
                    Check(window.SourceView.Model!=model&&window.TargetView.Model!=model,"Reviewed contacts preview independent native model owners");
                    SimpleControl<NumericUpDown>(window,"reviewedContactFloor").Value=(decimal)floor;
                    SimpleControl<CheckBox>(window,"reviewedContactPlane").Checked=true;
                    SimpleControl<Button>(window,"reviewedContactSuggest").PerformClick();await Wait(()=>!window.Busy);
                    var grid=SimpleControl<DataGridView>(window,"reviewedContactRanges");
                    File.WriteAllText(Path.Combine(output,"suggestions.txt"),SimpleControl<TextBox>(window,"reviewedContactReport").Text);
                    Check(!SimpleControl<Button>(window,"reviewedContactApply").Enabled,"Unapproved suggestions cannot Apply");
                    while(grid.Rows.Count>0){grid.ClearSelection();grid.Rows[0].Selected=true;SimpleControl<Button>(window,"reviewedContactRemove").PerformClick();}
                    foreach(var chain in feet)
                    {
                        SimpleControl<ComboBox>(window,"reviewedContactLimb").SelectedItem=chain.Role;SimpleControl<Button>(window,"reviewedContactAdd").PerformClick();
                        var row=grid.Rows[grid.Rows.Count-1];row.Cells["First"].Value=7;row.Cells["Last"].Value=11;row.Cells["Blend"].Value=3;row.Cells["Anchor"].Value=7;row.Cells["X"].Value=.002;row.Cells["Reviewed"].Value=true;
                    }
                    grid.EndEdit();SimpleControl<Button>(window,"reviewedContactPreview").PerformClick();await Wait(()=>!window.Busy);
                    string report=window.PreparedReport??SimpleControl<TextBox>(window,"reviewedContactReport").Text;File.WriteAllText(Path.Combine(output,"prepared-report.txt"),report);
                    Check(window.Candidate!=null,"Reviewed simultaneous full-foot correction prepares and fresh-verifies a native CHR0: "+report);
                    Check(window.EarlierCorrectionStart==null,"Earlier body correction is never silently approved by default");
                    SimpleControl<Button>(window,"reviewedContactAdvanced").PerformClick();
                    SimpleControl<CheckBox>(window,"reviewedContactEarlier").Checked=true;
                    Check(window.Candidate==null&&!SimpleControl<Button>(window,"reviewedContactApply").Enabled,"Changing the reviewed body scope invalidates the prepared candidate before Apply");
                    SimpleControl<CheckBox>(window,"reviewedContactEarlier").Checked=false;
                    SimpleControl<Button>(window,"reviewedContactAdvanced").PerformClick();
                    SimpleControl<Button>(window,"reviewedContactPreview").PerformClick();await Wait(()=>!window.Busy);
                    Check(window.Candidate!=null,"Restoring the original scope requires a fresh verified preparation");
                    Check(SetNativeContent(source)==before&&SetNativeContent(reference)==referenceBefore&&project.Animations.Count==count,"Preview preserves the exact current animation, Wait1 and project animation list");
                    SimpleControl<CheckBox>(window,"reviewedContactPlane").Focus();
                    bool navigation=true;foreach(var region in new Control[]{window.SourceView.Viewport,window.TargetView.Viewport,grid,SimpleControl<TextBox>(window,"reviewedContactReport")})
                        navigation&=(bool)Call(window,"ProcessCmdKey",default(Message),Keys.F6)&&region.ContainsFocus;
                    Check(navigation,"Application F6 routing reaches both native previews, contact rows and report without OS input injection");
                    Check((bool)Call(window,"ProcessCmdKey",default(Message),Keys.Shift|Keys.F6)&&grid.ContainsFocus,"Shift+F6 restores the preceding contact region");
                    var protectedNames=setup.ProtectedMotionBones.Select(i=>setup.BoneNames[i]).ToArray();
                    Check(RecoveryKeys(source,protectedNames)==RecoveryKeys(window.Candidate,protectedNames),"Contact correction preserves every protected source-control key, tangent, paired side and transform flag");
                    bool exterior=true;foreach(float sample in new[]{0f,.25f,.5f,2.75f,3f,13f,13.371f,17.25f,23f})
                        exterior&=model.AllBones.All(b=>RecoverySamePose(RecoverySample(source,b,sample),RecoverySample(window.Candidate,b,sample)));
                    Check(exterior,"Independent native evaluator retains original beginning and released exterior at withheld fractional times");
                    int failedCommands=editor._undoSaves.Count;bool failedDirty=(bool)Field(form,"_fighterDirty"),nativeDirty=source.IsDirty;
                    string controlsBefore=new JavaScriptSerializer{MaxJsonLength=32*1024*1024}.Serialize(project.AnimationControls);
                    form.ReviewedContactsBeforeCommit=()=>{throw new IOException("Injected reviewed contact commit failure");};
                    try{SimpleControl<Button>(window,"reviewedContactApply").PerformClick();}finally{form.ReviewedContactsBeforeCommit=null;}
                    Check(window.Candidate==null&&SetNativeContent(source)==before&&editor._undoSaves.Count==failedCommands,"A failed contact commit restores exact curves and creates no Undo command or actionable candidate");
                    Check((bool)Field(form,"_fighterDirty")==failedDirty&&source.IsDirty==nativeDirty&&new JavaScriptSerializer{MaxJsonLength=32*1024*1024}.Serialize(project.AnimationControls)==controlsBefore,
                        "A failed contact commit restores native/project dirty state and exact ordered control metadata");
                    Check(((FighterAsset)Call(form,"SelectedFighterAsset")).ReviewedContacts==null&&editor.CurrentFrame==playhead&&ReferenceEquals(editor.SelectedCHR0,source),"Failed Apply preserves clip selection/frame and leaves no contact recipe");
                    await SimpleControlsReady(form);SimpleControl<Button>(window,"reviewedContactPreview").PerformClick();await Wait(()=>!window.Busy);
                    Check(window.Candidate!=null,"A failed commit can be freshly prepared again without reopening the document");
                    foreach(var size in new[]{new Size(1050,690),new Size(1700,950)})foreach(int text in new[]{100,175})
                    {
                        Call(form,"SetWorkspaceTextSize",text);window.Font=WorkspaceAppearance.Font;window.ClientSize=size;window.PerformLayout();await CaptureWindow(window,Path.Combine(output,"reviewed-contact-"+size.Width+"-"+text+".png"));
                    }
                    Call(form,"SetWorkspaceTextSize",100);
                    SimpleControl<Button>(window,"reviewedContactApply").PerformClick();
                }
                catch(Exception error){failure=error;window.Close();}finally{done=true;}
            };
            int commands=editor._undoSaves.Count;
            await (Task)Call(form,"ShowReviewedContacts");await Wait(()=>done);if(failure!=null)throw failure;
            string after=SetNativeContent(source);
            Check(before!=after&&editor._undoSaves.Count==commands+1,"Contact Apply changes the current animation through exactly one native command");
            Check(ReferenceEquals(editor.SelectedCHR0,source)&&ReferenceEquals(editor.TargetModel,model)&&editor.CurrentFrame==playhead&&project.Animations.Count==count,"Contact Apply preserves clip/model identity, frame and sibling ownership");
            Check(CompactCamera(form)==camera&&ReferenceEquals(editor.SelectedBone,selectedBone),"Current contact Apply preserves camera and native bone selection");
            Check((string)Call(form,"NativeControlHash",model,".mdl0")==modelBefore&&SetNativeContent(reference)==referenceBefore,"Contact Apply preserves native rig, binds, all skin weights and original Wait1");
            var asset=(FighterAsset)Call(form,"SelectedFighterAsset");Check(asset.ReviewedContacts!=null,"Current standalone native animation owns its complete frozen contact recipe");
            ReviewedNativeContactOracle(model,source,asset.ReviewedContacts,setup,output,"corrected");
            editor.Undo();Check(SetNativeContent(source)==before&&asset.ReviewedContacts==null,"One Undo restores exact original content and removes only the new contact recipe");
            editor.Redo();Check(SetNativeContent(source)==after&&asset.ReviewedContacts!=null,"One Redo restores exact corrected content and complete contact evidence");
            string saved=Path.Combine(output,"reviewed-contact.fpstudio");project.Save(saved);
            using(var reopened=FighterProject.Open(saved))Check(reopened.Animations.Single(a=>a.Id==asset.Id).ReviewedContacts?.Context==asset.ReviewedContacts.Context,"Project Save/reopen retains full contact patch, support and phase evidence");
            await SimpleControlsReady(form);
            object frozen=Call(form,"FreezeWorkspaceExport");
            try
            {
                await (Task)Call(form,"PrepareOptimizedExportSnapshot",frozen,new NativeExportOptimizationSettings{Policy=NativeExportOptimizationPolicy.ErrorBounded},CancellationToken.None,null);
                string optimized=Path.Combine(output,"contact-verified-export.chr0");Call(form,"ExportWorkspaceSnapshot",frozen,optimized);
                string exportReport=(string)Field(frozen,"OptimizationReport");File.WriteAllText(Path.Combine(output,"contact-export-report.txt"),exportReport);
                Check(exportReport.Contains("Decoded export independently rechecked")&&(string)Field(frozen,"EffectiveExportPolicy")=="Exact","Explicit optimized copy retains Exact policy and freshly rechecks complete absolute contact acceptance");
                using(var decoded=NodeFactory.FromFile(null,optimized) as CHR0Node)ReviewedNativeContactOracle(model,decoded,asset.ReviewedContacts,setup,output,"decoded");
                Check(SetNativeContent(source)==after&&editor._undoSaves.Count==commands+1,"Verified export preserves corrected editable master and native Undo");
            }
            finally{((IDisposable)frozen).Dispose();}
            done=false;form.ReviewedContactsReady=window=>{
                SimpleControl<CheckBox>(window,"reviewedContactPlane").Checked=true;SimpleControl<Button>(window,"reviewedContactSuggest").PerformClick();
                Check(window.Busy,"Reopened contact Suggest uses the existing asynchronous busy guard");SimpleControl<Button>(window,"reviewedContactCancel").PerformClick();
                Check(!window.Busy&&window.Candidate==null&&!SimpleControl<Button>(window,"reviewedContactApply").Enabled,"Cancelling busy contact preparation leaves no actionable candidate");
                window.Close();done=true;};await (Task)Call(form,"ShowReviewedContacts");await Wait(()=>done);
            Check(SetNativeContent(source)==after&&asset.ReviewedContacts!=null&&editor._undoSaves.Count==commands+1,"Closing reopened contact review preserves applied curves, full recipe and Undo without another command");
            foreach(var file in originalFiles)Check(FighterProject.HashFile(file.Key)==file.Value,"Reviewed contact workflow preserves private original input bytes");
        }
    }
}
