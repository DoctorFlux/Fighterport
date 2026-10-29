using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using BrawlLib.Wii.Models;
using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // The literal original has N=8, nine stored rotation samples and an exact
    // held terminal: [..., 5, 5]. Change only the authored interior at t=2.
    // Reusing L1 would erase that edit; I12 must retain the edited Hermite
    // tuples. A distinct hidden terminal or changed final tuple is ineligible.
    static void NativeEditedDenseRetentionChecks(string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Edited dense retention qualification requires the private desktop.");
        Directory.CreateDirectory(output);
        foreach (int version in new[] { 4, 5 })
        {
            string path = EditedDenseMixedFixture(output, "held-v" + version, version, false, true);
            string originalHash = FighterProject.HashFile(path);
            using (var clip = NodeFactory.FromFile(null, path) as CHR0Node)
            {
                var entry = (CHR0EntryNode)clip.Children.Single();
                Check(entry.WorkingUncompressed.Length == 0 && IndexedCHR0EntryStorage.HasEncodedDenseRotation(entry),
                    "Edited dense v" + version + ": zero-length native child is bounded by its complete owner and still identified as sampled rotation");
                var originalStorage = PreciseCHR0EntryNode.CaptureStorage(entry);
                string translation = originalStorage[6].ContentKey;
                Check(originalStorage[3].IsDense && originalStorage[3].EncodedValues.Last() == originalStorage[3].EncodedValues[7],
                    "Edited dense v" + version + ": independent original contains the complete held ninth sample");
                EditDenseInterior(entry);
                Check(PreciseCHR0EntryNode.CaptureStorage(entry) == null, "Edited dense v" + version + ": strict imported adoption still refuses a changed dense axis");
                var captured = PreciseCHR0EntryNode.CaptureStorageForWrite(entry);
                Console.WriteLine("TRACE edited dense write capture: "+captured.Diagnostic);
                Check(captured.Accepted && captured.UsesDenseI12Fallback && captured.Storage != null &&
                    !captured.Storage.MatchesGroup(entry.Keyframes, 1) && captured.Storage.MatchesGroup(entry.Keyframes, 2) &&
                    captured.Storage[6].ContentKey == translation && captured.Diagnostic.Contains("Same-native interpolation only"),
                    "Edited dense v" + version + ": explicit bounded write capture distinguishes an interior edit from an unknown terminal and retains the unrelated indexed group");
                string expected = NativeAnimationOptimization.SourceContentKey(clip);
                string copied = Path.Combine(output, "held-v" + version + "-copy.chr0");
                using (var snapshot = NativeAnimationOptimization.CopySource(clip)) snapshot.ExportChecked(copied);
                EditedDenseRoundtrip(entry, expected, translation, copied, version, "source-aware copy");
                PreciseCHR0EntryNode.Promote(clip);
                Check(NativeAnimationOptimization.SourceContentKey(clip) == expected,
                    "Edited dense v" + version + ": promotion changes the write policy without changing edited authoring data");
                string promoted = Path.Combine(output, "held-v" + version + "-promoted.chr0"); clip.ExportChecked(promoted);
                EditedDenseRoundtrip((CHR0EntryNode)clip.Children.Single(), expected, translation, promoted, version, "native promotion");
                File.WriteAllText(Path.Combine(output, "held-v" + version + "-capture.json"), new JavaScriptSerializer().Serialize(new
                {
                    captured.Accepted, captured.UsesDenseI12Fallback, captured.Diagnostic,
                    OriginalSha256 = originalHash, CopySha256 = FighterProject.HashFile(copied), PromotedSha256 = FighterProject.HashFile(promoted),
                    Scope = "Same native decoded Hermite master and retained held endpoint; no general game/runtime compatibility or new sampled encoding claim."
                }));
            }
            Check(FighterProject.HashFile(path) == originalHash, "Edited dense v" + version + ": both write routes leave the immutable original bytes unchanged");
            // Match the worker's actual order: adopt the original plan first,
            // then edit it. Direct Export must enforce the same terminal gate
            // as CopySource; it must not bypass it through a precise entry.
            using(var clip=NodeFactory.FromFile(null,path) as CHR0Node)
            {
                PreciseCHR0EntryNode.Promote(clip);var entry=(CHR0EntryNode)clip.Children.Single();
                string translation=PreciseCHR0EntryNode.CaptureStorage(entry)[6].ContentKey;
                EditDenseInterior(entry);var capture=PreciseCHR0EntryNode.CaptureStorageForWrite(entry);
                Check(capture.Accepted&&capture.UsesDenseI12Fallback,"Edited dense v"+version+": promote-before-edit uses the shared held-terminal proof");
                string expected=NativeAnimationOptimization.SourceContentKey(clip);
                string direct=Path.Combine(output,"held-v"+version+"-precise-direct.chr0");clip.ExportChecked(direct);
                EditedDenseRoundtrip(entry,expected,translation,direct,version,"promote-before-edit direct export");
                string copied=Path.Combine(output,"held-v"+version+"-precise-copy.chr0");
                using(var snapshot=NativeAnimationOptimization.CopySource(clip))snapshot.ExportChecked(copied);
                EditedDenseRoundtrip(entry,expected,translation,copied,version,"promote-before-edit source copy");
            }
            string unsupported=EditedDenseMixedFixture(output,"precise-distinct-v"+version,version,true,true);
            using(var clip=NodeFactory.FromFile(null,unsupported) as CHR0Node)
            {
                PreciseCHR0EntryNode.Promote(clip);EditDenseInterior((CHR0EntryNode)clip.Children.Single());
                EditedDenseReject(clip,"precise distinct hidden terminal / v"+version,Path.Combine(output,"precise-distinct-v"+version+"-rejected.chr0"));
            }
            foreach (string failure in new[] { "distinct-terminal", "changed-final-value", "changed-final-tangent", "changed-fixed-axis", "changed-loop", "changed-duration", "changed-flags" })
            {
                string bad = EditedDenseMixedFixture(output, failure + "-v" + version, version, failure == "distinct-terminal", true);
                using (var clip = NodeFactory.FromFile(null, bad) as CHR0Node)
                {
                    var entry = (CHR0EntryNode)clip.Children.Single(); EditDenseInterior(entry);
                    if (failure == "changed-final-value") entry.KeyArrays[3].Entries.Last()._value += .25f;
                    if (failure == "changed-final-tangent") entry.KeyArrays[3].Entries.Last()._tangent += .25f;
                    if (failure == "changed-fixed-axis") entry.KeyArrays[4].Entries.Single()._value = .25f;
                    if (failure == "changed-loop") clip.Loop = true;
                    if (failure == "changed-duration") clip.FrameCount++;
                    if (failure == "changed-flags") entry.ClassicScaleOff = !entry.ClassicScaleOff;
                    entry.SignalPropertyChange();
                    EditedDenseReject(clip, failure + " / v" + version);
                }
            }
            string gap = EditedDenseLiteral(output, "unbounded-v" + version, version, false, false);
            using (var clip = NodeFactory.FromFile(null, gap) as CHR0Node)
            { EditDenseInterior((CHR0EntryNode)clip.Children.Single()); EditedDenseReject(clip, "unbounded native terminal / v" + version); }
            using (var clip = NodeFactory.FromFile(null, path) as CHR0Node)
            {
                PreciseCHR0EntryNode.Promote(clip);
                var precise = (CHR0EntryNode)clip.Children.Single(); PreciseCHR0EntryNode.RestoreStorage(precise, null);
                var captured = PreciseCHR0EntryNode.CaptureStorageForWrite(precise);
                Check(captured.Accepted && !captured.UsesDenseI12Fallback && captured.Storage == null &&
                    PreciseCHR0EntryNode.CaptureStorage(precise) == null,
                    "Edited dense v" + version + ": explicit precise null remains authoritative and cannot re-adopt the old dense backing");
            }
            EditedDenseOpaqueRuleChecks(output,version);
        }
    }

    static void EditedDenseOpaqueRuleChecks(string output,int version)
    {
        string path=EditedDenseMixedFixture(output,"held-rule2-v"+version,version,false,true);
        byte[] original=File.ReadAllBytes(path);DenseWrite32(original,version==4?0x24:0x28,2);File.WriteAllBytes(path,original);
        string originalHash=FighterProject.HashFile(path);
        foreach(bool promoteFirst in new[]{false,true})
        using(var clip=NodeFactory.FromFile(null,path) as CHR0Node)
        {
            Check(clip.NativeScalingRule==2,"Edited dense v"+version+": opaque source scaling metadata is read as 2");
            if(promoteFirst)PreciseCHR0EntryNode.Promote(clip);
            var entry=(CHR0EntryNode)clip.Children.Single();EditDenseInterior(entry);
            string expected=NativeAnimationOptimization.SourceContentKey(clip);
            var captured=PreciseCHR0EntryNode.CaptureStorageForWrite(entry);
            Check(captured.Accepted&&captured.UsesDenseI12Fallback,
                "Edited dense v"+version+": unchanged opaque source metadata permits only the same held-tail authoring fallback / promoted="+promoteFirst);
            string prefix=Path.Combine(output,"held-rule2-v"+version+(promoteFirst?"-precise":"-raw"));
            string copied=prefix+"-copy.chr0";
            using(var snapshot=NativeAnimationOptimization.CopySource(clip))snapshot.ExportChecked(copied);
            PreciseCHR0EntryNode.Promote(clip);string direct=prefix+"-direct.chr0";clip.ExportChecked(direct);
            foreach(string saved in new[]{copied,direct})
            using(var reopened=NodeFactory.FromFile(null,saved) as CHR0Node)
                Check(reopened!=null&&reopened.NativeScalingRule==2&&DenseRead32(File.ReadAllBytes(saved),version==4?0x24:0x28)==2&&
                    NativeAnimationOptimization.SourceContentKey(reopened)==expected,
                    "Edited dense v"+version+": source-aware copy/direct Export retains opaque rule 2 and all edited native content / "+Path.GetFileName(saved));
            bool targetRejected=false;
            try{using(var target=NativeAnimationOptimization.Copy(clip)) { }}
            catch(NotSupportedException error){targetRejected=error.Message.Contains("nonzero scaling rule");}
            Check(targetRejected,"Edited dense v"+version+": opaque source retention does not qualify nonzero scaling for target optimization");
        }
        Check(FighterProject.HashFile(path)==originalHash&&File.ReadAllBytes(path).SequenceEqual(original),
            "Edited dense v"+version+": opaque-rule authoring routes preserve the exact original native bytes");
    }

    static string EditedDenseLiteral(string output, string label, int version, bool distinctTerminal, bool bounded)
    {
        // Existing literal fixture code patches native bytes rather than using
        // any new optimizer/codec path. The final word is deliberately held or
        // distinct, and the unbounded fixture retains the unknown old envelope.
        var samples = new byte[] { 0, 1, 3, 2, 4, 3, 5, 5, distinctTerminal ? (byte)7 : (byte)5 };
        string path = DenseLiteralL1(output, label, version, 8, true, 0, 1, samples);
        byte[] bytes = File.ReadAllBytes(path); DenseWrite32(bytes, version == 4 ? 0x20 : 0x24, 0);
        if (bounded) bytes = DenseTrimLiteralEnvelope(bytes, 8 + samples.Length);
        File.WriteAllBytes(path, bytes); return path;
    }

    static string EditedDenseMixedFixture(string output, string label, int version, bool distinctTerminal, bool bounded)
    {
        string literal = EditedDenseLiteral(output, label + "-literal", version, distinctTerminal, bounded);
        string path = Path.Combine(output, label + ".chr0");
        using (var clip = NodeFactory.FromFile(null, literal) as CHR0Node)
        using (var owned = NativeAnimationOptimization.CopySource(clip))
        {
            var entry = (CHR0EntryNode)owned.Children.Single();
            var rotation = PreciseCHR0EntryNode.CaptureStorage(entry);
            var channels = Enumerable.Range(0, 9).Select(c => rotation?[c]).ToArray();
            entry.UseModelTranslate = false;
            for (int frame = 0; frame < 8; frame++) entry.Keyframes.SetFrameValue(6, frame, frame * .125f, true)._tangent = .125f;
            entry.Keyframes.SetFrameValue(7, 0, 0, true)._tangent = entry.Keyframes.SetFrameValue(8, 0, 0, true)._tangent = 0;
            channels[6] = IndexedCHR0CurveStorage.Create(AnimDataFormat.I4, Enumerable.Range(0, 8).ToArray(), Enumerable.Range(0, 8).ToArray(), Enumerable.Repeat(4, 8).ToArray(), 0, .125f);
            PreciseCHR0EntryNode.RestoreStorage(entry, new IndexedCHR0EntryStorage(channels)); owned.ExportChecked(path);
        }
        return path;
    }

    static void EditDenseInterior(CHR0EntryNode entry)
    {
        foreach (var key in entry.KeyArrays[3].Entries.Where(k => k._index == 2)) key._value += .25f;
        entry.Keyframes.PreserveInterpolation = true; entry.SignalPropertyChange();
    }

    static void EditedDenseRoundtrip(CHR0EntryNode edited, string expected, string translation, string path, int version, string route)
    {
        using (var decoded = NodeFactory.FromFile(null, path) as CHR0Node)
        {
            var entry = (CHR0EntryNode)decoded.Children.Single();
            Check(NativeAnimationOptimization.SourceContentKey(decoded) == expected,
                "Edited dense v" + version + " " + route + ": fresh native resource retains all edited tuples, metadata and six flags");
            Check(!IndexedCHR0EntryStorage.HasEncodedDenseRotation(entry) && NativeChr0SizeReport.Read(path).Streams.ContainsKey("I12") &&
                PreciseCHR0EntryNode.CaptureStorage(entry)?[6]?.ContentKey == translation,
                "Edited dense v" + version + " " + route + ": changed rotation uses I12 while unrelated I4 bytes remain identical");
            for (int sample = 0; sample <= 64; sample++)
                for (int channel = 0; channel < 9; channel++)
                    Check(ReplayFloatBits(edited.GetFrameValue(channel, sample / 8f)) == ReplayFloatBits(entry.GetFrameValue(channel, sample / 8f)),
                        "Edited dense native interpolation witness v" + version + " / " + route + " / " + channel + " / " + sample);
        }
    }

    static void EditedDenseReject(CHR0Node clip, string label, string directPath=null)
    {
        var entries = clip.Children.ToArray(); string content = NativeAnimationOptimization.SourceContentKey(clip);
        var capture = PreciseCHR0EntryNode.CaptureStorageForWrite((CHR0EntryNode)entries.Single());
        Check(!capture.Accepted && capture.Storage == null && !string.IsNullOrWhiteSpace(capture.Diagnostic),
            "Edited dense " + label + ": unsupported boundary/context is distinguished from a safe interior fallback");
        bool promotionRejected = false, copyRejected = false;
        try { PreciseCHR0EntryNode.Promote(clip); } catch (InvalidOperationException) { promotionRejected = true; }
        try { using (var snapshot = NativeAnimationOptimization.CopySource(clip)) { } } catch (InvalidDataException) { copyRejected = true; }
        Check(promotionRejected && copyRejected && clip.Children.SequenceEqual(entries) && NativeAnimationOptimization.SourceContentKey(clip) == content,
            "Edited dense " + label + ": both writes stop before replacing original entries or changing the edited master");
        if(directPath!=null)
        {
            var precise=(PreciseCHR0EntryNode)entries.Single();var plan=precise.IndexedStorage;
            bool rejected=false;try{clip.ExportChecked(directPath);}catch(InvalidOperationException){rejected=true;}
            Check(rejected&&!File.Exists(directPath)&&ReferenceEquals(precise.IndexedStorage,plan)&&NativeAnimationOptimization.SourceContentKey(clip)==content,
                "Edited dense "+label+": direct precise Export rejects before publication and preserves its immutable plan and live curves");
        }
    }
}
