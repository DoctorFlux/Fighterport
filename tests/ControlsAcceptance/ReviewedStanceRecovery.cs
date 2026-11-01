using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    static async Task ReviewedStanceRecovery(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Stance recovery acceptance requires a private desktop.");
        string Required(string flag)=>args[Array.IndexOf(args,flag)+1];
        string path=Required("--reviewed-stance-project"),fixture=Required("--fitting-reference-fixture");
        var json=new JavaScriptSerializer{MaxJsonLength=64*1024*1024,RecursionLimit=200};
        var inputs=new[]{args[0],args[1],path,fixture}.ToDictionary(p=>p,FighterProject.HashFile);
        float floor=(float)json.Deserialize<AuthenticFloatDenseFixture>(File.ReadAllText(fixture)).FittingFloorHeight.Value;
        using(var project=FighterProject.Open(path))using(var form=new ModelForm())
        {
            form.InitializeFighterProject(project);form.Show();form.ClientSize=new Size(1700,950);
            await GameLibraryWait(()=>form.FighterEditor.TargetModel is MDL0Node&&form.FighterEditor.SelectedCHR0!=null&&(project.SelectedAnimation==null||project.SelectedAnimation.EndsWith("|@"+OptimizationNativeIdentity(form.FighterEditor.SelectedCHR0),StringComparison.Ordinal)),"Fitted project did not restore its exact native member.",60);
            await SimpleControlsReady(form);
            var editor=form.FighterEditor;var clip=editor.SelectedCHR0;var model=(MDL0Node)editor.TargetModel;
            // Re-enter fitting through the real UI. The old descriptor-only
            // fixture did not flush its live curves; never weaken the exact
            // fitted-result gate to make that stale fixture pass.
            if(!args.Contains("--stance-current-fitted"))
            {
                string fitOutput=Path.Combine(output,"fit-replay");Directory.CreateDirectory(fitOutput);
                await ReviewedRecoveryCase(form,clip,Required("--target-motion"),fitOutput,false,"Wait1",fixture);
                form.SaveFighterProject(Path.Combine(output,"fresh-fitted.fpstudio"));await SimpleControlsReady(form);
            }
            var asset=(FighterAsset)Call(form,"SelectedFighterAsset");
            var fit=(NativeTransitionFit)Call(form,"TransitionRecipeFor",asset,clip);fit.Validate();
            Check(fit.InitialResultSha256==(string)Call(form,"NativeControlHash",clip,".chr0"),"Fresh actual fitting retains the exact current-result dependency gate");
            var setup=await Await<ControlReply>(Call(form,"LoadControls"));
            var exit=fit.Inputs.Single(i=>i.Role=="exit");string referenceFile=Path.Combine(output,"frozen-reference.chr0");File.WriteAllBytes(referenceFile,Convert.FromBase64String(exit.Snapshot));
            Check(FighterProject.HashFile(referenceFile)==exit.Sha256,"Recovery resolves the exact retained target first-frame reference snapshot");
            var travel=setup.Roles.Where(r=>r.Reviewed&&r.Role=="RootMotion"&&r.Bone.HasValue).Select(r=>setup.BoneNames[r.Bone.Value]).ToArray();
            using(var reference=NodeFactory.FromFile(null,referenceFile) as CHR0Node)
                Check(model.AllBones.All(b=>RecoverySamePose(RecoverySample(clip,b,clip.FrameCount-1),RecoverySample(travel.Contains(b.Name)?clip:reference,b,travel.Contains(b.Name)?clip.FrameCount-1:0))),"The saved fitted master ends in the target's frozen reference first frame under exact attack-owned travel");
            string[] protectedNames=setup.ProtectedMotionBones.Select(i=>setup.BoneNames[i]).ToArray();
            string before=SetNativeContent(clip),modelBefore=(string)Call(form,"NativeControlHash",model,".mdl0"),camera=CompactCamera(form),recipeBefore=json.Serialize(fit);
            int join=clip.FrameCount-10,commands=editor._undoSaves.Count,count=project.Animations.Count;editor.SetFrame(join+2);int playhead=editor.CurrentFrame;var bone=editor.SelectedBone;
            using(var master=NativeAnimationOptimization.CopySource(clip))
            {
                master.ExportChecked(Path.Combine(output,"01-fitted-master.chr0"));
                bool done=false;Exception failure=null;
                form.ReviewedContactsReady=async window=>
                {
                    try
                    {
                        var recover=SimpleControl<CheckBox>(window,"reviewedContactRecovery");Check(recover.Visible,"Exact frozen-first-frame fitting exposes the existing dialog's reviewed stance mode");recover.Checked=true;
                        SimpleControl<NumericUpDown>(window,"reviewedContactFloor").Value=(decimal)floor;
                        SimpleControl<CheckBox>(window,"reviewedContactPlane").Checked=true;
                        SimpleControl<Button>(window,"reviewedContactSuggest").PerformClick();await Wait(()=>!window.Busy);
                        var grid=SimpleControl<DataGridView>(window,"reviewedContactRanges");
                        File.WriteAllText(Path.Combine(output,"proposal.txt"),SimpleControl<TextBox>(window,"reviewedContactReport").Text);
                        Check(grid.Rows.Count>=2&&!SimpleControl<Button>(window,"reviewedContactPreview").Enabled,"Staged holds and steps remain unapproved until every phase is reviewed");
                        Check(!SimpleControl<Button>(window,"reviewedContactAdd").Enabled&&!SimpleControl<Button>(window,"reviewedContactRemove").Enabled&&grid.Columns["First"].ReadOnly,"A reviewed staged plan cannot silently lose a phase or stale its frozen timing");
                        foreach(DataGridViewRow row in grid.Rows)row.Cells["Reviewed"].Value=true;grid.EndEdit();
                        SimpleControl<Button>(window,"reviewedContactPreview").PerformClick();await GameLibraryWait(()=>!window.Busy,"Bounded offline contact preparation did not finish.",120);
                        string report=window.PreparedReport??SimpleControl<TextBox>(window,"reviewedContactReport").Text;File.WriteAllText(Path.Combine(output,"preparation.txt"),report);
                        Check(window.Candidate!=null,"Stance preview freshly serializes and verifies its independent native candidate: "+report);
                        Check(SetNativeContent(clip)==before&&editor._undoSaves.Count==commands,"Preparation preserves the current editable master and native Undo");
                        Check(RecoveryKeys(master,protectedNames)==RecoveryKeys(window.Candidate,protectedNames),"Every protected root/control key, tangent, flag and paired side remains exact");
                        Check(RecoveryRetainedKeysExact(master,window.Candidate,0,join),"Recovery retains every beginning/middle key through the exact join");
                        Check(new[]{0f,(float)join,(float)(clip.FrameCount-1)}.All(t=>model.AllBones.All(b=>RecoverySamePose(RecoverySample(master,b,t),RecoverySample(window.Candidate,b,t)))),"The original first pose, join and own-Wait1 first-frame endpoint remain exact");
                        if(args.Contains("--stance-layouts"))foreach(var size in new[]{new Size(1050,690),new Size(1700,950)})foreach(int text in new[]{100,175})
                        {window.Font=new Font(WorkspaceAppearance.Font.FontFamily,WorkspaceAppearance.Font.Size*text/100f);window.ClientSize=size;window.PerformLayout();await CaptureWindow(window,Path.Combine(output,"stance-"+size.Width+"-"+text+".png"));}
                        SimpleControl<Button>(window,"reviewedContactApply").PerformClick();
                    }
                    catch(Exception error){failure=error;window.Close();}finally{done=true;}
                };
                await (Task)Call(form,"ShowReviewedContacts");await Wait(()=>done);if(failure!=null)throw failure;
                string after=SetNativeContent(clip);
                Check(after!=before&&editor._undoSaves.Count==commands+1,"Reviewed stance Apply updates the same current animation through exactly one native Undo transaction");
                Check(ReferenceEquals(clip,editor.SelectedCHR0)&&ReferenceEquals(model,editor.TargetModel)&&editor.CurrentFrame==playhead&&ReferenceEquals(bone,editor.SelectedBone)&&CompactCamera(form)==camera&&project.Animations.Count==count,"Apply preserves exact target/member, bone, frame, camera and sibling ownership");
                var recipe=(NativeReviewedContacts)Call(form,"ReviewedContactsFor",asset,clip);recipe.Validate();
                Check(modelBefore==(string)Call(form,"NativeControlHash",model,".mdl0")&&json.Serialize(Call(form,"TransitionRecipeFor",asset,clip))==recipeBefore,"Recovery preserves authentic binds, every skin weight and historical fitting evidence");
                clip.ExportChecked(Path.Combine(output,"02-contact-corrected.chr0"));
                ReviewedNativeContactOracle(model,clip,recipe,setup,output,"corrected");
                editor.Undo();Check(SetNativeContent(clip)==before&&Call(form,"ReviewedContactsFor",asset,clip)==null,"One Undo restores the exact fitted master and removes only the new contact recipe");
                editor.Redo();Check(SetNativeContent(clip)==after&&Call(form,"ReviewedContactsFor",asset,clip)!=null,"One Redo restores exact corrected native content and complete reviewed phases");await SimpleControlsReady(form);
                object frozen=Call(form,"FreezeWorkspaceExport");
                try
                {
                    await (Task)Call(form,"PrepareOptimizedExportSnapshot",frozen,new NativeExportOptimizationSettings{Policy=NativeExportOptimizationPolicy.ErrorBounded},CancellationToken.None,null);
                    string exported=Path.Combine(output,"03-verified-export.chr0");Call(form,"ExportWorkspaceSnapshot",frozen,exported);
                    File.WriteAllText(Path.Combine(output,"export.txt"),(string)Field(frozen,"OptimizationReport"));
                    Check((string)Field(frozen,"EffectiveExportPolicy")=="Exact","Contact/fitting export retains the existing Exact-only gate instead of spending the contact budget again");
                    using(var decoded=NodeFactory.FromFile(null,exported) as CHR0Node)ReviewedNativeContactOracle(model,decoded,recipe,setup,output,"decoded");
                }
                finally{((IDisposable)frozen).Dispose();}
                form.SaveFighterProject(Path.Combine(output,"reviewed-stance.fpstudio"));await SimpleControlsReady(form);
                using(var reopened=FighterProject.Open(Path.Combine(output,"reviewed-stance.fpstudio")))using(var fresh=new ModelForm())
                {
                    Check(reopened.Animations.SelectMany(a=>a.ReviewedContactRecipes()).Single().Context==recipe.Context,"Project Save/reopen retains complete reviewed phase, plane, skin, anchor and protection context");
                    fresh.InitializeFighterProject(reopened);fresh.Show();
                    await GameLibraryWait(()=>fresh.FighterEditor.TargetModel is MDL0Node&&fresh.FighterEditor.SelectedCHR0!=null&&
                        (reopened.SelectedAnimation==null||reopened.SelectedAnimation.EndsWith("|@"+OptimizationNativeIdentity(fresh.FighterEditor.SelectedCHR0),StringComparison.Ordinal)),"Saved corrected project did not restore its exact animation.",60);
                    await SimpleControlsReady(fresh);
                    var freshClip=fresh.FighterEditor.SelectedCHR0;var freshModel=(MDL0Node)fresh.FighterEditor.TargetModel;
                    Check(SetNativeContent(freshClip)==after,"Actual native project reopen retains every corrected key, tangent, track, flag and metadata field");
                    var freshSetup=await Await<ControlReply>(Call(fresh,"LoadControls"));
                    ReviewedNativeContactOracle(freshModel,freshClip,recipe,freshSetup,output,"project-reopened");
                    using(var copy=NativeAnimationOptimization.CopySource(freshClip))copy.ExportChecked(Path.Combine(output,"04-project-reopened.chr0"));
                }
                Check(SetNativeContent(clip)==after&&editor._undoSaves.Count==commands+1,"Exact verified export and project Save preserve the corrected master and Undo");
                File.WriteAllText(Path.Combine(output,"bytes.json"),json.Serialize(Directory.GetFiles(output,"0*.chr0").Select(p=>new{File=Path.GetFileName(p),Bytes=new FileInfo(p).Length,Sha256=FighterProject.HashFile(p)}).ToArray()));
            }
            foreach(var input in inputs)Check(FighterProject.HashFile(input.Key)==input.Value,"Stance testing preserves private original input bytes");
        }
    }
}
