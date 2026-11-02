using FighterPort.Adapters.Melee;
using System;
using System.IO;
using System.Linq;
class Program
{
    static int Main(string[] args)
    {
        try
        {
            System.Diagnostics.Process.GetCurrentProcess().PriorityClass=System.Diagnostics.ProcessPriorityClass.BelowNormal;
            if(args[0]=="--procedural"){NativeCases.Run(args[1]);return 0;}
            using(var source=new MeleeSource(args[0]))
            {
                Console.WriteLine("SOURCE "+source.GameId+" rev "+source.Revision+" native files "+source.Files.Count);
                if(args.Length>1&&args[1]=="--probe")
                {
                    var native=MeleeNative.ReadMembers(source.Read(args[2])).SelectMany(MeleeNative.Models).Single();
                    if(args.Contains("--rig-roles"))
                    {
                        var policy=MeleeBoneEditPolicy.Read(native,source.Read("PlCo.dat"));foreach(var role in policy.Roles)Console.WriteLine("ROLE "+role.Key+" "+role.Value);
                        var joints=MeleeNative.Joints(native.Root);var parents=Enumerable.Repeat(-1,joints.Length).ToArray();
                        for(int i=0;i<joints.Length;i++)for(var child=joints[i].Child;child!=null;child=child.Next)parents[Array.FindIndex(joints,j=>j._s==child._s)]=i;
                        var bind=new MeleePose(native);for(int i=0;i<joints.Length;i++)Console.WriteLine("JOINT "+i+" parent "+parents[i]+" world "+bind.World[i].Translation);
                        return 0;
                    }
                    var report=MeleeProjection.Prepare(native);var pose=new MeleePose(native);
                    foreach(var mesh in report.Meshes)foreach(var vertex in mesh.Vertices)pose.VertexMatrix(mesh,vertex);
                    Console.WriteLine("POSE PASS "+native.Symbol+" DOBJ "+report.DisplayObjects);
                    var data=MeleeNative.ReadMembers(source.Read(args[3])).SelectMany(m=>m.Native.Roots).Where(r=>r.Data is HSDRaw.Melee.Pl.SBM_FighterData).Select(r=>(HSDRaw.Melee.Pl.SBM_FighterData)r.Data).Single();
                    var dol=source.ReadExecutable();var actor=MeleeInventory.ModelActor(native.Symbol);int index=MeleeCostumeBinding.FindIndex(dol,args[2],native.Symbol,actor,data.ModelLookupTables.CostumeVisibilityLookups.Length);Console.WriteLine("COSTUME "+index);var parts=MeleePartVisibility.Create(data.ModelLookupTables,report.DisplayObjects,"probe","probe",index);
                    for(int i=0;i<parts.GroupCount;i++)Console.WriteLine("GROUP "+i+" variants "+parts.VariantCount(i));
                    Console.WriteLine("VISIBLE "+string.Join(",",parts.Snapshot().Select((v,i)=>new{v,i}).Where(v=>v.v).Select(v=>v.i)));return 0;
                }
                if(args.Length>1 && args[1]=="--inventory")
                {
                    var timer=System.Diagnostics.Stopwatch.StartNew();var inventory=MeleeInventory.Read(source);
                    Console.WriteLine("INVENTORY models "+inventory.Models.Count+" motions "+inventory.Motions.Count+" associated "+inventory.Motions.Count(m=>m.ModelKey!=null)+" issues "+inventory.Issues.Count+" ms "+timer.ElapsedMilliseconds);
                    foreach(var issue in inventory.Issues.Take(8))Console.WriteLine("ISSUE "+issue);
                    if(args.Contains("--body-policy"))
                    {
                        byte[] common=source.Read("PlCo.dat");var policies=new System.Collections.Generic.List<string>();int available=0;
                        foreach(var model in inventory.Models)
                        {
                            var native=MeleeNative.ReadMembers(source.Read(model.File)).SelectMany(MeleeNative.Models).Single(m=>m.Key==model.Key);
                            var policy=MeleeBoneEditPolicy.Read(native,common);if(policy.Available)available++;
                            policies.Add(model.File+" | "+model.Key+" | "+MeleeNative.RigHash(native)+" | "+(policy.Available?"body="+policy.BodyJoint+" protected="+string.Join(",",policy.ProtectedJoints):policy.Evidence));
                        }
                        Console.WriteLine("NATIVE BODY POLICY available "+available+" unavailable "+(inventory.Models.Count-available));
                        if(args.Length>2)File.WriteAllLines(args[2]+".body-policy.txt",policies);
                    }
                    if(args.Contains("--models"))
                    {
                        int passed=0,failed=0,rebuilt=0,fallback=0;var issues=new System.Collections.Generic.List<string>();
                        foreach(var model in inventory.Models)
                        {
                            try
                            {
                                var native=MeleeNative.ReadMembers(source.Read(model.File)).SelectMany(MeleeNative.Models).Single(m=>m.Key==model.Key);
                                var report=MeleeProjection.Prepare(native);
                                if(report.Meshes.Count==0||report.Meshes.Sum(m=>m.Vertices.Length)==0)throw new Exception("No native render geometry.");
                                var pose=new MeleePose(native);
                                foreach(var mesh in report.Meshes)foreach(var vertex in mesh.Vertices)
                                {var point=System.Numerics.Vector3.Transform(vertex.NativePosition,pose.VertexMatrix(mesh,vertex));MeleeNative.Finite(point.X);MeleeNative.Finite(point.Y);MeleeNative.Finite(point.Z);}
                                if(args.Contains("--roundtrip")){var write=MeleeNativeWriter.PrepareOriginalSnapshot(native.Member);if(MeleeNativeWriter.GraphHash(new HSDRaw.HSDRawFile(write.CopyBytes()))!=write.GraphSha256)throw new Exception("Native graph roundtrip differs.");if(write.Rebuilt)rebuilt++;else fallback++;}
                                if(args.Contains("--edited-roundtrip"))
                                {
                                    float before=native.Root.TX;
                                    var edited= MeleeNativeWriter.PrepareEditedSnapshot(native.Member,file=>((HSDRaw.Common.HSD_JOBJ)file.Roots.Single(r=>r.Name==native.Symbol).Data).TX=before+.125f);
                                    var reopened=MeleeNative.ReadMembers(edited.CopyBytes()).SelectMany(MeleeNative.Models).Single(m=>m.Symbol==native.Symbol);
                                    if(reopened.Root.TX!=before+.125f||native.Root.TX!=before)throw new Exception("Edited native model isolation failed.");
                                }
                                passed++;
                            }
                            catch(Exception e){failed++;issues.Add(model.File+" "+model.Symbol+": "+e.Message);}
                        }
                        Console.WriteLine("NATIVE MODEL PREPARATION passed "+passed+" unavailable "+failed+" exact graph rebuilds "+rebuilt+" original-byte fallbacks "+fallback);
                        foreach(string issue in issues.Take(12))Console.WriteLine("MODEL ISSUE "+issue);
                        if(args.Length>2)File.WriteAllLines(args[2]+".models.txt",new[]{"passed="+passed,"unavailable="+failed}.Concat(issues));
                    }
                    if(args.Length>2)File.WriteAllText(args[2],new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength=int.MaxValue }.Serialize(inventory));
                    return 0;
                }
                foreach(var file in source.Files.Where(f=>Path.GetFileName(f.Path).StartsWith("PlMr",StringComparison.Ordinal)).Take(10))
                {
                    Console.WriteLine("FILE "+file.Path+" "+file.Length);
                    try
                    {
                        var members=MeleeNative.ReadMembers(source.Read(file.Path));Console.WriteLine("MEMBERS "+members.Count);
                        foreach(var member in members.Take(2))foreach(var root in member.Native.Roots)
                        {
                            Console.WriteLine("ROOT "+member.Offset+" "+root.Name+" "+root.Data.GetType().Name);
                            foreach(var model in MeleeNative.Models(member))Console.WriteLine("MODEL JOINTS "+MeleeNative.Joints(model.Root).Length+" "+MeleeNative.RigHash(model));
                            foreach(var motion in MeleeNative.Motions(member))Console.WriteLine("MOTION LAST "+motion.LastFrame);
                        }
                    }
                    catch(Exception e){Console.WriteLine("ISSUE "+e);}
                }
            }
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
