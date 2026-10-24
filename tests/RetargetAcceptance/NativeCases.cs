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
    private static readonly List<object> Checks = new List<object>();
    private static string ContactClip;
    private static bool FocusedSupportCases;
    private static string[] ReviewContactNames;
    private static string Option(string[] args, string key) { int i = Array.IndexOf(args, key); return i >= 0 ? args[i + 1] : null; }
    private static void Check(bool condition, string name, object evidence = null)
    { if (!condition) throw new InvalidOperationException("FAIL: " + name + " " + new JavaScriptSerializer().Serialize(evidence)); Checks.Add(new {Name = name, Passed = true, Evidence = evidence}); }
    private static Dictionary<string, string> Hashes(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(p => p, FighterProject.HashFile);
    private static int Run(string[] args)
    {
        ContactClip=Option(args,"--contact-clip");
        FocusedSupportCases=args.Contains("--review-only") || args.Contains("--slow-review") || args.Contains("--cliff-review") || args.Contains("--locomotion-review");
        ReviewContactNames=args.Contains("--locomotion-review")?new[]{"Run","JumpSquat","JumpF","Fall","LandingAirN"}:args.Contains("--cliff-review")?new[]{"Wait1","CliffClimbQuick","CliffAttackQuick","CliffClimbSlow","CliffAttackSlow"}:
            args.Contains("--slow-review")?new[]{"Wait1","CliffClimbSlow","CliffAttackSlow"}:new[]{"Wait1","CliffClimbQuick","CliffAttackQuick"};
        string mario = Option(args, "--mario-root") ?? Environment.GetEnvironmentVariable("FIGHTERPORT_TEST_MARIO_ROOT");
        string snake = Option(args, "--snake-root") ?? Environment.GetEnvironmentVariable("FIGHTERPORT_TEST_SNAKE_ROOT");
        string output = Path.GetFullPath(Option(args, "--output") ?? Path.Combine(Path.GetTempPath(), "FighterPortRetarget", Guid.NewGuid().ToString("N")));
        if (mario == null || snake == null) throw new ArgumentException("Supply local --mario-root and --snake-root folders.");
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new ArgumentException("Output must be new or empty.");
        Directory.CreateDirectory(output);
        var before = Hashes(mario).Concat(Hashes(snake)).ToDictionary(p => p.Key, p => p.Value);
        using (ResourceNode sourceRoot = NodeFactory.FromFile(null, Path.Combine(mario, "FitMario00.pac")))
        using (ResourceNode targetRoot = NodeFactory.FromFile(null, Path.Combine(snake, "FitSnake00.pac")))
        using (ResourceNode motions = NodeFactory.FromFile(null, Path.Combine(mario, "FitMarioMotionEtc.pac")))
        {
            MDL0Node sourceModel = Walk(sourceRoot).ToList().OfType<MDL0Node>().Single(m => m.Name == "FitMario00");
            MDL0Node targetModel = Walk(targetRoot).ToList().OfType<MDL0Node>().Single(m => m.Name == "FitSnake00");
            var clips = Walk(motions).ToList().OfType<CHR0Node>().ToDictionary(c => c.Name);
            BrawlRig source = BrawlRetargeter.Capture(sourceModel), target = BrawlRetargeter.Capture(targetModel);
            var plan = new HumanoidRetargeter(source.Rig, target.Rig);
            Check(plan.Mapping.Count(m => m.Role == "spine" && m.Source == "WaistN" && m.Target == "BodyN") == 1 && plan.Mapping.Length >= 45,
                "Reviewed semantic mapping handles Mario WaistN to Snake BodyN and both mirrored limb hierarchies", new {Mapping = plan.Mapping, plan.LegScale, plan.ArmScale, plan.HeadScale, plan.UnmappedSource, plan.UnmappedTarget});
            Check(plan.LegScale > 2 && plan.ArmScale > 1.4 && plan.ArmScale < plan.LegScale,
                "Measured limb ratios distinguish long Snake legs from arm and head proportions", new {plan.LegScale, plan.ArmScale, plan.HeadScale});
            RigPose rest = plan.Evaluate(source.Rig.Rest).Pose;
            double restError = Enumerable.Range(0, target.Bones.Length).Max(i => Vector3.Distance(rest.Point(i), target.Rig.Rest.Point(i)));
            Check(restError < 0.002, "Rest-pose retarget preserves the complete target bind skeleton", new {MaximumError = restError});
            if(ContactClip==null)
            {
            CharacterizationCases(source,target,output);
            NativeAnonymousCharacterization(sourceModel,targetModel,clips["Wait1"],output);
            StanceCases(source.Rig,target.Rig);StanceCases(target.Rig,source.Rig);
            FlightCases(source.Rig,target.Rig);FlightCases(target.Rig,source.Rig);
            if(args.Contains("--mapping-only"))
            {
                Check(before.All(p=>FighterProject.HashFile(p.Key)==p.Value),"Mapping characterization preserves every original input");
                File.WriteAllText(Path.Combine(output,"mapping-results.json"),new JavaScriptSerializer{MaxJsonLength=16000000}.Serialize(new{Passed=true,Checks}));
                Console.WriteLine("PASS: "+Checks.Count+" mapping checks");return 0;
            }
            TrajectoryCases(source,target,output);
            AdditionalRigCases(source, target, clips["Attack11"]);
            var contactPlan=new HumanoidRetargeter(source.Rig,target.Rig);
            CHR0Node idle=clips["Wait1"];
            var originalContacts=new ContactMotion(contactPlan,t=>BrawlRetargeter.Sample(source,idle,t),idle.FrameCount-1,true,requireFloorEvidence:true);
            string originalName=idle.Name;
            try
            {
                idle.Name="AttackAirUnrelatedName";
                var renamedContacts=new ContactMotion(contactPlan,t=>BrawlRetargeter.Sample(source,idle,t),idle.FrameCount-1,true,requireFloorEvidence:true);
                Check(originalContacts.FloorEnabled && renamedContacts.FloorEnabled && originalContacts.PlantedFootSamples==renamedContacts.PlantedFootSamples &&
                    Enumerable.Range(0,5).All(i=>Vector3.Distance(originalContacts.Evaluate(i*0.37f).Pose.Point("left.foot"),renamedContacts.Evaluate(i*0.37f).Pose.Point("left.foot"))<0.00001f),
                    "Renaming a real idle to a misleading airborne name leaves Auto contacts and poses unchanged");
            }
            finally {idle.Name=originalName;}
            using(var airborne=BrawlRetargeter.Bake(source,target,clips["Attack11"],groundMode:GroundContactMode.Airborne))
            {
                var expected=airborne.Plan.Evaluate(BrawlRetargeter.Sample(source,clips["Attack11"],3)).Pose;
                var actual=airborne.Contacts.Evaluate(3).Pose;
                Check(!airborne.Contacts.FloorEnabled && airborne.Contacts.PlantedFootSamples==0 && Enumerable.Range(0,target.Bones.Length).Max(i=>Vector3.Distance(expected.Point(i),actual.Point(i)))<0.001,
                    "Explicit Airborne disables ground correction while retaining mapped FK, proportions and limb IK");
            }
            using(var grounded=BrawlRetargeter.Bake(source,target,clips["AttackAirF"],groundMode:GroundContactMode.Grounded))
                Check(grounded.Contacts.FloorEnabled,"Explicit Grounded enables source-floor analysis independently of the motion name");
            using(var precise=BrawlRetargeter.Bake(source,target,clips["Wait1"],groundMode:GroundContactMode.SourceContacts))
            using(var stable=BrawlRetargeter.Bake(source,target,clips["Wait1"]))
                Check(precise.Contacts.FloorEnabled && precise.Contacts.StabilizedStandingFeet.Length==0 && stable.Contacts.StabilizedStandingFeet.Length==2 && stable.Contacts.PlantedFootSamples>precise.Contacts.PlantedFootSamples,
                    "Source Contacts preserves small source lifts; Auto learns persistent standing support from motion geometry");
            foreach (string name in args.Contains("--contacts-only") || args.Contains("--reverse-only") ? new string[0] : FocusedSupportCases?new[]{"Wait1"}:new[] {"Wait1", "Attack11", "Run", "JumpF", "Wait2", "AppealHi"})
            {
                if (!clips.TryGetValue(name, out CHR0Node clip)) continue;
                float sourceError = 0;
                foreach (float key in new[] {0f, (clip.FrameCount - 1) * 0.37f, clip.FrameCount - 1f})
                {
                    RigPose sampled = BrawlRetargeter.Sample(source, clip, key);
                    sourceModel.ApplyCHR(clip, key + 1);
                    sourceError = Math.Max(sourceError, Enumerable.Range(0, source.Bones.Length).Max(i => Vector3.Distance(sampled.Point(i), BrawlRetargeter.Vector(source.Bones[i].Matrix.GetPoint()))));
                }
                Check(sourceError < 0.01, name + ": independent native FK agrees with adapter sampling at fractional times", new {MaximumError = sourceError});
                using (RetargetResult result = BrawlRetargeter.Bake(source, target, clip))
                {
                    string path = Path.Combine(output, name + "-Snake.chr0");
                    result.Animation.ExportChecked(path);
                    using (var reopened = (CHR0Node)NodeFactory.FromFile(null, path))
                    {
                        Check(reopened.FrameCount == clip.FrameCount && reopened.Loop == clip.Loop && reopened.Children.Count == target.Bones.Length &&
                            reopened.Children.All(c => target.Bones.Any(b => b.Name == c.Name)) && reopened.FindChild("BodyN", false) != null && reopened.FindChild("WaistN", false) == null,
                            name + ": new native CHR0 fresh-decodes with target-only tracks and original timing", new {reopened.FrameCount, reopened.Loop});
                        double integerError = 0, fractionalError = 0, skinError = 0, beforeIk = 0, afterIk = 0; int clamped = 0;
                        string worstBone = null; float worstFrame = 0;
                        foreach (float key in Enumerable.Range(0, clip.FrameCount).Select(i => (float)i).Concat(Enumerable.Range(0, clip.FrameCount - 1).Select(i => i + 0.37f)))
                        {
                            RigPose sampled = BrawlRetargeter.Sample(source, clip, key);
                            RetargetPose corrected = result.Contacts.Evaluate(key);
                            RigPose fk = plan.Evaluate(sampled, false).Pose;
                            targetModel.ApplyCHR(reopened, key + 1);
                            double error = Enumerable.Range(0, target.Bones.Length).Max(i => Vector3.Distance(corrected.Pose.Point(i), BrawlRetargeter.Vector(target.Bones[i].Matrix.GetPoint())));
                            if (key == (int)key) integerError = Math.Max(integerError, error); else if (error > fractionalError)
                            {
                                fractionalError = error; worstFrame = key;
                                worstBone = target.Bones.OrderByDescending((b) => Vector3.Distance(corrected.Pose.Point(Array.IndexOf(target.Bones,b)), BrawlRetargeter.Vector(b.Matrix.GetPoint()))).First().Name;
                            }
                            foreach (var goal in corrected.Goals)
                            {
                                beforeIk += Vector3.Distance(fk.Point(goal.Key), goal.Value);
                                afterIk += Vector3.Distance(corrected.Pose.Point(goal.Key), goal.Value);
                            }
                            clamped += corrected.ClampedLimbs;
                            var actual = Vertices(targetModel);
                            targetModel.ApplyCHR(result.Animation, key + 1);
                            var expected = Vertices(targetModel);
                            skinError = Math.Max(skinError, actual.Zip(expected, (a,b) => Vector3.Distance(a,b)).Max());
                            if (actual.Any(p => !RigMath.Finite(p))) throw new InvalidOperationException("Non-finite native skin.");
                        }
                        Check(integerError < 0.06 && skinError < 0.06, name + ": integer-frame native evaluation and skinned export round trip preserve the solved pose",
                            new {IntegerError = integerError, FractionalError = fractionalError, WorstBone = worstBone, WorstFrame = worstFrame, SkinRoundTripError = skinError, BeforeIk = beforeIk, AfterIk = afterIk, Clamped = clamped});
                        Check(fractionalError < 1.0, name + ": withheld fractional native samples remain within one target model unit", new {MaximumError = fractionalError, WorstBone=worstBone, WorstFrame=worstFrame});
                        Check(afterIk <= beforeIk + 0.01, name + ": proportion and limb correction reduce body-relative goal error", new {BeforeIk = beforeIk, AfterIk = afterIk, Clamped = clamped});
                    }
                }
            }
            AdditionalNativeCases(sourceModel, targetModel, clips["Attack11"], Path.Combine(snake, "FitSnakeMotionEtc.pac"), output, mario);
            }
            if(!args.Contains("--reverse-only"))ContactCases(source,target,clips,output,targetModel);
            using(ResourceNode snakeMotions=NodeFactory.FromFile(null,Path.Combine(snake,"FitSnakeMotionEtc.pac")))
                ReverseCases(target,source,snakeMotions,sourceModel,Path.Combine(mario,"FitMarioMotionEtc.pac"),Path.Combine(output,"Snake-to-Mario"));
        }
        var after = Hashes(mario).Concat(Hashes(snake)).ToDictionary(p => p.Key, p => p.Value);
        Check(before.Count == after.Count && before.All(p => after.TryGetValue(p.Key, out string v) && v == p.Value), "Every original source file retains its SHA256", new {Files = before.Count});
        string report = Path.Combine(output, "retarget-results.json");
        File.WriteAllText(report, new JavaScriptSerializer {MaxJsonLength = 16000000}.Serialize(new {Passed = true, Checks, SourceFiles = before.Count,
            ApplicationSha256=FighterProject.HashFile(typeof(FighterProject).Assembly.Location), LibrarySha256=FighterProject.HashFile(typeof(CHR0Node).Assembly.Location)}));
        Console.WriteLine("PASS: " + Checks.Count + " retarget checks. " + report);
        return 0;
    }
    private static Vector3[] Vertices(MDL0Node model) => model.PolygonList.Cast<MDL0ObjectNode>().SelectMany(o => o.Vertices).Select(v => BrawlRetargeter.Vector(v.WeightedPosition)).ToArray();
}
