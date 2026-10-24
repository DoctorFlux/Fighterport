using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        internal Action<SceneFittingDialog> SceneFittingReady;
        private void OpenAdvancedSceneFitting()
        {
            if(_libraryPane==null)BuildLibraryPane();
            if(LibrarySelection?.State!=LibrarySourceState.Available){if(!_libraryPane.Visible)ToggleAnimationLibrary();_fighterStatus.Text="Select an authentic source clip in Library, then Advanced Fitting.";return;}
            ShowAdvancedSceneFitting(LibrarySelection.Clip.Id);
        }
        private void ShowAdvancedSceneFitting(string clipId)
        {
            if(!(modelEditControl1.TargetModel is MDL0Node target)||_controlBusy||_retargetBusy||_workspaceBusy||_pinGesture!=null)return;
            modelEditControl1.StopAnim();_libraryTimer?.Stop();if(_libraryPlay!=null)_libraryPlay.Text="Play";
            using(var source=LoadLibrarySource(clipId))
            {
                if(source==null)return;var project=_fighterProject;var selected=modelEditControl1.SelectedCHR0;
                string targetHash=NativeControlHash(target,".mdl0"),selectedHash=selected==null?null:NativeControlHash(selected,".chr0");var sourceReference=source.Reference.Copy();
                string snapshotStage=Path.Combine(_controlStage,"fitting-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(snapshotStage);
                string sourceModel=Path.Combine(snapshotStage,"source.mdl0"),motion=Path.Combine(snapshotStage,"source.chr0"),targetModel=Path.Combine(snapshotStage,"target.mdl0");
                source.Model.Export(sourceModel);source.Animation.Export(motion);target.Export(targetModel);
                string sourceFingerprint=EngineRigReview.FingerprintOf(source.Model),targetFingerprint=EngineRigReview.FingerprintOf(target);
                var json=new JavaScriptSerializer{MaxJsonLength=16*1024*1024,RecursionLimit=200};string key;
                using(var sha=SHA256.Create())key=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(json.Serialize(new{Source=sourceReference,Target=targetHash})))).Replace("-","");
                var saved=project.SceneFittingReviews.SingleOrDefault(r=>r.Key==key);var options=saved==null?new SceneFittingOptions():json.Deserialize<SceneFittingOptions>(saved.Options);
                AnimationControlClient client=null;string loadedKey=null;string contextHash=null;ResourceNode sourceRoot=null,targetRoot=null;var renderReferences=new System.Collections.Generic.List<ResourceNode>();
                Action current=()=>
                {
                    if(IsDisposed||_fighterProject!=project||modelEditControl1.TargetModel!=target||modelEditControl1.SelectedCHR0!=selected||NativeControlHash(target,".mdl0")!=targetHash||(selected!=null&&NativeControlHash(selected,".chr0")!=selectedHash))throw new InvalidOperationException("The target model or animation changed. This fitting review is stale; reopen it for the current target.");
                    using(var verified=_library.Open(clipId))if(json.Serialize(verified.Reference)!=json.Serialize(sourceReference))throw new InvalidOperationException("The authentic library source changed. This fitting review is stale.");
                };
                try
                {
                    // Full native roots retain textures and native identities;
                    // the viewer owns these copies and never poses live resources.
                    ResourceNode CloneRoot(ResourceNode original,string sourcePath,string name)
                    {
                        string extension=Path.GetExtension(sourcePath);if(string.IsNullOrEmpty(extension))extension=".brres";string path=Path.Combine(snapshotStage,name+extension);original.Export(path);return NodeFactory.FromFile(null,path);
                    }
                    System.Collections.Generic.IEnumerable<ResourceNode> Walk(ResourceNode node){yield return node;foreach(var child in node.Children)foreach(var descendant in Walk(child))yield return descendant;}
                    sourceRoot=CloneRoot(source.Model.RootNode,source.ModelPath,"source-preview");targetRoot=CloneRoot(target.RootNode,project.Model.WorkingPath,"target-preview");
                    foreach(var reference in modelEditControl1.ModelPanel._resourceList.Select(r=>r.RootNode).Distinct().Where(r=>r!=target.RootNode))renderReferences.Add(CloneRoot(reference,reference.FilePath??"reference.brres","target-reference-"+renderReferences.Count));
                    var sourcePreview=Walk(sourceRoot).OfType<MDL0Node>().First(m=>EngineRigReview.FingerprintOf(m)==sourceFingerprint);var targetPreview=Walk(targetRoot).OfType<MDL0Node>().First(m=>NativeControlHash(m,".mdl0")==targetHash);
                    using(var window=new SceneFittingDialog(sourceRoot,sourcePreview,source.Animation,targetRoot,targetPreview,options,modelEditControl1.ModelPanel.CurrentViewport,renderReferences))
                    {
                        window.ContextPath=saved?.ContextPath;window.ContextClip=saved?.ContextClip;
                        async Task<SceneFittingReply> LoadReview(SceneFittingOptions value,string path,string contextClip)
                        {
                            current();string hash=string.IsNullOrEmpty(path)?null:FighterProject.HashFile(path);
                            string dependency=json.Serialize(new{SourceBinding=EngineBinding(source.Model),TargetBinding=EngineBinding(target),Context=hash,Clip=contextClip});
                            if(client==null){client=new AnimationControlClient();loadedKey=null;}
                            var session=client;var result=await session.Send(new{Operation="fitting-load",SourceAssociation=sourceReference.AssociationEvidence,SourceRegistration=sourceReference.ClipId,SourceModel=sourceModel,Motion=motion,Model=targetModel,SourceBinding=EngineBinding(source.Model),TargetBinding=EngineBinding(target),Path=path,ClipKey=contextClip});
                            current();if(client!=session)throw new OperationCanceledException("Fitting review was cancelled.");if(hash!=(string.IsNullOrEmpty(path)?null:FighterProject.HashFile(path))||dependency!=json.Serialize(new{SourceBinding=EngineBinding(source.Model),TargetBinding=EngineBinding(target),Context=hash,Clip=contextClip}))throw new InvalidOperationException("Companion context or reviewed rig binding changed during setup; stale review discarded.");loadedKey=dependency;contextHash=hash;return result.Fitting;
                        }
                        window.LoadReview=LoadReview;
                        window.Prepare=async(value,path,contextClip)=>
                        {
                            string hash=string.IsNullOrEmpty(path)?null:FighterProject.HashFile(path);string dependency=json.Serialize(new{SourceBinding=EngineBinding(source.Model),TargetBinding=EngineBinding(target),Context=hash,Clip=contextClip});
                            if(client==null||loadedKey!=dependency)await LoadReview(value,path,contextClip);current();var session=client;
                            var result=await session.Send(new{Operation="fitting-prepare",Fitting=value});current();if(client!=session)throw new OperationCanceledException("Fitting review was cancelled.");if(hash!=(string.IsNullOrEmpty(path)?null:FighterProject.HashFile(path))||dependency!=json.Serialize(new{SourceBinding=EngineBinding(source.Model),TargetBinding=EngineBinding(target),Context=hash,Clip=contextClip}))throw new InvalidOperationException("Companion context or reviewed rig binding changed during preparation; stale draft discarded.");return result.Fitting;
                        };
                        window.ReadContext=async path=>{current();if(client==null)client=new AnimationControlClient();var session=client;var response=await session.Send(new{Operation="fitting-context-list",Path=path});current();if(client!=session)throw new OperationCanceledException("Context read was cancelled.");return response.Fitting;};
                        window.CancelWork=()=>{client?.Dispose();client=null;loadedKey=null;};
                        window.ReviewCoordinates=isSource=>
                        {
                            var model=isSource?source.Model:target;var rig=CaptureFighterRig(model);ReviewEngineCoordinates(model,rig.Rig.Roles.ToDictionary(p=>p.Key,p=>rig.Bones[p.Value].Name));loadedKey=null;
                        };
                        window.OriginalNext=()=>{current();return BakeWithProgress(source.Model,target,source.Animation);};
                        window.SaveReview=async(value,reply,path,contextClip)=>
                        {
                            current();string hash=string.IsNullOrEmpty(path)?null:FighterProject.HashFile(path);
                            if(reply!=null&&hash!=contextHash)throw new InvalidOperationException("Companion context changed; prepare a new review before saving its receipt.");
                            if(reply!=null&&loadedKey!=json.Serialize(new{SourceBinding=EngineBinding(source.Model),TargetBinding=EngineBinding(target),Context=hash,Clip=contextClip}))throw new InvalidOperationException("Reviewed rig binding changed; prepare a new review before saving its receipt.");
                            if(reply!=null)
                            {
                                var session=client??throw new InvalidOperationException("Reload the fitting review before saving its receipt.");
                                await session.Send(new{Operation="fitting-check",State=reply.Dependency,Fitting=value});current();
                                if(client!=session||window.IsDisposed||window.Reply!=reply||json.Serialize(window.ReadOptions())!=json.Serialize(value))throw new OperationCanceledException("Fitting review changed while saving; stale receipt discarded.");
                                if(hash!=(string.IsNullOrEmpty(path)?null:FighterProject.HashFile(path))||loadedKey!=json.Serialize(new{SourceBinding=EngineBinding(source.Model),TargetBinding=EngineBinding(target),Context=hash,Clip=contextClip}))throw new InvalidOperationException("Companion context or rig binding changed during save; stale receipt discarded.");
                            }
                            var before=project.SceneFittingReviews.SingleOrDefault(r=>r.Key==key);var after=new SceneFittingReviewState{Key=key,Options=json.Serialize(value),Receipt=reply?.Receipt,Source=sourceReference.Copy(),TargetFingerprint=targetFingerprint,ContextPath=path,ContextClip=contextClip,ContextHash=hash};
                            void Store(SceneFittingReviewState state){project.SceneFittingReviews.RemoveAll(r=>r.Key==key);if(state!=null)project.SceneFittingReviews.Add(state);MarkFighterChanged();}
                            Store(after);modelEditControl1.RecordEditorCommand(()=>Store(before),()=>Store(after));
                        };
                        window.Shown+=(s,e)=>SceneFittingReady?.Invoke(window);window.ShowDialog(this);
                    }
                }
                finally
                {
                    client?.Dispose();sourceRoot?.Dispose();targetRoot?.Dispose();foreach(var reference in renderReferences)reference.Dispose();
                    string path=Path.GetFullPath(snapshotStage),parent=Path.GetFullPath(_controlStage);if(Path.GetDirectoryName(path)==parent&&Path.GetFileName(path).StartsWith("fitting-",StringComparison.Ordinal))try{Directory.Delete(path,true);}catch(IOException){/* A cancelled native reader may still be releasing its private snapshot. */}
                }
            }
        }
    }
}
