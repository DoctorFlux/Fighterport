using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        internal Action<ReviewedContactsDialog> ReviewedContactsReady;
        internal Action ReviewedContactsBeforeCommit;
        private NativeReviewedContacts ReviewedContactsFor(FighterAsset asset,CHR0Node clip)=>ReferenceEquals(clip.RootNode,clip)?asset.ReviewedContacts:
            asset.ClipReviewedContacts.SingleOrDefault(c=>c.Entry==WorkspaceNativeIdentity(clip))?.Recipe;
        private void StoreReviewedContacts(FighterAsset asset,CHR0Node clip,NativeReviewedContacts recipe)
        {
            if(ReferenceEquals(clip.RootNode,clip)){asset.ReviewedContacts=recipe;return;}
            string entry=WorkspaceNativeIdentity(clip);asset.ClipReviewedContacts.RemoveAll(c=>c.Entry==entry);
            if(recipe!=null)asset.ClipReviewedContacts.Add(new NativeClipReviewedContacts{Entry=entry,Recipe=recipe});
        }
        private bool ReviewedContactBusy()=>_controlBusy||_controlSetupQueued||_retargetBusy||_workspaceBusy||_pinGesture!=null||_curvePending||modelEditControl1.AwaitingRedoSave;
        private async void OpenReviewedContacts(){try{await ShowReviewedContacts();}catch(Exception error){if(!IsDisposed)_fighterStatus.Text="Foot contacts: "+error.Message;}}
        private async Task ShowReviewedContacts()
        {
            if(ReviewedContactBusy())throw new InvalidOperationException("Finish the current native edit first.");
            var model=modelEditControl1.TargetModel as MDL0Node??throw new InvalidOperationException("Open a target model.");
            RequireSavedAnimationExportTarget(model);
            var source=modelEditControl1.SelectedCHR0??throw new InvalidOperationException("Select the current target animation.");
            var asset=SelectedFighterAsset()??throw new InvalidOperationException("Import the current animation into this project first.");
            if(modelEditControl1.SelectedSHP0!=null)throw new NotSupportedException("Reviewed contacts require static native model geometry; active shape animation needs its complete geometry adapter.");
            var semantics=ExportNativeSemanticLimitations(model,source);
            if(semantics.Length>0)throw new NotSupportedException("Reviewed contact correction needs qualified native transform semantics: "+string.Join("; ",semantics));
            modelEditControl1.StopAnim();await LoadControls();
            Control invoker=this;while(invoker is ContainerControl container&&container.ActiveControl!=null)invoker=container.ActiveControl;
            if(ReferenceEquals(invoker,this))invoker=null;
            var project=_fighterProject;var json=new JavaScriptSerializer{MaxJsonLength=32*1024*1024,RecursionLimit=200};
            string key=ControlKey(),liveState=ControlState(key),modelHash=NativeControlHash(model,".mdl0"),motionHash=NativeControlHash(source,".chr0");
            var previous=ReviewedContactsFor(asset,source);
            var intervals=new List<TransitionProtected>();
            TransitionProtected fittingMiddle=null;
            if(TransitionRecipeFor(asset,source) is NativeTransitionFit fit)
            {
                fit.Validate();var options=json.Deserialize<TransitionFitOptions>(fit.Options);intervals.AddRange(options.Protected??new TransitionProtected[0]);
                fittingMiddle=new TransitionProtected{Start=options.Entry==null?0:options.EntrySamples-1,End=options.Exit==null?source.FrameCount-1:source.FrameCount-options.ExitSamples,Reason="Retained fitted action middle"};
            }
            string dependencies=json.Serialize(new{asset.Retarget,asset.ContactBake,Previous=previous,Fits=asset.TransitionRecipes().ToArray()});
            string originalState=liveState;
            string nativeSupport=null;
            var support=asset.Retarget?.OriginalSupport;
            if(support?.Json!=null)
            {
                support.Validate();
                bool knownResult=motionHash==support.InitialResultHash||motionHash==TransitionRecipeFor(asset,source)?.InitialResultSha256||motionHash==previous?.InitialResultHash;
                if(knownResult&&support.TargetRig==BrawlRetargeter.SkeletonFingerprint(model)&&support.SourceRig==asset.Retarget.SourceRig&&support.Frames==source.FrameCount)
                    nativeSupport=json.Serialize(support);
            }
            void Current()
            {
                if(IsDisposed||_fighterProject!=project||!ReferenceEquals(modelEditControl1.TargetModel,model)||!ReferenceEquals(modelEditControl1.SelectedCHR0,source)||
                    ReviewedContactBusy()||modelEditControl1.SelectedSHP0!=null||ControlState(key)!=liveState||NativeControlHash(model,".mdl0")!=modelHash||NativeControlHash(source,".chr0")!=motionHash||
                    json.Serialize(new{asset.Retarget,asset.ContactBake,Previous=ReviewedContactsFor(asset,source),Fits=asset.TransitionRecipes().ToArray()})!=dependencies)
                    throw new InvalidOperationException("The animation, rig, fitting references or contact setup changed. Reopen contact review.");
            }
            string stage=Path.Combine(_controlStage,"reviewed-contact-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
            var roots=new List<ResourceNode>();AnimationControlClient client=null;
            using(var preview=NativeAnimationOptimization.CopySource(source))
            try
            {
                string modelPath=Path.Combine(stage,"model.mdl0"),masterPath=Path.Combine(stage,"master.chr0");model.Export(modelPath);preview.ExportChecked(masterPath);
                if(previous!=null)
                {
                    previous.Validate();
                    if(previous.ModelHash!=modelHash||previous.InitialResultHash!=motionHash)
                        throw new InvalidOperationException("This animation was edited after contact correction. Review its existing constraints before replacing the saved contact recipe.");
                    File.WriteAllBytes(masterPath,Convert.FromBase64String(previous.BaseSnapshot));originalState=previous.OriginalState;
                }
                var baseBytes=File.ReadAllBytes(masterPath);string baseHash=FighterProject.HashFile(masterPath);
                if(baseBytes.Length>8*1024*1024)throw new InvalidOperationException("The full-quality contact baseline exceeds the current review budget.");
                var copies=new Dictionary<ResourceNode,string>();
                ResourceNode Copy(ResourceNode root,string name)
                {
                    if(!copies.TryGetValue(root,out string file))
                    {
                        string ext=Path.GetExtension(root.FilePath);if(string.IsNullOrEmpty(ext))ext=".brres";file=Path.Combine(stage,name+ext);
                        NativeFittingPreviewSnapshot.WriteRawContext(root,file,model,modelPath);copies.Add(root,file);
                    }
                    var result=NativeFittingPreviewSnapshot.OpenContext(root,file,model,modelPath);roots.Add(result);return result;
                }
                IEnumerable<ResourceNode> WalkContact(ResourceNode root){yield return root;foreach(var child in root.Children)foreach(var node in WalkContact(child))yield return node;}
                ResourceNode beforeRoot=Copy(model.RootNode,"original"),afterRoot=Copy(model.RootNode,"result");
                MDL0Node Match(ResourceNode root)=>WalkContact(root).OfType<MDL0Node>().Single(m=>NativeControlHash(m,".mdl0")==modelHash);
                var references=modelEditControl1.ModelPanel._resourceList.Select(r=>r.RootNode).Distinct().Where(r=>r!=model.RootNode&&NativeFittingPreviewSnapshot.HasTextureDependencies(r)).Select((r,i)=>Copy(r,"visual-reference-"+i)).ToArray();
                var controls=await LoadControls();
                string[] feet=controls.Chains.Where(c=>c.Reviewed&&c.Role.StartsWith("Foot.",StringComparison.Ordinal)).Select(c=>c.Role).ToArray();
                if(feet.Length==0)throw new InvalidOperationException("Review the target's existing foot IK chains before contact correction.");
                CHR0Node prepared=null;string preparedKey=null,preparedState=null;NativeReviewedContacts metadata=null;
                using(var window=new ReviewedContactsDialog(beforeRoot,Match(beforeRoot),afterRoot,Match(afterRoot),preview,feet,modelEditControl1.ModelPanel.CurrentViewport,references))
                {
                    var coordinates=ReviewedFighterCoordinates(model);
                    window.SourceView.Up=window.TargetView.Up=coordinates?.Up??new[]{0f,1f,0f};
                    window.SourceView.Forward=window.TargetView.Forward=coordinates?.Forward??new[]{0f,0f,1f};
                    window.CancelWork=()=>{client?.Dispose();client=null;};
                    window.Suggest=async floor=>
                    {
                        Current();if(client==null)client=new AnimationControlClient();var session=client;
                        var reply=await session.Send(new{Operation="contact-set-suggest",Model=modelPath,Motion=masterPath,State=originalState,
                            ContactSurface=new{SurfaceMode="Ground",SourceFloor=floor,TargetFloor=floor,ExplicitSurface=true},NativeSupportJson=nativeSupport,ReviewedProtectedRanges=intervals.ToArray(),ReviewedFittingMiddle=fittingMiddle,
                            ContactProtection=nativeSupport==null?NativeTransitionContacts.Resolve(model,asset,true):new string[0]});
                        Current();if(window.IsDisposed||client!=session)throw new OperationCanceledException();return reply;
                    };
                    window.Prepare=async(context,edits,earlier)=>
                    {
                        Current();if(client==null)client=new AnimationControlClient();var session=client;
                        var reply=await session.Send(new{Operation="contact-set-prepare",Model=modelPath,Motion=masterPath,State=originalState,ReviewedContactsJson=context,ReviewedRangeEdits=edits,NativeSupportJson=nativeSupport,ReviewedCorrectionStart=earlier});
                        Current();if(window.IsDisposed||client!=session)throw new OperationCanceledException();
                        var candidate=NodeFactory.FromFile(null,masterPath) as CHR0Node??throw new InvalidDataException("The frozen contact baseline could not reopen.");
                        try
                        {
                            PreciseCHR0EntryNode.Promote(candidate);
                            foreach(var track in reply.Tracks??new ControlTrack[0])
                            {
                                if(!CanEditBodyBone(track.Bone))throw new InvalidDataException("Contact correction attempted to change a protected motion control.");
                                var entry=candidate.Children.OfType<CHR0EntryNode>().SingleOrDefault(e=>e.Name==track.Bone)??candidate.CreateEntry(track.Bone);
                                var old=entry.KeyArrays.Select(a=>a.Entries.Select(k=>new ControlKey{Frame=k._index,Value=k._value,Tangent=k._tangent}).ToArray()).ToArray();
                                bool Changed(int group)=>json.Serialize(old.Skip(group*3).Take(3).ToArray())!=json.Serialize(track.Channels.Skip(group*3).Take(3).ToArray());
                                bool rotate=Changed(1),translate=Changed(2);NativeControlSnapshot.Write(entry,track.Channels);
                                if(rotate)entry.UseModelRotate=false;if(translate)entry.UseModelTranslate=false;
                            }
                            candidate.Name=source.Name;candidate.SignalPropertyChange();string file=Path.Combine(stage,"prepared-"+Guid.NewGuid().ToString("N")+".chr0");candidate.Export(file);
                            using(var fresh=NodeFactory.FromFile(null,file) as CHR0Node)
                                if(fresh==null||NativeAnimationOptimization.SourceContentKey(fresh)!=NativeAnimationOptimization.SourceContentKey(candidate)||NativeAnimationOptimization.StorageKey(fresh)!=NativeAnimationOptimization.StorageKey(candidate))
                                    throw new InvalidDataException("Contact keys, flags or native storage changed during fresh reopen.");
                            var proof=json.Deserialize<Dictionary<string,object>>(reply.Receipt);
                            await session.Send(new{Operation="contact-set-verify",Model=modelPath,Motion=file,Path=masterPath,State=originalState,ReviewedContactsJson=reply.ContactContextJson,ReviewedContactProof=proof,NativeSupportJson=nativeSupport});
                            Current();if(window.IsDisposed||client!=session)throw new OperationCanceledException();
                            metadata=new NativeReviewedContacts{ModelHash=modelHash,BaseHash=baseHash,BaseSnapshot=Convert.ToBase64String(baseBytes),OriginalState=originalState,
                                Context=reply.ContactContextJson,Receipt=reply.Receipt,InitialResultHash=NativeControlHash(candidate,".chr0")};metadata.Validate();
                            prepared=candidate;preparedState=reply.State;preparedKey=json.Serialize(new{Edits=edits,Earlier=earlier});
                            window.PreparedReport="Contact result freshly reopened and verified. Apply changes this animation with one Undo action."+(earlier.HasValue?" Reviewed earlier body range begins at frame "+(earlier.Value+1)+"; its first pose and roots stay exact.":"")+"\r\n"+reply.Receipt+"\r\n"+string.Join("\r\n",reply.Diagnostics??new string[0]);
                            return candidate;
                        }
                        catch{candidate.Dispose();throw;}
                    };
                    window.Apply=candidate=>
                    {
                        Current();if(!ReferenceEquals(candidate,prepared)||preparedKey!=json.Serialize(new{Edits=window.ReadEdits(),Earlier=window.EarlierCorrectionStart}))throw new InvalidOperationException("Contact ranges changed. Preview again.");
                        metadata.Validate();long retained=project.Animations.SelectMany(a=>a.ReviewedContactRecipes()).Where(r=>!ReferenceEquals(r,previous)).Sum(r=>(long)r.BaseSnapshot.Length+r.Context.Length+r.OriginalState.Length+r.Receipt.Length);
                        if(retained+metadata.BaseSnapshot.Length+metadata.Context.Length+metadata.OriginalState.Length+metadata.Receipt.Length>32L*1024*1024)throw new InvalidOperationException("Complete contact references exceed this project's review budget.");
                        var names=source.Children.Concat(candidate.Children).Select(c=>c.Name).Distinct(StringComparer.Ordinal).ToArray();
                        var before=NativeControlSnapshot.Capture(source,names);var after=NativeControlSnapshot.Capture(candidate,names);
                        bool projectDirty=_fighterDirty;string title=Text;
                        var controlsBefore=project.AnimationControls.ToArray();
                        var entryDirty=source.Children.ToDictionary(n=>n.Name,n=>n.HasChanged,StringComparer.Ordinal);
                        var ownerDirty=new Dictionary<ResourceNode,bool>();for(ResourceNode owner=source;owner!=null;owner=owner.Parent)ownerDirty[owner]=owner.HasChanged;
                        void Restore(bool corrected)
                        {
                            bool restoring=_restoringCurve;_restoringCurve=true;
                            try
                            {
                                (corrected?after:before).Restore(source);PreciseCHR0EntryNode.Promote(source);StoreReviewedContacts(asset,source,corrected?metadata:previous);
                                project.AnimationControls.RemoveAll(s=>s.Key==key);string state=corrected?preparedState:liveState;if(state!=null)StoreControls(key,state);
                                modelEditControl1.UpdateModel();modelEditControl1.RefreshControlCurveTarget();modelEditControl1.InterpolationEditor?.RootChanged();modelEditControl1.KeyframePanel?.UpdateKeyframes();
                                _curveBefore=_curveTarget?.Parent is CHR0Node active?NativeControlSnapshot.Capture(active,new[]{_curveTarget.Name}):null;
                                InvalidateControls();MarkFighterChanged();
                            }
                            finally{_restoringCurve=restoring;}
                        }
                        try{Restore(true);ReviewedContactsBeforeCommit?.Invoke();metadata.InitialResultHash=NativeControlHash(source,".chr0");}
                        catch
                        {
                            Restore(false);
                            project.AnimationControls.Clear();project.AnimationControls.AddRange(controlsBefore);
                            foreach(var entry in source.Children)if(entryDirty.TryGetValue(entry.Name,out bool changed))entry.HasChanged=changed;
                            foreach(var owner in ownerDirty)owner.Key.HasChanged=owner.Value;
                            _fighterDirty=projectDirty;Text=title;UpdateFighterStatus();throw;
                        }
                        modelEditControl1.RecordEditorCommand(()=>Restore(false),()=>Restore(true));modelEditControl1.ModelPanel.Invalidate();UpdateFighterStatus();
                    };
                    window.Shown+=(s,e)=>ReviewedContactsReady?.Invoke(window);window.ShowDialog(this);
                }
            }
            finally
            {
                client?.Dispose();foreach(var root in roots)root.Dispose();
                string full=Path.GetFullPath(stage);if(Path.GetDirectoryName(full)==Path.GetFullPath(_controlStage)&&Path.GetFileName(full).StartsWith("reviewed-contact-",StringComparison.Ordinal))
                    try{Directory.Delete(full,true);}catch(IOException){}
                if(!IsDisposed){if(invoker!=null&&!invoker.IsDisposed&&invoker.Visible&&invoker.Enabled&&invoker.CanFocus)invoker.Focus();else modelEditControl1.ModelPanel.Focus();}
            }
        }
    }
}
