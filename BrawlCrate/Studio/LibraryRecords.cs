using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace BrawlCrate.Studio
{
    public enum LibrarySourceState { Available, Missing, SourceChanged, NeedsModelAssociation, UnsupportedChannelVersion }

    [XmlRoot("FighterPortAnimationLibrary")]
    public sealed class LibraryCatalogue
    {
        [XmlAttribute] public int Schema { get; set; } = 1;
        public long Generation { get; set; }
        public string LastOperation { get; set; }
        [XmlArrayItem("Mount")] public List<LibraryMount> Mounts { get; set; } = new List<LibraryMount>();
        [XmlArrayItem("Model")] public List<LibraryModel> Models { get; set; } = new List<LibraryModel>();
        [XmlArrayItem("Clip")] public List<LibraryAnimation> Clips { get; set; } = new List<LibraryAnimation>();
        [XmlArrayItem("Location")] public List<LibraryGameLocation> GameLocations { get; set; } = new List<LibraryGameLocation>();
    }

    // L0 mounts ordinary files. Container providers can later resolve the same
    // virtual resource references without changing the owning fighter document.
    public sealed class LibraryMount
    {
        public string Id { get; set; }
        public string Provider { get; set; } = "brawl-file-v1";
        public string Game { get; set; }
        public string GameVersion { get; set; }
        public string Platform { get; set; } = "Wii";
        public string BuildEvidence { get; set; }
        public string LocalPath { get; set; }
        public string ContainerSha256 { get; set; }
        public long Bytes { get; set; }
        public long NativeWriteTicks { get; set; }
        public string GameSourceId { get; set; }
        public string VirtualPath { get; set; }
        public string OriginPath { get; set; }
        public string OriginSha256 { get; set; }
        public long OriginBytes { get; set; }
        public long OriginWriteTicks { get; set; }
        public string OriginPartition { get; set; }
    }

    public sealed class LibraryModel
    {
        public string Id { get; set; }
        public string MountId { get; set; }
        public string Character { get; set; }
        public string Variant { get; set; }
        public string Name { get; set; }
        public string ResourceKey { get; set; }
        public string ResourcePath { get; set; }
        public string ResourceSha256 { get; set; }
        public string SkeletonSha256 { get; set; }
        public string RestInterpretation { get; set; } = "brawl-mdl0-bind-v1";
        public int NativeVersion { get; set; }
        public bool HasGeometry { get; set; } = true;
        public bool Shadow { get; set; }
        [XmlArrayItem("MotionMount")] public List<string> MotionMountIds { get; set; } = new List<string>();
    }

    // Configuration pointers are not decoded/indexed source mounts. Saving a
    // future game's installation/ROM path never claims adapter support.
    public sealed class LibraryGameLocation
    {
        public string Id { get; set; }
        public string Game { get; set; }
        public string GameVersion { get; set; }
        public string Path { get; set; }
        public string Label { get; set; }
        public bool Indexed { get; set; }
        public bool IsDirectory { get; set; }
        public string Provider { get; set; } = "path-only";
        public string ContainerSha256 { get; set; }
        public long Bytes { get; set; }
        public string ToolPath { get; set; }
        public string ToolSha256 { get; set; }
        public string InventorySha256 { get; set; }
        public int NativeFiles { get; set; }
        public string NativeGameId { get; set; }
        public int NativeRevision { get; set; }
        public string NativeRegion { get; set; }
    }

    public sealed class LibraryAnimation
    {
        public string SourceCharacter { get; set; }
        public string UnboundSourceTracks { get; set; }
        public string Id { get; set; }
        public int Revision { get; set; } = 1;
        public string MountId { get; set; }
        public string ModelId { get; set; }
        public string Name { get; set; }
        public string ResourceKey { get; set; }
        public string ResourcePath { get; set; }
        public string ResourceSha256 { get; set; }
        public string Channel { get; set; }
        public int NativeVersion { get; set; }
        public int FrameCount { get; set; }
        public bool Loop { get; set; }
        public int TicksPerSecond { get; set; } = 60;
        public string TimebaseEvidence { get; set; } = "Brawl game ticks; CHR0 does not encode FPS";
        public string Interpolation { get; set; } = "Native CHR0 keys/tangents; held/linear/cubic preserved";
        public string AssociationEvidence { get; set; }
        public string AssociationIssue { get; set; }
        public string Tags { get; set; }
    }

    public sealed class LibraryRegistration
    {
        public string ModelPath { get; set; }
        public string ModelKey { get; set; }
        public string MotionPath { get; set; }
        public string Character { get; set; }
        public string Game { get; set; } = "Brawl";
        public string GameVersion { get; set; } = "Build not supplied";
        public string BuildEvidence { get; set; } = "User supplied; native package has no authoritative game-build identity";
        public bool ApproveAssociation { get; set; }
    }

    // A small pinned snapshot, never the global index. Baked fighters can open
    // offline; new conversions always resolve and verify the live catalogue.
    public sealed class LibraryClipReference
    {
        public string SourceProvider { get; set; }
        public string ExtractorSha256 { get; set; }
        public string ModelPartition { get; set; }
        public string MotionPartition { get; set; }
        public string UnboundSourceTracks { get; set; }
        public LibraryClipReference Copy()=>(LibraryClipReference)MemberwiseClone();
        public string ClipId { get; set; }
        public int Revision { get; set; }
        public string ModelId { get; set; }
        public string Game { get; set; }
        public string GameVersion { get; set; }
        public string Platform { get; set; }
        public string BuildEvidence { get; set; }
        public string Adapter { get; set; }
        public string ModelMountId { get; set; }
        public string MotionMountId { get; set; }
        public string ModelKey { get; set; }
        public string MotionKey { get; set; }
        public string ModelContainerSha256 { get; set; }
        public string MotionContainerSha256 { get; set; }
        public string ModelResourceSha256 { get; set; }
        public string MotionResourceSha256 { get; set; }
        public string SourceRig { get; set; }
        public string RestInterpretation { get; set; }
        public string AssociationEvidence { get; set; }
        public int NativeVersion { get; set; }
        public int FrameCount { get; set; }
        public bool Loop { get; set; }
        public int TicksPerSecond { get; set; }
        public string TimebaseEvidence { get; set; }
        public string Channel { get; set; }
        public string TargetRig { get; set; }
        public string TargetModelContentSha256 { get; set; }
        public string MappingSha256 { get; set; }
        public string InitialBakeSha256 { get; set; }
        public string LastExportSha256 { get; set; }
        public string LastExportFormat { get; set; }
        public string GameSourceId { get; set; }
        public string GameSourceSha256 { get; set; }
        public string ModelVirtualPath { get; set; }
        public string MotionVirtualPath { get; set; }
    }
}
