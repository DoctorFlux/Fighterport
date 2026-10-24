using BrawlCrate.Studio;
using BrawlLib.Internal;
using BrawlLib.Modeling;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private readonly AnimationControlClient _controlEngine=new AnimationControlClient();
        private readonly string _controlStage=Path.Combine(Path.GetTempPath(),"FighterPort","NextControls",Guid.NewGuid().ToString("N"));
        private ToolStripMenuItem _controlFollow,_controlPinsOnly,_controlPickFK;
        private int _controlEpoch;
        private bool _controlBusy;
        private bool _controlSetupQueued,_controlReloadRequested;
        private string _loadedControlKey;
        private MDL0Node _loadedControlModel;
        private CHR0Node _loadedControlClip;
        private string _loadedControlModelHash,_loadedControlClipHash;
        private string[] _controlBoneNames;
        private Form _controlsWindow;
        internal int ControlRotationAxis=1;

        private string ControlKey()=> (_fighterProject.Model?.Id??"model")+"|"+(_fighterProject.ModelNodePath??"")+"|"+(SelectedFighterAnimationKey()??"pose");
        private string ControlState(string key)
        {
            var exact=_fighterProject.AnimationControls.FirstOrDefault(s=>s.Key==key);if(exact!=null)return exact.Json;
            var animation=modelEditControl1.TargetAnimation;if(animation==null||key!=ControlKey())return null;
            var asset=RegisteredFighterAssets().FirstOrDefault(a=>_fighterRoots.TryGetValue(a.Id,out var root)&&ReferenceEquals(animation.RootNode,root));
            if(asset==null||DiscoverFighterAnimations(animation.RootNode).Count(n=>n.TreePath==animation.TreePath)!=1)return null;
            string legacy=(_fighterProject.Model?.Id??"model")+"|"+(_fighterProject.ModelNodePath??"")+"|"+asset.Id+"|"+animation.TreePath;
            return _fighterProject.AnimationControls.FirstOrDefault(s=>s.Key==legacy)?.Json;
        }
        private void StoreControls(string key,string state)
        {
            var existing=_fighterProject.AnimationControls.FirstOrDefault(s=>s.Key==key);
            if(existing==null)_fighterProject.AnimationControls.Add(new AnimationControlState{Key=key,Json=state});else existing.Json=state;
        }
        private void InitializeAnimationControls(ToolStrip toolbar)
        {
            var menu=new ToolStripDropDownButton("Animation Controls"){Name="fighterAnimationControls"};
            menu.DropDownItems.Add("Set Up Animation Controls",null,(s,e)=>ControlAction(async()=>{await LoadControls();MarkFighterChanged();}));
            menu.DropDownItems.Add("Review IK Chains and Shared Bones...",null,(s,e)=>ControlAction(ReviewControlSetup));
            menu.DropDownItems.Add("Review Control Mapping and Coordinates...",null,(s,e)=>RunFighterAction(()=>
            {
                if(!(modelEditControl1.TargetModel is MDL0Node model))throw new InvalidOperationException("Open a model first.");
                var rig=CaptureFighterRig(model,true);
                if(ReviewEngineCoordinates(model,rig.Rig.Roles.ToDictionary(p=>p.Key,p=>rig.Bones[p.Value].Name)))InvalidateControls();
            }));
            menu.DropDownItems.Add("Create Animation From Pose...",null,(s,e)=>CreateControlAnimationFromUi());
            menu.DropDownItems.Add("Pin Selected Bone",null,(s,e)=>ControlAction(()=>AttachControlPin(false)));
            menu.DropDownItems.Add("Soft Pin Hands and Feet",null,(s,e)=>ControlAction(()=>AttachControlPin(true)));
            _controlFollow=new ToolStripMenuItem("Follow Existing Animation"){CheckOnClick=true};menu.DropDownItems.Add(_controlFollow);
            _controlPinsOnly=new ToolStripMenuItem("Pick Pins Only"){CheckOnClick=true};
            _controlPinsOnly.CheckedChanged+=(s,e)=>modelEditControl1.PickAnimationPinsOnly=_controlPinsOnly.Checked;menu.DropDownItems.Add(_controlPinsOnly);
            _controlPickFK=new ToolStripMenuItem("Select FK Controls When Picking Bones"){CheckOnClick=true};_controlPickFK.CheckedChanged+=(s,e)=>modelEditControl1.PickAnimationFKControls=_controlPickFK.Checked;menu.DropDownItems.Add(_controlPickFK);
            menu.DropDownItems.Add("Remove Selected Pin",null,(s,e)=>ControlAction(RemoveControlPin));
            menu.DropDownItems.Add("Controls Window...",null,(s,e)=>ShowAnimationControls());
            toolbar.Items.Add(menu);
            modelEditControl1.AnimationPinDown=BeginControlGesture;
            modelEditControl1.AnimationPinMove=MoveControlGesture;
            modelEditControl1.AnimationPinUp=EndControlGesture;
            modelEditControl1.AnimationPinCancel=CancelControlGesture;
            modelEditControl1.AnimationControlsBusy=()=>_controlBusy||_retargetBusy||_workspaceBusy;
            modelEditControl1.AnimationHandleSelectionChanged=SyncControlSelection;
            modelEditControl1.AnimationCurveTargetChanged=ObserveControlCurve;
            modelEditControl1.AnimationCurveEdited=QueueControlCurveEdit;
            modelEditControl1.ControlChannelEdited=EditControlChannel;
            modelEditControl1.TargetModelChanged+=(s,e)=>InvalidateControls();
            modelEditControl1.leftPanel.listAnims.SelectedIndexChanged+=(s,e)=>InvalidateControls();
            FormClosed+=(s,e)=>{_controlEpoch++;_controlEngine.Dispose();_controlsWindow?.Dispose();try{if(Directory.Exists(_controlStage))Directory.Delete(_controlStage,true);}catch(IOException){}};
        }
        private void InvalidateControls()
        {
            _controlEpoch++;_loadedControlKey=null;modelEditControl1.AnimationAuthoringPose=null;modelEditControl1.AnimationPins=new ControlPin[0];modelEditControl1.AnimationHandles=new ControlPin[0];modelEditControl1.SelectedAnimationPin=null;RefreshControlTree();
            // Setup after loading, including model-only documents. No donor needed.
            QueueControlSetup();
        }
        private void QueueControlSetup()
        {
            if(!IsHandleCreated||IsDisposed)return;
            if(_controlBusy||_retargetBusy||_workspaceBusy){_controlReloadRequested=true;return;}
            if(_controlSetupQueued)return;_controlSetupQueued=true;
            BeginInvoke(new Action(()=>{_controlSetupQueued=false;if(_controlBusy||_retargetBusy||_workspaceBusy){_controlReloadRequested=true;return;}_controlReloadRequested=false;if(modelEditControl1.TargetModel!=null&&!modelEditControl1.Playing)ControlAction(async()=>{await LoadControls();});}));
        }
        private async void ControlAction(Func<Task> action)
        {
            if(_controlBusy||_retargetBusy||_workspaceBusy)return;_controlBusy=true;
            try{await action();}
            catch(Exception e){if(!IsDisposed)_fighterStatus.Text="Animation controls: "+e.Message;}
            finally{_controlBusy=false;if(_controlReloadRequested)QueueControlSetup();}
        }
        private async Task<ControlReply> LoadControls()
        {
            if(modelEditControl1.Playing)throw new InvalidOperationException("Pause playback before setting up or editing controls.");
            var model=modelEditControl1.TargetModel as MDL0Node??throw new InvalidOperationException("Open a supported native model first.");
            var clip=modelEditControl1.SelectedCHR0;string key=ControlKey();int epoch=++_controlEpoch,frame=modelEditControl1.CurrentFrame;
            Directory.CreateDirectory(_controlStage);string modelPath=Path.Combine(_controlStage,Guid.NewGuid().ToString("N")+".mdl0"),motionPath=clip==null?null:Path.Combine(_controlStage,Guid.NewGuid().ToString("N")+".chr0");
            // Snapshot actual edited native nodes on their owner thread. Workers
            // never touch controls/native scene objects or run during playback.
            model.Export(modelPath);clip?.Export(motionPath);
            string modelHash=FighterProject.HashFile(modelPath),clipHash=motionPath==null?null:FighterProject.HashFile(motionPath);
            try
            {
                var reply=await _controlEngine.Send(new{Operation="load",Model=modelPath,Motion=motionPath,State=ControlState(key),TargetBinding=EngineBinding(model),Frame=Math.Max(0,frame-1)});
                if(IsDisposed||epoch!=_controlEpoch||modelEditControl1.TargetModel!=model||modelEditControl1.SelectedCHR0!=clip||ControlKey()!=key||modelEditControl1.CurrentFrame!=frame||NativeControlHash(model,".mdl0")!=modelHash||(clip!=null&&NativeControlHash(clip,".chr0")!=clipHash)){_controlReloadRequested=true;throw new InvalidOperationException("The model, animation or frame changed; the stale setup was discarded.");}
                _loadedControlKey=key;_loadedControlModel=model;_loadedControlClip=clip;_controlBoneNames=reply.BoneNames;modelEditControl1.AnimationAuthoringBones=reply.BoneNames;StoreControls(key,reply.State);PublishPins(reply);
                _loadedControlModelHash=modelHash;_loadedControlClipHash=clipHash;
                if(clip==null){modelEditControl1.AnimationAuthoringPose=reply.Pose;PreviewControlPose(model,reply.Pose);}
                _fighterStatus.Text=reply.Diagnostics?.Length>0?string.Join(" · ",reply.Diagnostics):"Animation controls ready. Pick a diamond to move, rotate or scale it.";
                return reply;
            }
            finally {if(File.Exists(modelPath))File.Delete(modelPath);if(motionPath!=null&&File.Exists(motionPath))File.Delete(motionPath);}
        }
        private void PublishPins(ControlReply reply)
        {
            modelEditControl1.AnimationPins=reply.Pins??new ControlPin[0];
            if(reply.BoneNames!=null)modelEditControl1.AnimationHandles=reply.BoneNames.Select((name,i)=>new ControlPin{Id="fk:"+i,Bone=i,BoneName=name,Name=name}).Concat((reply.Chains??new ControlChain[0]).Select(c=>new ControlPin{Id="ik:"+c.Effector,Bone=c.Effector,BoneName=reply.BoneNames[c.Effector],Name=c.Role,Soft=true})).Concat((reply.Chains??new ControlChain[0]).Select(c=>new ControlPin{Id="pole:"+c.Effector,Bone=c.Effector,BoneName=reply.BoneNames[c.Effector],Name=c.Role,RootName=reply.BoneNames[c.Root],MiddleName=reply.BoneNames[c.Middle],Pole=c.Pole,PoleParent=c.PoleParent})).ToArray();
            RefreshControlTree();modelEditControl1.RefreshControlCurveTarget();
            if(!_curvePending&&_curveTarget?.Parent is CHR0Node curveClip)_curveBefore=NativeControlSnapshot.Capture(curveClip,new[]{_curveTarget.Name});
            modelEditControl1.ModelPanel.Invalidate();
        }
        private async Task AttachControlPin(bool limbs)
        {
            string selectedBone=modelEditControl1.SelectedBone?.Name??modelEditControl1.SelectedControlHandle?.BoneName;
            if(!limbs&&selectedBone==null)throw new InvalidOperationException("Select an existing bone first.");
            string key=ControlKey(),before=ControlState(key);await LoadControls();int epoch=_controlEpoch;
            int bone=Array.IndexOf(_controlBoneNames,selectedBone);
            var reply=await _controlEngine.Send(new{Operation=limbs?"pin-limbs":"pin",Bone=bone,Frame=Math.Max(0,modelEditControl1.CurrentFrame-1),Follow=_controlFollow.Checked});
            if(epoch!=_controlEpoch||key!=ControlKey())return;string after=reply.State;StoreControls(key,after);PublishPins(reply);MarkFighterChanged();
            modelEditControl1.RecordEditorCommand(()=>RestoreControlMetadata(key,before),()=>RestoreControlMetadata(key,after));
        }
        private async Task RemoveControlPin()
        {
            string pin=modelEditControl1.SelectedAnimationPin;if(pin==null)return;if(!Guid.TryParse(pin,out _))throw new InvalidOperationException("Select a pin in the Pins group to remove it.");string key=ControlKey(),before=ControlState(key);await LoadControls();int epoch=_controlEpoch;
            var reply=await _controlEngine.Send(new{Operation="remove",Pin=pin,Frame=Math.Max(0,modelEditControl1.CurrentFrame-1)});
            if(epoch!=_controlEpoch||key!=ControlKey())return;StoreControls(key,reply.State);PublishPins(reply);MarkFighterChanged();
            modelEditControl1.RecordEditorCommand(()=>RestoreControlMetadata(key,before),()=>RestoreControlMetadata(key,reply.State));
        }
        private void RestoreControlMetadata(string key,string json)
        {
            if(json==null)_fighterProject.AnimationControls.RemoveAll(s=>s.Key==key);else StoreControls(key,json);
            _loadedControlKey=null;modelEditControl1.AnimationPins=new ControlPin[0];MarkFighterChanged();
            ControlAction(async()=>{await LoadControls();});
        }

        private sealed class PinGesture
        {
            internal int Epoch,Frame;internal string Key,Pin,BeforeState,PreviewError;internal MDL0Node Model;internal CHR0Node Clip;
            internal string ModelHash,ClipHash;
            internal float[][] BeforePose;internal float[] Latest;internal bool Ready,Pumping,Released,Cancelled;
        }
        private PinGesture _pinGesture;
        private async void BeginControlGesture(ControlPin pin)
        {
            if(_controlBusy||_retargetBusy||modelEditControl1.Playing)return;
            if(modelEditControl1.SelectedAnimationPin!=pin.Id)modelEditControl1.SelectControlHandle(pin);
            if(modelEditControl1.SelectedCHR0!=null&&modelEditControl1.CurrentFrame==0)modelEditControl1.SetFrame(1);
            var gesture=new PinGesture{Key=ControlKey(),Pin=pin.Id,Frame=modelEditControl1.CurrentFrame,Model=modelEditControl1.TargetModel as MDL0Node,Clip=modelEditControl1.SelectedCHR0,BeforeState=ControlState(ControlKey())};
            _pinGesture=gesture;_controlBusy=true;
            try
            {
                var loaded=await LoadControls();gesture.Epoch=_controlEpoch;gesture.BeforePose=loaded.Pose;
                gesture.ModelHash=_loadedControlModelHash;gesture.ClipHash=_loadedControlClipHash;
                if(!GestureCurrent(gesture))return;
                await _controlEngine.Send(new{Operation="begin",Bone=pin.Bone,Pin=pin.Id,Frame=Math.Max(0,gesture.Frame-1)});
                gesture.Ready=true;await PumpControlGesture(gesture);
            }
            catch(Exception e){CancelControlGesture();if(!IsDisposed)_fighterStatus.Text="Animation controls: "+e.Message;}
            finally{if(_pinGesture==null||!gesture.Ready)_controlBusy=false;}
        }
        private bool GestureCurrent(PinGesture g)=>!IsDisposed&&!g.Cancelled&&ReferenceEquals(_pinGesture,g)&&!modelEditControl1.Playing&&modelEditControl1.SelectedAnimationPin==g.Pin&&modelEditControl1.TargetModel==g.Model&&modelEditControl1.SelectedCHR0==g.Clip&&modelEditControl1.CurrentFrame==g.Frame&&ControlKey()==g.Key&&(g.Epoch==0||g.Epoch==_controlEpoch);
        private void MoveControlGesture(float[] goal){var g=_pinGesture;if(g!=null){g.Latest=goal;if(g.Ready&&!g.Pumping)ControlPump(g);}}
        private async void ControlPump(PinGesture g){try{await PumpControlGesture(g);}catch(Exception e){CancelControlGesture();if(!IsDisposed)_fighterStatus.Text="Animation controls: "+e.Message;}}
        private void EndControlGesture(){var g=_pinGesture;if(g!=null){g.Released=true;if(g.Ready&&!g.Pumping)ControlPump(g);}}
        private async Task PumpControlGesture(PinGesture g)
        {
            if(g.Pumping||!g.Ready)return;g.Pumping=true;
            try
            {
                while(GestureCurrent(g)&&g.Latest!=null)
                {
                    var goal=g.Latest;g.Latest=null;ControlReply reply;
                    try{reply=await _controlEngine.Send(new{Operation="preview",Values=goal});}
                    catch(InvalidOperationException error){if(!GestureCurrent(g))break;g.PreviewError=error.Message;_fighterStatus.Text="Last valid pose retained: "+error.Message;continue;}
                    g.PreviewError=null;
                    if(!GestureCurrent(g))break;PreviewControlPose(g.Model,reply.Pose);PublishPins(reply);
                }
                if(!GestureCurrent(g)){CancelControlGesture();return;}
                if(!g.Released)return;
                var commit=await _controlEngine.Send(new{Operation="commit"});
                if(!GestureCurrent(g)){CancelControlGesture();return;}
                if(NativeControlHash(g.Model,".mdl0")!=g.ModelHash||(g.Clip!=null&&NativeControlHash(g.Clip,".chr0")!=g.ClipHash))throw new InvalidOperationException("Native model or animation content changed during the edit. The stale gesture was discarded.");
                if(commit.Committed)
                {
                    var tracks=commit.Tracks??new ControlTrack[0];var before=NativeControlSnapshot.Capture(g.Clip,tracks.Select(t=>t.Bone));
                    NativeControlSnapshot.Apply(g.Clip,tracks);var after=NativeControlSnapshot.Capture(g.Clip,tracks.Select(t=>t.Bone));
                    StoreControls(g.Key,commit.State);PublishPins(commit);MarkFighterChanged();
                    if(g.Clip==null)modelEditControl1.AnimationAuthoringPose=commit.Pose;
                    modelEditControl1.RecordEditorCommand(()=>RestoreGesture(g,before,g.BeforeState,g.BeforePose),()=>RestoreGesture(g,after,commit.State,commit.Pose));
                }
                if(g.Clip!=null)modelEditControl1.UpdateModel();
                _pinGesture=null;_controlBusy=false;_fighterStatus.Text=g.PreviewError!=null?"Last valid pose retained: "+g.PreviewError:commit.Committed?(g.Clip==null?"Model pose saved. Create Animation From Pose to make native keys.":"Pin edit saved to animation channels. Save Fighter or Export to keep a native file."):"Pin selected.";
            }
            finally{g.Pumping=false;}
        }
        private void RestoreGesture(PinGesture g,NativeControlSnapshot snapshot,string state,float[][] pose)
        {
            if(g.Model==null||!_fighterRoots.Values.Contains(g.Model.RootNode))return;
            snapshot.Restore(g.Clip);if(modelEditControl1.TargetModel!=g.Model)modelEditControl1.TargetModel=g.Model;if(modelEditControl1.SelectedCHR0!=g.Clip)modelEditControl1.SelectedCHR0=g.Clip;modelEditControl1.SetFrame(g.Frame);
            if(g.Clip==null)PreviewControlPose(g.Model,pose);else modelEditControl1.UpdateModel();RestoreControlMetadata(g.Key,state);
        }
        private void CancelControlGesture()
        {
            var g=_pinGesture;if(g==null)return;g.Cancelled=true;_pinGesture=null;_controlBusy=false;
            if(!IsDisposed&&modelEditControl1.TargetModel==g.Model){if(g.Clip!=null)modelEditControl1.UpdateModel();else if(g.BeforePose!=null)PreviewControlPose(g.Model,g.BeforePose);}
        }
        private string NativeControlHash(ResourceNode node,string extension)
        {
            Directory.CreateDirectory(_controlStage);string path=Path.Combine(_controlStage,Guid.NewGuid().ToString("N")+extension);
            try{node.Export(path);return FighterProject.HashFile(path);}finally{if(File.Exists(path))File.Delete(path);}
        }
        private void PreviewControlPose(MDL0Node model,float[][] pose)
        {
            if(model==null||pose==null||model._linker.BoneCache.Length!=pose.Length)return;
            for(int i=0;i<pose.Length;i++){var v=pose[i];var bone=model._linker.BoneCache.Single(b=>b.Name==_controlBoneNames[i]);bone.FrameState=new FrameState(new Vector3(v[0],v[1],v[2]),new Vector3(v[3],v[4],v[5]),new Vector3(v[6],v[7],v[8]));}
            foreach(var root in model.BoneList.OfType<MDL0BoneNode>())root.RecalcFrameState();model.WeightMeshes();modelEditControl1.ModelPanel.Invalidate();
        }
        private Task CreateControlAnimation()=>CreateNamedControlAnimation(null,60);
        private async Task CreateNamedControlAnimation(string requestedName,int samples)
        {
            await LoadControls();var model=(MDL0Node)modelEditControl1.TargetModel;var selected=modelEditControl1.SelectedCHR0;int epoch=_controlEpoch;string key=ControlKey();
            string modelHash=_loadedControlModelHash,clipHash=_loadedControlClipHash;
            string name=requestedName??"NewAnimation";int suffix=2;if(requestedName==null)while(_fighterProject.Animations.Any(a=>Path.GetFileNameWithoutExtension(a.SourcePath)==name))name="NewAnimation"+suffix++;
            var created=await _controlEngine.Send(new{Operation="create-animation",Name=name,SampleCount=samples});
            if(IsDisposed||epoch!=_controlEpoch||modelEditControl1.TargetModel!=model||modelEditControl1.SelectedCHR0!=selected||ControlKey()!=key||NativeControlHash(model,".mdl0")!=modelHash||(selected!=null&&NativeControlHash(selected,".chr0")!=clipHash))throw new InvalidOperationException("The model or source pose changed. The stale animation was discarded.");
            using(var clip=new CHR0Node{Name=created.Name,FrameCount=created.SampleCount,Loop=created.Loop})
            {
                NativeControlSnapshot.Apply(clip,created.Tracks);
                var generated=_fighterProject.CreateAnimation(name+".chr0",clip.Export);
                RememberFighterImport();_fighterProject.Animations.Add(generated);var root=FighterRoot(generated);_fighterProject.SelectedAnimation=FighterAnimationKey(generated,root);_fighterProject.Frame=1;RebuildFighterScene();MarkFighterChanged();
                StoreControls(ControlKey(),created.State);InvalidateControls();
            }
        }

        private sealed class NativeControlSnapshot
        {
            private sealed class Entry {internal string Bone;internal ControlKey[][] Keys;internal bool Scale,Rotate,Translate,CompApply,CompParent,Classic;}
            private readonly List<Entry> _entries=new List<Entry>();
            internal string ContentKey=>string.Join("|",_entries.Select(e=>e.Bone+":"+e.Scale+e.Rotate+e.Translate+e.CompApply+e.CompParent+e.Classic+":"+(e.Keys==null?"absent":string.Join(";",e.Keys.Select(c=>string.Join(",",c.Select(k=>k.Frame+":"+k.Value.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+":"+k.Tangent.ToString("R",System.Globalization.CultureInfo.InvariantCulture))))))));
            internal static NativeControlSnapshot Capture(CHR0Node clip,IEnumerable<string> bones)
            {
                var result=new NativeControlSnapshot();foreach(string name in bones.Distinct())
                {
                    var node=clip?.Children.OfType<CHR0EntryNode>().SingleOrDefault(n=>n.Name==name);var e=new Entry{Bone=name};result._entries.Add(e);if(node==null)continue;
                    e.Scale=node.UseModelScale;e.Rotate=node.UseModelRotate;e.Translate=node.UseModelTranslate;e.CompApply=node.ScaleCompensateApply;e.CompParent=node.ScaleCompensateParent;e.Classic=node.ClassicScaleOff;
                    e.Keys=node.KeyArrays.Select(a=>a.Entries.Select(k=>new ControlKey{Frame=k._index,Value=k._value,Tangent=k._tangent}).ToArray()).ToArray();
                }return result;
            }
            internal void Restore(CHR0Node clip)
            {
                if(clip==null)return;foreach(var e in _entries)
                {
                    var node=clip.Children.OfType<CHR0EntryNode>().SingleOrDefault(n=>n.Name==e.Bone);if(e.Keys==null){node?.Remove();continue;}if(node==null)node=clip.CreateEntry(e.Bone);
                    Write(node,e.Keys);node.UseModelScale=e.Scale;node.UseModelRotate=e.Rotate;node.UseModelTranslate=e.Translate;node.ScaleCompensateApply=e.CompApply;node.ScaleCompensateParent=e.CompParent;node.ClassicScaleOff=e.Classic;
                }clip.SignalPropertyChange();
            }
            internal static void Apply(CHR0Node clip,ControlTrack[] tracks)
            {
                if(clip==null)return;foreach(var track in tracks){var node=clip.Children.OfType<CHR0EntryNode>().SingleOrDefault(n=>n.Name==track.Bone)??clip.CreateEntry(track.Bone);Write(node,track.Channels);if(track.Channels.Take(3).Any(c=>c.Length>0))node.UseModelScale=false;if(track.Channels.Skip(3).Take(3).Any(c=>c.Length>0))node.UseModelRotate=false;if(track.Channels.Skip(6).Take(3).Any(c=>c.Length>0))node.UseModelTranslate=false;}PreciseCHR0EntryNode.Promote(clip);clip.SignalPropertyChange();
            }
            internal static void Write(CHR0EntryNode node,ControlKey[][] channels)
            {
                for(int i=0;i<9;i++){var array=node.KeyArrays[i];while(array.KeyCount>0)array.Remove(array.Entries.First()._index);foreach(var k in channels[i])node.SetKeyframe(i,k.Frame,k.Value,true,true)._tangent=k.Tangent;}node.Keyframes.PreserveInterpolation=true;node.SignalPropertyChange();
            }
        }
    }
}
