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
        internal Action<ContactBakeDialog> ContactBakeReady;
        private async void OpenContactPlacement(){try{await ShowContactPlacement();}catch(Exception error){if(!IsDisposed)_fighterStatus.Text="Contact authoring: "+error.Message;}}
        private async Task ShowContactPlacement()
        {
            if(_controlBusy||_controlSetupQueued||_retargetBusy||_workspaceBusy||_pinGesture!=null)throw new InvalidOperationException("Finish the current native edit first.");
            var model=modelEditControl1.TargetModel as MDL0Node??throw new InvalidOperationException("Open a native model.");var source=modelEditControl1.SelectedCHR0??throw new InvalidOperationException("Select a native animation.");var asset=SelectedFighterAsset()??throw new InvalidOperationException("Register this animation first.");modelEditControl1.StopAnim();
            var setup=await LoadControls();var project=_fighterProject;string key=ControlKey(),state=ControlState(key),modelHash=NativeControlHash(model,".mdl0"),sourceHash=NativeControlHash(source,".chr0");var json=new JavaScriptSerializer{MaxJsonLength=32*1024*1024,RecursionLimit=200};string dependency=json.Serialize(new{asset.Retarget,asset.ContactBake});
            void Current(){if(IsDisposed||_fighterProject!=project||modelEditControl1.TargetModel!=model||modelEditControl1.SelectedCHR0!=source||NativeControlHash(model,".mdl0")!=modelHash||NativeControlHash(source,".chr0")!=sourceHash||ControlState(key)!=state||json.Serialize(new{asset.Retarget,asset.ContactBake})!=dependency)throw new InvalidOperationException("Native model, animation, contact setup or provenance changed. Reopen contact authoring.");}
            string stage=Path.Combine(_controlStage,"contact-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);var roots=new List<ResourceNode>();AnimationControlClient client=null;
            try
            {
                string modelPath=Path.Combine(stage,"model.mdl0"),motionPath=Path.Combine(stage,"source.chr0");model.Export(modelPath);
                using(var snapshot=NativeAnimationOptimization.CopySource(source))snapshot.ExportChecked(motionPath);
                var rootSnapshots=new Dictionary<ResourceNode,string>();
                ResourceNode Copy(ResourceNode root,string name)
                {
                    if(!rootSnapshots.TryGetValue(root,out var path))
                    {
                        string extension=Path.GetExtension(root.FilePath);if(string.IsNullOrEmpty(extension))extension=".brres";path=Path.Combine(stage,name+extension);
                        NativeFittingPreviewSnapshot.WriteRawContext(root,path,model,modelPath);rootSnapshots.Add(root,path);
                    }
                    var copy=NativeFittingPreviewSnapshot.OpenContext(root,path,model,modelPath);roots.Add(copy);return copy;
                }
                IEnumerable<ResourceNode> Walk(ResourceNode root){yield return root;foreach(var child in root.Children)foreach(var node in Walk(child))yield return node;}
                ResourceNode a=Copy(model.RootNode,"before"),b=Copy(model.RootNode,"after");var am=Walk(a).OfType<MDL0Node>().Single(m=>NativeControlHash(m,".mdl0")==modelHash);var bm=Walk(b).OfType<MDL0Node>().Single(m=>NativeControlHash(m,".mdl0")==modelHash);var references=modelEditControl1.ModelPanel._resourceList.Select(r=>r.RootNode).Distinct().Where(r=>r!=model.RootNode&&NativeFittingPreviewSnapshot.HasTextureDependencies(r)).Select((r,i)=>Copy(r,"reference-"+i)).ToArray();
                string preparedOptions=null,copiedState=null;CHR0Node prepared=null;NativeContactBake metadata=null;
                using(var window=new ContactBakeDialog(a,am,b,bm,source,setup.Chains.Where(c=>c.Reviewed).Select(c=>setup.BoneNames[c.Effector]).ToArray(),modelEditControl1.ModelPanel.CurrentViewport,references))
                {
                    window.CancelWork=()=>{client?.Dispose();client=null;};
                    window.Prepare=async options=>
                    {
                        Current();byte[] baseline=File.ReadAllBytes(motionPath);string baseHash=FighterProject.HashFile(motionPath);
                        if(asset.ContactBake!=null){asset.ContactBake.Validate();if(asset.ContactBake.ModelHash!=modelHash)throw new InvalidOperationException("The saved contact reference belongs to another native model revision.");baseline=Convert.FromBase64String(asset.ContactBake.BaseSnapshot);baseHash=asset.ContactBake.BaseHash;}
                        if(options.Action=="Release"&&asset.ContactBake==null)throw new InvalidOperationException("Release needs a saved authored contact and its frozen original motion. Legacy retarget contact counts do not define a release timeline.");
                        if(baseline.Length>8*1024*1024)throw new InvalidOperationException("The frozen original exceeds the 8 MiB contact review budget.");string originalPath=Path.Combine(stage,"original.chr0");File.WriteAllBytes(originalPath,baseline);
                        if(client==null)client=new AnimationControlClient();var session=client;var reply=await session.Send(new{Operation="contact-prepare",Model=modelPath,Motion=motionPath,State=state,Path=originalPath,ContactEdit=options,ContactProtection=NativeTransitionContacts.Resolve(model,asset,true)});
                        Current();if(client!=session||window.IsDisposed)throw new OperationCanceledException();
                        var proof=json.Deserialize<Dictionary<string,object>>(reply.Receipt);int first=Convert.ToInt32(proof["FirstChanged"]),last=Convert.ToInt32(proof["LastChanged"]);var candidate=(CHR0Node)NodeFactory.FromFile(null,motionPath);
                        try
                        {
                            candidate.Name=source.Name+(options.Action=="Release"?" (released)":" (contact)");
                            PreciseCHR0EntryNode.Promote(candidate);
                            foreach(var track in reply.Tracks)
                            {
                                if(!CanEditBodyBone(track.Bone))continue;
                                var entry=candidate.Children.OfType<CHR0EntryNode>().SingleOrDefault(e=>e.Name==track.Bone)??candidate.CreateEntry(track.Bone);var channels=entry.KeyArrays.Select(array=>array.Entries.Select(k=>new ControlKey{Frame=k._index,Value=k._value,Tangent=k._tangent}).ToArray()).ToArray();
                                for(int c=3;c<6;c++){var exterior=channels[c].Where(k=>k.Frame<first||k.Frame>last).ToArray();channels[c]=exterior.Concat(track.Channels[c].Where(k=>k.Frame>=first&&k.Frame<=last)).OrderBy(k=>k.Frame).ToArray();}NativeControlSnapshot.Write(entry,channels);entry.UseModelRotate=false;
                            }
                            PreciseCHR0EntryNode.Promote(candidate);candidate.SignalPropertyChange();string saved=Path.Combine(stage,"candidate-"+Guid.NewGuid().ToString("N")+".chr0");candidate.Export(saved);
                            using(var fresh=(CHR0Node)NodeFactory.FromFile(null,saved))if(NativeControlSnapshot.Capture(candidate,candidate.Children.Select(c=>c.Name)).ContentKey!=NativeControlSnapshot.Capture(fresh,fresh.Children.Select(c=>c.Name)).ContentKey)throw new InvalidDataException("Contact keys/tangents/flags did not reopen exactly.");
                            await session.Send(new{Operation="contact-verify",Model=modelPath,Motion=saved,State=reply.State,ContactProof=proof});Current();if(client!=session||window.IsDisposed)throw new OperationCanceledException();
                            metadata=new NativeContactBake{Algorithm=reply.Algorithm,ModelHash=modelHash,BaseHash=baseHash,BaseSnapshot=Convert.ToBase64String(baseline),Options=json.Serialize(options),Receipt=reply.Receipt};metadata.Validate();prepared=candidate;preparedOptions=json.Serialize(options);copiedState=reply.State;window.PreparedReport="Fresh native CHR0 verified at quarter frames within the fixed contact bounds.\r\n"+reply.Receipt+"\r\n"+string.Join("\r\n",reply.Diagnostics??new string[0]);return candidate;
                        }
                        catch{candidate.Dispose();throw;}
                    };
                    window.Apply=(options,candidate)=>
                    {
                        Current();if(prepared!=candidate||preparedOptions!=json.Serialize(options))throw new InvalidOperationException("Contact settings changed; prepare again.");metadata.Validate();if(project.Animations.Sum(item=>(long)(item.ContactBake?.BaseSnapshot?.Length??0))+metadata.BaseSnapshot.Length>32L*1024*1024)throw new InvalidOperationException("Frozen contact originals exceed the project review budget.");var created=project.CreateAnimation(candidate.Name+".chr0",candidate.Export);created.RigIdentity=BrawlRetargeter.SkeletonFingerprint(model);created.ContactBake=metadata;created.Retarget=asset.Retarget==null?null:json.Deserialize<RetargetProvenance>(json.Serialize(asset.Retarget));
                        var before=project.Animations.ToArray();string selected=SelectedFighterAnimationKey();int frame=modelEditControl1.CurrentFrame;project.Animations.Add(created);project.SelectedAnimation=FighterAnimationKey(created,FighterRoot(created));project.Frame=1;RebuildTransitionScene();string generatedKey=ControlKey();StoreControls(generatedKey,copiedState);InvalidateControls();
                        void Restore(bool add){project.Animations.Clear();project.Animations.AddRange(before);if(add)project.Animations.Add(created);project.SelectedAnimation=add?FighterAnimationKey(created,FighterRoot(created)):selected;project.Frame=add?1:frame;project.AnimationControls.RemoveAll(c=>c.Key==generatedKey);if(add)StoreControls(generatedKey,copiedState);RebuildTransitionScene();MarkFighterChanged();}
                        modelEditControl1.RecordEditorCommand(()=>Restore(false),()=>Restore(true));MarkFighterChanged();modelEditControl1.QueueFrameFighterModel();
                    };
                    window.Shown+=(s,e)=>ContactBakeReady?.Invoke(window);window.ShowDialog(this);
                }
            }
            finally{client?.Dispose();foreach(var root in roots)root.Dispose();string full=Path.GetFullPath(stage);if(Path.GetDirectoryName(full)==Path.GetFullPath(_controlStage)&&Path.GetFileName(full).StartsWith("contact-",StringComparison.Ordinal))try{Directory.Delete(full,true);}catch(IOException){}}
        }
    }
}
