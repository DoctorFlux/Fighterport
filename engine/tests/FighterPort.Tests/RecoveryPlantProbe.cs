using FighterPort.Core;
using FighterPort.Adapters.Brawl;
using System.Numerics;
using System.Text.Json;

internal static class RecoveryPlantProbe
{
    internal sealed record Case(string Id,string TargetModel,string MasterClip,string ControlSetup,string OwnReference,string ReferenceFixture);
    internal sealed record Corpus(Case[] Cases);
    internal static async Task Run(string manifest,string output,bool prepare)
    {
        var corpus=JsonSerializer.Deserialize<Corpus>(await File.ReadAllTextAsync(manifest),ProjectStore.Json)!;
        if(corpus.Cases.Length is <1 or >64)throw new InvalidDataException("Bounded recovery corpus required.");
        var adapter=new BrawlAdapter(Environment.GetEnvironmentVariable("FIGHTERPORT_BRAWL_WORKER")??throw new InvalidOperationException("Pin the native worker."));
        var summaries=new List<object>();
        var reportJson=new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false};
        for(int i=0;i<corpus.Cases.Length;i++)
        {
            var item=corpus.Cases[i];string dest=Path.Combine(output,"c"+(i+1).ToString("00"));Directory.CreateDirectory(dest);
            var project=await adapter.ImportAsync(item.TargetModel,item.MasterClip);var clip=project.Clips.Single();
            using var packet=JsonDocument.Parse(await File.ReadAllTextAsync(item.ControlSetup));
            using var state=JsonDocument.Parse(packet.RootElement.GetProperty("State").GetString()!);
            var rig=state.RootElement.GetProperty("Setup").Deserialize<ControlRigSetup>(ProjectStore.Json)!;
            clip=clip with{Id=state.RootElement.GetProperty("Clip").GetGuid()};project=project with{Clips=[clip],ControlRigs=[rig]};
            int join=clip.SampleCount-10,last=clip.SampleCount-1;
            var regions=SurfaceContacts.Regions(project.Scene,rig).Where(r=>rig.Chains.Any(c=>c.Reviewed&&c.Effector==r.Bone&&c.Role is "Foot.L" or "Foot.R")).ToArray();var measurements=new List<object>();
            var end=AnimationService.Evaluate(project.Scene,clip,last);
            SkinSurfacePoint[] Full(int bone)=>project.Scene.Meshes.SelectMany((m,mi)=>m.Vertices.Select((v,vi)=>(v,mi,vi)))
                .Where(p=>Enumerable.Range(0,p.v.InfluenceCount).Any(j=>p.v.WeightAt(j)>0&&PoseGeometry.Ancestor(project.Scene,bone,(int)p.v.JointAt(j))))
                .Select(p=>new SkinSurfacePoint(p.v.Position,p.v.Joints,p.v.Weights){Influences=p.v.Influences,Mesh=p.mi,Vertex=p.vi}).ToArray();
            using var referenceEvidence=JsonDocument.Parse(await File.ReadAllTextAsync(item.ReferenceFixture));
            float floor=referenceEvidence.RootElement.GetProperty("FittingFloorHeight").GetSingle();
            foreach(var region in regions)
            {
                var chain=rig.Chains.Single(c=>c.Reviewed&&c.Effector==region.Bone);
                float length=chain.Calibration!.UpperLength+chain.Calibration.LowerLength;
                var patch=SurfaceContacts.Patch(region,end,Vector3.UnitY,length*.006f);
                foreach(double time in Enumerable.Range(join*4,(last-join)*4+1).Select(t=>t/4d))
                {
                    var pose=AnimationService.Evaluate(project.Scene,clip,time);
                    var centre=patch.Aggregate(Vector3.Zero,(v,p)=>v+ContactGeometry.Point(p,pose))/patch.Length;
                    measurements.Add(new{region.Role,Time=time,Length=length,Floor=floor,Minimum=region.Points.Min(p=>ContactGeometry.Point(p,pose).Y),FullMinimum=Full(region.Bone).Min(p=>ContactGeometry.Point(p,pose).Y),Centre=centre,
                        Ankle=pose.World[chain.Effector].Translation});
                }
            }
            await ProjectStore.SaveAsync(project,Path.Combine(dest,"master.fighterport"));
            await File.WriteAllTextAsync(Path.Combine(dest,"foot-motion.json"),JsonSerializer.Serialize(measurements,reportJson));
            var context=ReviewedContactSets.SuggestRecovery(project,clip,rig,new GroundContactSettings{SurfaceMode=SupportSurfaceMode.Ground,ExplicitSurface=true,SourceFloor=floor,SourceNormal=[0,1,0]},[],new(0,join,"Frozen action prefix"));
            context=context with{Ranges=context.Ranges.Select(r=>r with{Reviewed=true}).ToArray()};
            await File.WriteAllTextAsync(Path.Combine(dest,"reviewed-context.json"),JsonSerializer.Serialize(context,ProjectStore.Json));
            object? receipt=null;string? failure=null;
            if(prepare)try{var result=ReviewedContactSets.Prepare(project,clip,rig,context,progress:s=>Console.WriteLine(item.Id+": "+s));receipt=result.Receipt;
                await File.WriteAllTextAsync(Path.Combine(dest,"prepared.json"),JsonSerializer.Serialize(result,ProjectStore.Json));}
            catch(Exception e){failure=e.ToString();await File.WriteAllTextAsync(Path.Combine(dest,"failure.txt"),failure);}
            summaries.Add(new{item.Id,Floor=floor,Join=join,Last=last,Receipt=receipt,Failure=failure});
            await File.WriteAllTextAsync(Path.Combine(output,"summary.json"),JsonSerializer.Serialize(summaries,reportJson));
        }
    }
}
