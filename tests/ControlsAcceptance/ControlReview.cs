using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static async Task ControlReview(string modelPath,string motionPath,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Review acceptance requires the private desktop.");
        string sourceModel=FighterProject.HashFile(modelPath),sourceMotion=FighterProject.HashFile(motionPath);
        var project=FighterProject.Create("Reviewed existing rig");var form=new ModelForm();form.InitializeFighterProject(project);form.Show();Call(form,"ImportFighterModel",modelPath);Call(form,"ImportFighterAnimations",new[]{new[]{motionPath}});
        var editor=form.FighterEditor;var clip=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().FirstOrDefault(c=>c.Name=="Wait1")??editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().First();foreach(ListViewItem item in editor.leftPanel.listAnims.Items)item.Selected=ReferenceEquals(item.Tag,clip);editor.SetFrame(1);editor.FrameFighterModel();await Task.Delay(150);await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        var loaded=await Await<ControlReply>(Call(form,"LoadControls"));var model=(MDL0Node)editor.TargetModel;var roleBindings=loaded.Roles.Where(r=>r.Bone.HasValue).ToDictionary(r=>r.Role,r=>loaded.BoneNames[r.Bone.Value]);
        foreach(var c in loaded.Chains){string side=c.Role.Substring(c.Role.LastIndexOf('.')+1);var names=c.Role.StartsWith("Hand")?new[]{"UpperArm.","Elbow.","Wrist."}:new[]{"UpperLeg.","Knee.","Ankle."};var bones=new[]{c.Root,c.Middle,c.Effector};for(int i=0;i<3;i++)roleBindings[names[i]+side]=loaded.BoneNames[bones[i]];}
        project.RigReviews.Add(new EngineRigReview{Fingerprint=EngineRigReview.FingerprintOf(model),Roles=roleBindings.Keys.ToArray(),Bones=roleBindings.Values.ToArray(),NativeRoles=new string[0],NativeBones=new string[0],Right=new[]{1f,0,0},Up=new[]{0f,1,0},Forward=new[]{0f,0,1},Evidence="Original acceptance fixture: explicit native role and perpendicular-axis review"});
        loaded=await Await<ControlReply>(Call(form,"LoadControls"));Check(loaded.Chains.Length>0&&loaded.Chains.All(c=>c.Reviewed),"Existing coordinate/role review loads valid direct native IK chains");var chain=loaded.Chains.First();string before=loaded.State,key=(string)Call(form,"ControlKey"),modelHash=(string)Call(form,"NativeControlHash",model,".mdl0"),clipHash=(string)Call(form,"NativeControlHash",clip,".chr0");
        var client=(AnimationControlClient)Field(form,"_controlEngine");bool rejected=false;
        try{await client.Send(new{Operation="review-controls",Review=new{Role=chain.Role,Bones=new[]{chain.Effector,chain.Middle,chain.Root},Pole=new[]{0f,1,0},Roles=new Dictionary<string,int?>(),Evidence="Independent invalid hierarchy case"}});}catch(InvalidOperationException){rejected=true;}
        Check(rejected&&(await client.Send(new{Operation="cancel"})).State==before,"Invalid reversed chain rejects atomically without changing saved setup");
        rejected=false;
        try{await client.Send(new{Operation="review-controls",Review=new{Role=chain.Role,Bones=new[]{chain.Root,chain.Middle,chain.Effector},Pole=new[]{0f,1,0},Roles=new Dictionary<string,int?>{{"Hips",chain.Effector}},Evidence="Independent atomic chain-plus-invalid-body review"}});}catch(InvalidOperationException){rejected=true;}
        Check(rejected&&(await client.Send(new{Operation="cancel"})).State==before,"A valid chain followed by an invalid shared-body binding rejects the entire review atomically");
        form.ControlReviewReady=dialog=>{dialog.DialogResult=DialogResult.Cancel;dialog.Close();};await(Task)Call(form,"ReviewControlSetup");Check((string)Call(form,"ControlState",key)==before,"Cancel in the actual review window preserves the exact control document");
        Task capture=null;form.ControlReviewReady=dialog=>
        {
            var role=(ComboBox)dialog.Controls.Find("reviewIkRole",true).Single();role.SelectedItem=chain.Role;
            ((NumericUpDown)dialog.Controls.Find("reviewIkPole0",true).Single()).Value=0;((NumericUpDown)dialog.Controls.Find("reviewIkPole1",true).Single()).Value=1;((NumericUpDown)dialog.Controls.Find("reviewIkPole2",true).Single()).Value=0;
            var tabs=dialog.Controls.OfType<TabControl>().Single();tabs.SelectedIndex=1;
            var table=(DataGridView)dialog.Controls.Find("reviewSharedBones",true).Single();var head=table.Rows.Cast<DataGridViewRow>().Single(r=>(string)r.Cells[0].Value=="Head");head.Cells[1].Value=-1;head.Cells["Confirm"].Value=true;
            ((TextBox)dialog.Controls.Find("reviewNewRole",true).Single()).Text="Custom.Editor.Role";((Button)dialog.Controls.Find("reviewAddRole",true).Single()).PerformClick();table.Rows.Cast<DataGridViewRow>().Single(r=>(string)r.Cells[0].Value=="Custom.Editor.Role").Cells[1].Value=0;
            tabs.SelectedIndex=0;capture=CaptureReviewTabs(dialog,output);dialog.BeginInvoke(new Action(async()=>{await capture;((Button)dialog.Controls.Find("reviewSave",true).Single()).PerformClick();}));
        };
        await(Task)Call(form,"ReviewControlSetup");if(capture!=null)await capture;
        string after=(string)Call(form,"ControlState",key);var reviewed=await Await<ControlReply>(Call(form,"LoadControls"));
        Check(after!=before&&reviewed.Chains.Single(c=>c.Role==chain.Role).Pole.SequenceEqual(new[]{0f,1,0}),"Actual chain/pole review persists after reload with the same coordinate review still present");
        Check(reviewed.Roles.Any(r=>r.Role=="Head"&&r.Bone==null&&r.Reviewed),"Shared bone review preserves an explicit unassignment rather than guessing it again");
        Check(reviewed.Roles.Any(r=>r.Role=="Custom.Editor.Role"&&r.Bone==0&&r.Reviewed),"The native review window adds a custom shared role on an existing native bone");
        var coordinateReview=project.RigReviews.Single();Array.Reverse(coordinateReview.Roles);Array.Reverse(coordinateReview.Bones);var reordered=await Await<ControlReply>(Call(form,"LoadControls"));Check(reordered.State==after,"Reordering stored coordinate bindings preserves the exact reviewed setup and pole");
        Check((string)Call(form,"NativeControlHash",model,".mdl0")==modelHash&&(string)Call(form,"NativeControlHash",clip,".chr0")==clipHash,"Review changes only editor metadata; native model and animation bytes remain identical");
        editor.Undo();await Wait(()=>!(bool)Field(form,"_controlBusy"));var undone=await Await<ControlReply>(Call(form,"LoadControls"));Check(undone.State==before,"One native Undo restores the exact setup, pole and shared role state");
        editor.Redo();await Wait(()=>!(bool)Field(form,"_controlBusy"));reviewed=await Await<ControlReply>(Call(form,"LoadControls"));Check(reviewed.State==after,"Native Redo restores the saved review without recomputing mappings");
        before=reviewed.State;form.ControlReviewReady=dialog=>{((CheckBox)dialog.Controls.Find("reviewApplyIk",true).Single()).Checked=true;clip.Loop=!clip.Loop;((Button)dialog.Controls.Find("reviewSave",true).Single()).PerformClick();};bool stale=false;
        try{await(Task)Call(form,"ReviewControlSetup");}catch(InvalidOperationException e){stale=e.Message.Contains("stale");}finally{clip.Loop=!clip.Loop;}
        Check(stale&&(string)Call(form,"ControlState",key)==before,"Changed native animation rejects stale review publication without overwriting saved controls");
        var pending=(Task<ControlReply>)Call(form,"LoadControls");clip.Loop=!clip.Loop;stale=false;try{await pending;}catch(InvalidOperationException e){stale=e.Message.Contains("stale");}finally{clip.Loop=!clip.Loop;}
        Check(stale&&(string)Call(form,"ControlState",key)==before,"Native content edited while setup is loading rejects asynchronous publication");
        pending=(Task<ControlReply>)Call(form,"LoadControls");editor.SetFrame(2);stale=false;try{await pending;}catch(InvalidOperationException e){stale=e.Message.Contains("stale");}finally{editor.SetFrame(1);}
        Check(stale&&(string)Call(form,"ControlState",key)==before,"Changing the preview frame while setup is loading rejects stale pose and pin publication");
        string path=Path.Combine(output,"reviewed.fpstudio");form.SaveFighterProject(path);var fresh=FighterProject.Open(path);var second=new ModelForm();second.InitializeFighterProject(fresh);second.Show();await Task.Delay(150);await Wait(()=>!(bool)Field(second,"_controlBusy")&&!(bool)Field(second,"_controlSetupQueued"));var reopened=await Await<ControlReply>(Call(second,"LoadControls"));
        Check(reopened.Chains.Single(c=>c.Role==chain.Role).Pole.SequenceEqual(new[]{0f,1,0})&&reopened.Roles.Any(r=>r.Role=="Head"&&r.Bone==null),"Fresh project viewer restores the exact reviewed pole and explicit shared-bone unassignment");
        Check(FighterProject.HashFile(modelPath)==sourceModel&&FighterProject.HashFile(motionPath)==sourceMotion,"All control reviews preserve supplied source model and animation bytes");
        second.FighterEditor.FrameFighterModel();await CaptureWindow(second,Path.Combine(output,"reviewed-project-viewer.png"));
        foreach(var f in new[]{second,form}){((AnimationControlClient)Field(f,"_controlEngine")).Dispose();f.FighterProject.Dispose();BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.Instances.Remove(f.FighterEditor);f.Dispose();}GC.Collect();GC.WaitForPendingFinalizers();
    }
    static async Task CaptureReviewTabs(ControlReviewDialog dialog,string output)
    {
        await CaptureWindow(dialog,Path.Combine(output,"review-controls.png"));
        dialog.Controls.OfType<TabControl>().Single().SelectedIndex=1;
        await CaptureWindow(dialog,Path.Combine(output,"review-shared-bones.png"));
    }
}
