using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Web.Script.Serialization;

internal static partial class Program
{
    private static int CoordinateLegacyProjectChecks(string projectPath,string baselinePath,string output)
    {
        Directory.CreateDirectory(output);
        var json=new JavaScriptSerializer{MaxJsonLength=16*1024*1024};
        var baseline=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(baselinePath));
        var expected=((System.Collections.IEnumerable)baseline["Dependencies"]).Cast<object>().Select(v=>(string)v).ToArray();
        string projectHash=FighterProject.HashFile(projectPath);
        Check(projectHash==Convert.ToString(baseline["ProjectSHA256"])&&expected.Length>0,
            "Legacy protection test is bound to the independently frozen qualified52 project and dependency closure");
        var hashes=new Dictionary<string,string>{{projectPath,projectHash},{baselinePath,FighterProject.HashFile(baselinePath)}};
        using(var project=FighterProject.Open(projectPath))
        {
            foreach(var ownedAsset in project.RegisteredAssets())
            {
                string owned=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath),ownedAsset.RelativePath));
                hashes[owned]=FighterProject.HashFile(owned);
            }
            var asset=project.Animations.Single(a=>a.Id==Convert.ToString(baseline["Id"]));
            Check(asset.Retarget.Algorithm=="bind-world-fk-body-contact-ik-v7"&&NativeTransitionContacts.HasBakedSupport(asset),
                "An authentic v7 project with baked support opens without relabelling or recalibrating its recorded result");
            string recipe=json.Serialize(asset.Retarget);
            using(var native=NodeFactory.FromFile(null,project.Model.WorkingPath))
            {
                var model=Walk(native).OfType<MDL0Node>().Single(m=>m.Name==Convert.ToString(baseline["Model"]));
                string rigHash=BrawlRetargeter.SkeletonFingerprint(model),nativeHash=FighterProject.HashFile(project.Model.WorkingPath);
                var actual=NativeTransitionContacts.Resolve(model,asset,true);
                Check(actual.SequenceEqual(expected),
                    "V7 support/skin-influence/attachment ancestor closure is identical to the independent52 closure after coordinate calibration changes",new{actual});
                string algorithm=asset.Retarget.Algorithm;asset.Retarget.Algorithm="unknown-coordinate-solver";
                bool rejected=false;try{NativeTransitionContacts.Resolve(model,asset,true);}catch(InvalidDataException){rejected=true;}
                Check(rejected,"Unknown saved solvers cannot borrow v7 support proof");
                rejected=false;try{project.SaveCopy(Path.Combine(output,"unknown-algorithm.fpstudio"));}catch(InvalidDataException){rejected=true;}
                Check(rejected&&!File.Exists(Path.Combine(output,"unknown-algorithm.fpstudio")),"Unknown retarget algorithms still fail project validation without publishing a file");
                asset.Retarget.Algorithm=algorithm;
                string engine=asset.Retarget.EngineReceipt;asset.Retarget.EngineReceipt="unqualified-engine-context";
                rejected=false;try{NativeTransitionContacts.Resolve(model,asset,true);}catch(InvalidDataException){rejected=true;}
                asset.Retarget.EngineReceipt=engine;Check(rejected,"V7 allowance retains the native-only engine-receipt guard");
                string identity=asset.RigIdentity;asset.RigIdentity=new string('a',64);
                rejected=false;try{NativeTransitionContacts.Resolve(model,asset,true);}catch(InvalidDataException){rejected=true;}
                asset.RigIdentity=identity;Check(rejected,"V7 allowance retains exact target-rig identity protection");
                rejected=false;try{NativeTransitionContacts.Resolve(model,asset,false);}catch(NotSupportedException){rejected=true;}
                Check(rejected,"Legacy baked contacts still require preservation rather than being silently released");

                var rotation=Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.UnitZ,.31f)*Quaternion.CreateFromAxisAngle(Vector3.UnitY,.57f));
                var reviewed=RigMath.ReviewedCoordinateFrame(Vector3.Transform(Vector3.UnitX,rotation),Vector3.Transform(Vector3.UnitY,rotation),Vector3.Transform(Vector3.UnitZ,rotation));
                var map=asset.Retarget.Mapping.ToDictionary(m=>m.Role,m=>m.Target);
                var original=BrawlRetargeter.Capture(model,map,coordinateFrame:reviewed);
                Check(original.Rig.CoordinateFrame.HasValue&&CoordinateAngle(RigMath.BodyFrame(original.Rig),rotation)<.01,
                    "Native capture honors a full reviewed frame independently of default Brawl up and anatomy");
                string snapshot=Path.Combine(output,"reviewed-frame-snapshot.mdl0");model.Export(snapshot);
                using(var loaded=NodeFactory.FromFile(null,snapshot) as MDL0Node)
                {
                    var copy=BrawlRetargeter.Capture(loaded,map,original.Rig.CoordinateUp,original.Rig.CoordinateFrame);
                    Check(copy.Rig.CoordinateFrame.HasValue&&CoordinateAngle(RigMath.BodyFrame(copy.Rig),rotation)<.01&&copy.Fingerprint==original.Fingerprint,
                        "Native retarget snapshot capture retains complete reviewed coordinates and exact native bind identity");
                    Check(new HumanoidRetargeter(original.Rig,copy.Rig).CalibrationEvidence=="reviewed-coordinate-frame-v1",
                        "Snapshot calibration reports reviewed coordinate evidence rather than a silent anatomical fallback");
                }
                Check(BrawlRetargeter.SkeletonFingerprint(model)==rigHash&&FighterProject.HashFile(project.Model.WorkingPath)==nativeHash&&json.Serialize(asset.Retarget)==recipe,
                    "Coordinate review/snapshot and compatibility guards preserve authentic model bytes, binds and complete historical v7 recipe");
            }
            string copyPath=Path.Combine(output,"legacy-v7-copy.fpstudio");project.SaveCopy(copyPath);
            using(var copy=FighterProject.Open(copyPath))
                Check(copy.Animations.Single(a=>a.Id==asset.Id).Retarget.Algorithm=="bind-world-fk-body-contact-ik-v7"&&
                    json.Serialize(copy.Animations.Single(a=>a.Id==asset.Id).Retarget)==recipe,
                    "Legacy v7 Save/fresh reopen retains its original algorithm and contact provenance without a schema migration");
        }
        Check(hashes.All(p=>FighterProject.HashFile(p.Key)==p.Value),"Legacy coordinate tests leave the original project, owned saved assets and frozen52 evidence byte-for-byte unchanged");
        File.WriteAllText(Path.Combine(output,"coordinate-compatibility.json"),json.Serialize(new{Passed=true,Checks,OriginalFiles=hashes,ExpectedDependencies=expected}));
        Console.WriteLine("PASS "+Checks.Count+" coordinate compatibility checks");return 0;
    }
}
