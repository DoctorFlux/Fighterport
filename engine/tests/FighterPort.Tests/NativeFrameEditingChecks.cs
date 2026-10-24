using System.Numerics;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;

internal static class NativeFrameEditingChecks
{
    public static async Task RunAsync(string directory,Action<bool,string> check)
    {
        Directory.CreateDirectory(directory);var adapter=new BrawlAdapter(Environment.GetEnvironmentVariable("FIGHTERPORT_BRAWL_WORKER")??throw new InvalidOperationException("Supply the audited local native worker."));
        var fixture=ProceduralFixture.Create();string dae=Path.Combine(directory,"original.dae"),model=Path.Combine(directory,"original.mdl0"),motion=Path.Combine(directory,"original.chr0");FixtureDae.Write(fixture.Scene,dae);await adapter.RunAsync(new("fixture",dae,motion,null,model,[],[]));
        var original=await adapter.ImportAsync(model,motion);string modelHash=ProjectStore.Hash(model),motionHash=ProjectStore.Hash(motion);
        var seed=new EditorDocument(original);seed.Seek(12);var pose=seed.Evaluate();int root=Array.FindIndex(original.Scene.Bones,b=>b.Name=="Root"),arm=Array.FindIndex(original.Scene.Bones,b=>b.Name=="Arm.R");
        using(var edit=seed.BeginTransformEdit())edit.Commit(new Dictionary<int,Transform>{{root,pose.Local[root] with{Translation=pose.Local[root].Translation+new Vector3(.4f,0,0)}},{arm,pose.Local[arm] with{Rotation=pose.Local[arm].Rotation+new Vector3(0,0,5)}}});
        string seeded=Path.Combine(directory,"seeded.chr0");await adapter.ExportAsync(seed.Project,seed.Clip,seeded);var source=await adapter.ImportAsync(model,seeded);string seededHash=ProjectStore.Hash(seeded);string deform=AnimationControls.DeformChecksum(source.Scene);
        foreach(string operation in new[]{"clear","insert","delete","shorten"})
        {
            var document=new EditorDocument(source);document.SelectedBone=root;document.Seek(12);
            bool changed=operation switch{"clear"=>document.ClearAnimationFrame(TransformComponents.Translation,true),"insert"=>document.InsertAnimationFrame(),"delete"=>document.DeleteAnimationFrame(),_=>document.SetAnimationSampleCount(20)};
            check(changed&&document.Clip.RewriteNativeChannels,"Native "+operation+" marks owned channels authoritative, including removed keys");
            string output=Path.Combine(directory,operation+".chr0");var response=await adapter.ExportAsync(document.Project,document.Clip,output);var actual=response.Project!.Clips.Single();
            check(actual.SampleCount==(operation=="insert"?25:operation=="delete"?23:operation=="shorten"?20:24),"Fresh native "+operation+" retains the independently expected sample count");
            double[] rootTimes=operation switch{"clear"=>[0,23],"insert"=>[0,13,24],"delete"=>[0,22],_=>[0,12]};
            check(actual.Tracks.Single(t=>t.Bone=="Root").Channels[6]!.Keys.Select(k=>k.Frame).SequenceEqual(rootTimes),"Fresh native "+operation+" has the independently expected key indices, without deleted source keys");
            double[] times=[0,.5,7.125,11.5,Math.Min(18.75,actual.LastSampleFrame),actual.LastSampleFrame];var independent=await adapter.RunAsync(new("import",model,output,null,null,[],times));float maximum=0;var vertices=document.Project.Scene.Meshes.SelectMany(m=>m.Vertices).ToArray();
            for(int i=0;i<times.Length;i++)
            {
                var evaluated=AnimationService.Evaluate(document.Project.Scene,document.Clip,times[i]);for(int j=0;j<vertices.Length;j++){var expected=independent.ExpectedVertices![i][j];maximum=Math.Max(maximum,Vector3.Distance(AnimationService.Skin(vertices[j],evaluated),new(expected[0],expected[1],expected[2])));}
            }
            check(maximum<.003f,$"Independent native skin after {operation} matches the owned edit at withheld fractional times ({maximum:R}; existing original-fixture 0.003 bound)");
            var edited=document.Project;document.Undo();check(ReferenceEquals(document.Project,source),"Native "+operation+" Undo restores the exact original project revision");document.Redo();check(ReferenceEquals(document.Project,edited),"Native "+operation+" Redo restores the stored edited revision");
            check(AnimationControls.DeformChecksum(document.Project.Scene)==deform,"Native "+operation+" preserves the complete imported deform basis");
        }
        var partial=new EditorDocument(source);partial.SelectedBone=root;partial.EditAnimationChannel(6,null);string rejected=Path.Combine(directory,"partial-group.chr0");
        try{await adapter.ExportAsync(partial.Project,partial.Clip,rejected);check(false,"A partly absent native channel group must not be silently materialized");}catch(NotSupportedException){check(!File.Exists(rejected),"Unencodable partial native group rejects before creating an output or inventing fixed keys");}
        check(ProjectStore.Hash(model)==modelHash&&ProjectStore.Hash(motion)==motionHash&&ProjectStore.Hash(seeded)==seededHash,"All original native model/motion bytes remain unchanged across fresh write/reopen checks");
        IAnimationFileWriter writer=adapter;var draft=source.Clips[0] with{Id=Guid.NewGuid(),NativeWrite=NativeWriteMode.NewResource,NativeWriteName="SavedDraft",Retarget=new(Guid.NewGuid(),source.Scene.SkeletonRevision,Guid.NewGuid(),Guid.NewGuid(),RootMotionPolicy.Preserve,RetargetService.Version){IsDraft=true}};var draftProject=source with{Clips=[draft]};
        string draftPath=Path.Combine(directory,"saved-draft.chr0");var receipt=await writer.WriteAnimationAsync(draftProject,draft,draftPath);
        check(writer.Supports(draftProject,draft)&&receipt.Reopened&&receipt.Sha256==ProjectStore.Hash(draftPath)&&receipt.Diagnostics.Any(d=>d.Contains("unvalidated",StringComparison.OrdinalIgnoreCase)),"A retarget draft saves through the common writer as a fresh CHR0 with an explicit unvalidated-result receipt");
        var reopenedDraft=await adapter.ImportAsync(model,draftPath);var expectedDraft=AnimationService.Evaluate(source.Scene,source.Clips[0],12);var actualDraft=AnimationService.Evaluate(reopenedDraft.Scene,reopenedDraft.Clips[0],12);
        check(Vector3.Distance(expectedDraft.World[root].Translation,actualDraft.World[root].Translation)<.00001f&&draftProject.Clips[0].Retarget!.IsDraft&&ProjectStore.Hash(seeded)==seededHash,"Saving a native retarget draft preserves its prepared pose and original bytes without promoting it to accepted motion");
    }
}
