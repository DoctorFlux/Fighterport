using System.Text.Json.Serialization;

namespace FighterPort.Core;

// Bone indices are scoped to the exact native asset revision. They are neither
// semantic identities across rigs nor replacements for exported native names.
public record RetargetChannelBone(Guid SkeletonRevision,int BoneIndex,string NativeName);
public record RetargetTargetChannel(RetargetChannelBone Bone,string[] Channels);
public record RetargetSourceChannel(int TrackIndex,RetargetChannelBone? Source,string SourceTrackName,string Channel,
    RetargetTargetChannel[] Targets,RetargetBindingEvidence? Binding,string Policy,string Status,string Reason)
{
    // No numerical confidence/error or native-encoding pass is fabricated from
    // successful mapping. A destination writer must supply its own fresh receipt.
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public float? ApproximationError {get;init;}
    public string ErrorStatus {get;init;}="NotMeasured";
    public string DestinationEncoding {get;init;}="NotVerified";
}
public record RetargetTargetOnlyChannel(RetargetChannelBone Target,string Policy,string Reason);
public record ChannelTransferLedger(string Version,Guid SourceClipRevision,Guid SourceSkeletonRevision,Guid TargetSkeletonRevision,
    SourceClipRevision? NativeSource,RetargetSourceChannel[] SourceChannels,RetargetTargetOnlyChannel[] TargetOnly)
{
    public string MappingVersion {get;init;}=RetargetBoneMapping.Version;
    public string BaselineVersion {get;init;}=BaselineMotionTransfer.Version;
    public DerivedReachChannels[] ReachDerivedChannels {get;init;}=[];
    public DerivedSceneBodyChannels? SceneBodyDerivedChannels {get;init;}
    // Null means historical evidence was not recorded; an explicit empty
    // array belongs to a current ledger with no measured derived rotations.
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public RetargetTargetChannel[]? ArticulatedSceneDerivedChannels {get;init;}
}
public record DerivedSceneBodyChannels(RetargetChannelBone SourceBody,RetargetTargetChannel TargetBody,string Policy,string ErrorStatus);
public record DerivedReachChannels(string Role,RetargetChannelBone[] SourcePath,RetargetChannelBone SourceAnchor,
    RetargetTargetChannel[] TargetChannels,string Policy,string ErrorStatus);

// An audit of actual authored source channels and the existing transfer policy,
// not a claim of automatic discovery or a new chain-distribution implementation.
public static class RetargetChannelLedger
{
    public const string Version="source-channel-transfer-ledger-6";
    static readonly string[] Channels=["Scale.X","Scale.Y","Scale.Z","Rotation.X","Rotation.Y","Rotation.Z","Translation.X","Translation.Y","Translation.Z"];
    public static ChannelTransferLedger Build(Scene source,AnimationClip clip,Scene target,RetargetMappingReport mapping,RootMotionPolicy rootPolicy,bool exactCurves=false,BodyReachReceipt? reach=null,SceneBodyFitReceipt? sceneBody=null)
    {
        var bySource=mapping.Bones.ToDictionary(p=>p.Source);var rows=new List<RetargetSourceChannel>();
        RetargetChannelBone Identity(Scene scene,int bone)=>new(scene.SkeletonRevision,bone,scene.Bones[bone].Name);
        RetargetTargetChannel Destination(int bone,string[] channels)=>new(Identity(target,bone),channels);
        bool Ancestor(int ancestor,int bone)
        {
            for(int current=source.Bones[bone].Parent;current>=0;current=source.Bones[current].Parent)if(current==ancestor)return true;
            return false;
        }
        for(int trackIndex=0;trackIndex<clip.Tracks.Length;trackIndex++)
        {
            var track=clip.Tracks[trackIndex];var candidates=source.Bones.Select((b,i)=>(b,i)).Where(p=>p.b.Name==track.Bone).Select(p=>p.i).ToArray();
            int bone=candidates.Length==1?candidates[0]:-1;var identity=bone>=0?Identity(source,bone):null;
            foreach(int channel in Enumerable.Range(0,track.Channels.Length).Where(c=>track.Channels[c] is {Keys.Length:>0}).Concat(track.Rotation is {Keys.Length:>0}?[-1]:[]))
            {
                string name=channel==-1?"Rotation.Quaternion":channel<Channels.Length?Channels[channel]:"Unknown."+channel;
                bool rotation=channel is >=3 and <=5 or -1;
                if(channel>=Channels.Length)
                {
                    rows.Add(new(trackIndex,identity,track.Bone,name,[],null,"Unsupported","Unresolved","The evaluation model has no policy for this authored channel index."));continue;
                }
                if(channel is >=3 and <=5&&track.Rotation is {Keys.Length:>0})
                {
                    if(exactCurves&&bone>=0&&bySource.TryGetValue(bone,out var inactive))
                        rows.Add(new(trackIndex,identity,track.Bone,name,[Destination(inactive.Target,[name])],inactive.Binding,"Exact inactive scalar copy; quaternion precedence","CopiedInactive","The scalar curve is copied independently, but quaternion rotation owns evaluated orientation."));
                    else rows.Add(new(trackIndex,identity,track.Bone,name,[],null,"Quaternion precedence","PreservedSourceOnly","The source evaluator uses the quaternion curve in place of these scalar Euler values; the original curves remain in the authentic source."));
                    continue;
                }
                if(bone<0)
                {
                    rows.Add(new(trackIndex,null,track.Bone,name,[],null,"No unique source identity","Unresolved",candidates.Length==0?"The source track has no bone in its authentic model.":"Duplicate native names need an explicit track-to-bone identity; none is inferred from storage order."));continue;
                }
                if(bySource.TryGetValue(bone,out var pair))
                {
                    string[] targetChannels=exactCurves?[name]:rotation?["Rotation.X","Rotation.Y","Rotation.Z"]:
                        channel<3?[name]:["Translation.X","Translation.Y","Translation.Z"];
                    string policy=exactCurves?"Exact independent curve copy":rotation?"World rest-relative rotation, reconstructed to target local Euler":
                        channel<3?"Authored relative scale on target rest":pair.Role is "RootMotion" or "Hips"?"Actor/body translation policy: "+rootPolicy:"Authored local translation with physical rest-segment ratio";
                    rows.Add(new(trackIndex,identity,track.Bone,name,[Destination(pair.Target,targetChannels)],pair.Binding,policy,"Mapped",
                        exactCurves?"Independent target curve copy; source keys remain immutable.":"Pose-transfer policy only. The pipeline reconstruction receipt rates interpolation separately; body/reach fitting, contact satisfaction and destination-native encoding require their own evidence."));
                }
                else
                {
                    var rotationTargets=!exactCurves&&rotation?mapping.Bones.Where(p=>Ancestor(bone,p.Source)).Select(p=>p.Target).ToHashSet():[];
                    // Root/body translation uses accumulated source world
                    // positions, not only the mapped bone's local channels.
                    // An unmapped ancestor's translation or scale can therefore
                    // affect these outputs even without a direct correspondence.
                    var positionTargets=!exactCurves?mapping.Bones.Where(p=>(p.Role is "RootMotion" or "Hips")&&Ancestor(bone,p.Source)).Select(p=>p.Target).ToHashSet():[];
                    var affected=rotationTargets.Union(positionTargets).Order().Select(t=>Destination(t,
                        (rotationTargets.Contains(t)?new[]{"Rotation.X","Rotation.Y","Rotation.Z"}:[])
                        .Concat(positionTargets.Contains(t)?["Translation.X","Translation.Y","Translation.Z"]:[]).ToArray())).ToArray();
                    string reason=mapping.SourceOnly.FirstOrDefault(p=>p.Bone==bone)?.Reason??"No explicit corresponding target bone.";
                    string policy=rotationTargets.Count>0?"Accumulated ancestor rotation; no reviewed distribution":"Unsupported direct transfer";
                    if(positionTargets.Count>0)policy=(rotationTargets.Count>0?"Accumulated ancestor rotation and ":"")+"Indirect actor/body world-position influence under "+rootPolicy+"; no reviewed distribution";
                    string status=rotationTargets.Count>0?"AncestorInfluenceUnreviewed":positionTargets.Count>0?"AncestorPositionInfluenceUnreviewed":"Unmapped";
                    if(rotationTargets.Count>0)reason+=" This ancestor can influence mapped descendant world rotations; this is not a measured many-to-one transfer or an exclusive chain-distribution policy.";
                    if(positionTargets.Count>0)reason+=" Accumulated ancestor rotation/translation/scale can change actor/body positions and their target translation curves. Actor subtraction, root policy and rest geometry may cancel or transform that influence; neither direct scale/translation preservation nor an effect magnitude has been verified.";
                    if(affected.Length==0)reason+=" The authentic source retains this channel; no direct target-channel preservation is established.";
                    rows.Add(new(trackIndex,identity,track.Bone,name,affected,null,policy,status,reason));
                }
            }
        }
        var derivedBones=reach?.Chains.Where(c=>c.Solved+c.NonConverged>0).SelectMany(c=>c.TargetBones).ToHashSet()??[];
        return new(Version,clip.Revision,source.SkeletonRevision,target.SkeletonRevision,clip.Source,rows.ToArray(),
            mapping.TargetOnly.Select(p=>new RetargetTargetOnlyChannel(Identity(target,p.Bone),derivedBones.Contains(p.Bone)?
                "Target-only native path may receive derived reach rotation; rest translation/scale retained; no direct donor-channel correspondence":
                "Unchanged native local rest retained by pose transfer; no contact-derived keys",p.Reason)).ToArray())
        {ReachDerivedChannels=reach?.Chains.Where(c=>c.Solved+c.NonConverged>0).Select(c=>new DerivedReachChannels(c.Role,c.SourceBones.Select(b=>Identity(source,b)).ToArray(),Identity(source,c.SourceAnchor),
            c.TargetBones.Select(b=>Destination(b,["Rotation.X","Rotation.Y","Rotation.Z"])).ToArray(),
            "Source-relative native-path reach derives target rotations after base channel transfer; translations/scales remain authored. Distal world orientation is retained. Per-source-channel influence and action fidelity are not quantified.",
            "Separate reach/reconstruction/native receipts; no contact or action preservation pass")).ToArray()??[],
         SceneBodyDerivedChannels=sceneBody is {Enabled:true,SourceBody:>=0,TargetBody:>=0}&&
            (sceneBody.Articulated?.ActualBodyTranslationDerived==true||sceneBody.AppliedProposals>0&&(sceneBody.TranslationProposals??sceneBody.AppliedProposals)>0)?new(Identity(source,sceneBody.SourceBody),Destination(sceneBody.TargetBody,["Translation.X","Translation.Y","Translation.Z"]),
            "Source-selected native skin/scene proximity derives body translation; reviewed native rotations may be coordinated in the same objective. Actor motion, native rest/binds and scale are unchanged. Soft observations are not contacts, fixed pins or temporal locks.",
            "Scene-body/reconstruction/native receipts; action, constraints and temporal support remain review"):null,
         ArticulatedSceneDerivedChannels=sceneBody?.Articulated?.ModifiedRotationBones.Select(b=>Destination(b,["Rotation.X","Rotation.Y","Rotation.Z"])).ToArray()??[]};
    }
}
