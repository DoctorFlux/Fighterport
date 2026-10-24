// Native and persistence acceptance, using local user-provided files only.
// This harness has no bundled game assets and does not generate replacements.
using BrawlCrate.Studio;
using BrawlLib.Internal;
using BrawlLib.OpenGL;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

internal static partial class Program
{
    private static readonly List<object> Results = new List<object>();

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] == "--reopen")
                return ReopenChild(args);
            ViewportSelectionRegression();
            DispatcherTimingRegression();
            if (args.Length > 0 && args[0] == "--viewport-only")
            {
                Console.WriteLine("PASS: viewport replacement keeps the active camera on the visible viewport without creating a GL handle.");
                return 0;
            }
            string marioRoot = Option(args, "--mario-root") ?? Environment.GetEnvironmentVariable("FIGHTERPORT_TEST_MARIO_ROOT");
            string snakeRoot = Option(args, "--snake-root") ?? Environment.GetEnvironmentVariable("FIGHTERPORT_TEST_SNAKE_ROOT");
            if (string.IsNullOrEmpty(marioRoot) || string.IsNullOrEmpty(snakeRoot))
                throw new ArgumentException("Supply --mario-root and --snake-root, or FIGHTERPORT_TEST_MARIO_ROOT / FIGHTERPORT_TEST_SNAKE_ROOT. Files stay local.");
            string output = Path.GetFullPath(Option(args, "--output") ?? Path.Combine(Path.GetTempPath(), "FighterPortV2Acceptance", Guid.NewGuid().ToString("N")));
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Acceptance output must be a new or empty directory: " + output);
            Directory.CreateDirectory(output);
            Dictionary<string, string> sourceHashes = HashTree(marioRoot).Concat(HashTree(snakeRoot)).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);

            NativeCase(marioRoot, "Mario", "FitMario00", 297, Path.Combine(output, "Mario"));
            NativeCase(snakeRoot, "Snake", "FitSnake00", 355, Path.Combine(output, "Snake"));
            StorageCases(Path.Combine(marioRoot, "FitMario00.pac"), Path.Combine(marioRoot, "FitMarioMotionEtc.pac"), Path.Combine(output, "Storage"));

            Dictionary<string, string> finalHashes = HashTree(marioRoot).Concat(HashTree(snakeRoot)).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
            Check(sourceHashes.Count == finalHashes.Count && sourceHashes.All(p => finalHashes.TryGetValue(p.Key, out string value) && value == p.Value), "Every provided original source file retains its full SHA256 after native and persistence checks", new { Files = sourceHashes.Count });
            var report = new { Passed = true, Checks = Results, SourceFiles = sourceHashes.Count, NativeBackend = typeof(MDL0Node).Assembly.FullName, Product = typeof(FighterProject).Assembly.FullName };
            string reportPath = Path.Combine(output, "acceptance-results.json");
            File.WriteAllText(reportPath, new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.Serialize(report), new UTF8Encoding(false));
            Console.WriteLine("PASS: " + Results.Count + " acceptance checks; " + sourceHashes.Count + " original files unchanged.");
            Console.WriteLine(reportPath);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void ViewportSelectionRegression()
    {
        using (var panel = new UncreatedViewportPanel())
        {
            GLViewport removed = GLViewport.DefaultPerspective;
            panel.AddViewport(removed);
            if (!ReferenceEquals(panel.CurrentViewport, removed))
                throw new InvalidOperationException("Viewport regression precondition failed.");
            panel.SeedHighlightedViewport(removed);
            panel.ClearViewports();
            bool removedReferencesCleared = panel.CachedCurrentViewport == null && panel.CachedHighlightedViewport == null;
            GLViewport replacement = GLViewport.DefaultPerspective;
            panel.AddViewport(replacement);
            Check(removedReferencesCleared && ReferenceEquals(panel.CurrentViewport, replacement) &&
                  !ReferenceEquals(panel.CurrentViewport, removed) && ReferenceEquals(panel.CurrentViewport.Camera, replacement.Camera) &&
                  !panel.IsHandleCreated && panel.Context == null,
                "Replacing native viewports clears removed camera references and selects the visible replacement before any mouse click or GL handle creation");
        }
    }

    private sealed class UncreatedViewportPanel : GLPanel
    {
        public GLViewport CachedCurrentViewport => _currentViewport;
        public GLViewport CachedHighlightedViewport => _highlightedViewport;
        public void SeedHighlightedViewport(GLViewport viewport) { _highlightedViewport = viewport; }
    }

    private static void NativeCase(string sourceRoot, string fighter, string mainModelName, int expectedClips, string output)
    {
        Directory.CreateDirectory(output);
        string modelPath = Path.Combine(sourceRoot, "Fit" + fighter + "00.pac");
        string motionsPath = Path.Combine(sourceRoot, "Fit" + fighter + "MotionEtc.pac");
        string modelHash = Hash(modelPath), motionHash = Hash(motionsPath);
        using (ResourceNode costume = NodeFactory.FromFile(null, modelPath))
        using (ResourceNode motion = NodeFactory.FromFile(null, motionsPath))
        {
            Check(costume is ARCNode && motion is ARCNode, fighter + ": actual costume and separate motion sources decode as native ARC/PAC");
            costume.Populate(); motion.Populate();
            List<ResourceNode> costumeNodes = Walk(costume).ToList(), motionNodes = Walk(motion).ToList();
            MDL0Node model = costumeNodes.OfType<MDL0Node>().Single(m => m.Name == mainModelName);
            List<CHR0Node> clips = motionNodes.OfType<CHR0Node>().ToList();
            int textures = costumeNodes.OfType<TEX0Node>().Count();
            Check(model.AllBones.Count > 50 && model.NumVertices > 1000 && model.NumTriangles > 1000 && textures > 0,
                fighter + ": primary native model contains skeleton, visible geometry and actual textures", new { Bones = model.AllBones.Count, Vertices = model.NumVertices, Triangles = model.NumTriangles, Textures = textures });
            Check(clips.Count == expectedClips, fighter + ": separate motion PAC lists every native CHR0", new { Clips = clips.Count });
            HashSet<string> bones = new HashSet<string>(model.AllBones.Select(b => b.Name), StringComparer.OrdinalIgnoreCase);
            int modelCompatible = clips.Count(c => c.Children.OfType<CHR0EntryNode>().Any(e => bones.Contains(e.Name)));
            Check(modelCompatible > 200, fighter + ": fighter animations resolve real target skeleton bones while weapon motions remain separately identifiable", new { CompatibleClips = modelCompatible, TotalClips = clips.Count });
            CHR0Node[] examples = new[] { "Attack11", "Run", "WalkFast", "Wait1", "Wait2" }
                .Select(n => clips.FirstOrDefault(c => c.Name == n)).Where(c => c != null && c.FrameCount > 5).Take(2).ToArray();
            Check(examples.Length == 2, fighter + ": two familiar fighter animation examples are present");
            PlaybackTimingRegression(examples[0], fighter);
            BodyPlaybackRegression(model, examples[0], fighter);
            foreach (CHR0Node clip in examples)
            {
                Vector3[] first = Pose(model, clip, 1);
                double maximumDelta = new[] { Math.Max(2, clip.FrameCount / 3), Math.Max(2, clip.FrameCount / 2), clip.FrameCount }
                    .Select(frame => MaximumDistance(first, Pose(model, clip, frame))).Max();
                Check(maximumDelta > 0.1, fighter + ": " + clip.Name + " changes the actual skinned mesh while scrubbing native frames", new { clip.FrameCount, clip.Loop, MaximumVertexDelta = maximumDelta });
            }

            string exportedPac = Path.Combine(output, "Costume-roundtrip.pac");
            costume.Export(exportedPac);
            using (ResourceNode reopened = NodeFactory.FromFile(null, exportedPac))
            {
                reopened.Populate();
                List<ResourceNode> reopenedNodes = Walk(reopened).ToList();
                MDL0Node reopenedModel = reopenedNodes.OfType<MDL0Node>().Single(m => m.Name == mainModelName);
                Check(reopened is ARCNode && reopenedModel.NumTriangles == model.NumTriangles && reopenedModel.AllBones.Count == model.AllBones.Count && reopenedNodes.OfType<TEX0Node>().Count() == textures,
                    fighter + ": native costume output fresh-decodes with mesh, skeleton and textures intact");
            }

            CHR0Node edited = examples[0];
            CHR0EntryNode entry = edited.Children.OfType<CHR0EntryNode>().First(e => bones.Contains(e.Name));
            float value = entry.GetFrameValue(5, 0) + 7;
            entry.SetKeyframe(5, 0, value);
            string editedChr = Path.Combine(output, "Edited-" + edited.Name + ".chr0");
            edited.Export(editedChr);
            using (CHR0Node reopened = CHR0Node.FromFile(editedChr))
            {
                Check(reopened.Name == edited.Name && reopened.FrameCount == edited.FrameCount && reopened.Loop == edited.Loop && Math.Abs(((CHR0EntryNode)reopened.FindChild(entry.Name, false)).GetFrameValue(5, 0) - value) < 0.03,
                    fighter + ": edited native CHR0 saves and reopens with exact key and frame metadata");
                Check(Pose(model, reopened, 1).All(Finite), fighter + ": reopened edited native animation produces finite skinned vertices");
            }
        }
        Check(Hash(modelPath) == modelHash && Hash(motionsPath) == motionHash, fighter + ": full original costume and motion PAC hashes unchanged");
    }

    private static Vector3[] Pose(MDL0Node model, CHR0Node clip, int frame)
    {
        model.ApplyCHR(clip, frame);
        return model.PolygonList.Cast<MDL0ObjectNode>().SelectMany(o => o.Vertices).Select(v => v.WeightedPosition).ToArray();
    }

    private static bool Finite(Vector3 point)
    {
        return !float.IsNaN(point._x) && !float.IsNaN(point._y) && !float.IsNaN(point._z) && !float.IsInfinity(point._x) && !float.IsInfinity(point._y) && !float.IsInfinity(point._z);
    }

    private static double MaximumDistance(Vector3[] left, Vector3[] right)
    {
        if (left.Length != right.Length || left.Length == 0) throw new InvalidOperationException("Native animation changed the vertex count.");
        return left.Zip(right, (a, b) => Math.Sqrt(Math.Pow(a._x - b._x, 2) + Math.Pow(a._y - b._y, 2) + Math.Pow(a._z - b._z, 2))).Max();
    }

    private static IEnumerable<ResourceNode> Walk(ResourceNode root)
    {
        yield return root;
        foreach (ResourceNode child in root.Children)
            foreach (ResourceNode descendant in Walk(child)) yield return descendant;
    }

    private static Dictionary<string, string> HashTree(string root)
    {
        return Directory.EnumerateFiles(Path.GetFullPath(root), "*", SearchOption.AllDirectories).ToDictionary(p => p, Hash, StringComparer.OrdinalIgnoreCase);
    }

    private static string Hash(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    private static string Option(string[] args, string key)
    {
        int index = Array.IndexOf(args, key);
        return index < 0 ? null : index + 1 < args.Length ? args[index + 1] : throw new ArgumentException("Missing value for " + key);
    }

    private static void Check(bool passed, string description, object evidence = null)
    {
        if (!passed) throw new InvalidOperationException("FAIL: " + description);
        Results.Add(new { Check = description, Passed = true, Evidence = evidence });
    }

    private static void MustReject(Action action, string description, string expectedMessage = null)
    {
        Exception rejected = null;
        try { action(); }
        catch (Exception error) when (error is InvalidDataException || error is IOException || error is UnauthorizedAccessException ||
                                      error is ArgumentException || error is System.Xml.XmlException ||
                                      error is InvalidOperationException || error is NotSupportedException)
        { rejected = error; }
        Check(rejected != null && (expectedMessage == null || rejected.ToString().IndexOf(expectedMessage, StringComparison.OrdinalIgnoreCase) >= 0), description,
            rejected == null ? null : new { ExceptionType = rejected.GetType().Name, rejected.Message, ExpectedMessage = expectedMessage, InnerMessage = rejected.InnerException?.Message });
    }
}
