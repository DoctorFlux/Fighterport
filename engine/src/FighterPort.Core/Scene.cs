using System.Numerics;
using System.Text.Json.Serialization;

namespace FighterPort.Core;

public readonly record struct RationalRate(int Numerator, int Denominator = 1)
{
    [JsonIgnore] public double FramesPerSecond => (double)Numerator / Denominator;
    public void Validate() { if (Numerator <= 0 || Denominator <= 0) throw new InvalidDataException("Invalid timebase."); }
}

public record Transform(Vector3 Scale, Vector3 Rotation, Vector3 Translation)
{
    public static Transform Identity => new(Vector3.One, Vector3.Zero, Vector3.Zero);
    public Matrix4x4 Matrix => Matrix4x4.CreateScale(Scale)
        * Matrix4x4.CreateRotationX(Rotation.X * MathF.PI / 180)
        * Matrix4x4.CreateRotationY(Rotation.Y * MathF.PI / 180)
        * Matrix4x4.CreateRotationZ(Rotation.Z * MathF.PI / 180)
        * Matrix4x4.CreateTranslation(Translation);
    public float[] Channels() => [Scale.X, Scale.Y, Scale.Z, Rotation.X, Rotation.Y, Rotation.Z, Translation.X, Translation.Y, Translation.Z];
    public static Transform From(float[] v) => new(new(v[0],v[1],v[2]),new(v[3],v[4],v[5]),new(v[6],v[7],v[8]));
}

public record Bone(string Name, int Parent, Transform Rest);
public record Vertex(Vector3 Position, Vector3 Normal, Vector2 UV, Vector4 Joints, Vector4 Weights, Vector4 Color)
{
    [JsonIgnore] public SkinInfluence[] Influences {get;init;}=[];
    [JsonPropertyName("Influences"),JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public SkinInfluence[]? InfluenceData {get=>Influences.Length==0?null:Influences;init=>Influences=value??[];}
    [JsonIgnore] public int InfluenceCount => SkinInfluences.Count(Influences);
    public float JointAt(int slot) => SkinInfluences.Joint(Joints, Influences, slot);
    public float WeightAt(int slot) => SkinInfluences.Weight(Weights, Influences, slot);
    public Vector2[] UVSets {get;init;}=[];
    public Vector4 Color1 {get;init;}=Vector4.One;
}
public record NativeDrawCall(string? Material,string? VisibilityBone,bool DefaultVisible,int Pass,int Priority,int MaterialIndex);
public record Mesh(string Name, Vertex[] Vertices, uint[] Indices)
{
    public string? Material { get; init; }
    public string? VisibilityBone { get; init; }
    public NativeDrawCall[] DrawCalls {get;init;}=[];
}
public record TextureImage(string Name,int Width,int Height,byte[] Rgba);
public record TextureBinding(string Texture,int Coordinate,int WrapS,int WrapT,int MinFilter,int MagFilter,float[] Matrix)
{
    public float[] BaseSrt {get;init;}=[1,1,0,0,0];
    public string? Palette {get;init;}
}
public record MaterialProgram(string VertexSource,string FragmentSource,Dictionary<string,Vector4> Colors,TextureBinding[] Bindings,
    int Cull,bool DepthTest,bool DepthWrite,int DepthCompare,bool Blend,int BlendSource,int BlendDestination,bool Subtract,bool LogicOperation,int LogicMode,string Generator)
{
    public string[] Limitations {get;init;}=[];
}
public record Material(string Name,string[] Textures,NativeResourceRef? Native)
{
    public MaterialProgram? Program {get;init;}
}
public record NativeResourceEntry(string Name,NativeResourceRef Resource,string NodeType);
public record TextureSwap(double Frame,string? Texture,string? Palette);
public record LayerTrack(string Target,int Slot,Curve?[] Curves,bool[]? Visibility=null)
{
    public uint ColorMask {get;init;}
    public TextureSwap[] TextureSwaps {get;init;}=[];
    public string? MorphTarget {get;init;}
    public bool NewMorphTarget {get;init;}
    public uint NativeFlags {get;init;}
    public NativeResourceRef? Native {get;init;}
}
public record NativeAnimationLayer(Guid Id,string Name,string Kind,int SampleCount,bool Loop,RationalRate Rate,NativeResourceRef Resource,LayerTrack[] Tracks,string PreviewStatus)
{
    public int MatrixMode {get;init;}
    public MorphBinding[] Morphs {get;init;}=[];
    public Diagnostic[] Diagnostics {get;init;}=[];
    public bool Edited {get;init;}
}
public record Scene(string Name, Bone[] Bones, Mesh[] Meshes)
{
    public Guid SkeletonRevision { get; init; } = Guid.NewGuid();
    public Material[] Materials { get; init; }=[];
    public TextureImage[] Textures { get; init; }=[];
    public void Validate()
    {
        if (Bones.Length>128 || Bones.Length==0&&Meshes.Length!=0) throw new InvalidDataException("A model requires 1..128 bones; an empty scene has no meshes.");
        if (Bones.Select(b=>b.Name).Distinct().Count()!=Bones.Length) throw new InvalidDataException("Duplicate bone identities require an explicit mapping.");
        foreach(var t in Textures)if(t.Width is <=0 or >4096 || t.Height is <=0 or >4096 || t.Rgba.Length!=t.Width*t.Height*4)throw new InvalidDataException("Invalid or over-budget texture buffer.");
        for (int i=0;i<Bones.Length;i++) if (Bones[i].Parent < -1 || Bones[i].Parent >= i) throw new InvalidDataException("Bones must be ordered parent before child.");
        foreach (var mesh in Meshes)
        {
            if (mesh.Indices.Any(x=>x>=mesh.Vertices.Length)) throw new InvalidDataException("Invalid triangle index.");
            foreach(var v in mesh.Vertices)
                if(!SkinInfluences.Valid(v.Joints,v.Weights,v.Influences,Bones.Length))throw new InvalidDataException("Invalid or inconsistent complete skin joint/weight data.");
        }
    }
}

public record SourceBuildIdentity(string Game, string Platform, string Build);
public record SourceFileProof(string OriginalPath,string CachePath,string Sha256);
public enum SourceKind { AssetFolder, GameInstallation, DiscImage, ModBuild }
public record SourceMount(Guid Id, string Path, SourceBuildIdentity Build, string Label = "", SourceKind Kind = SourceKind.AssetFolder)
{
    // Local external tool configuration, never a portable game/build identity.
    public string? DiscToolPath {get;init;}
}
public record NativeResourceRef(string Path, string Entry, string Sha256, string Format, int Version)
{
    public string? ContainerEntry {get;init;}
    public string? OriginalPath {get;init;}
}
public record SourceModelRevision(Guid Id,string Name,NativeResourceRef Resource,Bone[] OriginalSkeleton,string CoordinateConvention);
public record SourceClipRevision(Guid Id, NativeResourceRef Resource, NativeResourceRef? Model, Guid SkeletonRevision, RationalRate Timebase,
    Guid? SourceModelRevisionId=null,string AssociationEvidence="Source association requires review")
{
    public string? NativeName {get;init;}
}
public record TargetProfile(Guid Id, SourceBuildIdentity Build, string Mode)
{
    public string Name {get;init;}="";
    public string? Framework {get;init;}
    public string? FrameworkRevision {get;init;}
    public string? TemplateRevision {get;init;}
    public Guid[] SceneIds {get;init;}=[];
    public string SetupState {get;init;}="Unresolved";
    public string? NativeWorkspace {get;init;}
}
public record OutfitVariant(Guid Id, string Name, Guid ModelRevision, bool IsRecolor);
public record ProtectedInterval(double Start, double End, string Reason);
public enum RootMotionPolicy { Preserve, InPlace, ActorControlled }
public record ActorMotionKey(double Seconds,Vector3 Translation);
public record RigMapping(string SemanticRole, int Bone, string Evidence, bool Reviewed);
public record RigProfile(Guid Revision, Guid SkeletonRevision, RigMapping[] Mapping);
public record RetargetRecipe(Guid SourceRevision, Guid TargetSkeletonRevision, Guid SourceRig, Guid TargetRig, RootMotionPolicy RootPolicy, string SolverVersion)
{
    // Absent fields in historical projects explicitly mean the legacy backend.
    public string Backend {get;init;}=RetargetBackendIdentity.HistoricalId;
    public int BackendSchema {get;init;}=1;
    public RetargetPipelineReceipt? Pipeline {get;init;}
    public GroundContactSettings? GroundDetection {get;init;}
    public RetargetMappingReport? BoneMapping {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ChannelTransferLedger? ChannelLedger {get;init;}
    public FittingSettings Fitting {get;init;}=new();
    public RetargetTiming Timing {get;init;}=new();
    public string? SourceAnalysisKey {get;init;}
    public string AcceptanceProfile {get;init;}=RetargetAcceptance.Version;
    public ContactIntervalEdit[] ContactEdits {get;init;}=[];
    public string[] UntransferredChannels {get;init;}=[];
    public bool ChannelLossesReviewed {get;init;}
    public RetargetSupportBinding[] SupportBindings {get;init;}=[];
    public ActorMotionKey[] ExtractedActorMotion {get;init;}=[];
    public bool IsDraft {get;init;}
    public ContactValidation? Validation {get;init;}
    public ActionFidelityReport? ActionFidelity {get;init;}
    public double SourceSecondsAt(double targetSeconds)=>Timing.TrimStartSeconds+Timing.ExportSpeed*targetSeconds;
}
public record TransitionReference(Guid ClipRevision, Guid SkeletonRevision, double Phase, double Rate, bool Frozen);
public record TransitionRecipe(TransitionReference? Start, TransitionReference? End, int StartSamples, int EndSamples,
    ProtectedInterval[] Protected, RootMotionPolicy RootPolicy, string[] BodyMask, string[] Contacts, string SolverVersion)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public BoundaryFitReceipt? CurveFit {get;init;}
}
public record SlotAssignment(string Id, Guid? Animation, bool Protected, string Requirement)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? EditorOwner {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? ResourceKey {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? ProfileKey {get;init;}
}
public record MoveDefinition(Guid Id, string Name, string TargetScript, ProtectedInterval[] Events);
public record ScriptDocument(Guid Id, string Language, string Text);
public record AudioCue(Guid Id, string Resource, double Frame);
public record VfxBinding(Guid Id, string Resource, string Socket, double Frame);
public record PortraitSet(string? Csp, string? BattlePortrait, string? StockIcon);
public record BuildReceipt(Guid Target, string ToolVersion, string[] Inputs, string[] Outputs, bool ActualGameTested);
public record Diagnostic(string Code, string Message, bool Error = true);
public record OperationResult<T>(T? Value, Diagnostic[] Diagnostics, string[] LostChannels);
