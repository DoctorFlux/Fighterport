using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace BrawlCrate.Studio
{
    /// <summary>A small local project document. Native resource ownership stays in ModelForm.</summary>
    [XmlRoot("FighterPortStudio")]
    public sealed class FighterProject : IDisposable
    {
        public const int CurrentSchemaVersion = 1;
        public const long MaximumAssetBytes = 1024L * 1024 * 1024;
        public const int MaximumAnimations = 256;
        private const long MaximumDocumentBytes = 64L * 1024 * 1024;
        private readonly List<FighterAsset> _stagedAssets = new List<FighterAsset>();
        private string _stageDirectory;
        private bool _disposed;

        [XmlAttribute] public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public string Name { get; set; } = "New Fighter";
        public string TargetGame { get; set; } = "Brawl";
        public FighterAsset Model { get; set; }
        [XmlArrayItem("Animation")] public List<FighterAsset> Animations { get; set; } = new List<FighterAsset>();
        public string ModelNodePath { get; set; }
        public string SelectedAnimation { get; set; }
        public int Frame { get; set; }
        [XmlArrayItem("Controls")] public List<AnimationControlState> AnimationControls {get;set;} = new List<AnimationControlState>();
        [XmlArrayItem("Clip")] public List<WorkspaceClipLink> WorkspaceClips {get;set;} = new List<WorkspaceClipLink>();
        [XmlArrayItem("Rig")]public List<EngineRigReview> RigReviews {get;set;}=new List<EngineRigReview>();
        [XmlArrayItem("Review")]public List<SceneFittingReviewState> SceneFittingReviews {get;set;}=new List<SceneFittingReviewState>();
        [XmlArrayItem("Slot")]public List<NativeAnimationSlot> AnimationSlots {get;set;}=new List<NativeAnimationSlot>();
        [XmlArrayItem("Profile")]public List<NativeGameSlotProfile> GameSlotProfiles {get;set;}=new List<NativeGameSlotProfile>();
        public Retargeting.GroundContactMode GroundMode { get; set; }
        [XmlIgnore] public string ManifestPath { get; private set; }
        [XmlIgnore] public List<string> Warnings { get; } = new List<string>();

        public static FighterProject Create(string name)
        {
            var project = new FighterProject {Name = string.IsNullOrWhiteSpace(name) ? "New Fighter" : name.Trim()};
            project.Validate();
            return project;
        }

        public static FighterProject Open(string manifestPath)
        {
            string path = LocalPath(manifestPath);
            RequireOrdinaryFile(path);
            if (new FileInfo(path).Length > MaximumDocumentBytes)
                throw new InvalidDataException("This fighter project exceeds the 1 MB document limit.");
            FighterProject project;
            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumDocumentBytes,
                IgnoreComments = true
            };
            try
            {
                using (var reader = XmlReader.Create(path, readerSettings))
                    project = (FighterProject) new XmlSerializer(typeof(FighterProject)).Deserialize(reader);
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is XmlException)
            {
                throw new InvalidDataException("This file is not a valid FighterPort Studio project.", exception);
            }

            try
            {
                project.Validate();
                project.ManifestPath = path;
                foreach (FighterAsset asset in project.RegisteredAssets())
                {
                    string savedPath = project.ResolveSavedAsset(asset);
                    RequireOrdinaryFile(savedPath);
                    RequireAssetSize(savedPath);
                    if (!SameHash(HashFile(savedPath), asset.ContentSha256))
                        throw new InvalidDataException("A saved project asset was replaced or damaged: " + Path.GetFileName(savedPath));
                    asset.WorkingPath = project.CopyToStage(savedPath, asset.Id);
                    project._stagedAssets.Add(asset);
                    project.CheckOriginal(asset);
                }
                return project;
            }
            catch
            {
                project.Dispose();
                throw;
            }
        }

        /// <summary>Stage first; the caller registers the asset only after native parsing succeeds.</summary>
        public FighterAsset StageAsset(string sourcePath, string role)
        {
            ThrowIfDisposed();
            if (role != "model" && role != "animation")
                throw new ArgumentException("Asset role must be model or animation.", nameof(role));
            if (role == "animation" && Animations.Count >= MaximumAnimations)
                throw new InvalidOperationException("A project can register at most 256 motion files.");
            string path = LocalPath(sourcePath);
            RequireOrdinaryFile(path);
            RequireAssetSize(path);
            string extension = Path.GetExtension(path).ToLowerInvariant();
            string[] modelExtensions = {".pac", ".pcs", ".brres", ".mdl0"};
            string[] motionExtensions = {".pac", ".pcs", ".brres", ".chr0", ".srt0", ".pat0", ".clr0", ".vis0", ".shp0", ".scn0"};
            if (!(role == "model" ? modelExtensions : motionExtensions).Contains(extension))
                throw new NotSupportedException(role == "model"
                    ? "Open a supported Brawl PAC, PCS, BRRES or MDL0 model."
                    : "Add a supported Brawl motion PAC, PCS, BRRES or native animation file.");
            var asset = new FighterAsset {Id = Guid.NewGuid().ToString("N"), Role = role, SourcePath = path};
            asset.WorkingPath = CopyToStage(path, asset.Id);
            asset.SourceSha256 = asset.ContentSha256 = HashFile(asset.WorkingPath);
            _stagedAssets.Add(asset);
            return asset;
        }

        /// <summary>Own a generated native revision without inventing an external original source.</summary>
        public FighterAsset CreateAnimation(string displayName, Action<string> writeNative)
        {
            ThrowIfDisposed();
            if (Animations.Count >= MaximumAnimations) throw new InvalidOperationException("The animation registration limit is 256.");
            if (string.IsNullOrWhiteSpace(displayName) || Path.GetFileName(displayName) != displayName ||
                displayName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Path.GetExtension(displayName) != ".chr0")
                throw new ArgumentException("Choose a plain CHR0 name for the generated animation.");
            EnsureStageDirectory();
            var asset = new FighterAsset {Id = Guid.NewGuid().ToString("N"), Role = "animation", Generated = true, SourcePath = displayName};
            asset.WorkingPath = Path.Combine(_stageDirectory, asset.Id + ".chr0");
            _stagedAssets.Add(asset);
            writeNative(asset.WorkingPath);
            RequireOrdinaryFile(asset.WorkingPath); RequireAssetSize(asset.WorkingPath);
            asset.SourceSha256 = asset.ContentSha256 = HashFile(asset.WorkingPath);
            return asset;
        }

        /// <summary>Assets are immutable by content hash; descriptor replacement is atomic.</summary>
        public void Save(string manifestPath)
        {
            ThrowIfDisposed();
            Validate();
            string path = LocalPath(manifestPath);
            if (!string.Equals(Path.GetExtension(path), ".fpstudio", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Save fighter projects with the .fpstudio extension.");
            CheckSourceDestination(path);
            RequireNoLinksInPath(path);
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string dataFolderName = Path.GetFileName(path) + ".assets";
            string dataDirectory = Path.Combine(directory, dataFolderName);
            RequireNoLinksInPath(dataDirectory);
            Directory.CreateDirectory(dataDirectory);

            FighterAsset[] assets = RegisteredAssets().ToArray();
            var previousIdentities = assets.ToDictionary(asset => asset,
                asset => Tuple.Create(asset.ContentSha256, asset.RelativePath));
            string temporary = TemporarySibling(path);
            try
            {
                foreach (FighterAsset asset in assets)
                {
                    RequireOrdinaryFile(asset.WorkingPath);
                    RequireAssetSize(asset.WorkingPath);
                    string hash = HashFile(asset.WorkingPath);
                    string filename = hash + Path.GetExtension(asset.WorkingPath).ToLowerInvariant();
                    string destination = Path.Combine(dataDirectory, filename);
                    if (File.Exists(destination))
                    {
                        RequireOrdinaryFile(destination);
                        if (!SameHash(HashFile(destination), hash))
                            throw new InvalidDataException("A project asset with an existing content identity was modified.");
                    }
                    else
                    {
                        CopyNewFile(asset.WorkingPath, destination);
                        if (!SameHash(HashFile(destination), hash))
                            throw new IOException("A project asset could not be copied reliably.");
                    }
                    asset.ContentSha256 = hash;
                    asset.RelativePath = dataFolderName + "/" + filename;
                }

                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (var writer = XmlWriter.Create(stream, new XmlWriterSettings {Indent = true, Encoding = new UTF8Encoding(false), CloseOutput = false}))
                        new XmlSerializer(typeof(FighterProject)).Serialize(writer, this);
                    if (stream.Length > MaximumDocumentBytes)
                        throw new InvalidDataException("This project exceeds the 1 MB document limit.");
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                ManifestPath = path;
            }
            catch
            {
                // These identities describe the last committed descriptor. A failed
                // save must not make modified staging bytes look committed/clean.
                foreach (var previous in previousIdentities)
                {
                    previous.Key.ContentSha256 = previous.Value.Item1;
                    previous.Key.RelativePath = previous.Value.Item2;
                }
                throw;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        // A workspace snapshot must not redirect the ordinary Save command or
        // make the live .fpstudio document look committed to a temporary folder.
        internal void SaveCopy(string path)
        {
            string previous=ManifestPath;
            var identities=RegisteredAssets().ToDictionary(a=>a,a=>Tuple.Create(a.ContentSha256,a.RelativePath));
            try{Save(path);}
            finally{ManifestPath=previous;foreach(var item in identities){item.Key.ContentSha256=item.Value.Item1;item.Key.RelativePath=item.Value.Item2;}}
        }
        internal string DocumentIdentity()
        {
            using(var stream=new MemoryStream())using(var hash=SHA256.Create())
            {
                new XmlSerializer(typeof(FighterProject)).Serialize(stream,this);
                return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","");
            }
        }

        public void ProtectOutputPath(string outputPath) => ProtectOutputPath(outputPath, false);

        internal void ProtectOutputPath(string outputPath, bool atomicReplacement)
        {
            ThrowIfDisposed();
            string path = LocalPath(outputPath);
            RequireNoLinksInPath(path);
            CheckSourceDestination(path);
            if (SamePath(path, ManifestPath) || IsWithin(path, _stageDirectory) ||
                (!string.IsNullOrEmpty(ManifestPath) && IsWithin(path, ManifestPath + ".assets")))
                throw new InvalidOperationException("Choose a new native output outside the project's saved assets.");
            // A new filename also protects originals reachable through hard-link aliases.
            if ((!atomicReplacement && File.Exists(path)) || Directory.Exists(path))
                throw new InvalidOperationException("Native export needs a new filename. The existing file has been preserved.");
        }

        public IEnumerable<FighterAsset> RegisteredAssets()
        {
            if (Model != null) yield return Model;
            if (Animations != null)
                foreach (FighterAsset asset in Animations) yield return asset;
        }

        public FighterAsset CreateLibraryRevision(LibrarySourceLease source, Retargeting.RetargetResult result)=>CreateLibraryRevision(source,result,null);
        public FighterAsset CreateLibraryRevision(LibrarySourceLease source, Retargeting.RetargetResult result,NativeTimingFit timing)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            return CreateLibraryRevision(source.Reference,source.Animation.Name,source.ProvenanceModelPath??source.ModelPath,result,timing);
        }
        internal FighterAsset CreateLibraryRevision(LibraryClipReference source,string sourceName,string sourceModelPath,Retargeting.RetargetResult result,NativeTimingFit timing)
        {
            if(source==null||result==null)throw new ArgumentNullException(source==null?nameof(source):nameof(result));
            timing?.Validate(source.FrameCount);
            if(Model==null || source.SourceRig!=result.SourceRig ||
                result.Animation.FrameCount!=(timing?.Frames??source.FrameCount) || result.Animation.Loop!=(timing==null?source.Loop:false))
                throw new InvalidDataException("The source-bound bake does not match this source or target project.");
            string safe=new string(sourceName.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c).ToArray());
            var generated=CreateAnimation(safe+" (library).chr0",result.Animation.ExportChecked);
            var map=result.Plan.Mapping.Select(m=>new RetargetBoneBinding{Role=m.Role,Source=m.Source,Target=m.Target}).ToList();
            var reference=source.Copy();
            reference.TargetRig=result.TargetRig;reference.TargetModelContentSha256=HashFile(Model.WorkingPath);
            using(var sha=SHA256.Create())reference.MappingSha256=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                string.Join("\n",map.Select(m=>m.Role+":"+m.Source+":"+m.Target))))).Replace("-","").ToLowerInvariant();
            reference.InitialBakeSha256=generated.ContentSha256;
            generated.RigIdentity=result.TargetRig;
            generated.Retarget=new RetargetProvenance{Timing=timing,Algorithm=Retargeting.HumanoidRetargeter.Algorithm,MotionControlPolicy=Retargeting.HumanoidRetargeter.MotionControlPolicy,SourceModelPath=sourceModelPath,
                SourceModelSha256=reference.ModelContainerSha256,SourceRig=result.SourceRig,SourceAnimationId=reference.ClipId,
                SourceClip=sourceName+"|"+reference.MotionKey,FromProfile=result.Plan.Source.Profile,ToProfile=result.Plan.Target.Profile,
                LegScale=result.Plan.LegScale,ArmScale=result.Plan.ArmScale,MaximumLimbResidual=result.MaximumLimbResidual,ClampedLimbSamples=result.ClampedLimbSamples,
                GroundMode=GroundMode,FloorContactsEnabled=result.Contacts.FloorEnabled,SuspendedSupportEnabled=result.Contacts.Suspended!=null,
                PlantedFootSamples=result.Contacts.PlantedFootSamples,PlantedHandSamples=result.Contacts.PlantedHandSamples,PlantedBodySamples=result.Contacts.PlantedBodySamples,
                LockedContactSamples=result.Contacts.LockedSamples,MaximumContactResidual=result.MaximumContactResidual,MaximumPelvisCorrection=result.MaximumPelvisCorrection,
                IgnoredSourceBones=result.IgnoredSourceBones,Mapping=map,Library=reference};
            generated.Retarget.RecordKeyframeEvidence(result);
            generated.Retarget.OriginalSupport=NativeSupportEvidence.Capture(result,generated.ContentSha256);
            return generated;
        }

        private void Validate()
        {
            NativeAnimationSlot.Validate(AnimationSlots);
            if(GameSlotProfiles==null||GameSlotProfiles.Count>16||GameSlotProfiles.Any(p=>p==null)||GameSlotProfiles.Select(p=>p.Id).Distinct().Count()!=GameSlotProfiles.Count)throw new InvalidDataException("Invalid native motion slot profiles.");foreach(var profile in GameSlotProfiles){profile.Validate();if(!Animations.Any(a=>a.Id==profile.SourceAsset))throw new InvalidDataException("A slot profile lost its exact source registration.");}if(AnimationSlots.Any(s=>s.ProfileId!=null&&!GameSlotProfiles.Any(p=>p.Id==s.ProfileId&&p.Entries.Any(e=>e.Entry==s.NativeEntry))))throw new InvalidDataException("A native slot references another template inventory.");
            if(SceneFittingReviews==null||SceneFittingReviews.Count>64||SceneFittingReviews.Any(r=>r==null||string.IsNullOrEmpty(r.Key)||r.Key.Length>32768||string.IsNullOrEmpty(r.Options)||r.Options.Length>32768||(r.Receipt?.Length??0)>8*1024*1024||r.Source==null||!ValidHash(r.TargetFingerprint)||(r.ContextPath?.Length??0)>32767||(r.ContextClip?.Length??0)>128||(r.ContextHash!=null&&!ValidHash(r.ContextHash)))||SceneFittingReviews.Select(r=>r.Key).Distinct().Count()!=SceneFittingReviews.Count||SceneFittingReviews.Sum(r=>(long)(r.Receipt?.Length??0))>32L*1024*1024)throw new InvalidDataException("Invalid advanced fitting review metadata.");
            if(RigReviews==null||RigReviews.Count>256||RigReviews.Any(r=>r==null||!ValidHash(r.Fingerprint)||r.Roles==null||r.Bones==null||r.Roles.Length!=r.Bones.Length||r.Roles.Length>65536||r.Roles.Distinct().Count()!=r.Roles.Length||r.Roles.Concat(r.Bones).Any(s=>string.IsNullOrWhiteSpace(s)||s.Length>256)||string.IsNullOrWhiteSpace(r.Evidence)||r.Evidence.Length>32768||new[]{r.Right,r.Up,r.Forward}.Any(a=>a==null||a.Length!=3||a.Any(v=>!Retargeting.RigMath.Finite(v)))))throw new InvalidDataException("Invalid reviewed native rig bindings/coordinates.");
            if(AnimationControls==null||AnimationControls.Count>4096||AnimationControls.Any(s=>s==null||string.IsNullOrEmpty(s.Key)||s.Key.Length>32768||string.IsNullOrEmpty(s.Json)||s.Json.Length>8*1024*1024)||AnimationControls.Select(s=>s.Key).Distinct().Count()!=AnimationControls.Count||AnimationControls.Sum(s=>(long)s.Json.Length)>32L*1024*1024)throw new InvalidDataException("Invalid or excessive editor control metadata.");
            if (SchemaVersion != CurrentSchemaVersion)
                throw new InvalidDataException("Unsupported fighter project version: " + SchemaVersion + ".");
            if (string.IsNullOrWhiteSpace(Name) || Name.Length > 200)
                throw new InvalidDataException("A fighter name must contain 1–200 characters.");
            if (TargetGame != "Brawl")
                throw new NotSupportedException("This milestone supports the Brawl / Project M / Project+ target profile.");
            if (Animations == null || Animations.Count > MaximumAnimations)
                throw new InvalidDataException("The motion file registration limit is 256.");
            if (!Enum.IsDefined(typeof(Retargeting.GroundContactMode),GroundMode) || Frame < 0 || Frame > 10000000 || (SelectedAnimation?.Length ?? 0) > 16384 || (ModelNodePath?.Length ?? 0) > 8192)
                throw new InvalidDataException("The saved preview selection is invalid.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach(var asset in RegisteredAssets())
            {
                if(asset.ClipTransitionFits==null||asset.ClipTransitionFits.Count>4096||asset.ClipTransitionFits.Any(f=>f==null||f.Fit==null||string.IsNullOrEmpty(f.Entry)||f.Entry.Length>16384||f.Entry.Split('/')[0]!="root"||f.Entry.Split('/').Skip(1).Any(part=>!int.TryParse(part,out int index)||index<0||part!=index.ToString(System.Globalization.CultureInfo.InvariantCulture)))||asset.ClipTransitionFits.Select(f=>f.Entry).Distinct(StringComparer.Ordinal).Count()!=asset.ClipTransitionFits.Count)throw new InvalidDataException("Invalid native clip fitting identities.");
                foreach(var record in asset.ClipTransitionFits)
                {
                    record.Fit.Validate();var original=record.Fit.Inputs.Single(i=>i.Role=="source");
                    if(asset.Role!="animation"||original.AssetId!=asset.Id||original.Entry!=asset.Id+"|@"+record.Entry)throw new InvalidDataException("A fitting receipt belongs to another native animation.");
                }
            }
            if(Animations.SelectMany(a=>a.TransitionRecipes()).Sum(f=>(long)(f.Inputs?.Sum(i=>i?.Snapshot?.Length??0)??0))>32L*1024*1024)throw new InvalidDataException("Frozen transition inputs exceed the project review budget.");
            if(Animations.Sum(a=>(long)(a.ContactBake?.BaseSnapshot?.Length??0))>32L*1024*1024)throw new InvalidDataException("Frozen contact originals exceed the project review budget.");
            if(Animations.SelectMany(a=>a.ReviewedContactRecipes()).Sum(r=>(long)(r.BaseSnapshot?.Length??0)+(r.Context?.Length??0)+(r.OriginalState?.Length??0)+(r.Receipt?.Length??0))>32L*1024*1024)
                throw new InvalidDataException("Complete reviewed contact context exceeds the project review budget.");
            if(Animations.Sum(a=>(long)(a.Retarget?.KeyframeReceipt?.Length??0))>32L*1024*1024)throw new InvalidDataException("Source-key comparison receipts exceed the project review budget.");
            if(Animations.Sum(a=>(long)(a.Retarget?.OriginalSupport?.Json?.Length??0)+(a.Retarget?.OriginalSupport?.CanonicalHeader?.Length??0))>32L*1024*1024)
                throw new InvalidDataException("Complete original support evidence exceeds the project review budget. Keep fewer prepared revisions per project; support timelines are never truncated.");
            foreach (FighterAsset asset in RegisteredAssets())
            {
                if (asset == null || !Guid.TryParseExact(asset.Id, "N", out _) || !ids.Add(asset.Id))
                    throw new InvalidDataException("The project contains an invalid or duplicate asset identity.");
                asset.TransitionFit?.Validate();
                asset.ContactBake?.Validate();
                asset.ReviewedContacts?.Validate();
                if(asset.ClipReviewedContacts==null||asset.ClipReviewedContacts.Count>20000||asset.ClipReviewedContacts.Any(c=>c==null||string.IsNullOrEmpty(c.Entry)||c.Entry.Length>32768||c.Recipe==null)||
                    asset.ClipReviewedContacts.Select(c=>c.Entry).Distinct(StringComparer.Ordinal).Count()!=asset.ClipReviewedContacts.Count)
                    throw new InvalidDataException("Invalid reviewed contact member identities.");
                foreach(var contact in asset.ClipReviewedContacts)contact.Recipe.Validate();
                if (asset.Role != (ReferenceEquals(asset, Model) ? "model" : "animation"))
                    throw new InvalidDataException("An asset has the wrong model or animation role.");
                if (!ValidHash(asset.SourceSha256) || !ValidHash(asset.ContentSha256) ||
                    string.IsNullOrWhiteSpace(asset.SourcePath) || asset.SourcePath.Length > 32767 || (asset.RigIdentity?.Length ?? 0) > 256)
                    throw new InvalidDataException("An asset source or content identity is invalid.");
                if (asset.Generated)
                {
                    if (asset.Role != "animation" || Path.GetFileName(asset.SourcePath) != asset.SourcePath ||
                        asset.SourcePath.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Path.GetExtension(asset.SourcePath) != ".chr0")
                        throw new InvalidDataException("A generated animation needs a plain native filename.");
                }
                else LocalPath(asset.SourcePath);
                if (asset.Retarget != null)
                {
                    RetargetProvenance p = asset.Retarget;
                    p.ValidateKeyframeEvidence();
                    if(p.Timing!=null){if(p.Library==null)throw new InvalidDataException("A timing fit requires its authentic library source.");p.Timing.Validate(p.Library.FrameCount);}
                    bool engine=p.Algorithm=="native-body-surface-96.2";
                    if(p.MotionControlPolicy!=null && p.MotionControlPolicy!=Retargeting.HumanoidRetargeter.MotionControlPolicy)throw new InvalidDataException("Unknown saved motion-control policy.");
                    if(p.IgnoredSourceBones==null||p.IgnoredSourceBones.Length>65536||p.IgnoredSourceBones.Any(n=>string.IsNullOrEmpty(n)||n.Length>256))throw new InvalidDataException("Invalid untransferred source-track report.");
                    if(engine&&(!p.ReviewDraft||string.IsNullOrEmpty(p.EngineReceipt)||p.EngineReceipt.Length>8*1024*1024))throw new InvalidDataException("Owned-engine drafts require their unresolved validation receipt.");
                    if (!Enum.IsDefined(typeof(Retargeting.GroundContactMode),p.GroundMode) || !asset.Generated || (!engine && p.Algorithm != Retargeting.HumanoidRetargeter.Algorithm && p.Algorithm != "bind-world-fk-body-contact-ik-v9" && p.Algorithm != "bind-world-fk-body-contact-ik-v8" && p.Algorithm != "bind-world-fk-body-contact-ik-v7" && p.Algorithm != "bind-world-fk-body-contact-ik-v6" && p.Algorithm != "bind-world-fk-body-contact-ik-v5" && p.Algorithm != "bind-world-fk-body-contact-ik-v4" && p.Algorithm != "bind-world-fk-body-contact-ik-v3" && p.Algorithm != "bind-world-fk-contact-ik-v2" && p.Algorithm != "bind-world-fk-two-bone-ik-v1") ||
                        !ValidHash(p.SourceModelSha256) || !ValidHash(p.SourceRig) || !ValidHash(asset.RigIdentity) ||
                        p.Mapping == null || p.Mapping.Count < (engine?1:17) || p.Mapping.Count > (engine?65536:128) ||
                        p.Mapping.Any(m => m == null || string.IsNullOrEmpty(m.Role) || string.IsNullOrEmpty(m.Source) || string.IsNullOrEmpty(m.Target) ||
                            m.Role.Length > 128 || m.Source.Length > 128 || m.Target.Length > 128) ||
                        string.IsNullOrWhiteSpace(p.SourceModelPath) || p.SourceModelPath.Length > 32767 ||
                        string.IsNullOrEmpty(p.SourceClip) || p.SourceClip.Length > 8192 ||
                        !Guid.TryParseExact(p.SourceAnimationId, "N", out _) || (p.Library==null && !Animations.Any(a => a.Id == p.SourceAnimationId)) ||
                        !engine&&(!Retargeting.RigMath.Finite(p.LegScale) || p.LegScale <= 0 || !Retargeting.RigMath.Finite(p.ArmScale) || p.ArmScale <= 0) ||
                        !Retargeting.RigMath.Finite(p.MaximumContactResidual) || p.MaximumContactResidual<0 ||
                        !Retargeting.RigMath.Finite(p.MaximumPelvisCorrection) || p.MaximumPelvisCorrection<0 ||
                        p.PlantedFootSamples<0 || p.PlantedHandSamples<0 || p.PlantedBodySamples<0 || p.LockedContactSamples<0 ||
                        p.PlantedFootSamples>131080 || p.PlantedHandSamples>131080 || p.PlantedBodySamples>524320 || p.LockedContactSamples>786480)
                        throw new InvalidDataException("The retarget provenance is invalid.");
                    p.OriginalSupport?.Validate();
                    if(p.OriginalSupport!=null&&(p.OriginalSupport.SourceRig!=p.SourceRig||p.OriginalSupport.TargetRig!=asset.RigIdentity))
                        throw new InvalidDataException("The original support context lost its exact source/target association.");
                    LocalPath(p.SourceModelPath);
                    if(p.Library!=null)
                    {
                        var link=p.Library;
                        if(!Guid.TryParseExact(link.ClipId,"N",out _) || link.ClipId!=p.SourceAnimationId || link.Revision<1 ||
                            !Guid.TryParseExact(link.ModelId,"N",out _) || !Guid.TryParseExact(link.ModelMountId,"N",out _) || !Guid.TryParseExact(link.MotionMountId,"N",out _) ||
                            !ValidHash(link.ModelContainerSha256)||!ValidHash(link.MotionContainerSha256)||!ValidHash(link.ModelResourceSha256)||!ValidHash(link.MotionResourceSha256)||
                            link.SourceRig!=p.SourceRig||link.TargetRig!=asset.RigIdentity||!ValidHash(link.TargetModelContentSha256)||!ValidHash(link.MappingSha256)||!ValidHash(link.InitialBakeSha256)||
                            string.IsNullOrWhiteSpace(link.Game)||string.IsNullOrWhiteSpace(link.GameVersion)||string.IsNullOrWhiteSpace(link.BuildEvidence)||
                            string.IsNullOrWhiteSpace(link.ModelKey)||string.IsNullOrWhiteSpace(link.MotionKey)||string.IsNullOrWhiteSpace(link.AssociationEvidence)||
                            link.Adapter!=BrawlLibrarySource.Provider||link.Channel!="CHR0 skeletal"||link.TicksPerSecond!=60||link.FrameCount<1||link.FrameCount>4096||
                            (link.NativeVersion!=4&&link.NativeVersion!=5)||(link.LastExportSha256!=null&&!ValidHash(link.LastExportSha256)))
                            throw new InvalidDataException("The immutable library reference is invalid.");
                        if(link.GameSourceId!=null&&(!Guid.TryParseExact(link.GameSourceId,"N",out _)||!ValidHash(link.GameSourceSha256)||
                            string.IsNullOrWhiteSpace(link.ModelVirtualPath)||string.IsNullOrWhiteSpace(link.MotionVirtualPath)))
                            throw new InvalidDataException("The target revision's game-source identity is invalid.");
                        // Optional on older preview revisions; new image bakes
                        // also preserve the exact extractor and partition.
                        if(link.SourceProvider==BrawlDiscImage.Provider&&(link.ExtractorSha256!=null||link.ModelPartition!="DATA"||link.MotionPartition!="DATA"))
                            throw new InvalidDataException("Invalid built-in disc source provenance.");
                        if(link.SourceProvider=="dolphin-brawl-disc-v1"&&(!ValidHash(link.ExtractorSha256)||link.ModelPartition!="DATA"||link.MotionPartition!="DATA"))
                            throw new InvalidDataException("The target revision's disc provenance is invalid.");
                    }
                }
            }
        }

        private string ResolveSavedAsset(FighterAsset asset)
        {
            string relative = asset.RelativePath;
            if (string.IsNullOrWhiteSpace(relative) || relative.Length > 1024 || Path.IsPathRooted(relative) ||
                relative.Contains(":") || relative.Split('/', '\\').Any(part => part == ".." || part == "." || part.Length == 0))
                throw new InvalidDataException("A saved asset path is unsafe.");
            string directory = Path.GetDirectoryName(ManifestPath);
            string dataDirectory = ManifestPath + ".assets";
            string path = Path.GetFullPath(Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithin(path, dataDirectory))
                throw new InvalidDataException("Saved assets must stay inside this fighter project's asset folder.");
            RequireNoLinksInPath(path);
            return path;
        }

        private void CheckOriginal(FighterAsset asset)
        {
            if (asset.Generated) return;
            try
            {
                if (!File.Exists(asset.SourcePath))
                    Warnings.Add("Original source is missing: " + Path.GetFileName(asset.SourcePath) + ". The saved project copy is available.");
                else if ((File.GetAttributes(asset.SourcePath) & FileAttributes.ReparsePoint) != 0 ||
                    new FileInfo(asset.SourcePath).Length > MaximumAssetBytes || !SameHash(HashFile(asset.SourcePath), asset.SourceSha256))
                    Warnings.Add("Original source has changed: " + Path.GetFileName(asset.SourcePath) + ". The saved project copy was preserved.");
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Warnings.Add("Original source cannot be checked: " + Path.GetFileName(asset.SourcePath) + ". The saved project copy is available.");
            }
        }

        private string CopyToStage(string path, string id)
        {
            EnsureStageDirectory();
            string destination = Path.Combine(_stageDirectory, id + Path.GetExtension(path).ToLowerInvariant());
            CopyNewFile(path, destination);
            return destination;
        }

        private void EnsureStageDirectory()
        {
            if (_stageDirectory == null)
            {
                string root = Path.Combine(Path.GetTempPath(), "FighterPortStudioV2");
                RequireNoLinksInPath(root);
                _stageDirectory = Path.Combine(root, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_stageDirectory);
            }
        }

        private void CheckSourceDestination(string path)
        {
            if (_stagedAssets.Concat(RegisteredAssets()).Any(asset => !asset.Generated && SamePath(path, asset.SourcePath)))
                throw new InvalidOperationException("A registered source cannot be overwritten. Choose a new output filename.");
        }

        private static void CopyNewFile(string source, string destination)
        {
            string temporary = TemporarySibling(destination);
            try
            {
                using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    input.CopyTo(output);
                    output.Flush(true);
                }
                File.Move(temporary, destination);
            }
            finally
            {
                // Failed copies never leave an apparently committed hash-named asset.
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        public static int DiagnosticFileHashes;
        public static string HashFile(string path)
        {
            System.Threading.Interlocked.Increment(ref DiagnosticFileHashes);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static string TemporarySibling(string path) => Path.Combine(Path.GetDirectoryName(path),
            ".fp-" + Guid.NewGuid().ToString("N") + ".tmp");

        private static bool ValidHash(string hash) => hash != null && hash.Length == 64 && hash.All(Uri.IsHexDigit);
        private static bool SameHash(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        private static bool SamePath(string left, string right) => !string.IsNullOrEmpty(right) && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        private static bool IsWithin(string path, string directory) => !string.IsNullOrEmpty(directory) && path.StartsWith(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        private static string LocalPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Choose a local file.");
            string full = Path.GetFullPath(path);
            if (full.StartsWith("\\\\", StringComparison.Ordinal) || full.IndexOf(':', 2) >= 0)
                throw new InvalidDataException("This project milestone accepts ordinary local drive paths.");
            return full;
        }

        private static void RequireOrdinaryFile(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("A required project or source file is missing.", path);
            if ((File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                throw new InvalidDataException("Project assets must be ordinary local files.");
        }

        private static void RequireAssetSize(string path)
        {
            long length = new FileInfo(path).Length;
            if (length == 0 || length > MaximumAssetBytes)
                throw new InvalidDataException("Native assets must contain data and be no larger than 1 GB.");
        }

        private static void RequireNoLinksInPath(string path)
        {
            string current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Linked project or output paths are not supported; choose an ordinary local folder.");
                current = Path.GetDirectoryName(current);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FighterProject));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_stageDirectory == null) return;
            try
            {
                RequireNoLinksInPath(_stageDirectory);
                foreach (FighterAsset asset in _stagedAssets)
                    if (IsWithin(asset.WorkingPath, _stageDirectory) && File.Exists(asset.WorkingPath))
                        File.Delete(asset.WorkingPath);
                if (!Directory.EnumerateFileSystemEntries(_stageDirectory).Any()) Directory.Delete(_stageDirectory);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is InvalidDataException)
            {
                // A locked staging file remains recoverable; never remove a broader directory.
            }
        }
    }

    public sealed class FighterAsset
    {
        [XmlAttribute] public string Id { get; set; }
        [XmlAttribute] public string Role { get; set; }
        [XmlAttribute] public bool Generated { get; set; }
        public string SourcePath { get; set; }
        public string SourceSha256 { get; set; }
        public string ContentSha256 { get; set; }
        public string RelativePath { get; set; }
        public string RigIdentity { get; set; }
        public RetargetProvenance Retarget { get; set; }
        public NativeTransitionFit TransitionFit {get;set;}
        [XmlArrayItem("Fit")] public List<NativeClipTransitionFit> ClipTransitionFits {get;set;}=new List<NativeClipTransitionFit>();
        public bool ShouldSerializeClipTransitionFits()=>ClipTransitionFits?.Count>0;
        public IEnumerable<NativeTransitionFit> TransitionRecipes()
        {
            if(TransitionFit!=null)yield return TransitionFit;
            foreach(var item in ClipTransitionFits??new List<NativeClipTransitionFit>())if(item?.Fit!=null)yield return item.Fit;
        }

        public NativeContactBake ContactBake {get;set;}
        public NativeReviewedContacts ReviewedContacts {get;set;}
        [XmlArrayItem("Contact")] public List<NativeClipReviewedContacts> ClipReviewedContacts {get;set;}=new List<NativeClipReviewedContacts>();
        public bool ShouldSerializeClipReviewedContacts()=>ClipReviewedContacts?.Count>0;
        public IEnumerable<NativeReviewedContacts> ReviewedContactRecipes()
        {
            if(ReviewedContacts!=null)yield return ReviewedContacts;
            foreach(var item in ClipReviewedContacts??new List<NativeClipReviewedContacts>())if(item?.Recipe!=null)yield return item.Recipe;
        }
        [XmlIgnore] public string WorkingPath { get; set; }
    }

    public sealed class RetargetProvenance
    {
        public NativeSupportEvidence OriginalSupport {get;set;}
        public Retargeting.RetargetKeyframeMode KeyframeMode {get;set;}
        public string KeyframeReceipt {get;set;}
        public RetargetDenseReferenceStatistics DenseReferenceStatistics {get;set;}
        public NativeTimingFit Timing {get;set;}
        public bool ReviewDraft {get;set;}
        public string EngineReceipt {get;set;}
        public LibraryClipReference Library { get; set; }
        public string Algorithm { get; set; }
        public string MotionControlPolicy { get; set; }
        public string SourceModelPath { get; set; }
        public string SourceModelSha256 { get; set; }
        public string SourceRig { get; set; }
        public string SourceAnimationId { get; set; }
        public string SourceClip { get; set; }
        public string FromProfile { get; set; }
        public string ToProfile { get; set; }
        public float LegScale { get; set; }
        public float ArmScale { get; set; }
        public float MaximumLimbResidual { get; set; }
        public int ClampedLimbSamples { get; set; }
        public bool FloorContactsEnabled { get; set; }
        public bool SuspendedSupportEnabled {get;set;}
        public Retargeting.GroundContactMode GroundMode { get; set; }
        public int PlantedFootSamples { get; set; }
        public int PlantedHandSamples { get; set; }
        public int PlantedBodySamples { get; set; }
        public int LockedContactSamples { get; set; }
        public float MaximumContactResidual { get; set; }
        public float MaximumPelvisCorrection { get; set; }
        [XmlArrayItem("Bone")] public string[] IgnoredSourceBones { get; set; } = new string[0];
        [XmlArrayItem("Bone")] public List<RetargetBoneBinding> Mapping { get; set; } = new List<RetargetBoneBinding>();

        internal void RecordKeyframeEvidence(Retargeting.RetargetResult result)
        {
            KeyframeMode=result.KeyframeMode;
            if(KeyframeMode==Retargeting.RetargetKeyframeMode.Dense)return;
            if(result.KeyframeReport==null)throw new InvalidDataException("A source-key target revision requires its independent native comparison.");
            result.KeyframeReport.Validate();
            KeyframeReceipt=new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=1024*1024,RecursionLimit=40}.Serialize(result.KeyframeReport);
            DenseReferenceStatistics=new RetargetDenseReferenceStatistics {FloorContactsEnabled=FloorContactsEnabled,SuspendedSupportEnabled=SuspendedSupportEnabled,
                PlantedFootSamples=PlantedFootSamples,PlantedHandSamples=PlantedHandSamples,PlantedBodySamples=PlantedBodySamples,LockedContactSamples=LockedContactSamples,
                MaximumContactResidual=MaximumContactResidual,MaximumPelvisCorrection=MaximumPelvisCorrection,MaximumLimbResidual=MaximumLimbResidual,ClampedLimbSamples=ClampedLimbSamples};
            // These statistics belong to the dense reference. Sparse curves must
            // not enter contact recovery as though those paths were validated.
            FloorContactsEnabled=SuspendedSupportEnabled=false;
            PlantedFootSamples=PlantedHandSamples=PlantedBodySamples=LockedContactSamples=ClampedLimbSamples=0;
            MaximumContactResidual=MaximumPelvisCorrection=MaximumLimbResidual=0;
            ValidateKeyframeEvidence();
        }
        public Retargeting.RetargetKeyframeReport ReadKeyframeReport()
        {
            if(string.IsNullOrEmpty(KeyframeReceipt)||KeyframeReceipt.Length>1024*1024)throw new InvalidDataException("Missing or excessive source-key comparison receipt.");
            var report=new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=1024*1024,RecursionLimit=40}.Deserialize<Retargeting.RetargetKeyframeReport>(KeyframeReceipt);
            if(report==null||report.Mode!=KeyframeMode)throw new InvalidDataException("The source-key recipe and comparison receipt disagree.");
            report.Validate();return report;
        }
        internal void ValidateKeyframeEvidence()
        {
            if(!Enum.IsDefined(typeof(Retargeting.RetargetKeyframeMode),KeyframeMode))throw new InvalidDataException("Unknown retarget keyframe representation.");
            if(KeyframeMode==Retargeting.RetargetKeyframeMode.Dense)
            {
                if(KeyframeReceipt!=null||DenseReferenceStatistics!=null)throw new InvalidDataException("A dense retarget cannot claim a source-key comparison.");
                return;
            }
            ReadKeyframeReport();
            if(DenseReferenceStatistics==null||FloorContactsEnabled||SuspendedSupportEnabled||PlantedFootSamples!=0||PlantedHandSamples!=0||
                PlantedBodySamples!=0||LockedContactSamples!=0||MaximumContactResidual!=0||MaximumPelvisCorrection!=0||MaximumLimbResidual!=0||ClampedLimbSamples!=0)
                throw new InvalidDataException("Sparse motion requires separately labelled historical dense statistics; dense contacts cannot qualify changed curves.");
            DenseReferenceStatistics.Validate();
        }
    }
    public sealed class RetargetDenseReferenceStatistics
    {
        public bool FloorContactsEnabled {get;set;}
        public bool SuspendedSupportEnabled {get;set;}
        public int PlantedFootSamples {get;set;}
        public int PlantedHandSamples {get;set;}
        public int PlantedBodySamples {get;set;}
        public int LockedContactSamples {get;set;}
        public float MaximumContactResidual {get;set;}
        public float MaximumPelvisCorrection {get;set;}
        public float MaximumLimbResidual {get;set;}
        public int ClampedLimbSamples {get;set;}
        internal void Validate()
        {
            if(new[]{MaximumContactResidual,MaximumPelvisCorrection,MaximumLimbResidual}.Any(v=>!Retargeting.RigMath.Finite(v)||v<0)||
                PlantedFootSamples<0||PlantedFootSamples>131080||PlantedHandSamples<0||PlantedHandSamples>131080||PlantedBodySamples<0||PlantedBodySamples>524320||
                LockedContactSamples<0||LockedContactSamples>786480||ClampedLimbSamples<0||ClampedLimbSamples>10000000)
                throw new InvalidDataException("Invalid historical dense retarget statistics.");
        }
    }
    public sealed class RetargetBoneBinding
    {
        [XmlAttribute] public string Role { get; set; }
        [XmlAttribute] public string Source { get; set; }
        [XmlAttribute] public string Target { get; set; }
    }
}
