using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
internal static partial class Program
{
    static async Task ContactAuthoringWorkflow(string modelPath,string motionPath,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Contact acceptance requires the private desktop.");var hashes=new[]{modelPath,motionPath}.ToDictionary(p=>p,FighterProject.HashFile);
        Console.WriteLine("TRACE contact project creation");var project=FighterProject.Create("Contact placement and release");Console.WriteLine("TRACE contact form creation");var form=new ModelForm();Console.WriteLine("TRACE contact project initialization");form.InitializeFighterProject(project);form.Show();Console.WriteLine("TRACE contact native import");Call(form,"ImportFighterModel",modelPath);Call(form,"ImportFighterAnimations",new[]{new[]{motionPath}});var editor=form.FighterEditor;
        var source=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().First(c=>c.Name=="Wait1");editor.leftPanel.listAnims.SelectedItems.Clear();foreach(ListViewItem row in editor.leftPanel.listAnims.Items)row.Selected=ReferenceEquals(row.Tag,source);editor.SetFrame(7);editor.FrameFighterModel();await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));var setup=await Await<ControlReply>(Call(form,"LoadControls"));var chain=setup.Chains.First(c=>c.Reviewed&&c.Role=="Hand.L");string effector=setup.BoneNames[chain.Effector];var model=(MDL0Node)editor.TargetModel;
        var upper=model._linker.BoneCache.Single(b=>b.Name==setup.BoneNames[chain.Root]).Matrix.GetPoint();var end=model._linker.BoneCache.Single(b=>b.Name==effector).Matrix.GetPoint();float[] offset={(upper._x-end._x)*.01f,(upper._y-end._y)*.01f,(upper._z-end._z)*.01f};
        string sourceHash=(string)Call(form,"NativeControlHash",source,".chr0"),modelHash=(string)Call(form,"NativeControlHash",model,".mdl0");Exception failure=null;bool done=false;int count=project.Animations.Count;
        // Opening/scrubbing/cancelling contact preview must not rebuild the
        // selected authoring animation or any containing native owner. Compare
        // raw buffers independently from decoded live curves and storage plans;
        // no assumption is made that resource aliases share the same buffer.
        var previewOwners=editor._openedFiles.Concat(new ResourceNode[]{source,source.RootNode,model.RootNode}).Distinct().ToArray();
        var previewGuard=ContactCapturePreviewOwners(previewOwners);int previewFrame=editor.CurrentFrame;
        bool cancelDone=false;
        form.ContactBakeReady=window=>
        {
            try
            {
                ContactCheckPreviewOwners(previewGuard,"Open contact preview");
                Check(window.SourceView.Model!=model&&window.TargetView.Model!=model,"Opening contact preview uses independent existing-rig model owners");
                ((TrackBar)window.Controls.Find("contactFrame",true).Single()).Value=Math.Min(8,source.FrameCount);
                window.Close();
            }
            catch(Exception error){failure=error;window.Close();}
            finally{cancelDone=true;}
        };
        await (Task)Call(form,"ShowContactPlacement");await Wait(()=>cancelDone);if(failure!=null)throw failure;
        ContactCheckPreviewOwners(previewGuard,"Cancelled contact preview");
        Check(ReferenceEquals(editor.SelectedCHR0,source)&&ReferenceEquals(editor.TargetModel,model)&&editor.CurrentFrame==previewFrame,"Cancelled contact preview preserves exact native selection, target and playhead");
        Check(project.Animations.Count==count,"Opening and cancelling contact preview creates no project animation");
        form.ContactBakeReady=async window=>
        {
            try
            {
                ((ComboBox)window.Controls.Find("contactBone",true).Single()).SelectedItem=effector;((NumericUpDown)window.Controls.Find("contact_start",true).Single()).Value=7;((NumericUpDown)window.Controls.Find("contact_end",true).Single()).Value=10;
                for(int i=0;i<3;i++)((NumericUpDown)window.Controls.Find("contact_"+new[]{"x","y","z"}[i],true).Single()).Value=(decimal)offset[i];
                ((NumericUpDown)window.Controls.Find("contact_ry",true).Single()).Value=3;
                Check(window.SourceView.Model!=model&&window.TargetView.Model!=model,"Contact review uses independent native renderer models");
                ((Button)window.Controls.Find("prepareContact",true).Single()).PerformClick();await Wait(()=>!window.Busy);var report=(TextBox)window.Controls.Find("contactReport",true).Single();File.WriteAllText(Path.Combine(output,"placement-report.txt"),report.Text);Check(window.Candidate!=null,"Reviewed native contact placement prepares and fresh-verifies real CHR0 quarter frames");
                Check(project.Animations.Count==count&&!((Button)window.Controls.Find("addContact",true).Single()).Enabled,"Contact preparation cannot publish before explicit review");
                var rig=BrawlRetargeter.Capture(model);int index=Array.FindIndex(rig.Bones,b=>b.Name==effector);var anchor=BrawlRetargeter.Sample(rig,source,6).World[index].Translation+new System.Numerics.Vector3(offset[0],offset[1],offset[2]);
                var proof=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,object>>(report.Text.Substring(report.Text.IndexOf('{'),report.Text.LastIndexOf('}')-report.Text.IndexOf('{')+1));float bound=Convert.ToSingle(proof["PositionBound"]);
                Check(Enumerable.Range(0,13).All(i=>System.Numerics.Vector3.Distance(BrawlRetargeter.Sample(rig,window.Candidate,6+i*.25f).World[index].Translation,anchor)<=bound),"Independent native pose sampling confirms the placed hand stays at its reviewed world anchor");
                System.Numerics.Matrix4x4.Decompose(BrawlRetargeter.Sample(rig,source,6).World[index],out _,out var captured,out _);var expectedRotation=System.Numerics.Quaternion.CreateFromRotationMatrix(System.Numerics.Matrix4x4.CreateFromQuaternion(captured)*System.Numerics.Matrix4x4.CreateRotationY(3*(float)Math.PI/180));
                double Angle(System.Numerics.Quaternion a,System.Numerics.Quaternion b){double dot=(double)a.X*b.X+(double)a.Y*b.Y+(double)a.Z*b.Z+(double)a.W*b.W;double aa=(double)a.X*a.X+(double)a.Y*a.Y+(double)a.Z*a.Z+(double)a.W*a.W,bb=(double)b.X*b.X+(double)b.Y*b.Y+(double)b.Z*b.Z+(double)b.W*b.W;return 2*Math.Acos(Math.Min(1,Math.Abs(dot)/Math.Sqrt(aa*bb)))*180/Math.PI;}
                Check(Enumerable.Range(0,13).All(i=>{System.Numerics.Matrix4x4.Decompose(BrawlRetargeter.Sample(rig,window.Candidate,6+i*.25f).World[index],out _,out var actual,out _);return Angle(actual,expectedRotation)<=.25;}),"Independent native matrix sampling confirms the rotated contact holds its requested world orientation between keys");
                ((TrackBar)window.Controls.Find("contactFrame",true).Single()).Value=7;await CaptureWindow(window,Path.Combine(output,"contact-placement.png"));((CheckBox)window.Controls.Find("contactReviewed",true).Single()).Checked=true;((Button)window.Controls.Find("addContact",true).Single()).PerformClick();Check(project.Animations.Count==count+1,"Contact placement imports an independent animation");
            }
            catch(Exception error){failure=error;window.Close();}finally{done=true;}
        };
        await (Task)Call(form,"ShowContactPlacement");await Wait(()=>done);if(failure!=null)throw failure;await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));var placed=project.Animations.Last();Check(placed.ContactBake?.Algorithm=="reviewed-contact-bake-1","Native placed result retains the frozen original and explicit contact receipt");
        editor.Undo();Check(project.Animations.Count==count,"Contact placement has one native Undo");editor.Redo();Check(project.Animations.Count==count+1,"Contact Redo restores the same baked result without solving");await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        form.SaveFighterProject(Path.Combine(output,"placed.fpstudio"));using(var fresh=FighterProject.Open(Path.Combine(output,"placed.fpstudio")))Check(fresh.Animations.Last().ContactBake.BaseHash==placed.ContactBake.BaseHash&&fresh.AnimationControls.Any(c=>c.Json.Contains("Reviewed editor contact")),"Fresh project reopen retains the fixed contact and its exact original motion reference");
        done=false;form.ContactBakeReady=async window=>
        {
            try
            {
                ((ComboBox)window.Controls.Find("contactAction",true).Single()).SelectedItem="Release";((ComboBox)window.Controls.Find("contactBone",true).Single()).SelectedItem=effector;((NumericUpDown)window.Controls.Find("contact_start",true).Single()).Value=9;
                ((Button)window.Controls.Find("prepareContact",true).Single()).PerformClick();await Wait(()=>!window.Busy);var report=(TextBox)window.Controls.Find("contactReport",true).Single();File.WriteAllText(Path.Combine(output,"release-report.txt"),report.Text);Check(window.Candidate!=null,"Native release prepares from the saved contact and frozen original motion");
                var rig=BrawlRetargeter.Capture(model);int index=Array.FindIndex(rig.Bones,b=>b.Name==effector);Check(System.Numerics.Vector3.Distance(BrawlRetargeter.Sample(rig,window.Candidate,15).World[index].Translation,BrawlRetargeter.Sample(rig,source,15).World[index].Translation)<.001f,"Released native limb rejoins the original animation after its blend");
                ((TrackBar)window.Controls.Find("contactFrame",true).Single()).Value=16;await CaptureWindow(window,Path.Combine(output,"contact-release.png"));((CheckBox)window.Controls.Find("contactReviewed",true).Single()).Checked=true;((Button)window.Controls.Find("addContact",true).Single()).PerformClick();
            }
            catch(Exception error){failure=error;window.Close();}finally{done=true;}
        };
        await (Task)Call(form,"ShowContactPlacement");await Wait(()=>done);if(failure!=null)throw failure;await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        var original=project.Animations.First(a=>!a.Generated);int imported=(int)Call(form,"LoadGameSlotProfile",original);Check(imported>100&&project.GameSlotProfiles.Single().SourceSha256==original.SourceSha256,"Brawl motion profile derives its native slots from the exact registered game template bytes");
        Check(project.GameSlotProfiles[0].Entries.All(e=>e.Version==4||e.Version==5)&&project.AnimationSlots.All(s=>!s.Required&&s.AnimationKey==null),"Native motion profiles retain real versions and do not invent required rules or automatic bindings");
        var slot=project.AnimationSlots.Single(s=>s.Id=="Wait1");Call(form,"AssignAnimationSlot",slot.Id,(string)Call(form,"SelectedFighterAnimationKey"));Check(project.AnimationSlots.Single(s=>s.Id=="Wait1").NativeEntry==slot.NativeEntry,"An exact native game slot accepts an explicitly reviewed independent result without changing its runtime entry identity");
        string NativeKeys(CHR0Node clip)=>string.Join("|",clip.Children.OfType<CHR0EntryNode>().Select(e=>e.Name+":"+e.UseModelScale+e.UseModelRotate+e.UseModelTranslate+e.ScaleCompensateApply+e.ScaleCompensateParent+e.ClassicScaleOff+":"+string.Join(";",e.KeyArrays.Select(a=>string.Join(",",a.Entries.Select(k=>k._index+":"+k._value.ToString("R")+":"+k._tangent.ToString("R")))))));
        string slotPath=Path.Combine(output,"Wait1-slot.chr0"),slotMaster=NativeKeys(editor.SelectedCHR0);
        using(var prepared=(IDisposable)Call(form,"FreezeOwnedWorkspaceExport",Call(form,"SelectedFighterAsset"),editor.SelectedCHR0,false,null))
        {
            var expected=project.GameSlotProfiles.Single().Entries.Single(e=>e.Entry==slot.NativeEntry);
            await (Task)Call(form,"PrepareOptimizedExportSnapshot",prepared,new NativeExportOptimizationSettings(),System.Threading.CancellationToken.None,null);
            Call(form,"PrepareSlotMetadataExport",prepared,expected.Name,expected.Version);
            Call(form,"ExportGameSlot",slot.Id,slotPath);
            using(var slotFile=(CHR0Node)NodeFactory.FromFile(null,slotPath))
            {
                var candidate=(CHR0Node)Field(prepared,"Candidate");
                Check(slotFile.Name==expected.Name&&slotFile.Version==expected.Version&&NativeAnimationOptimization.SourceContentKey(slotFile)==NativeAnimationOptimization.SourceContentKey(candidate)&&
                    NativeAnimationOptimization.StorageKey(slotFile)==NativeAnimationOptimization.StorageKey(candidate)&&FighterProject.HashFile(slotPath)==(string)Field(prepared,"PreparedEncodedSha256")&&
                    new FileInfo(slotPath).Length<=(long)Field(prepared,"MasterBytes"),
                    "Native slot export fresh-reopens as its independently prepared same-slot Exact copy, with complete metadata/storage and physical no-growth verification");
                using(var first=NodeFactory.FromFile(null,modelPath))using(var second=NodeFactory.FromFile(null,modelPath))
                {
                    var beforeModel=Walk(first).OfType<MDL0Node>().Single(node=>node.Name==model.Name);var afterModel=Walk(second).OfType<MDL0Node>().Single(node=>node.Name==model.Name);
                    beforeModel.Populate();afterModel.Populate();
                    OptimizationWithheldGeometry(beforeModel,afterModel,editor.SelectedCHR0,slotFile,Path.Combine(output,"slot-export-withheld-geometry.json"));
                }
            }
        }
        Check(NativeKeys(editor.SelectedCHR0)==slotMaster,"Independent slot export preserves the assigned full-quality editable master and authored native keys");
        done=false;form.AnimationSlotsReady=async window=>{try{await CaptureWindow(window,Path.Combine(output,"game-slot-profile.png"));}catch(Exception error){failure=error;}finally{window.Close();done=true;}};Call(form,"ShowAnimationSlots");await Wait(()=>done);if(failure!=null)throw failure;
        string released=Path.Combine(output,"released.chr0");form.ExportFighterResource(released);form.SaveFighterProject(Path.Combine(output,"released-slots.fpstudio"));using(var fresh=FighterProject.Open(Path.Combine(output,"released-slots.fpstudio")))Check(fresh.GameSlotProfiles.Single().Entries.Length==project.GameSlotProfiles[0].Entries.Length&&fresh.AnimationSlots.Single(s=>s.Id=="Wait1").AnimationKey!=null,"Game slot profile and reviewed assignment survive fresh native project reopening");
        Check((string)Call(form,"NativeControlHash",source,".chr0")==sourceHash&&(string)Call(form,"NativeControlHash",model,".mdl0")==modelHash&&hashes.All(p=>FighterProject.HashFile(p.Key)==p.Value),"Contact placement, release and game profiles preserve source bytes and native deform data");
        ((AnimationControlClient)Field(form,"_controlEngine")).Dispose();project.Dispose();BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.Instances.Remove(editor);form.Dispose();GC.Collect();GC.WaitForPendingFinalizers();await ControlGizmos(modelPath,released,output);
    }
    sealed class ContactPreviewOwnerGuard
    {
        public ResourceNode Owner;
        public string Content,Storage,Backing;
        public bool Dirty;
    }
    static ContactPreviewOwnerGuard[] ContactCapturePreviewOwners(ResourceNode[] owners)
        => owners.Select(owner=>new ContactPreviewOwnerGuard{Owner=owner,Content=ContactPreviewContent(owner,false),
            Storage=ContactPreviewContent(owner,true),Backing=Chr0ForensicBackingHash(owner),Dirty=owner.IsDirty}).ToArray();
    static string ContactPreviewContent(ResourceNode owner,bool storage)
    {
        // Includes every already-owned native animation, in native traversal
        // order, so an untouched sibling cannot silently change during staging.
        string content=string.Join("|",Walk(owner).OfType<CHR0Node>().Select(clip=>WritePathIdentity(clip)+":"+
            (storage?NativeAnimationOptimization.StorageKey(clip):NativeAnimationOptimization.SourceContentKey(clip))));
        return WritePathHash(System.Text.Encoding.UTF8.GetBytes(content));
    }
    static void ContactCheckPreviewOwners(ContactPreviewOwnerGuard[] before,string phase)
    {
        Check(before.All(item=>ContactPreviewContent(item.Owner,false)==item.Content),phase+" preserves selected/source and every owned sibling native animation coefficient and metadata");
        Check(before.All(item=>ContactPreviewContent(item.Owner,true)==item.Storage),phase+" preserves every owned native animation storage plan");
        Check(before.All(item=>item.Owner.IsDirty==item.Dirty),phase+" preserves every original native root dirty state");
        Check(before.All(item=>Chr0ForensicBackingHash(item.Owner)==item.Backing),phase+" preserves original WorkingUncompressed buffers without relying on aliases");
    }
}
