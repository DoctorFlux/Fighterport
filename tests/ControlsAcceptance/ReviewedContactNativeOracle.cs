using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Web.Script.Serialization;
using System.Threading.Tasks;

internal static partial class Program
{
    static Task ReviewedContactReopenOracle(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Reopened contact verification requires the private desktop.");
        string projectFile=args[Array.IndexOf(args,"--reviewed-contact-reopen")+1];
        using(var project=FighterProject.Open(projectFile))using(var root=NodeFactory.FromFile(null,args[0]))using(var animation=NodeFactory.FromFile(null,args[1]) as CHR0Node)
        {
            var recipe=project.Animations.SelectMany(a=>a.ReviewedContactRecipes()).Single();recipe.Validate();
            var model=(MDL0Node)root;foreach(var ignored in Walk(model)){}
            Check(FighterProject.HashFile(args[0])==recipe.ModelHash,"Saved contact recipe resolves the exact native target model, binds and complete weights");
            Check(FighterProject.HashFile(args[1])==recipe.InitialResultHash,"Saved contact recipe resolves the exact corrected native animation");
            var setup=new ControlReply{BoneNames=BrawlRetargeter.Capture(model).Bones.Select(b=>b.Name).ToArray()};
            ReviewedNativeContactOracle(model,animation,recipe,setup,output,"reopened-corrected");
            string export=Path.Combine(Path.GetDirectoryName(args[1]),"04-verified-export.chr0");
            using(var decoded=NodeFactory.FromFile(null,export) as CHR0Node)ReviewedNativeContactOracle(model,decoded,recipe,setup,output,"reopened-decoded");
        }
        return Task.CompletedTask;
    }
    sealed class ContactOracleVector {public float X{get;set;}public float Y{get;set;}public float Z{get;set;}public float W{get;set;}}
    sealed class ContactOraclePoint {public int Mesh{get;set;}public int Vertex{get;set;}}
    sealed class ContactOracleRow {public int Bone{get;set;}public int Start{get;set;}public int End{get;set;}public int Blend{get;set;}public int AnchorFrame{get;set;}public bool Reviewed{get;set;}public ContactOracleVector Orientation{get;set;}public ContactOraclePoint[] Patch{get;set;}public ContactOracleVector[] AnchorPoints{get;set;}}
    sealed class ContactOracleSurface {public float SourceFloor{get;set;}}
    sealed class ContactOracleContext {public ContactOracleRow[] Ranges{get;set;}public ContactOracleSurface Surface{get;set;}public int? EarlierBodyCorrectionStart{get;set;}}
    sealed class ContactOracleProof {public float PositionBound{get;set;}public float OrientationBound{get;set;}public double[] Witnesses{get;set;}}

    // Independent native ApplyCHR/WeightMeshes path. The worker's face-to-vertex
    // transport indices identify frozen patch samples; clearance additionally
    // checks EVERY native vertex with positive influence from the foot path.
    static unsafe void ReviewedNativeContactOracle(MDL0Node source,CHR0Node animation,NativeReviewedContacts recipe,ControlReply setup,string output,string label)
    {
        var json=new JavaScriptSerializer{MaxJsonLength=32*1024*1024,RecursionLimit=100};
        var context=json.Deserialize<ContactOracleContext>(recipe.Context);var proof=json.Deserialize<ContactOracleProof>(recipe.Receipt);
        string modelFile=Path.Combine(output,label+"-oracle.mdl0");source.ExportChecked(modelFile);
        string baselineFile=Path.Combine(output,label+"-oracle-baseline.chr0");File.WriteAllBytes(baselineFile,Convert.FromBase64String(recipe.BaseSnapshot));
        using(var baseline=NodeFactory.FromFile(null,baselineFile) as CHR0Node)
        using(var root=NodeFactory.FromFile(null,modelFile))
        {
            foreach(var ignored in Walk(root)){} // Resolve native lazy groups before using polygon/weight caches.
            var model=(MDL0Node)root;var objects=model.PolygonList.OfType<MDL0ObjectNode>().ToArray();
            var rows=context.Ranges.Where(r=>r.Reviewed).ToArray();
            var nativePoints=rows.Select(row=>row.Patch.Select(p=>
            {
                if(p.Mesh<0||p.Mesh>=objects.Length||p.Vertex<0||p.Vertex>=objects[p.Mesh]._manager._pointCount)throw new InvalidDataException("Native patch identity is out of bounds.");
                var obj=objects[p.Mesh];return obj.Vertices[((ushort*)obj._manager._indices.Address)[p.Vertex]];
            }).ToArray()).ToArray();
            var regions=rows.Select(row=>
            {
                var foot=model.AllBones.Single(b=>b.Name==setup.BoneNames[row.Bone]);
                bool Descends(MDL0BoneNode bone){for(;bone!=null;bone=bone.Parent as MDL0BoneNode)if(ReferenceEquals(bone,foot))return true;return false;}
                return objects.SelectMany(o=>o.Vertices).Where(v=>v.GetBoneWeights()?.Any(w=>w.Weight>0&&w.Bone is MDL0BoneNode bone&&Descends(bone))==true).Distinct().ToArray();
            }).ToArray();
            var soles=rows.Select((row,r)=>
            {
                model.ApplyCHR(baseline,row.AnchorFrame+1);float low=regions[r].Min(v=>v.WeightedPosition._y);
                var points=regions[r].Where(v=>v.WeightedPosition._y<=low+.00001f).ToArray();
                var center=nativePoints[r].Select(v=>v.WeightedPosition).Aggregate(BrawlLib.Internal.Vector3.Zero,(a,p)=>a+p)/nativePoints[r].Length;
                var expected=row.AnchorPoints.Aggregate(Vector3.Zero,(a,p)=>a+new Vector3(p.X,p.Y,p.Z))/row.AnchorPoints.Length;
                var shift=new BrawlLib.Internal.Vector3(expected.X-center._x,expected.Y-center._y,expected.Z-center._z);
                return new{Points=points,Anchors=points.Select(v=>v.WeightedPosition+shift).ToArray()};
            }).ToArray();
            File.WriteAllText(Path.Combine(output,label+"-native-contact-members.json"),json.Serialize(new{RegionVertices=regions.Select(r=>r.Length).ToArray(),PatchVertices=nativePoints.Select(r=>r.Length).ToArray(),FullSoleVertices=soles.Select(r=>r.Points.Length).ToArray()}));
            // A curved native sole can touch at one/two vertices. Requiring a
            // three-point planar patch invents flat geometry. Keep all actual
            // lowest samples, full-region clearance and orientation checks.
            Check(regions.All(r=>r.Length>0)&&nativePoints.All(r=>r.Length>0)&&soles.All(r=>r.Points.Length>0),label+": independent native oracle resolves full foot influence regions and exact frozen sole-patch identities");
            var times=new SortedSet<float>(proof.Witnesses.Select(t=>(float)t));
            for(float t=0;t<=animation.FrameCount-1;t+=.25f)times.Add(t);
            foreach(var row in rows)for(int t=row.Start-row.Blend;t<row.End+row.Blend;t++){times.Add(t+.137f);times.Add(t+.623f);}
            double drift=0,penetration=0,angle=0,earlierPenetration=0,originalEarlierPenetration=0;int samples=0;
            int contactBegin=rows.Min(r=>r.Start-r.Blend);var earlierSamples=new List<object>();
            foreach(float time in times)
            {
                model.ApplyCHR(animation,time+1);samples++;
                for(int r=0;r<rows.Length;r++)
                {
                    var row=rows[r];if(time>row.Start-row.Blend&&time<row.End+row.Blend)
                        foreach(var vertex in regions[r])penetration=Math.Max(penetration,context.Surface.SourceFloor-vertex.WeightedPosition._y);
                    if(time<row.Start||time>row.End)continue;
                    for(int p=0;p<row.Patch.Length;p++)
                    {
                        var actual=nativePoints[r][p].WeightedPosition;var expected=row.AnchorPoints[p];
                        drift=Math.Max(drift,Math.Sqrt(Math.Pow(actual._x-(double)expected.X,2)+Math.Pow(actual._y-(double)expected.Y,2)+Math.Pow(actual._z-(double)expected.Z,2)));
                    }
                    for(int p=0;p<soles[r].Points.Length;p++)drift=Math.Max(drift,SourceKeyDistance(soles[r].Points[p].WeightedPosition,soles[r].Anchors[p]));
                    var m=model.AllBones.Single(b=>b.Name==setup.BoneNames[row.Bone]).Matrix.GetRotationMatrix();
                    var matrix=new Matrix4x4(m[0],m[1],m[2],0,m[4],m[5],m[6],0,m[8],m[9],m[10],0,0,0,0,1);
                    var actualRotation=Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(matrix));
                    var expectedRotation=Quaternion.Normalize(new Quaternion(row.Orientation.X,row.Orientation.Y,row.Orientation.Z,row.Orientation.W));
                    double chord=Math.Min((actualRotation-expectedRotation).Length(),(actualRotation+expectedRotation).Length());
                    angle=Math.Max(angle,4*Math.Asin(Math.Min(1,chord/2))*180/Math.PI);
                }
                if(context.EarlierBodyCorrectionStart is {} earlier&&time>=earlier&&time<=contactBegin)
                {
                    double corrected=Math.Max(0,context.Surface.SourceFloor-regions.SelectMany(r=>r).Min(v=>v.WeightedPosition._y));
                    model.ApplyCHR(baseline,time+1);
                    double original=Math.Max(0,context.Surface.SourceFloor-regions.SelectMany(r=>r).Min(v=>v.WeightedPosition._y));
                    earlierPenetration=Math.Max(earlierPenetration,corrected);originalEarlierPenetration=Math.Max(originalEarlierPenetration,original);
                    earlierSamples.Add(new{NativeFrame=time,OriginalPenetration=original,CorrectedPenetration=corrected});
                }
            }
            File.WriteAllText(Path.Combine(output,label+"-earlier-body-observations.json"),json.Serialize(new{context.EarlierBodyCorrectionStart,ContactBegin=contactBegin,EarlierPenetration=earlierPenetration,OriginalEarlierPenetration=originalEarlierPenetration,Samples=earlierSamples,
                Note="Observed before reviewed contact establishment; inherited lying/action first pose remains exact. These observations do not authorize or claim a ground contact before the reviewed range."}));
            File.WriteAllText(Path.Combine(output,label+"-native-contact.json"),json.Serialize(new{Drift=drift,Penetration=penetration,OrientationDegrees=angle,proof.PositionBound,proof.OrientationBound,Samples=samples,
                AllPositiveFootInfluences=true,FootRegionVertices=regions.Select(r=>r.Length).ToArray(),FullSoleVertices=soles.Select(r=>r.Points.Length).ToArray(),QuarterFrames=true,WithheldFractions=new[]{.137,.623},NativeEvaluator="BrawlLib ApplyCHR and WeightMeshes; independent from Core contact pose evaluation",ContinuousProof=false,RuntimeAcceptance=false}));
            Check(!double.IsNaN(drift)&&drift<=proof.PositionBound&&penetration<=proof.PositionBound&&angle<=proof.OrientationBound,label+": independent native FK/all-influence skin passes absolute patch, uncapped foot clearance and orientation bounds");
        }
    }
}
