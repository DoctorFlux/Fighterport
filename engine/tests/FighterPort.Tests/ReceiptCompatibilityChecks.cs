using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FighterPort.Core;

// Storage-contract fixtures only. Explicit counts below test serialization,
// not measured optimizer outcomes or native/contact acceptance.
internal static class ReceiptCompatibilityChecks
{
    static readonly string[] refinementFields=["InitialMaximumSkinError","InitialMaximumRotationRadians","InitialMaximumTranslationError",
        "InitialMaximumScaleError","InitialMaximumEulerChannelDegrees","RefinementPasses","RefinedChannels","RefinedOutputSelected",
        "RefinementPolicy","AdaptiveFittingSamples","ReservedWithheldSamples"];
    static readonly string[] sceneFields=["Articulated","TranslationProposals","SourceEdgeAnchorPolicy","FeatureGapMeasured"];

    public static async Task RunAsync(string directory,Action<bool,string> check)
    {
        Directory.CreateDirectory(directory);
        string Json<T>(T value)=>JsonSerializer.Serialize(value,ProjectStore.Json);
        static string Digest(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        string Historical<T>(T value,IEnumerable<string> fields)
        {
            var node=JsonNode.Parse(Json(value))!.AsObject();foreach(string field in fields)node.Remove(field);
            return node.ToJsonString();
        }
        var motion=new PoseMotionReceipt("fixed-pose-shared-derivatives-2",17,23,.03f,.0001f,.04f,.001f,.5f,1.25,1.75,false,false,0)
        {
            MaximumTranslationError=.02f,TranslationBound=.0001f,MaximumScaleError=.001f,ScaleBound=.0001f,
            MaximumEulerChannelDegrees=.4f,EulerChannelBoundDegrees=.057f,WorstChannelFrame=.5,ProbeCoverage="Preserved historical coverage",
            InitialMaximumSkinError=0,InitialMaximumRotationRadians=0,InitialMaximumTranslationError=0,InitialMaximumScaleError=0,
            InitialMaximumEulerChannelDegrees=0,RefinementPasses=0,RefinedChannels=0,RefinedOutputSelected=false,
            RefinementPolicy="Explicit zero/false schema fixture",AdaptiveFittingSamples=0,ReservedWithheldSamples=0
        };
        string oldMotion=Historical(motion,refinementFields);
        var restoredMotion=JsonSerializer.Deserialize<PoseMotionReceipt>(oldMotion,ProjectStore.Json)!;
        check(refinementFields.All(name=>typeof(PoseMotionReceipt).GetProperty(name)!.GetValue(restoredMotion) is null)&&
            Json(restoredMotion)==oldMotion&&Digest(Json(restoredMotion))==Digest(oldMotion),
            "Historical reconstruction receipts preserve exact JSON bytes/hash with eleven absent refinement values remaining unknown, not inserted null evidence");
        check(JsonSerializer.Deserialize<PoseMotionReceipt>(Json(motion),ProjectStore.Json) is
            {InitialMaximumSkinError:0,InitialMaximumRotationRadians:0,InitialMaximumTranslationError:0,InitialMaximumScaleError:0,
             InitialMaximumEulerChannelDegrees:0,RefinementPasses:0,RefinedChannels:0,RefinedOutputSelected:false,AdaptiveFittingSamples:0,ReservedWithheldSamples:0},
            "Explicit zero refinement counts/errors and false selection remain serialized independently of absent historical evidence");

        var articulation=new ArticulatedSceneReceipt("schema-only",0,0,0,3,0,0,0,0,[]);
        var scene=new SceneBodyFitReceipt("native-soft-scene-body-7",true,12,3,9,0,0,.1f,.2f,.1f,2.25,0)
        {SourceBody=1,TargetBody=1,VolumePolicy=BodyVolumePolicy.ReportOverlap,Articulated=articulation,TranslationProposals=0,
            SourceEdgeAnchorPolicy="Explicit saved policy",FeatureGapMeasured=false};
        string oldScene=Historical(scene,sceneFields);
        var restoredScene=JsonSerializer.Deserialize<SceneBodyFitReceipt>(oldScene,ProjectStore.Json)!;
        check(sceneFields.All(name=>typeof(SceneBodyFitReceipt).GetProperty(name)!.GetValue(restoredScene) is null)&&
            Json(restoredScene)==oldScene&&Digest(Json(restoredScene))==Digest(oldScene),
            "Historical scene receipts preserve exact JSON bytes/hash with all four absent coordinated-fit fields remaining unknown");
        check(JsonSerializer.Deserialize<SceneBodyFitReceipt>(Json(scene),ProjectStore.Json) is
            {Articulated:not null,TranslationProposals:0,SourceEdgeAnchorPolicy:"Explicit saved policy",FeatureGapMeasured:false},
            "Explicit zero translation proposals, false feature measurement and present coordinated schema data survive receipt round trips");

        var project=ProceduralFixture.Create();var rig=AnimationControls.SetUp(project.Scene);
        var clip=project.Clips[0];int bone=Array.FindIndex(project.Scene.Bones,b=>b.Name==clip.Tracks[0].Bone);
        var identity=new RetargetChannelBone(project.Scene.SkeletonRevision,bone,project.Scene.Bones[bone].Name);
        var ledger=new ChannelTransferLedger("source-channel-transfer-ledger-4",clip.Revision,project.Scene.SkeletonRevision,
            project.Scene.SkeletonRevision,clip.Source,
            [new(0,identity,identity.NativeName,"Rotation.X",[new(identity,["Rotation.X"])],null,"Preserved schema fixture","Mapped","No new measured fidelity")],[])
            {ArticulatedSceneDerivedChannels=[]};
        string oldLedger=Historical(ledger,["ArticulatedSceneDerivedChannels"]);
        var restoredLedger=JsonSerializer.Deserialize<ChannelTransferLedger>(oldLedger,ProjectStore.Json)!;
        check(restoredLedger.ArticulatedSceneDerivedChannels is null&&Json(restoredLedger)==oldLedger&&Digest(Json(restoredLedger))==Digest(oldLedger),
            "A historical channel ledger has absent derived-rotation evidence and preserves its exact JSON/source-channel hash instead of acquiring an invented empty array");
        check(JsonSerializer.Deserialize<ChannelTransferLedger>(Json(ledger),ProjectStore.Json) is{ArticulatedSceneDerivedChannels.Length:0}&&
            JsonNode.Parse(Json(ledger))!.AsObject().ContainsKey("ArticulatedSceneDerivedChannels"),
            "An explicitly recorded current empty derived-channel array remains distinguishable from absent historical evidence");

        var recipe=new RetargetRecipe(clip.Revision,project.Scene.SkeletonRevision,rig.Id,rig.Id,RootMotionPolicy.Preserve,"historical-storage-fixture")
        {IsDraft=true,ChannelLedger=restoredLedger,
            Pipeline=new(RetargetBackendIdentity.CurrentId,2,[],[],["Unvalidated storage fixture"]){Reconstruction=restoredMotion,SceneBody=restoredScene}};
        var source=new SourceAssetSnapshot(Guid.NewGuid(),project.Scene,clip with{Retarget=recipe},rig,project.SourceModels)
            {Layers=project.NativeLayers,Resources=project.NativeResources};
        var owned=project with{Clips=[project.Clips[1] with{Retarget=recipe,SourceSnapshot=source.Id}],LibrarySources=[source]};
        string recipeJson=Json(recipe),ancestryHash=RetargetDependencies.Hash(owned.LibrarySources),contentHash=RetargetDependencies.Hash(owned);
        var inline=ProjectStore.ReadJson(Json(owned));
        check(Json(inline.Clips[0].Retarget)==recipeJson&&Json(inline.LibrarySources[0].Clip.Retarget)==recipeJson&&
            RetargetDependencies.Hash(inline.LibrarySources)==ancestryHash&&RetargetDependencies.Hash(inline)==contentHash,
            "Legacy inline project loading preserves historical receipt JSON and complete authentic source/owned-project proof hashes");
        string first=Path.Combine(directory,"historical-receipts.fighterport"),second=Path.Combine(directory,"historical-receipts-resaved.fighterport");
        await ProjectStore.SaveAsync(owned,first);var reopened=await ProjectStore.LoadAsync(first);await ProjectStore.SaveAsync(reopened,second);
        check(Json(reopened.Clips[0].Retarget)==recipeJson&&Json(reopened.LibrarySources[0].Clip.Retarget)==recipeJson&&
            RetargetDependencies.Hash(reopened.LibrarySources)==ancestryHash&&RetargetDependencies.Hash(reopened)==contentHash&&
            ProjectStore.Hash(first)==ProjectStore.Hash(second),
            "Actual pooled project save/read/save preserves historical receipt absence, authentic source ancestry and exact committed file/content hashes");
        var explicitRecipe=recipe with{ChannelLedger=ledger,Pipeline=recipe.Pipeline! with{Reconstruction=motion,SceneBody=scene}};
        string currentFile=Path.Combine(directory,"explicit-current-receipts.fighterport");
        await ProjectStore.SaveAsync(owned with{Clips=[owned.Clips[0] with{Retarget=explicitRecipe}]},currentFile);
        var current=await ProjectStore.LoadAsync(currentFile);
        check(Json(current.Clips[0].Retarget)==Json(explicitRecipe)&&current.Clips[0].Retarget?.ChannelLedger is{ArticulatedSceneDerivedChannels.Length:0}&&
            current.Clips[0].Retarget?.Pipeline?.Reconstruction is{RefinementPasses:0,RefinedOutputSelected:false}&&
            current.Clips[0].Retarget?.Pipeline?.SceneBody is{TranslationProposals:0,FeatureGapMeasured:false},
            "Actual project persistence retains explicit empty/zero/false current evidence while keeping the immutable historical source recipe unchanged");
        await File.WriteAllTextAsync(Path.Combine(directory,"receipt-compatibility.json"),Json(new
        {Claim="Storage metadata compatibility only; no new numerical/native/contact/action acceptance",HistoricalMotionHash=Digest(oldMotion),
            HistoricalSceneHash=Digest(oldScene),HistoricalLedgerHash=Digest(oldLedger),AncestryHash=ancestryHash,
            PooledProjectHash=ProjectStore.Hash(first),CurrentProjectHash=ProjectStore.Hash(currentFile)}));
    }
}
