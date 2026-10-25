using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        internal Action<TransitionFitDialog> TransitionFitReady;
        private void ShowTransitionFit()
        {
            if(!(modelEditControl1.TargetModel is MDL0Node model)||modelEditControl1.SelectedCHR0==null)throw new InvalidOperationException("Open a native model and select the target animation to fit.");
            if(_controlBusy||_controlSetupQueued||_retargetBusy||_workspaceBusy||_pinGesture!=null||_curvePending||modelEditControl1.AwaitingRedoSave)throw new InvalidOperationException("Finish the current animation/control edit before fitting.");
            var source=modelEditControl1.SelectedCHR0;if(source.FrameCount<3)throw new InvalidOperationException("Boundary fitting needs at least three existing native samples.");
            modelEditControl1.StopAnim();_libraryTimer?.Stop();if(_libraryPlay!=null)_libraryPlay.Text="Play";
            var project=_fighterProject;var choices=RegisteredFighterAssets().SelectMany(a=>DiscoverFighterAnimations(FighterRoot(a)).OfType<CHR0Node>().Select(c=>new TransitionClipChoice{Asset=a,Clip=c,Key=FighterAnimationKey(a,c)})).ToArray();
            var selected=choices.SingleOrDefault(c=>c.Clip==source)??throw new InvalidOperationException("Register this native animation with the fighter project before fitting.");
            string modelHash=NativeControlHash(model,".mdl0"),sourceHash=NativeControlHash(source,".chr0"),controlKey=ControlKey(),state=ControlState(controlKey);
            bool CurrentRecoveryBinding()=>_bodyBindingReady&&ReferenceEquals(_loadedControlModel,model)&&ReferenceEquals(_loadedControlClip,source)&&_loadedControlKey==controlKey;
            var currentPins=modelEditControl1.AnimationPins??new ControlPin[0];
            string pinReview=CurrentRecoveryBinding()?(currentPins.Length==0?"No editor pins in this exact current animation control setup.":string.Join("\r\n",currentPins.Select(pin=>pin.Id+" · "+pin.BoneName+" · "+(pin.Soft?"soft contact":"fixed editor pin")+" · position ("+string.Join(", ",(pin.Position??new float[0]).Select(value=>value.ToString("G6",System.Globalization.CultureInfo.InvariantCulture)))+")"))):"Current animation controls are not loaded. Set Up Animation Controls before exit contact recovery. Saved pins are independently checked by the engine.";
            var json=new JavaScriptSerializer{MaxJsonLength=32*1024*1024,RecursionLimit=200};
            string Provenance()=>json.Serialize(new{selected.Asset.Retarget,selected.Asset.TransitionFit});
            string sourceProvenance=Provenance();
            Action current=()=>{if(IsDisposed||_fighterProject!=project||modelEditControl1.TargetModel!=model||modelEditControl1.SelectedCHR0!=source||NativeControlHash(model,".mdl0")!=modelHash||NativeControlHash(source,".chr0")!=sourceHash||ControlState(controlKey)!=state||Provenance()!=sourceProvenance)throw new InvalidOperationException("The native model, source animation, contact/recovery provenance or pin setup changed. Reopen fitting; stale output was discarded.");};
            string stage=Path.Combine(_controlStage,"transition-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
            AnimationControlClient client=null;var roots=new List<ResourceNode>();
            try
            {
                void WriteSnapshot(TransitionClipChoice choice,string path)
                {
                    // Snapshot an independent precise clone. Recompressing a live
                    // edited imported node here could change retained source keys.
                    using(var frozen=FreezeOwnedWorkspaceExport(choice.Asset,choice.Clip,false))
                    {frozen.Validate();frozen.Candidate.ExportChecked(path);frozen.Validate();}
                }
                string modelPath=Path.Combine(stage,"model.mdl0"),sourcePath=Path.Combine(stage,"source.chr0");model.Export(modelPath);WriteSnapshot(selected,sourcePath);
                ResourceNode CopyRoot(ResourceNode root,string name)
                {
                    string extension=Path.GetExtension(root.FilePath);if(string.IsNullOrEmpty(extension))extension=".brres";string path=Path.Combine(stage,name+extension);root.Export(path);var copy=NodeFactory.FromFile(null,path)??throw new InvalidDataException("Native preview resource could not be reopened.");roots.Add(copy);return copy;
                }
                IEnumerable<ResourceNode> Walk(ResourceNode root){yield return root;foreach(var c in root.Children)foreach(var n in Walk(c))yield return n;}
                ResourceNode a=CopyRoot(model.RootNode,"before-model"),b=CopyRoot(model.RootNode,"after-model");var beforeModel=Walk(a).OfType<MDL0Node>().Single(m=>NativeControlHash(m,".mdl0")==modelHash);var afterModel=Walk(b).OfType<MDL0Node>().Single(m=>NativeControlHash(m,".mdl0")==modelHash);
                var references=modelEditControl1.ModelPanel._resourceList.Select(r=>r.RootNode).Distinct().Where(r=>r!=model.RootNode).Select((r,i)=>CopyRoot(r,"reference-"+i)).ToArray();
                CHR0Node prepared=null;string preparedKey=null,copiedState=null;NativeTransitionFit metadata=null;Dictionary<string,string> referenceHashes=null;
                void VerifyReferences(){if(referenceHashes==null)throw new InvalidOperationException("Prepare the transition first.");foreach(var proof in referenceHashes){var native=choices.Single(c=>c.Key==proof.Key);if(!project.RegisteredAssets().Contains(native.Asset)||NativeControlHash(native.Clip,".chr0")!=proof.Value)throw new InvalidOperationException("A transition reference changed. Prepare the fit again.");}}
                using(var window=new TransitionFitDialog(a,beforeModel,b,afterModel,source,choices,modelEditControl1.ModelPanel.CurrentViewport,references,pinReview,NativeTransitionContacts.HasBakedSupport(selected.Asset)||currentPins.Any(pin=>pin.Soft)))
                {
                    window.CancelWork=()=>{client?.Dispose();client=null;};
                    window.Prepare=async options=>
                    {
                        current();var contactBones=NativeTransitionContacts.Resolve(model,selected.Asset,options.PreserveContacts);if(!options.BindingReviewed)throw new InvalidOperationException("Review that source/reference clips are already authored for this model's native bones, bind space and units, and review their listed pin protections. Retarget donor clips first.");
                        var recoveredContactBones=NativeTransitionContacts.ReviewExitRecovery(options,source.FrameCount,source.Loop,contactBones,_protectedMotionBones,CurrentRecoveryBinding());
                        if(string.IsNullOrWhiteSpace(options.Name)||options.Name.Length>128||options.Name.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new InvalidOperationException("Choose a plain animation name of at most 128 characters.");
                        var inputs=new List<TransitionNativeInput>();var hashes=new Dictionary<string,string>();
                        string Snapshot(TransitionClipChoice choice,string role)
                        {
                            string path=Path.Combine(stage,role+"-"+Guid.NewGuid().ToString("N")+".chr0");WriteSnapshot(choice,path);byte[] bytes=File.ReadAllBytes(path);if(bytes.Length>8*1024*1024)throw new InvalidOperationException("Use a native clip below the 8 MiB transition snapshot budget.");
                            inputs.Add(new TransitionNativeInput{Role=role,Name=choice.Clip.Name,Samples=choice.Clip.FrameCount,AssetId=choice.Asset.Id,Entry=choice.Key,Sha256=FighterProject.HashFile(path),Snapshot=Convert.ToBase64String(bytes)});hashes[choice.Key]=NativeControlHash(choice.Clip,".chr0");return path;
                        }
                        Snapshot(selected,"source");string entry=options.Entry==null?null:Snapshot(choices.Single(c=>c.Key==options.Entry),"entry"),exit=options.Exit==null?null:Snapshot(choices.Single(c=>c.Key==options.Exit),"exit");
                        if(client==null)client=new AnimationControlClient();var session=client;
                        var floorSettings=options.PreventFootDipping?new{Normal=new[]{0f,1f,0f},Height=(float)options.FloorHeight,Reviewed=options.FloorReviewed,Evidence="Explicit native Y="+options.FloorHeight.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+" plane reviewed for exit recovery; authentic source/reference penetration baseline accepted."}:null;
                        var reply=await session.Send(new{Operation="transition-prepare",Model=modelPath,Motion=sourcePath,State=state,Boundary=new{EntryMotion=entry,ExitMotion=exit,options.EntryFrame,options.ExitFrame,options.EntryFrozen,options.ExitFrozen,options.EntryRate,options.ExitRate,Settings=new{options.EntrySamples,options.ExitSamples,options.Components,options.Bones,options.Protected,options.PreserveRootTranslation,options.NearestEquivalentAngles,options.PreserveContacts,options.ExitContactRecovery,ContactBones=contactBones,FloorSettings=floorSettings}}});
                        current();if(client!=session||window.IsDisposed)throw new OperationCanceledException("Transition fitting cancelled.");
                        foreach(var proof in hashes)if(NativeControlHash(choices.Single(c=>c.Key==proof.Key).Clip,".chr0")!=proof.Value)throw new InvalidOperationException("A reference changed during preparation; stale output discarded.");
                        if(reply.Tracks.Sum(t=>t.Channels.Sum(c=>c.Length))>200000)throw new InvalidOperationException("This fit exceeds the 200000 native-key review budget. Fit fewer bones or a smaller clip.");
                        var result=NodeFactory.FromFile(null,sourcePath) as CHR0Node??throw new InvalidDataException("Native source snapshot could not reopen.");
                        try
                        {
                            result.Name=options.Name;ApplyBoundaryTracks(result,reply.Tracks,options,model);
                            string proofPath=Path.Combine(stage,"candidate-"+Guid.NewGuid().ToString("N")+".chr0");result.Export(proofPath);
                            using(var decoded=NodeFactory.FromFile(null,proofPath) as CHR0Node)
                                if(decoded==null||decoded.FrameCount!=source.FrameCount||decoded.Loop!=source.Loop||NativeControlSnapshot.Capture(result,result.Children.Select(c=>c.Name)).ContentKey!=NativeControlSnapshot.Capture(decoded,decoded.Children.Select(c=>c.Name)).ContentKey)throw new InvalidDataException("Native fit did not fresh-reopen with its exact keys, tangents and flags. No result was added.");
                            var receipt=new NativeTransitionFit{Algorithm=reply.Algorithm,ModelSha256=modelHash,InitialResultSha256=FighterProject.HashFile(proofPath),Options=json.Serialize(options),Receipt=reply.Receipt,Inputs=inputs.ToArray()};receipt.Validate();
                            prepared=result;preparedKey=json.Serialize(options);copiedState=reply.State;metadata=receipt;referenceHashes=hashes;
                            window.PreparedReport=reply.Algorithm+"\r\nFixed "+source.FrameCount+" samples · native loop flag "+source.Loop+" · "+reply.Tracks.Length+" selected bone tracks.\r\nExact native keys/tangents/flags freshly reopened. References frozen with the independent result.\r\n"+string.Join("\r\n",reply.Diagnostics??new string[0])+"\r\nEntry reference: "+(options.Entry==null?"disabled":choices.Single(c=>c.Key==options.Entry).ToString())+" · phase "+(options.EntryFrame+1)+"\r\nExit reference: "+(options.Exit==null?"disabled":choices.Single(c=>c.Key==options.Exit).ToString())+" · phase "+(options.ExitFrame+1)+" · "+(options.ExitFrozen?"frozen authentic pose":"moving reference")+(options.ExitContactRecovery?"\r\nExplicit contact recovery after display frame "+(source.FrameCount-options.ExitSamples+1)+" through "+source.FrameCount+". Recovered body paths: "+string.Join(", ",recoveredContactBones)+"\r\nReference BODY uses the source-authored protected local controls at every time. Source root keys and travel remain unchanged; reference roots are never substituted. Earlier authored contact curves and fixed pin protections remain protected.":"")+(options.PreventFootDipping?"\r\nReviewed floor Y="+options.FloorHeight.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+". Per-region baseline: minimum of zero, original motion and authentic reference body under the same source-root placement. Authored endpoint penetration is retained.\r\nSaved floor verification receipt:\r\n"+reply.Receipt:"")+"\r\nPin review:\r\n"+pinReview;
                            return result;
                        }
                        catch{result.Dispose();throw;}
                    };
                    window.Apply=(options,candidate)=>
                    {
                        current();var contactBones=NativeTransitionContacts.Resolve(model,selected.Asset,options.PreserveContacts);NativeTransitionContacts.ReviewExitRecovery(options,source.FrameCount,source.Loop,contactBones,_protectedMotionBones,CurrentRecoveryBinding());VerifyReferences();if(candidate!=prepared||preparedKey!=json.Serialize(options))throw new InvalidOperationException("The prepared transition settings changed. Prepare again.");
                        if(_controlBusy||_retargetBusy||_workspaceBusy||_pinGesture!=null)throw new InvalidOperationException("Finish the current native edit before adding a fit.");metadata.Validate();
                        long total=project.Animations.Where(asset=>asset.TransitionFit!=null).Sum(asset=>asset.TransitionFit.Inputs.Sum(i=>(long)i.Snapshot.Length))+metadata.Inputs.Sum(i=>(long)i.Snapshot.Length);if(total>32L*1024*1024)throw new InvalidOperationException("This project exceeds the frozen transition reference budget. Save an independent fighter copy for further fitting.");
                        var generated=project.CreateAnimation(options.Name+".chr0",candidate.Export);generated.TransitionFit=metadata;generated.RigIdentity=BrawlRetargeter.SkeletonFingerprint(model);if(selected.Asset.Retarget!=null)generated.Retarget=json.Deserialize<RetargetProvenance>(json.Serialize(selected.Asset.Retarget));metadata.InitialResultSha256=NativeControlHash(FighterRoot(generated),".chr0");
                        var assetsBefore=project.Animations.ToArray();string selectionBefore=SelectedFighterAnimationKey();int frameBefore=modelEditControl1.CurrentFrame;
                        project.Animations.Add(generated);project.SelectedAnimation=FighterAnimationKey(generated,FighterRoot(generated));project.Frame=1;RebuildTransitionScene();string generatedKey=ControlKey();StoreControls(generatedKey,copiedState);InvalidateControls();
                        void Restore(bool added)
                        {
                            project.Animations.Clear();project.Animations.AddRange(assetsBefore);if(added)project.Animations.Add(generated);project.SelectedAnimation=added?FighterAnimationKey(generated,FighterRoot(generated)):selectionBefore;project.Frame=added?1:frameBefore;
                            project.AnimationControls.RemoveAll(s=>s.Key==generatedKey);if(added)StoreControls(generatedKey,copiedState);RebuildTransitionScene();MarkFighterChanged();modelEditControl1.QueueFrameFighterModel();
                        }
                        modelEditControl1.RecordEditorCommand(()=>Restore(false),()=>Restore(true));MarkFighterChanged();modelEditControl1.QueueFrameFighterModel();UpdateFighterStatus();_fighterStatus.Text="Added independent boundary fit. Existing duration, source animation and reference snapshots retained. Save Fighter or export CHR0.";
                    };
                    if(selected.Asset.TransitionFit is NativeTransitionFit saved&&string.Equals(saved.ModelSha256,modelHash,StringComparison.OrdinalIgnoreCase))window.RestoreOptions(json.Deserialize<TransitionFitOptions>(saved.Options));
                    window.Shown+=(s,e)=>TransitionFitReady?.Invoke(window);window.ShowDialog(this);
                }
            }
            finally
            {
                client?.Dispose();foreach(var root in roots)root.Dispose();string path=Path.GetFullPath(stage),parent=Path.GetFullPath(_controlStage);if(Path.GetDirectoryName(path)==parent&&Path.GetFileName(path).StartsWith("transition-",StringComparison.Ordinal))try{Directory.Delete(path,true);}catch(IOException){}
            }
        }
        private void ApplyBoundaryTracks(CHR0Node clip,ControlTrack[] tracks,TransitionFitOptions options,MDL0Node model)
        {
            foreach(var track in tracks)
            {
                if(!CanEditBodyBone(track.Bone))continue;
                var bone=model._linker.BoneCache.Single(b=>b.Name==track.Bone);var entry=clip.Children.OfType<CHR0EntryNode>().SingleOrDefault(e=>e.Name==track.Bone)??clip.CreateEntry(track.Bone);
                bool scale=entry.UseModelScale,rotate=entry.UseModelRotate,translate=entry.UseModelTranslate;
                var channels=entry.KeyArrays.Select(array=>array.Entries.Select(k=>new ControlKey{Frame=k._index,Value=k._value,Tangent=k._tangent}).ToArray()).ToArray();
                for(int i=0;i<9;i++)
                {
                    bool change=(options.Components&(i<3?1:i<6?2:4))!=0&&!(i>=6&&options.PreserveRootTranslation&&!(bone.Parent is MDL0BoneNode));if(!change)continue;
                    // Native originals own every protected key bit, including
                    // signed zero and both duplicate sides. Transport supplies
                    // only the new boundary keys and inserted join knots.
                    var retained=channels[i].Where(k=>!(options.Entry!=null&&k.Frame>=0&&k.Frame<options.EntrySamples-1)&&!(options.Exit!=null&&k.Frame>clip.FrameCount-options.ExitSamples&&k.Frame<clip.FrameCount)).ToArray();var times=new HashSet<int>(retained.Select(k=>k.Frame));channels[i]=track.Channels[i].Where(k=>!times.Contains(k.Frame)).Concat(retained).OrderBy(k=>k.Frame).ToArray();
                }
                NativeControlSnapshot.Write(entry,channels);entry.UseModelScale=(options.Components&1)!=0?false:scale;entry.UseModelRotate=(options.Components&2)!=0?false:rotate;entry.UseModelTranslate=(options.Components&4)!=0&&!(options.PreserveRootTranslation&&!(bone.Parent is MDL0BoneNode))?false:translate;
            }
            PreciseCHR0EntryNode.Promote(clip);clip.SignalPropertyChange();
        }
        private void RebuildTransitionScene()
        {
            // Native model-list refresh clears history while rebinding controls.
            // A fitting import keeps the exact same model and resource objects;
            // preserve their existing command history and the in-flight cursor.
            var editor=modelEditControl1;var undo=editor._undoSaves.ToArray();var redo=editor._redoSaves.ToArray();int cursor=editor._saveIndex;bool undoing=editor._undoing;var pending=editor._currentUndo;
            try{RebuildFighterScene();}
            finally{editor._undoSaves.Clear();editor._undoSaves.AddRange(undo);editor._redoSaves.Clear();editor._redoSaves.AddRange(redo);editor._saveIndex=cursor;editor._undoing=undoing;editor._currentUndo=pending;editor.UpdateUndoButtons();}
        }
    }
}
