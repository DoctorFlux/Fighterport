using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static async Task WorkspaceUnifiedOptimization(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Private desktop required.");
        var originals=new[]{args[0],args[1]}.ToDictionary(p=>p,FighterProject.HashFile);
        using(var project=FighterProject.Create("One optimization, native current animation"))
        using(var form=new ModelForm())
        {
            form.InitializeFighterProject(project);form.Show();form.ClientSize=new Size(1700,950);
            Call(form,"ImportFighterModel",args[0]);Call(form,"ImportFighterAnimations",new[]{new[]{args[1]}});
            await GameLibraryWait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"),"Initial native controls did not settle.",120);
            var editor=form.FighterEditor;var clip=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().First(c=>!c.Loop&&c.FrameCount>=12);
            editor.SelectedCHR0=clip;await SimpleControlsReady(form);
            var setup=await Await<ControlReply>(Call(form,"LoadControls"));var model=(MDL0Node)editor.TargetModel;
            editor.SelectedBone=model.AllBones.Single(b=>b.Name==setup.BoneNames[setup.Roles.Single(r=>r.Role=="Hips").Bone.Value]);
            editor.SetFrame(3);editor.UpdateModel();editor.FrameFighterModel();await SimpleControlsReady(form);
            var bone=editor.SelectedBone;int frame=editor.CurrentFrame,undo=editor._undoSaves.Count;
            string identity=OptimizationNativeIdentity(clip),before=SetNativeContent(clip),storage=NativeAnimationOptimization.StorageKey(clip),camera=CompactCamera(form);
            var siblings=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Where(c=>c!=clip).ToDictionary(c=>c,SetNativeContent);
            var roots=(setup.ProtectedMotionBones??new int[0]).Select(i=>setup.BoneNames[i]).ToArray();
            var witness=new GameLibraryDocument(form);
            async Task Open(Func<AnimationOptimizationDialog,Task> exercise)
            {
                bool done=false;Exception failure=null;
                form.OptimizationReady=async window=>{try{await exercise(window);}catch(Exception error){failure=error;}finally{window.Close();done=true;}};
                Call(form,"ShowAnimationOptimization");await GameLibraryWait(()=>done,"Unified optimization did not complete.",180);form.OptimizationReady=null;
                if(failure!=null)throw failure;await SimpleControlsReady(form);
            }
            await Open(async window=>
            {
                Check(!SimpleDescendants(window).Any(c=>c is ComboBox||c is CheckBox),"Current-animation optimization exposes one action without quality/storage selectors");
                Check(window.ReadHighQuality()&&window.ReadSmallFile()&&window.ReadCompactStorage(),"One action includes approved bounded reduction and compact native storage");
                var prepare=window.Prepare;var pending=new TaskCompletionSource<CHR0Node>();window.Prepare=_=>pending.Task;
                var work=window.Analyze();SimpleControl<Button>(window,"optimizationCancel").PerformClick();pending.SetResult(NativeAnimationOptimization.Copy(clip));await work;window.Prepare=prepare;
                Check(!window.Busy&&window.Candidate==null&&!window.PreparedCanApply,"Cancelled preparation discards late results without a document command");witness.CheckPreserved("unified cancellation");
            });
            string optimized=null,optimizedStorage=null;
            await Open(async window=>
            {
                await window.Analyze();Check(window.Candidate!=null,"Unified preparation returns an independent complete native candidate");
                File.WriteAllText(Path.Combine(output,"unified-report.txt"),window.PreparedReport);
                ReplayRootsMatch(clip,window.Candidate,model,roots);
                OptimizationWithheldGeometry(window.SourceView,window.TargetView,clip,window.Candidate,Path.Combine(output,"unified-withheld.json"),true);
                witness.CheckPreserved("whole-animation preparation");
                foreach(int text in new[]{100,125,150,175})foreach(var size in new[]{new Size(980,720),new Size(1600,960)})
                {
                    Call(form,"SetWorkspaceTextSize",text);window.ApplyTextSize();window.ClientSize=size;window.PerformLayout();
                    foreach(string name in new[]{"optimizeAnalyze","optimizeApply","optimizationClose","optimizationFrame","optimizationIdentity","optimizationSummary"})
                    {
                        var c=SimpleControl<Control>(window,name);
                        Check(c.Visible&&window.ClientRectangle.Contains(window.RectangleToClient(c.RectangleToScreen(c.ClientRectangle))),"Unified control fits "+name+" / "+text+" / "+size.Width);
                    }
                    await CaptureOptimization(window,Path.Combine(output,"unified-"+text+"-"+size.Width+".png"));
                }
                optimized=SetNativeContent(window.Candidate);optimizedStorage=NativeAnimationOptimization.StorageKey(window.Candidate);
                Check(window.PreparedCanApply,"Authentic imported current animation has a verified useful saving");
                SimpleControl<Button>(window,"optimizeApply").PerformClick();
            });
            Check(ReferenceEquals(editor.SelectedCHR0,clip)&&ReferenceEquals(editor.SelectedBone,bone)&&editor.CurrentFrame==frame&&OptimizationNativeIdentity(clip)==identity&&CompactCamera(form)==camera,"Unified Apply preserves exact native identity, target/bone selection, frame and camera");
            Check(SetNativeContent(clip)==optimized&&NativeAnimationOptimization.StorageKey(clip)==optimizedStorage&&editor._undoSaves.Count==undo+1,"Unified Apply creates exactly one native Undo command including storage");
            editor.Undo();await SimpleControlsReady(form);Check(SetNativeContent(clip)==before&&NativeAnimationOptimization.StorageKey(clip)==storage,"Native Undo restores original curves and storage");
            editor.Redo();await SimpleControlsReady(form);Check(SetNativeContent(clip)==optimized&&NativeAnimationOptimization.StorageKey(clip)==optimizedStorage,"Native Redo restores verified optimized curves and storage");
            await Open(async window=>{int prepares=form.OptimizationPrepares;await window.Analyze();Check(window.Candidate!=null&&!window.PreparedCanApply&&form.OptimizationPrepares==prepares,"Unchanged session optimization prevents cumulative approximation and redundant preparation");});
            string exported=Path.Combine(output,"unified-current.chr0");form.ExportSimpleWorkspaceResult(exported);
            using(var decoded=NodeFactory.FromFile(null,exported) as CHR0Node)Check(decoded!=null&&SetNativeContent(decoded)==optimized,"Verified export before project Save reopens exact optimized native content");
            form.SaveFighterProject(Path.Combine(output,"unified-current.fpstudio"));
            using(var reopened=FighterProject.Open(Path.Combine(output,"unified-current.fpstudio")))
            using(var root=NodeFactory.FromFile(null,reopened.Animations.Single(a=>a.Id==((FighterAsset)Call(form,"SelectedFighterAsset")).Id).WorkingPath))
                Check(Walk(root).OfType<CHR0Node>().Any(c=>SetNativeContent(c)==optimized),"Saved project independently reopens optimized exact member");
            Check(siblings.All(p=>SetNativeContent(p.Key)==p.Value)&&originals.All(p=>FighterProject.HashFile(p.Key)==p.Value),"Optimization, Undo, Save and export preserve siblings and immutable source/model files");
        }
    }
}
