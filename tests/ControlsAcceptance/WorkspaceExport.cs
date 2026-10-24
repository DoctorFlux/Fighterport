using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static async Task WorkspaceExport(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Private desktop required.");
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",Path.Combine(output,"isolated-library"));
        using (var startup = MainForm.StartViewerApplication())
        {
            var form = (ModelForm)Field(MainForm.Instance, "_fighterForm");
            try
            {
                Call(form, "ImportFighterModel", args[0]);
                Call(form, "ImportFighterAnimations", new[] { new[] { args[1] } });
                var editor = form.FighterEditor;
                var clip = editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().First(c => c.Name == "Wait1");
                editor.SelectedCHR0 = clip; editor.SetFrame(3); await SimpleControlsReady(form);
                var setup = await Await<ControlReply>(Call(form, "LoadControls"));
                editor.SetFrame(3);
                editor.SelectedBone = ((MDL0Node)editor.TargetModel)._linker.BoneCache.Single(b => b.Name == setup.BoneNames[setup.Roles.Single(r => r.Role == "Hips").Bone.Value]);
                string before = LifecycleContent(clip);
                var field = editor.CHR0Editor._transBoxes[3];
                field.Text = (editor.SelectedBone.FrameState._rotate._x + 2).ToString(); Call(field, "Apply");
                await SimpleControlsReady(form);
                string edited = LifecycleContent(clip); int undo = editor._undoSaves.Count;
                var children = clip.Children.ToArray();
                Check(edited != before && undo > 0 && form.FighterProject.ManifestPath == null, "Real imported body edit precedes project Save");
                string path = Path.Combine(output, "native-before-save.chr0");
                form.ExportSimpleWorkspaceResult(path);
                using (var decoded = NodeFactory.FromFile(null, path) as CHR0Node)
                    Check(decoded != null && NativeDifference(clip, decoded) == null, "Independent decoder matches all native keys/tangents/flags/timing before project Save");
                Check(edited == LifecycleContent(clip) && clip.Children.SequenceEqual(children) && editor._undoSaves.Count == undo && editor.CurrentFrame == 3, "Verified export preserves live node identities, exact document, frame and native Undo");
                editor.Undo(); await SimpleControlsReady(form); Check(LifecycleContent(clip) == before, "Native body Undo survives verified export");
                editor.Redo(); await SimpleControlsReady(form); Check(LifecycleContent(clip) == edited, "Native body Redo survives verified export");
                string hash = FighterProject.HashFile(path);
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    bool failed = false; try { form.ExportSimpleWorkspaceResult(path); } catch (IOException) { failed = true; }
                    Check(failed, "Locked publication reports failure");
                }
                Check(FighterProject.HashFile(path) == hash && !Directory.GetFiles(output, ".*.chr0").Any(), "Failed publication preserves existing destination and removes candidate");
                var frozen = Call(form, "FreezeWorkspaceExport"); editor.SetFrame(4);
                // Navigation alone is safe; content changes invalidate the frozen operation.
                clip.Loop = !clip.Loop;
                bool stale = false; try { Call(form, "ExportWorkspaceSnapshot", frozen, path); } catch (System.Reflection.TargetInvocationException e) { stale = e.InnerException is InvalidOperationException; }
                ((IDisposable)frozen).Dispose();
                clip.Loop = !clip.Loop; editor.SetFrame(3);
                Check(stale && FighterProject.HashFile(path) == hash, "Changed frozen animation dependencies invalidate publication without replacing destination");
                var corrupt = Call(form, "FreezeWorkspaceExport");
                ((CHR0Node)corrupt.GetType().GetField("Candidate",Private|System.Reflection.BindingFlags.Public).GetValue(corrupt)).Loop = !clip.Loop;
                bool invalid = false; try { Call(form, "ExportWorkspaceSnapshot", corrupt, path); } catch (System.Reflection.TargetInvocationException e) { invalid = e.InnerException is InvalidDataException; }
                ((IDisposable)corrupt).Dispose();
                Check(invalid && FighterProject.HashFile(path) == hash, "Independent validation rejects a damaged candidate and preserves the existing destination");
                foreach (int text in new[] { 100, 200 }) foreach (int width in new[] { 1050, 1700 })
                {
                    form.ClientSize = new Size(width, 950); Call(form, "SetWorkspaceTextSize", text);
                    using (FlowModal("workspaceFilePath", dialog =>
                    {
                        Check(SimpleControl<Label>(dialog, "workspaceFileDescription").Text.Contains(clip.Name), "Export dialog identifies the exact clip at " + width + "/" + text);
                        SimpleControl<TextBox>(dialog, "workspaceFilePath").Text = path;
                        CaptureDialog(dialog, output, "export-review-" + width + "-" + text + ".png");
                        using (FlowModal("workspaceOverwriteCancel", confirm => SimpleControl<Button>(confirm, "workspaceOverwriteCancel").PerformClick()))
                            SimpleControl<Button>(dialog, "workspaceFileAccept").PerformClick();
                        Check(dialog.Visible && FighterProject.HashFile(path) == hash, "Cancelled overwrite keeps dialog and original destination");
                        SimpleControl<Button>(dialog, "workspaceFileCancel").PerformClick();
                    })) Call(form, "ChooseSimpleWorkspaceExport");
                }
                using (FlowModal("workspaceFilePath", dialog =>
                {
                    SimpleControl<TextBox>(dialog,"workspaceFilePath").Text=path;
                    using(FlowModal("workspaceOverwriteAccept", confirm=>SimpleControl<Button>(confirm,"workspaceOverwriteAccept").PerformClick()))
                        SimpleControl<Button>(dialog,"workspaceFileAccept").PerformClick();
                    Check(dialog.DialogResult==DialogResult.OK,"Confirmed export publishes through the application-owned dialog: "+SimpleControl<Label>(dialog,"workspaceFileValidation").Text);
                })) Call(form,"ChooseSimpleWorkspaceExport");
                Check(FighterProject.HashFile(path)==hash,"Confirmed atomic overwrite retains independently verified exact content");
                Check(edited == LifecycleContent(clip) && editor._undoSaves.Count == undo, "All review/cancellation layouts preserve live channels and Undo");
                // Original procedural sparse tracks and native metadata exercise exactness independently of game fixtures.
                var fixture = new CHR0Node { Name = "Wait1", Version = 5, FrameCount = 19, Loop = true, OriginalPath = "authored/original" };
                fixture.UserEntries.Add(new UserDataClass { Name = "author", DataType = UserValueType.String, Entries = new[] { "procedural" } });
                fixture.UserEntries.Add(new UserDataClass { Name = "number", DataType = UserValueType.Int, Entries = new[] { "42", "-7" } });
                fixture.UserEntries.Add(new UserDataClass { Name="precise-float", DataType=UserValueType.Float, Entries=new[]{1.2345679f.ToString("R")} });
                var track = fixture.CreateEntry("Body");
                track.SetKeyframe(3, 0, 1.234567f, true, true)._tangent = .1234567f;
                track.SetKeyframe(3, 13, -5.678912f, true, true)._tangent = -.7654321f;
                track.UseModelTranslate = true; track.ScaleCompensateApply = true;
                fixture.CreateEntry("AbsentChannels").UseModelRotate = true;
                // Exercise the same staging through a registered native import, never normalize test expectations.
                PreciseCHR0EntryNode.Promote(fixture);
                string fixturePath = Path.Combine(output, "procedural-input.chr0"); fixture.ExportChecked(fixturePath); fixture.Dispose();
                Call(form, "ImportFighterAnimations", new[] { new[] { fixturePath } });
                var procedural = editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Single(c => c.Name == "Wait1" && c.Version == 5);
                editor.SelectedCHR0 = procedural; await SimpleControlsReady(form);
                string proceduralContent = LifecycleContent(procedural), result = Path.Combine(output, "procedural-output.chr0");
                bool originalProtected=false;try{form.ExportSimpleWorkspaceResult(fixturePath);}catch(InvalidOperationException){originalProtected=true;}
                Check(originalProtected,"Original CHR0 input is protected even when overwrite is requested");
                form.ExportSimpleWorkspaceResult(result);
                using (var decoded = NodeFactory.FromFile(null, result) as CHR0Node)
                    Check(NativeDifference(procedural, decoded) == null && decoded.Version == 5 && decoded.OriginalPath == "authored/original" && decoded.UserEntries[0].Entries.Single() == "procedural" && decoded.UserEntries[1].Entries.SequenceEqual(new[] { "42", "-7" }) && float.Parse(decoded.UserEntries[2].Entries.Single()) == 1.2345679f, "Sparse/absent tracks and original version/path/user metadata survive precise candidate decoding");
                Check(LifecycleContent(procedural) == proceduralContent, "Procedural export leaves live native content unchanged");
            }
            finally { SimpleDisposeForm(form); }
        }
    }
}
