using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    // Dedicated layout evidence: one owned native document, two explicit Analyze
    // operations, sixteen actual offscreen native-renderer captures, no Apply.
    static async Task WorkspaceOptimizationLayout(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Optimization layout capture requires the private desktop.");
        string priorLibrary = Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", Path.Combine(output, "isolated-library"));
        var originals = new[] { args[0], args[1] }.Distinct().ToDictionary(path => path, FighterProject.HashFile);
        var project = FighterProject.Create("Native optimization text layout");
        var form = new ModelForm();
        var layouts = new List<object>();
        try
        {
            form.InitializeFighterProject(project); form.Show(); form.ClientSize = new Size(1700, 950);
            Call(form, "SetWorkspaceTextSize", 100);
            Call(form, "ImportFighterModel", args[0]);
            Call(form, "ImportFighterAnimations", new[] { new[] { args[1] } });
            await GameLibraryWait(() => form.FighterEditor.TargetModel != null && form.FighterEditor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Any(),
                "Native layout target and motion did not open.");
            await SimpleControlsReady(form);
            var editor = form.FighterEditor;
            var clips = editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().ToArray();
            var clip = clips.FirstOrDefault(c => c.Name == "AttackS3S" && !c.Loop && c.FrameCount >= 12 && c.FrameCount <= 96)
                ?? clips.First(c => !c.Loop && c.FrameCount >= 12 && c.FrameCount <= 96);
            foreach (ListViewItem item in editor.leftPanel.listAnims.Items) item.Selected = ReferenceEquals(item.Tag, clip);
            editor.SelectedCHR0 = clip; await SimpleControlsReady(form);
            var setup = await Await<ControlReply>(Call(form, "LoadControls"));
            var model = (MDL0Node)editor.TargetModel;
            string bodyName = setup.BoneNames[setup.Roles.Single(role => role.Role == "Hips").Bone.Value];
            var bone = model.AllBones.Single(b => b.Name == bodyName);
            var track = clip.Children.OfType<CHR0EntryNode>().SingleOrDefault(t => t.Name == bodyName) ?? clip.CreateEntry(bodyName);
            var scale = track.KeyArrays[0]; while (scale.KeyCount > 0) scale.Remove(scale.Entries.First()._index);
            for (int frame = 0; frame < clip.FrameCount; frame++) track.Keyframes.SetFrameValue(0, frame, bone.BindState[0], true)._tangent = 0;
            // The native scale group must retain explicit authentic sibling axes.
            for (int channel = 1; channel < 3; channel++)
                if (track.KeyArrays[channel].KeyCount == 0) track.Keyframes.SetFrameValue(channel, 0, bone.BindState[channel], true)._tangent = 0;
            track.UseModelScale = false; track.Keyframes.PreserveInterpolation = true;
            PreciseCHR0EntryNode.Promote(clip); clip.SignalPropertyChange(); Call(form, "MarkFighterChanged");
            editor.SelectedBone = bone; editor.SetFrame(3); editor.UpdateModel(); editor.FrameFighterModel();
            await SimpleControlsReady(form);
            int beforeKeys = OptimizationKeyCount(clip), beforeApplies = form.OptimizationApplies;
            string content = NativeAnimationOptimization.ContentKey(clip);
            var witness = new GameLibraryDocument(form);
            bool complete = false; Exception failure = null; int analyzes = 0;
            form.OptimizationReady = async window =>
            {
                try
                {
                    var mode = SimpleControl<ComboBox>(window, "optimizationMode");
                    Check(mode.Items.Count == 2 && mode.SelectedIndex == 0 && !window.ReadHighQuality(), "Layout case starts in Exact with the qualified High quality mode available");
                    foreach (int selectedMode in new[] { 0, 1 })
                    {
                        if (mode.SelectedIndex != selectedMode) mode.SelectedIndex = selectedMode;
                        await window.Analyze(); analyzes++;
                        string modeName = selectedMode == 0 ? "Exact" : "High quality";
                        Check(!window.Busy && window.Candidate != null && window.PreparedCanApply && OptimizationKeyCount(window.Candidate) < beforeKeys,
                            modeName + " layout uses a real independently prepared native reduction");
                        foreach (int text in new[] { 100, 125, 150, 175 })
                        foreach (var size in new[] { new Size(980, 720), new Size(1600, 960) })
                        {
                            Call(form, "SetWorkspaceTextSize", text); window.ApplyTextSize(); window.ClientSize = size; window.PerformLayout();
                            window.SourceView.ShownFrame(); window.TargetView.ShownFrame();
                            var visible = SimpleDescendants(window).Where(control => control.Visible).ToArray();
                            foreach (var control in visible)
                            {
                                var bounds = window.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
                                string name = string.IsNullOrEmpty(control.Name) ? control.GetType().Name : control.Name;
                                bool fits = control.ClientSize.Width > 0 && control.ClientSize.Height > 0 && window.ClientRectangle.Contains(bounds) &&
                                    (control.Parent == null || control.Parent.ClientRectangle.Contains(control.Bounds));
                                if (!fits)
                                {
                                    string fault = "optimization-layout-failure-" + selectedMode + "-" + size.Width + "-" + text;
                                    File.WriteAllText(Path.Combine(output, fault + ".json"), new JavaScriptSerializer().Serialize(new { Name = name, Mode = modeName, TextPercent = text,
                                        ControlBounds = control.Bounds.ToString(), ControlClient = control.ClientRectangle.ToString(), DialogBounds = bounds.ToString(), DialogClient = window.ClientRectangle.ToString(),
                                        ParentName = control.Parent?.Name, ParentClient = control.Parent?.ClientRectangle.ToString(), FontHeight = control.Font.Height,
                                        ComboPreferredHeight = mode.PreferredHeight, ComboHeight = mode.Height, ComboItemHeight = mode.ItemHeight }));
                                    await CaptureOptimization(window, Path.Combine(output, fault + ".png"));
                                }
                                Check(fits, "Visible optimization layout fits its parent and dialog: " + name + " / " + modeName + " / " + text + "% / " + size.Width +
                                    " control=" + control.Bounds + " parent=" + control.Parent?.ClientRectangle);
                            }
                            Check(window.SourceView.Viewport.Width > 0 && window.SourceView.Viewport.Height > 0 && window.TargetView.Viewport.Width > 0 && window.TargetView.Viewport.Height > 0,
                                "Both original native renderers remain visible: " + modeName + " / " + text + "% / " + size.Width);
                            string image = "optimization-layout-" + (selectedMode == 0 ? "exact" : "hq") + "-" + size.Width + "-" + text + ".png";
                            await CaptureOptimization(window, Path.Combine(output, image));
                            witness.CheckPreserved("optimization layout " + modeName + " " + text + "% " + size.Width);
                            layouts.Add(new { Mode = modeName, TextPercent = text, Width = size.Width, Height = size.Height, VisibleControls = visible.Length, Image = image,
                                SourceWidth = window.SourceView.Viewport.Width, SourceHeight = window.SourceView.Viewport.Height,
                                TargetWidth = window.TargetView.Viewport.Width, TargetHeight = window.TargetView.Viewport.Height });
                        }
                    }
                }
                catch (Exception error) { failure = error; }
                finally { if (!window.IsDisposed) window.Close(); complete = true; }
            };
            Call(form, "ShowAnimationOptimization");
            await GameLibraryWait(() => complete, "Native optimization text layout did not finish.", 180);
            form.OptimizationReady = null;
            if (failure != null) throw failure;
            Check(analyzes == 2 && layouts.Count == 16 && form.OptimizationApplies == beforeApplies,
                "Dedicated layout case uses exactly two Analyze actions and sixteen native captures without Apply");
            witness.CheckPreserved("closed Exact/HQ text-layout comparison");
            Check(NativeAnimationOptimization.ContentKey(clip) == content && originals.All(pair => FighterProject.HashFile(pair.Key) == pair.Value),
                "Text-layout verification preserves the exact owned native clip and every original input file");
            File.WriteAllText(Path.Combine(output, "optimization-layout-cases.json"), new JavaScriptSerializer().Serialize(new { Analyzes = analyzes, Applies = 0, Layouts = layouts }));
        }
        finally
        {
            form.OptimizationReady = null;
            if (!form.IsDisposed) { Call(form, "SetWorkspaceTextSize", 100); SimpleDisposeForm(form); }
            project.Dispose(); Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", priorLibrary);
        }
    }
}
