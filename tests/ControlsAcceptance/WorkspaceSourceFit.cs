using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    static async Task SourceFitSelect(ModelForm form,string id)
    {
        var item=((AnimationLibrary)Field(form,"_library")).Read().Clips.Single(c=>c.Id==id);
        SimpleControl<TextBox>(form,"librarySearch").Text=item.SourceCharacter+" "+item.Name;
        ((Timer)Field(form,"_librarySearchTimer")).Stop();Call(form,"RefreshLibrary",id);
        await GameLibraryWait(()=>!(bool)Field(form,"_libraryReplacingTree")&&!((Timer)Field(form,"_librarySearchTimer")).Enabled&&!((Timer)Field(form,"_libraryPopulateTimer")).Enabled,"Filtered disc character outline did not settle",90);
        var tree=SimpleControl<TreeView>(form,"libraryClips");
        tree.SelectedNode=AllNodes(tree.Nodes).Single(n=>(n.Tag as LibrarySearchItem)?.Clip.Id==id);
        Check(tree.SelectedNode.Parent.Level==1,"Disc clips sit directly under their character, without a model/costume tree level");
    }

    static async Task SourceFitPixels(ModelForm form,string path)
    {
        var view=(ModelPanel)Field(form,"_libraryViewport");
        bool polygons=view.RenderPolygons,bones=view.RenderBones,floor=view.RenderFloor;
        try
        {
            view.RenderFloor=false;view.RenderBones=false;view.RenderPolygons=false;
            view.Capture();view.Invalidate();view.Update();
            using(var empty=view.GetScreenshot(view.ClientRectangle,false))
            {
                view.RenderPolygons=true;view.Capture();view.Invalidate();view.Update();
                using(var model=view.GetScreenshot(view.ClientRectangle,false))
                {
                    int changed=0;
                    for(int y=0;y<model.Height;y+=2)for(int x=0;x<model.Width;x+=2)
                        if(model.GetPixel(x,y).ToArgb()!=empty.GetPixel(x,y).ToArgb())changed++;
                    Check(view.Width>150&&view.Height>=150&&changed>120,"Small source viewport actually renders model geometry, independently of its lease/label and floor");
                    model.Save(path);
                }
            }
        }
        finally{view.RenderPolygons=polygons;view.RenderBones=bones;view.RenderFloor=floor;view.Invalidate();}
        await CaptureCheckpoint();
    }

    static async Task WorkspaceSourceFit(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Source/fit workflow requires a private desktop.");
        string Arg(string name)=>args[Array.IndexOf(args,name)+1];
        string directory=Path.Combine(output,"library");Directory.CreateDirectory(directory);
        File.Copy(Arg("--library-index"),Path.Combine(directory,"catalogue.xml"));
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",directory);
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_PREFERENCES",Path.Combine(output,"preferences"));
        var original=new[]{args[0],args[1],Arg("--target-motion")}.Distinct().ToDictionary(p=>p,FighterProject.HashFile);
        using(var startup=MainForm.StartViewerApplication())
        {
            var form=(ModelForm)Field(MainForm.Instance,"_fighterForm");
            try
            {
                form.ClientSize=new Size(1700,950);await SimpleControlsReady(form);
                Call(form,"ImportFighterModel",args[0]);Call(form,"ImportFighterAnimations",new[]{new[]{Arg("--target-motion")}});await SimpleControlsReady(form);
                Call(form,"UseSharedSourceLibrary");await LibraryQuerySettled(form);
                var editor=form.FighterEditor;var library=(AnimationLibrary)Field(form,"_library");
                string character=Arg("--source-character"),move=Arg("--clip");
                var item=library.Browse(character+" "+move).Single(i=>i.Clip.SourceCharacter==character&&i.Clip.Name==move&&i.State==LibrarySourceState.Available);
                await SourceFitSelect(form,item.Clip.Id);
                var target=(MDL0Node)editor.TargetModel;string targetBytes=BrawlLibrarySource.ResourceHash(target);
                Check(SimpleControl<Button>(form,"libraryPreviewOriginal").Text=="Load as source"&&SimpleControl<Button>(form,"libraryOpenTarget").Text=="Open as target",
                    "Pinned actions explicitly choose the small source or editable main target destination");
                var sourceView=(ModelPanel)Field(form,"_libraryViewport");
                // Reproduce a source camera left in an incompatible projection/
                // zoom before load. Exact native data must still appear on load.
                sourceView.CurrentViewport.SetProjectionType(BrawlLib.OpenGL.ViewportProjection.Orthographic);
                sourceView.Camera.Set(new BrawlLib.Internal.Vector3(100000),new BrawlLib.Internal.Vector3(90),new BrawlLib.Internal.Vector3(10000));
                SimpleControl<Button>(form,"libraryPreviewOriginal").PerformClick();
                var source=(LibrarySourceLease)Field(form,"_libraryLease");
                Check(source.Reference.ClipId==item.Clip.Id&&!sourceView.Camera.Orthographic&&ReferenceEquals(editor.TargetModel,target),"Load as source frames the exact original in its own initialized camera without replacing the main target");
                await SourceFitPixels(form,Path.Combine(output,"source-geometry-first.png"));
                int opens=AnimationLibrary.DiagnosticOpens,bakes=BrawlCrate.Studio.Retargeting.BrawlRetargeter.DiagnosticBakes,discOpens=BrawlDiscImage.DiagnosticOpens;
                SimpleControl<TrackBar>(form,"libraryOriginalFrame").Value=2;
                SimpleControl<Button>(form,"libraryOriginalPlay").PerformClick();await Task.Delay(120);
                if(((Timer)Field(form,"_libraryTimer")).Enabled)SimpleControl<Button>(form,"libraryOriginalPlay").PerformClick();
                Check(AnimationLibrary.DiagnosticOpens==opens&&BrawlCrate.Studio.Retargeting.BrawlRetargeter.DiagnosticBakes==bakes&&BrawlDiscImage.DiagnosticOpens==discOpens,"Source scrubbing/playback performs no native source loading, disc reads or retarget baking");
                var costume=library.GetCostumes(item.Clip.Id).FirstOrDefault(c=>c.Available&&!c.IsCanonical);
                if(costume!=null)
                {
                    Call(form,"SelectLibraryCostume",costume.ModelId);
                    var next=library.Browse(character+" Wait1").Single(i=>i.Clip.SourceCharacter==character&&i.Clip.Name=="Wait1"&&i.State==LibrarySourceState.Available);
                    await SourceFitSelect(form,next.Clip.Id);
                    Check(SimpleControl<ComboBox>(form,"libraryCostume").SelectedItem is LibraryCostumeOption chosen&&chosen.ModelId==costume.ModelId,"A character's exact compatible costume remains selected when switching animations");
                    SimpleControl<Button>(form,"libraryPreviewOriginal").PerformClick();
                    Check(((LibrarySourceLease)Field(form,"_libraryLease")).Reference.ModelId==costume.ModelId,"Source preview loads the selected exact costume");
                }
                foreach(var layout in new[]{new{Width=1700,Height=950,Text=100},new{Width=1700,Height=950,Text=175},new{Width=1100,Height=800,Text=175},new{Width=1100,Height=800,Text=100}})
                {
                    Call(form,"SetWorkspaceTextSize",layout.Text);form.ClientSize=new Size(layout.Width,layout.Height);await Task.Delay(80);
                    SimpleControl<Button>(form,"libraryPreviewOriginal").PerformClick();
                    string label=layout.Width+"-"+layout.Text;
                    await SourceFitPixels(form,Path.Combine(output,"source-geometry-"+label+".png"));
                    var host=SimpleControl<TableLayoutPanel>(form,"librarySourcePreviewHost");
                    foreach(string name in new[]{"libraryPreviewOriginal","libraryOpenTarget","libraryUseOnTarget","sourceFitTarget"})
                    {var button=SimpleControl<Button>(form,name);Check(host.ClientRectangle.Contains(host.RectangleToClient(button.RectangleToScreen(button.ClientRectangle))),"Source/load/fit actions stay visible beside the source at "+label);}
                    await CaptureSimpleWorkspace(form,Path.Combine(output,"source-destinations-"+label+".png"));
                }
                Call(form,"SetWorkspaceTextSize",100);form.ClientSize=new Size(1700,950);
                await SourceFitSelect(form,item.Clip.Id);
                SimpleControl<Button>(form,"libraryPreviewOriginal").PerformClick();
                var displayed=(LibrarySourceLease)Field(form,"_libraryLease");
            UseModelSelectMode(form, BrawlCrate.Studio.Retargeting.RetargetKeyframeMode.Dense);
                bool transferred=false;
                using(OnRetargetReady(form,dialog=>transferred=true))SimpleControl<Button>(form,"libraryUseOnTarget").PerformClick();
                await SimpleControlsReady(form);
                var asset=form.FighterProject.Animations.Last();var clip=editor.SelectedCHR0;
                Check(transferred&&asset.Retarget.Library.ClipId==displayed.Reference.ClipId&&asset.Retarget.Library.ModelId==displayed.Reference.ModelId&&ReferenceEquals(Field(form,"_libraryLease"),displayed),"Actual Use on This Model retargets the exact clip and costume shown in the small source preview and keeps it visible");
                var reference=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Single(c=>c.Name=="Wait1"&&c.RootNode!=clip.RootNode);
                editor.SetFrame(7);editor.SelectedBone=target._linker.BoneCache.Single(b=>b.Name=="HipN");
                await SimpleControlsReady(form);
                string before=SetNativeContent(clip),key=(string)Call(form,"ControlKey"),provenance=new JavaScriptSerializer().Serialize(asset.Retarget);
                var setup=await Await<ControlReply>(Call(form,"LoadControls"));
                string[] roots=setup.Roles.Where(r=>r.Reviewed&&new[]{"SceneRoot","RootMotion","RootOrientation","BodyOrientation"}.Contains(r.Role)).Select(r=>setup.BoneNames[r.Bone.Value]).Distinct().ToArray();
                string rootBefore=RecoveryKeys(clip,roots);int count=form.FighterProject.Animations.Count,frame=editor.CurrentFrame,history=editor._undoSaves.Count;var bone=editor.SelectedBone;
                Exception failure=null;bool finished=false;string candidate=null;
                form.TransitionFitReady=async window=>
                {
                    try
                    {
                        Check(!window.Controls.Find("transition_saveAsNew",true).Any()&&window.ReadOptions().Name==clip.Name,"Retargeted single-clip fitting defaults to the current exact animation identity/name");
                        foreach(string side in new[]{"entry","exit"})
                        {var combo=SimpleControl<ComboBox>(window,"transition_"+side);combo.SelectedItem=combo.Items.OfType<TransitionClipChoice>().Single(c=>ReferenceEquals(c.Clip,reference));}
                        SimpleControl<CheckBox>(window,"transition_contacts").Checked=true;
                        SimpleControl<CheckBox>(window,"transition_boundaryContactRecovery").Checked=false;
                        SimpleControl<CheckBox>(window,"transition_matchReferenceControls").Checked=false;
                        var contacts=NativeTransitionContacts.Resolve(target,asset,true);
                        var fitBone=target._linker.BoneCache.First(b=>(bool)Call(form,"CanEditBodyBone",b.Name)&&!contacts.Contains(b.Name)&&
                            clip.Children.OfType<CHR0EntryNode>().Any(e=>e.Name==b.Name&&e.KeyArrays.Skip(3).Take(3).Any(c=>c.KeyCount>0)));
                        var mask=SimpleControl<CheckedListBox>(window,"transitionBones");for(int i=0;i<mask.Items.Count;i++)mask.SetItemChecked(i,Convert.ToString(mask.Items[i])==fitBone.Name);
                        SimpleControl<CheckBox>(window,"transition_binding").Checked=true;
                        SimpleControl<Button>(window,"prepareTransition").PerformClick();await Wait(()=>!window.Busy);
                        Check(window.Candidate!=null,"Prepare both boundaries for current-clip application: "+SimpleControl<TextBox>(window,"transitionReport").Text);
                        candidate=SetNativeContent(window.Candidate);
                        // A changed reference must reject even after review.
                        var entry=reference.Children.OfType<CHR0EntryNode>().First(e=>e.KeyArrays[3].KeyCount>0);var sample=entry.KeyArrays[3].Entries.First();float value=sample._value;
                        sample._value+=1;reference.SignalPropertyChange();SimpleControl<Button>(window,"applyTransition").PerformClick();
                        Check(window.Candidate==null&&SetNativeContent(clip)==before&&asset.TransitionFit==null,"Stale current-clip Apply publishes no curves or recipe");
                        sample._value=value;reference.SignalPropertyChange();
                        SimpleControl<Button>(window,"prepareTransition").PerformClick();await Wait(()=>!window.Busy);Check(window.Candidate!=null,"Reprepare after restoring the exact native reference");candidate=SetNativeContent(window.Candidate);
                        await CaptureWindow(window,Path.Combine(output,"fit-current-review.png"));
                        SimpleControl<Button>(window,"applyTransition").PerformClick();
                    }
                    catch(Exception error){failure=error;window.Close();}
                    finally{finished=true;}
                };
                Call(form,"ShowTransitionFit");await Wait(()=>finished);if(failure!=null)throw failure;await SimpleControlsReady(form);
                string fitted=SetNativeContent(clip);
                Check(fitted==candidate&&fitted!=before&&form.FighterProject.Animations.Count==count&&ReferenceEquals(editor.SelectedCHR0,clip)&&ReferenceEquals(editor.TargetModel,target)&&ReferenceEquals(editor.SelectedBone,bone)&&editor.CurrentFrame==frame,
                    "Apply writes the prepared fit to the exact current clip without adding/selecting a duplicate, rebuilding the target or moving its playhead/bone selection");
                Check(asset.TransitionFit!=null&&new JavaScriptSerializer().Serialize(asset.Retarget)==provenance&&RecoveryKeys(clip,roots)==rootBefore,"Applied current fit retains portable recipe, original Next provenance and exact protected source-local controls");
                Check(editor._undoSaves.Count==history+1,"Applying both windows creates one command on the existing native Undo stack");
                editor.Undo();await SimpleControlsReady(form);Check(SetNativeContent(clip)==before&&asset.TransitionFit==null,"Native Undo restores exact pre-fit curves and recipe");
                editor.Redo();await SimpleControlsReady(form);Check(SetNativeContent(clip)==fitted&&asset.TransitionFit!=null,"Native Redo restores exact fitted curves and recipe without preparing again");
                await ReviewBodyEdit(form,2);editor.Undo();await SimpleControlsReady(form);Check(SetNativeContent(clip)==fitted,"A later native body edit undoes back to the fitted animation");
                string export=Path.Combine(output,"fit-before-save.chr0");form.ExportSimpleWorkspaceResult(export);
                using(var decoded=NodeFactory.FromFile(null,export) as CHR0Node)Check(NativeDifference(clip,decoded)==null,"Verified export before project Save contains the newly applied complete native fit");
                string save=Path.Combine(output,"fit-current.fpstudio");form.SaveFighterProject(save);
                using(var reopened=FighterProject.Open(save))
                {
                    var owned=reopened.Animations.Single(a=>a.Id==asset.Id);
                    File.WriteAllText(Path.Combine(output,"retarget-before.json"),provenance);File.WriteAllText(Path.Combine(output,"retarget-reopened.json"),new JavaScriptSerializer().Serialize(owned.Retarget));
                    using(var decoded=NodeFactory.FromFile(null,owned.WorkingPath) as CHR0Node)Check(NativeDifference(clip,decoded)==null,"Save/reopen retains every authored native key, tangent, semantic flag and timing field of the applied fit");
                    var serializer=new System.Xml.Serialization.XmlSerializer(typeof(RetargetProvenance));RetargetProvenance normalized;
                    using(var text=new StringWriter()){serializer.Serialize(text,asset.Retarget);using(var input=new StringReader(text.ToString()))normalized=(RetargetProvenance)serializer.Deserialize(input);}
                    Check(owned.TransitionFit!=null&&owned.TransitionFit.Options==asset.TransitionFit.Options&&owned.TransitionFit.Inputs.Select(i=>i.Sha256).SequenceEqual(asset.TransitionFit.Inputs.Select(i=>i.Sha256))&&new JavaScriptSerializer().Serialize(owned.Retarget)==new JavaScriptSerializer().Serialize(normalized),"Save/reopen keeps the fit on the same exact animation ID with its full recipe and original Next provenance, including normal XML collection defaults");
                }
                string committed=FighterProject.HashFile(save);int savedUndo=editor._undoSaves.Count;bool saveFailed=false;
                using(var locked=new FileStream(save,FileMode.Open,FileAccess.Read,FileShare.Read))
                    try{form.SaveFighterProject(save);}catch(IOException){saveFailed=true;}
                Check(saveFailed&&FighterProject.HashFile(save)==committed&&SetNativeContent(clip)==fitted&&editor._undoSaves.Count==savedUndo&&asset.TransitionFit!=null,"Failed project publication preserves the committed destination, applied current fit, recipe and native Undo");
                Check(BrawlLibrarySource.ResourceHash(target)==targetBytes&&original.All(p=>FighterProject.HashFile(p.Key)==p.Value),"Source/fit/save workflow preserves authentic target binds/weights and all supplied original files");
                await CaptureSimpleWorkspace(form,Path.Combine(output,"fit-current-main.png"));
            }
            finally{SimpleDisposeForm(form);}
        }
    }
}
