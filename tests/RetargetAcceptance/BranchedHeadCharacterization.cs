using BrawlCrate.Studio.Retargeting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // A tall appendage is not evidence that its parent is the central head.
    // These fixtures contain no game, character or action names and vary native
    // storage order while retaining the same authentic parent relationships.
    private static int BranchedHeadCharacterizationChecks(string output)
    {
        Directory.CreateDirectory(output);
        var observations = new List<object>();
        foreach (bool permuted in new[] { false, true })
        foreach (string convention in new[] { "mixamo", "unreal" })
        {
            var ambiguous = BranchedHeadFixture(false, false, permuted, convention);
            var proposal = SkeletonCharacterizer.Analyze(ambiguous.Joints);
            Check(!proposal.Roles.ContainsKey("head") && proposal.Missing.Contains("head") &&
                proposal.Warnings.Any(w => w.Contains("no appendage tip was assigned as the head")),
                "Separate nonzero cranial branches require review instead of assigning the tallest appendage as head",
                new { permuted, convention, proposal.Warnings });
            bool blocked = false;
            try { SkeletonCharacterizer.Validate(ambiguous.Joints, proposal.Roles); }
            catch (InvalidDataException error) { blocked = error.Message.Contains("head"); }
            Check(blocked, "Automatic retarget admission rejects an unresolved central head binding");
            var reviewed = new Dictionary<string, int>(proposal.Roles) { { "head", ambiguous.Head } };
            SkeletonCharacterizer.Validate(ambiguous.Joints, reviewed);
            Check(reviewed["head"] != reviewed["neck"], "An explicit distinct reviewed head remains an available mapping path");

            var central = BranchedHeadFixture(true, false, permuted, convention);
            var inferred = SkeletonCharacterizer.Analyze(central.Joints);
            Check(inferred.Roles.TryGetValue("head", out int head) && head == central.Head &&
                inferred.Evidence["head"].StartsWith("bind geometry", StringComparison.Ordinal),
                "A central unnamed head joint stays inferable when taller appendages branch beneath it",
                new { permuted, convention, head, central.Head });
            SkeletonCharacterizer.Validate(central.Joints, inferred.Roles);

            var named = BranchedHeadFixture(false, true, permuted, convention);
            var namedMap = SkeletonCharacterizer.Analyze(named.Joints);
            Check(namedMap.Roles.TryGetValue("head", out int namedHead) && namedHead == named.Head &&
                namedMap.Evidence["head"] == "name + hierarchy validation",
                "An explicitly named head remains selected despite a taller sibling appendage",
                new { permuted, convention, namedHead, named.Head });
            SkeletonCharacterizer.Validate(named.Joints, namedMap.Roles);
            observations.Add(new { permuted, convention, AmbiguousHeadBlocked = blocked,
                CentralHead = head, NamedHead = namedHead });
        }
        File.WriteAllText(Path.Combine(output, "branched-head-characterization.json"),
            new JavaScriptSerializer().Serialize(new { Passed = true, Checks, Observations = observations }));
        return 0;
    }

    private sealed class BranchedHeadRig
    {
        internal RigJoint[] Joints;
        internal int Head;
    }

    private static BranchedHeadRig BranchedHeadFixture(bool central, bool named, bool permuted, string convention)
    {
        var baseRig = CoordinateFixture("anonymous-core-", 1, 0, 0, Quaternion.Identity).Rig;
        var names = baseRig.Joints.Select((_, i) => "unlabeled" + i).ToArray();
        foreach (var role in baseRig.Roles.Where(pair => pair.Key != "head"))
        {
            string suggestion = GenericRoleName(role.Key, convention);
            if (suggestion != null) names[role.Value] = suggestion;
        }
        int neck = baseRig.Roles["neck"], head = baseRig.Roles["head"];
        if (named) names[head] = "Head";
        var joints = baseRig.Joints.Select((joint, i) => new RigJoint(names[i], joint.Parent, joint.Bind)).ToList();
        void Add(string name, int parent, Vector3 translation) => joints.Add(new RigJoint(name, parent,
            new JointTransform(translation, Quaternion.Identity, Vector3.One)));
        if (central)
        {
            // The tallest tip is beyond the real central branching head.
            Add("unlabeled-a", head, new Vector3(0, 9, -1));
            Add("unlabeled-b", head, new Vector3(1, 3, 0));
        }
        else
        {
            // The tallest chain shares only the neck with a distinct central
            // head chain. Bind geometry alone cannot choose between branches.
            int tall = joints.Count;
            Add("unlabeled-a", neck, new Vector3(0, 8, -1));
            Add("unlabeled-a-tip", tall, new Vector3(0, 2, -2));
            Add("unlabeled-b", neck, new Vector3(-1, 4, 0));
        }
        if (!permuted) return new BranchedHeadRig { Joints = joints.ToArray(), Head = head };
        var order = new List<int>();
        Action<int> visit = null;
        visit = parent =>
        {
            foreach (int child in Enumerable.Range(0, joints.Count).Where(i => joints[i].Parent == parent).Reverse())
            { order.Add(child); visit(child); }
        };
        visit(-1);
        var indices = order.Select((old, index) => new { old, index }).ToDictionary(pair => pair.old, pair => pair.index);
        return new BranchedHeadRig
        {
            Joints = order.Select(old => new RigJoint(joints[old].Name,
                joints[old].Parent < 0 ? -1 : indices[joints[old].Parent], joints[old].Bind)).ToArray(),
            Head = indices[head]
        };
    }
}
