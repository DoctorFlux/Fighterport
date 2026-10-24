using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;

internal static class PoseNativeRevalidationChecks
{
    internal sealed record Input(string From,string To,string Clip,int Samples,string SourceProject,string TargetProject,string Native,bool Ledge,bool NativeExact);
    sealed record PreparedSource(FighterProject Project,ControlRigSetup Rig,string Deform);
    public static async Task RunAsync(string directory,string manifestPath,Action<bool,string> check)
    {
        if(OperatingSystem.IsWindows())Process.GetCurrentProcess().PriorityClass=ProcessPriorityClass.BelowNormal;
        Directory.CreateDirectory(directory);manifestPath=Path.GetFullPath(manifestPath);
        var inputs=JsonSerializer.Deserialize<Input[]>(await File.ReadAllTextAsync(manifestPath),ProjectStore.Json)??throw new InvalidDataException("Native revalidation manifest was empty.");
        check(inputs.Length>0&&inputs.All(i=>i.NativeExact)&&inputs.Select(i=>Path.GetFullPath(i.Native)).Distinct(StringComparer.OrdinalIgnoreCase).Count()==inputs.Length,"The revalidation manifest contains distinct previously verified native files, not duplicate output rows");
        var sources=new Dictionary<string,PreparedSource>(StringComparer.OrdinalIgnoreCase);
        var hashes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var oracleDocuments=new Dictionary<string,JsonDocument>(StringComparer.OrdinalIgnoreCase);
        var results=new List<object>();int nativePasses=0,reconstructionPasses=0,reviewRows=0;bool unchanged=true;
        string Hash(string path)
        {
            path=Path.GetFullPath(path);if(!hashes.TryGetValue(path,out string? hash))hashes.Add(path,hash=ProjectStore.Hash(path));return hash;
        }
        bool SamePath(string? a,string b)=>a is not null&&string.Equals(Path.GetFullPath(a),Path.GetFullPath(b),StringComparison.OrdinalIgnoreCase);
        bool Proofs(FighterProject project)
        {
            bool same=true;
            foreach(var resource in ProjectStore.SourceReferences(project).DistinctBy(r=>(Path.GetFullPath(r.Path),r.Sha256)))same&=string.Equals(Hash(resource.Path),resource.Sha256,StringComparison.OrdinalIgnoreCase);
            return same;
        }
        JsonElement Oracle(Input input)
        {
            string path=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(input.Native))!,"retarget-matrix.json");
            if(!oracleDocuments.TryGetValue(path,out var json))oracleDocuments.Add(path,json=JsonDocument.Parse(File.ReadAllText(path)));
            return json.RootElement.GetProperty("Cases").EnumerateArray().Single(c=>c.GetProperty("From").GetString()==input.From&&c.GetProperty("To").GetString()==input.To&&c.GetProperty("Clip").GetString()==input.Clip&&c.TryGetProperty("Native",out var native)&&native.GetString()==Path.GetFileName(input.Native));
        }
        async Task Save(bool complete=false)
        {
            string json=JsonSerializer.Serialize(new
            {
                Pipeline=RetargetPipeline.Version,Reconstruction=PoseMotionReconstruction.Version,Manifest=manifestPath,ManifestSha256=Hash(manifestPath),Complete=complete,Expected=inputs.Length,NativePassed=nativePasses,ReconstructionPassed=reconstructionPasses,Review=reviewRows,UnchangedInputs=unchanged,ProcessorCount=Environment.ProcessorCount,Priority=Process.GetCurrentProcess().PriorityClass.ToString(),Cases=results,
                Claim="Re-rates the current reconstruction receipt against unchanged outputs already freshly decoded and independently checked by the native matrix. No second export/worker invocation, contact/Apply-ready, artistic or game success is inferred."
            },new JsonSerializerOptions(ProjectStore.Json){WriteIndented=true,IgnoreReadOnlyProperties=false});
            // Project defaults intentionally omit read-only properties. Evidence
            // uses anonymous read-only records, so verify it was not erased.
            using(var proof=JsonDocument.Parse(json))if(proof.RootElement.GetProperty("NativePassed").GetInt32()!=nativePasses||proof.RootElement.GetProperty("Cases").GetArrayLength()!=results.Count)throw new InvalidDataException("Incomplete native revalidation evidence serialization.");
            string path=Path.Combine(directory,"native-revalidation.json"),temporary=path+".tmp";
            await File.WriteAllTextAsync(temporary,json);File.Move(temporary,path,true);
        }
        try
        {
            Hash(manifestPath);
            foreach(var input in inputs)
            {
                Console.WriteLine($"TEST: current native receipt / {input.From} -> {input.To} / {input.Clip}");
                string sourcePath=Path.GetFullPath(input.SourceProject),targetPath=Path.GetFullPath(input.TargetProject),nativePath=Path.GetFullPath(input.Native);
                string targetProjectHash=Hash(targetPath),nativeHash=Hash(nativePath);
                if(!sources.TryGetValue(sourcePath,out var source))
                {
                    Hash(sourcePath);var original=await ProjectStore.LoadAsync(sourcePath);source=new(original,BrawlControlProfiles.SetUp(original),AnimationControls.DeformChecksum(original.Scene));sources.Add(sourcePath,source);
                    check(Proofs(original),input.From+": authentic native source files still match their stored immutable proofs");
                }
                var decoded=await ProjectStore.LoadAsync(targetPath);
                if(decoded.Clips.Length!=1)throw new InvalidDataException("Expected one freshly decoded native motion: "+targetPath);
                var actual=decoded.Clips[0];var targetRig=BrawlControlProfiles.SetUp(decoded);string deform=AnimationControls.DeformChecksum(decoded.Scene);
                var sourceClip=source.Project.Clips.Single(c=>c.Source?.NativeName==input.Clip);string sourceKeys=RetargetDependencies.Hash(sourceClip.Tracks);
                bool nativeProof=Proofs(decoded)&&ProjectStore.SourceReferences(decoded).Any(r=>(SamePath(r.Path,nativePath)||SamePath(r.OriginalPath,nativePath))&&string.Equals(r.Sha256,nativeHash,StringComparison.OrdinalIgnoreCase)&&string.Equals(Hash(r.Path),nativeHash,StringComparison.OrdinalIgnoreCase));
                var oracle=Oracle(input);bool independentOracle=oracle.GetProperty("ExactNativeKeys").GetBoolean()&&oracle.GetProperty("NativeBoneError").GetSingle()<.005f&&oracle.GetProperty("NativeSkinError").GetSingle()<.0003f&&oracle.GetProperty("Samples").GetInt32()==input.Samples;
                check(nativeProof&&independentOracle,$"{input.From} -> {input.To} {input.Clip}: decoded target proof matches the actual native content and its independent worker matrix receipt");
                var context=new GroundContactSettings{ExplicitSurface=true,SourceFloor=0,TargetFloor=0,SourceEdge=input.Ledge?new(source.Rig.Axes!.Forward,0):null,TargetEdge=input.Ledge?new(targetRig.Axes!.Forward,0):null};
                var operation=RetargetService.Bake(source.Project,sourceClip,source.Rig,decoded,targetRig,targetRate:actual.Rate,ground:context);
                if(operation.Value is not {} bake)throw new InvalidDataException("Current pose draft failed: "+string.Join("; ",operation.Diagnostics.Select(d=>d.Code+": "+d.Message)));
                var (exact,comparisons,differences)=NativeRetargetMatrix.ExactCurves(bake.Clip,actual,decoded.Scene);
                var probes=new SortedSet<double>{Math.Max(0,actual.PreviewEndFrame)};
                for(double frame=0;frame<actual.SampleCount;frame+=.25)probes.Add(frame);
                foreach(double frame in new[]{.125,.2,1d/3,.375,.618033988749895,.875,13.8107,38.9002,59.9999})if(frame>=0&&frame<actual.SampleCount)probes.Add(frame);
                foreach(double boundary in sourceClip.Tracks.SelectMany(t=>t.Channels.OfType<Curve>()).SelectMany(c=>c.Keys).Select(k=>k.Frame))foreach(double delta in new[]{-.125,0,.125})if(boundary+delta>=0&&boundary+delta<actual.SampleCount)probes.Add(boundary+delta);
                if(bake.Clip.LastKeyFrame>=bake.Clip.SampleCount&&actual.LastKeyFrame>=actual.SampleCount)probes.Add(actual.SampleCount);
                var vertices=decoded.Scene.Meshes.SelectMany(m=>m.Vertices).ToArray();float boneError=0,skinError=0;double worstBone=0,worstSkin=0;
                foreach(double frame in probes)
                {
                    var expected=AnimationService.Evaluate(decoded.Scene,bake.Clip,frame);var fresh=AnimationService.Evaluate(decoded.Scene,actual,frame);
                    for(int bone=0;bone<fresh.World.Length;bone++){float error=Vector3.Distance(expected.World[bone].Translation,fresh.World[bone].Translation);if(error>boneError){boneError=error;worstBone=frame;}}
                    foreach(var vertex in vertices){float error=Vector3.Distance(AnimationService.Skin(vertex,expected),AnimationService.Skin(vertex,fresh));if(error>skinError){skinError=error;worstSkin=frame;}}
                }
                bool timing=actual.SampleCount==input.Samples&&actual.SampleCount==sourceClip.SampleCount&&actual.SampleCount==bake.Clip.SampleCount&&actual.Rate==sourceClip.Rate&&actual.Rate==bake.Clip.Rate&&actual.Loop==sourceClip.Loop&&actual.Loop==bake.Clip.Loop;
                bool native=exact&&timing&&boneError<.005f&&skinError<.0003f;if(native)nativePasses++;
                var reconstruction=bake.Clip.Retarget?.Pipeline?.Reconstruction;
                if(reconstruction?.Passed==true)reconstructionPasses++;
                var validation=RetargetAcceptance.Measure(decoded,targetRig,bake);
                bool review=bake.Contacts.Length==0&&bake.Clip.Retarget is {IsDraft:true}&&operation.Diagnostics.Any(d=>d.Code=="RETARGET_CONTACTS_DISABLED_REVIEW")&&operation.Diagnostics.Any(d=>d.Code=="RETARGET_POSE_REWRITE_REVIEW")&&!RetargetAcceptance.Ready(validation,operation.Diagnostics,operation.LostChannels,true)&&bake.Source.Clip.Id==sourceClip.Id&&bake.Source.Clip.Revision==sourceClip.Revision&&bake.Clip.Retarget.SourceRevision==sourceClip.Revision;
                if(review)reviewRows++;
                bool preserved=AnimationControls.DeformChecksum(source.Project.Scene)==source.Deform&&AnimationControls.DeformChecksum(decoded.Scene)==deform&&RetargetDependencies.Hash(sourceClip.Tracks)==sourceKeys&&RetargetDependencies.Hash(bake.Source.Clip.Tracks)==sourceKeys;
                unchanged&=preserved;
                results.Add(new{input.From,input.To,input.Clip,input.Samples,input.Ledge,Status=native?"native-exact-pose-review-contact-disabled":"native-revalidation-failed",SourceProject=sourcePath,TargetProject=targetPath,TargetProjectSha256=targetProjectHash,Native=nativePath,NativeSha256=nativeHash,NativeProofMatched=nativeProof,IndependentPriorWorkerOracle=independentOracle,ExactNative=exact,NativeKeyComparisons=comparisons,NativeDifferences=differences,Timing=timing,Probes=probes.ToArray(),AllVertices=vertices.Length,NativeBoneError=boneError,WorstBoneFrame=worstBone,NativeSkinError=skinError,WorstSkinFrame=worstSkin,Reconstruction=reconstruction,ContactValidation=validation,ContactCount=bake.Contacts.Length,ApplyReady=false,ReviewVerified=review,ImmutableProofs=preserved,operation.Diagnostics,operation.LostChannels});
                await Save();
                check(native,$"{input.From} -> {input.To} {input.Clip}: latest receipt retains exact native keys/tangents, timing and full-skin fractional agreement ({boneError:G6}/{skinError:G6})");
                check(review&&preserved,$"{input.From} -> {input.To} {input.Clip}: source ancestry/native rigs remain unchanged; suspended contacts and open pose quality prevent Apply");
                Console.WriteLine($"REVIEW: reconstruction {reconstruction?.Passed}, skin {reconstruction?.MaximumSkinError:G6}, rotation {reconstruction?.MaximumRotationRadians:G6}, translation {reconstruction?.MaximumTranslationError:G6}, scale {reconstruction?.MaximumScaleError:G6}; native data agreement is separate.");
            }
            foreach(var proof in hashes){string current=ProjectStore.Hash(proof.Key);unchanged&=string.Equals(current,proof.Value,StringComparison.OrdinalIgnoreCase);}
            await Save(true);
            check(unchanged,"Native receipt revalidation preserves every authentic source, decoded project, native output and manifest file hash");
            check(nativePasses==inputs.Length&&reviewRows==inputs.Length,"All manifest rows have native agreement while remaining explicit contact-suspended pose-review drafts");
        }
        finally{foreach(var document in oracleDocuments.Values)document.Dispose();}
    }
}
