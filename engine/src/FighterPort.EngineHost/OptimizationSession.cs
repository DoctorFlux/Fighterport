using System.Text.Json;
using FighterPort.Core;

sealed record OptimizationRequest(string InputPath,string OutputPath);
sealed record OptimizationRawKey(float Frame,int ValueBits,int TangentBits,int? FrameBits=null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public float NativeTime=>FrameBits is {} bits?BitConverter.Int32BitsToSingle(bits):Frame;
}
sealed record OptimizationRawTrack(string Bone,OptimizationRawKey[][] Channels,int FrameLimit,bool Loop);
sealed record OptimizationBindBits(string Bone,int[] Bits);
sealed record OptimizationGeneratedPairSite(string Bone,int Channel,float Frame);
sealed record OptimizationExplicitBounds(double RotationDegrees,double Translation,double Scale,double GeometryRelative,
    double RotationDerivative,double TranslationDerivative,double ScaleDerivative);
sealed record OptimizationInput(string Name,int SampleCount,bool Loop,bool HighQuality,double LengthScale,
    OptimizationRawTrack[] Tracks,string[] ExactBones,string[] ProtectedBones,int[] MandatoryFrames,
    ProtectedInterval[] ProtectedRanges,bool ProtectionKnown,double PrecisionScale=1,
    long MaximumWork=2_000_000,int MaximumCandidateSpans=100_000,
    OptimizationBindBits[]? BindPoses=null,OptimizationRawTrack[]? CandidateTracks=null,
    bool SmallFile=false,int[]? FitJoinFrames=null,float[]? GeneratedPairFrames=null,OptimizationGeneratedPairSite[]? GeneratedPairSites=null,OptimizationExplicitBounds? ExplicitBounds=null,
    bool EnableSpanRefitting=false);

sealed partial class ControlSession
{
    const long OptimizationFileLimit=256L*1024*1024;

    async Task<object> OptimizeAnimation(Request request)
    {
        var operation=request.Optimization??throw new InvalidDataException("Missing animation optimization input.");
        if(string.IsNullOrEmpty(request.Model))throw new InvalidDataException("Optimization requires the exact native target model snapshot.");
        string inputPath=Path.GetFullPath(operation.InputPath),outputPath=Path.GetFullPath(operation.OutputPath);
        if(!string.Equals(Path.GetExtension(inputPath),".json",StringComparison.OrdinalIgnoreCase)||
            !string.Equals(Path.GetExtension(outputPath),".json",StringComparison.OrdinalIgnoreCase)||
            !string.Equals(Path.GetDirectoryName(inputPath),Path.GetDirectoryName(outputPath),StringComparison.OrdinalIgnoreCase)||
            string.Equals(inputPath,outputPath,StringComparison.OrdinalIgnoreCase)||
            string.Equals(Path.GetFullPath(request.Model),outputPath,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Optimization uses distinct generated JSON files in the same operation directory.");
        if(File.Exists(outputPath))throw new IOException("Optimization output already exists; choose a new generated output path.");
        // Keep the generated input read-only throughout computation/publication.
        // Native CHR0 tracks arrive separately, so orphan tracks never prevent a
        // model-only import and their original signed float bits remain intact.
        using var inputStream=new FileStream(inputPath,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(inputStream.Length is <=0 or >OptimizationFileLimit)throw new NotSupportedException("Optimization input must fit the 256 MiB generated-file budget.");
        var input=await JsonSerializer.DeserializeAsync<OptimizationInput>(inputStream,ProjectStore.Json)
            ??throw new InvalidDataException("Empty animation optimization input.");
        ValidateOptimizationInput(input);
        if(input.HighQuality&&!input.ProtectionKnown)
            throw new InvalidOperationException("Review protected controls for this exact native rig before high-quality optimization.");

        var target=await adapter.ImportModelAsync(request.Model);
        var scene=target.Scene;
        var targetNames=scene.Bones.Select(b=>b.Name).ToArray();
        var targetSet=new HashSet<string>(targetNames,StringComparer.Ordinal);
        var exact=new HashSet<string>(input.ExactBones,StringComparer.Ordinal);
        var protectedBones=new HashSet<string>(input.ProtectedBones,StringComparer.Ordinal);
        if(exact.Concat(protectedBones).Any(name=>!targetSet.Contains(name)))
            throw new InvalidDataException("Optimization protection names do not belong to the exact native target skeleton.");

        ControlState? state=null;
        if(!string.IsNullOrEmpty(request.State))state=JsonSerializer.Deserialize<ControlState>(request.State,ProjectStore.Json)
            ??throw new InvalidDataException("Invalid saved optimization controls.");
        string fingerprint=AnimationControls.Fingerprint(scene);
        if(state is not null)
        {
            if(state.Clip==Guid.Empty||state.Fingerprint!=fingerprint||state.Setup is null||state.Setup.SkeletonFingerprint!=fingerprint||
                state.Contacts is null||state.AuthoringContacts is null)
                throw new InvalidDataException("Saved optimization controls belong to another native rig revision.");
            bool InvalidBone(int bone)=>bone<0||bone>=scene.Bones.Length;
            if(state.Setup.BoneRoles is null||state.Setup.Chains is null||
                state.Setup.BoneRoles.Any(r=>InvalidBone(r.Bone))||
                state.Setup.Chains.Any(c=>InvalidBone(c.Root)||InvalidBone(c.Middle)||InvalidBone(c.Effector))||
                state.Setup.BodyBone is {} body&&InvalidBone(body)||
                (state.Setup.PreservedHelperBones??[]).Any(InvalidBone))
                throw new InvalidDataException("Saved optimization controls contain an invalid native rig binding.");
            void ProtectAncestors(int bone)
            {
                if(InvalidBone(bone))throw new InvalidDataException("A saved optimization constraint references an absent native bone.");
                for(int i=bone;i>=0;i=scene.Bones[i].Parent)protectedBones.Add(scene.Bones[i].Name);
            }
            void ProtectSurface(SkinSurfacePoint point)
            {
                if(!ContactGeometry.Valid(point,scene.Bones.Length))throw new InvalidDataException("A saved optimization constraint has invalid skin dependencies.");
                for(int axis=0;axis<point.InfluenceCount;axis++)if(point.WeightAt(axis)>0)ProtectAncestors((int)point.JointAt(axis));
            }
            void ProtectContact(ContactPin pin)
            {
                if(pin is null)throw new InvalidDataException("A saved optimization contact is missing.");
                ProtectAncestors(pin.Bone);
                foreach(var point in (pin.SurfacePoints??[]).Concat(pin.SurfaceRegion??[])
                    .Concat(pin.Clearance?.Points??[]).Concat(pin.Clearance?.SupportPoints??[])
                    .Concat(pin.Reference?.SurfaceTripod??[]))ProtectSurface(point);
                if(pin.Reference?.Bone is {} support)ProtectAncestors(support);
            }
            foreach(var pin in state.BonePins??[])
            {
                if(pin.SkeletonFingerprint!=fingerprint||pin.Clip is {} owner&&owner!=state.Clip)
                    throw new InvalidDataException("A saved optimization pin belongs to another native rig or clip.");
                ProtectAncestors(pin.Bone);
            }
            foreach(var contact in state.Contacts??[])
            {
                if(contact.SkeletonFingerprint!=fingerprint||contact.Clip!=state.Clip)
                    throw new InvalidDataException("Saved optimization contacts belong to another native rig or clip.");
                foreach(var pin in contact.Pins??[])ProtectContact(pin);
            }
            foreach(var pin in state.AuthoringContacts??[])ProtectContact(pin);
            // These are reviewed semantic bindings, never native-name guesses.
            foreach(var role in state.Setup.BoneRoles.Where(r=>r.Reviewed&&
                r.SemanticRole is "SceneRoot" or "RootMotion" or "RootOrientation" or "BodyOrientation"))
                exact.Add(scene.Bones[role.Bone].Name);
            if(state.Setup.BoneRoles.Any(r=>r.Reviewed&&r.SemanticRole=="Hips"))
                foreach(int bone in AnimationControls.ProtectedMotionBones(scene,state.Setup))exact.Add(scene.Bones[bone].Name);
        }

        BoneTrack[] DecodeTracks(OptimizationRawTrack[] raw)=>raw.Select(track=>new BoneTrack(track.Bone,track.Channels.Select(channel=>channel.Length==0?null:
            new Curve(channel.Select(key=>new Key(key.NativeTime,BitConverter.Int32BitsToSingle(key.ValueBits),
                BitConverter.Int32BitsToSingle(key.TangentBits),BitConverter.Int32BitsToSingle(key.TangentBits))).ToArray(),
                Interpolation.Hermite,track.Loop?track.FrameLimit:null)).ToArray())).ToArray();
        var tracks=DecodeTracks(input.Tracks);
        var clip=new AnimationClip(state?.Clip??Guid.NewGuid(),input.Name,input.SampleCount,new(60,1),input.Loop,tracks);
        // Retries tighten only scalar construction tolerances. Authentic bind
        // scale, protections and independent native acceptance limits never move.
        var frozenBounds=new AnimationOptimizationSettings {Mode=input.SmallFile?AnimationOptimizationMode.SmallFile:AnimationOptimizationMode.HighQuality};
        if(input.ExplicitBounds is {} b)
        {
            double[] bounds=[b.RotationDegrees,b.Translation,b.Scale,b.GeometryRelative,b.RotationDerivative,b.TranslationDerivative,b.ScaleDerivative];
            double[] ceilings=[.1,.002,.0001,.001,.1,.002,.0001];
            if(bounds.Where((v,i)=>!double.IsFinite(v)||v<=0||v>ceilings[i]).Any())throw new InvalidDataException("Export limits must remain within the approved explicit bounds.");
            frozenBounds=frozenBounds with {EulerValueBoundDegrees=b.RotationDegrees,EulerSpeedBoundDegreesPerFrame=b.RotationDerivative,
                TranslationAbsoluteCap=b.Translation,TranslationSpeedAbsoluteCapPerFrame=b.TranslationDerivative,
                ScaleValueBound=b.Scale,ScaleSpeedBoundPerFrame=b.ScaleDerivative};
        }
        double precision=input.PrecisionScale;
        var settings=frozenBounds with
        {
            Mode=input.HighQuality?frozenBounds.Mode:AnimationOptimizationMode.Exact,
            BindBoundsDiagonal=input.LengthScale,
            ScaleValueBound=frozenBounds.ScaleValueBound*precision,
            ScaleSpeedBoundPerFrame=frozenBounds.ScaleSpeedBoundPerFrame*precision,
            EulerValueBoundDegrees=frozenBounds.EulerValueBoundDegrees*precision,
            EulerSpeedBoundDegreesPerFrame=frozenBounds.EulerSpeedBoundDegreesPerFrame*precision,
            TranslationValueFraction=frozenBounds.TranslationValueFraction*precision,
            TranslationSpeedFractionPerFrame=frozenBounds.TranslationSpeedFractionPerFrame*precision,
            TranslationAbsoluteCap=frozenBounds.TranslationAbsoluteCap*precision,
            TranslationSpeedAbsoluteCapPerFrame=frozenBounds.TranslationSpeedAbsoluteCapPerFrame*precision,
            MaximumWork=input.MaximumWork,
            MaximumCandidateSpans=input.MaximumCandidateSpans,
            EnableSpanRefitting=input.HighQuality&&input.EnableSpanRefitting,
            ExactOnlyBones=exact.Order(StringComparer.Ordinal).ToArray(),
            PreserveStructureBones=protectedBones.Order(StringComparer.Ordinal).ToArray(),
            MandatoryFrames=input.MandatoryFrames.Select(f=>(double)f).ToArray(),
            FitJoinFrames=(input.FitJoinFrames??[]).Select(f=>(double)f).ToArray(),
            GeneratedPairFrames=(input.GeneratedPairFrames??[]).Select(f=>(double)f).ToArray(),
            GeneratedPairSites=(input.GeneratedPairSites??[]).Select(site=>new AnimationOptimizationPairSite(site.Bone,site.Channel,site.Frame)).ToArray(),
            ProtectedRanges=input.ProtectedRanges,
            BindPoses=(input.BindPoses??[]).Select(b=>new AnimationOptimizationBindPose(b.Bone,b.Bits.Select(BitConverter.Int32BitsToSingle).ToArray())).ToArray()
        };
        if(settings.BindPoses.Any(b=>!targetSet.Contains(b.Bone)))throw new InvalidDataException("Native bind cleanup contains a bone absent from this exact target.");
        if(input.CandidateTracks is {} proposed)
        {
            var certificate=await Task.Run(()=>AnimationOptimization.CertifyCandidate(clip,DecodeTracks(proposed),settings,targetBoneNames:targetNames));
            string certificatePending=outputPath+"."+Guid.NewGuid().ToString("N")+".pending";
            try
            {
                using(var file=new FileStream(certificatePending,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                using(var bounded=new OptimizationBoundedStream(file,OptimizationFileLimit))
                {
                    await JsonSerializer.SerializeAsync(bounded,new{Certificate=certificate},new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false});
                    bounded.Flush();file.Flush(true);
                }
                File.Move(certificatePending,outputPath);
                return new{Ok=true,OptimizationPath=outputPath,Algorithm=AnimationOptimization.Version};
            }
            finally{if(File.Exists(certificatePending))File.Delete(certificatePending);}
        }
        var result=await Task.Run(()=>AnimationOptimization.Optimize(clip,targetNames,settings));
        var originals=input.Tracks.ToDictionary(t=>t.Bone,StringComparer.Ordinal);
        var outputOptions=new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false};
        string pending=outputPath+"."+Guid.NewGuid().ToString("N")+".pending";
        try
        {
            using(var file=new FileStream(pending,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            using(var bounded=new OptimizationBoundedStream(file,OptimizationFileLimit))
            using(var writer=new Utf8JsonWriter(bounded))
            {
                writer.WriteStartObject();
                void WriteTracks(string property,BoneTrack[] values)
                {
                writer.WriteStartArray(property);
                foreach(var track in values)
                {
                    var native=originals[track.Bone];
                    writer.WriteStartObject();writer.WriteString("Bone",track.Bone);
                    writer.WriteNumber("FrameLimit",native.FrameLimit);writer.WriteBoolean("Loop",native.Loop);
                    writer.WriteStartArray("Channels");
                    foreach(var curve in track.Channels)
                    {
                        writer.WriteStartArray();
                        foreach(var key in curve?.Keys??[])
                        {
                            if(!double.IsFinite(key.Frame)||(double)(float)key.Frame!=key.Frame||key.InTangent!=key.OutTangent||
                                BitConverter.SingleToInt32Bits(key.InTangent)!=BitConverter.SingleToInt32Bits(key.OutTangent))
                                throw new InvalidDataException("Optimization produced an unsupported native key representation.");
                            writer.WriteStartObject();writer.WriteNumber("Frame",(float)key.Frame);
                            writer.WriteNumber("FrameBits",BitConverter.SingleToInt32Bits((float)key.Frame));
                            writer.WriteNumber("ValueBits",BitConverter.SingleToInt32Bits(key.Value));
                            writer.WriteNumber("TangentBits",BitConverter.SingleToInt32Bits(key.OutTangent));writer.WriteEndObject();
                        }
                        writer.WriteEndArray();
                    }
                    writer.WriteEndArray();writer.WriteEndObject();
                }
                writer.WriteEndArray();
                }
                WriteTracks("Tracks",result.Tracks);
                if(result.PreRefitTracks is {} previous)WriteTracks("PreRefitTracks",previous);
                writer.WritePropertyName("RemovedUnmatchedTracks");JsonSerializer.Serialize(writer,result.RemovedUnmatchedTracks,outputOptions);
                writer.WritePropertyName("RemovedBindTracks");JsonSerializer.Serialize(writer,result.RemovedBindTracks,outputOptions);
                writer.WritePropertyName("Report");JsonSerializer.Serialize(writer,result.Report,outputOptions);
                writer.WritePropertyName("ExactBones");JsonSerializer.Serialize(writer,settings.ExactOnlyBones,outputOptions);
                writer.WritePropertyName("ProtectedBones");JsonSerializer.Serialize(writer,settings.PreserveStructureBones,outputOptions);
                writer.WriteNumber("PrecisionScale",input.PrecisionScale);
                writer.WriteBoolean("SmallFile",input.SmallFile);
                writer.WriteBoolean("EnableSpanRefitting",input.HighQuality&&input.EnableSpanRefitting);
                writer.WritePropertyName("GeneratedPairFrames");writer.WriteStartArray();foreach(var frame in input.GeneratedPairFrames??[])writer.WriteNumberValue(frame);writer.WriteEndArray();
                writer.WritePropertyName("GeneratedPairSites");JsonSerializer.Serialize(writer,input.GeneratedPairSites??[],ProjectStore.Json);
                writer.WriteNumber("MaximumWork",input.MaximumWork);
                writer.WriteNumber("MaximumCandidateSpans",input.MaximumCandidateSpans);
                writer.WriteEndObject();writer.Flush();file.Flush(true);
            }
            File.Move(pending,outputPath);
            return new{Ok=true,OptimizationPath=outputPath,Algorithm=AnimationOptimization.Version};
        }
        finally { if(File.Exists(pending))File.Delete(pending); }
    }

    static void ValidateOptimizationInput(OptimizationInput input)
    {
        if(string.IsNullOrEmpty(input.Name)||input.Name.Length>256||input.SampleCount is <1 or >65535||
            input.Tracks is null||input.Tracks.Length>65536||input.Tracks.Any(t=>t is null)||
            input.ExactBones is null||input.ProtectedBones is null||input.MandatoryFrames is null||input.ProtectedRanges is null)
            throw new InvalidDataException("Invalid bounded native optimization input.");
        if(!double.IsFinite(input.LengthScale)||input.LengthScale<0||input.HighQuality&&input.LengthScale<=0)
            throw new InvalidDataException("Optimization requires a finite authentic bind-bounds diagonal; high quality needs a positive length.");
        if(!double.IsFinite(input.PrecisionScale)||input.PrecisionScale<=0||input.PrecisionScale>1||
            input.MaximumWork is <0 or >2_000_000||input.MaximumCandidateSpans is <0 or >100_000)
            throw new InvalidDataException("Optimization precision must be in (0,1] and remaining proof/candidate budgets must stay inside their aggregate limits.");
        if(input.Tracks.Select(t=>t.Bone).Distinct(StringComparer.Ordinal).Count()!=input.Tracks.Length||
            input.Tracks.Any(t=>string.IsNullOrEmpty(t.Bone)||t.Bone.Length>256||t.FrameLimit is <1 or >65536||
                t.Channels is null||t.Channels.Length!=9||t.Channels.Any(c=>c is null)))
            throw new InvalidDataException("Optimization needs exact unique tracks and nine sparse native channel arrays.");
        long keys=0;
        foreach(var track in input.Tracks)foreach(var channel in track.Channels)
        {
            keys+=channel.LongLength;
            if(keys>AnimationOptimization.MaximumInputKeys)throw new NotSupportedException("Optimization exceeds its two-million native-key budget.");
            for(int i=0;i<channel.Length;i++)
            {
                var key=channel[i];
                if(key is null||!float.IsFinite(key.Frame)||!float.IsFinite(key.NativeTime)||key.NativeTime<0||key.NativeTime>=track.FrameLimit||
                    key.FrameBits is {} bits&&BitConverter.SingleToInt32Bits(key.Frame)!=bits&&!(key.Frame==0&&key.NativeTime==0)||
                    i>0&&key.NativeTime<channel[i-1].NativeTime||
                    !float.IsFinite(BitConverter.Int32BitsToSingle(key.ValueBits))||
                    !float.IsFinite(BitConverter.Int32BitsToSingle(key.TangentBits)))
                    throw new InvalidDataException("Optimization native keys must be ordered finite signed-float32 data inside their retained frame limit.");
            }
        }
        if(input.ExactBones.Length>65536||input.ProtectedBones.Length>65536||
            input.ExactBones.Concat(input.ProtectedBones).Any(n=>string.IsNullOrEmpty(n)||n.Length>256)||
            input.MandatoryFrames.Length>200000||input.MandatoryFrames.Any(f=>f<0||f>input.SampleCount)||
            input.FitJoinFrames is {} fitFrames&&(fitFrames.Length>200000||fitFrames.Any(f=>f<0||f>input.SampleCount))||
            input.GeneratedPairFrames is {} generatedFrames&&(generatedFrames.Length>200000||generatedFrames.Any(f=>f<0||f>input.SampleCount))||
            input.GeneratedPairSites is {} generatedSites&&(generatedSites.Length>2000000||generatedSites.Any(site=>site is null||string.IsNullOrEmpty(site.Bone)||site.Bone.Length>256||site.Channel<0||site.Channel>8||site.Frame<0||site.Frame>input.SampleCount))||
            input.ProtectedRanges.Length>65536||input.ProtectedRanges.Any(r=>r is null||!double.IsFinite(r.Start)||
                !double.IsFinite(r.End)||r.Start<0||r.End<r.Start||r.End>input.SampleCount||r.Reason is null||r.Reason.Length>4096))
            throw new InvalidDataException("Invalid bounded optimization frame or bone protections.");
        if(input.BindPoses is {} binds && (binds.Length>65536||binds.Any(b=>b is null||string.IsNullOrEmpty(b.Bone)||b.Bone.Length>256||b.Bits is null||b.Bits.Length!=9
                ||b.Bits.Any(v=>!float.IsFinite(BitConverter.Int32BitsToSingle(v))))||binds.Select(b=>b.Bone).Distinct(StringComparer.Ordinal).Count()!=binds.Length))
            throw new InvalidDataException("Invalid bounded native bind-channel data.");
        if(input.CandidateTracks is {} candidate)
        {
            // Reuse every raw-track validation, without recursive candidate data.
            ValidateOptimizationInput(input with{Tracks=candidate,CandidateTracks=null});
        }
    }

    sealed class OptimizationBoundedStream(Stream target,long limit):Stream
    {
        long written;
        public override bool CanRead=>false;
        public override bool CanSeek=>false;
        public override bool CanWrite=>true;
        public override long Length=>written;
        public override long Position {get=>written;set=>throw new NotSupportedException();}
        public override void Flush()=>target.Flush();
        public override void Write(byte[] buffer,int offset,int count)=>Write(buffer.AsSpan(offset,count));
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if(buffer.Length>limit-written)throw new NotSupportedException("Optimization output exceeds its 256 MiB generated-file budget.");
            target.Write(buffer);written+=buffer.Length;
        }
        public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
    }
}
