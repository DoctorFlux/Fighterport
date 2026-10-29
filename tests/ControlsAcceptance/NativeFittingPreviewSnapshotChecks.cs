using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using BrawlLib.Wii.Models;
using BrawlLib.Wii.Textures;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;

internal static partial class Program
{
    static void NativeFittingPreviewSnapshotChecks(string modelPath,CHR0Node source,string output)
    {
        string originalContent=NativeAnimationOptimization.SourceContentKey(source),originalStorage=NativeAnimationOptimization.StorageKey(source);
        string directory=Path.Combine(output,"fitting-preview-snapshot");Directory.CreateDirectory(directory);
        string fixturePath=Path.Combine(directory,"mixed-context.brres");
        // Fixture construction has independent owners. Only this independent
        // fixture is serialized; the project's authentic animation is read-only.
        using(var modelRoot=NodeFactory.FromFile(null,modelPath))
        {
            var model=Walk(modelRoot).OfType<MDL0Node>().First(m=>{m.Populate();return m.Objects.Length>0&&m.Name.IndexOf("shadow",StringComparison.OrdinalIgnoreCase)<0;});
            ResourceNode parent=model.Parent;while(parent!=null&&!(parent is BRRESNode))parent=parent.Parent;
            var brres=parent as BRRESNode??throw new InvalidOperationException("Mixed fitting fixture needs an authentic model BRRES.");
            var copy=NativeAnimationOptimization.CopySource(source);copy.Name="FittingMixedMotion";brres.GetOrCreateFolder<CHR0Node>().AddChild(copy);
            using(var bitmap=new Bitmap(16,16,PixelFormat.Format8bppIndexed))
            {
                // Bitmap.Palette returns an independent palette copy, which
                // remains owned by the fixture after the bitmap is disposed.
                var palette=bitmap.Palette;var colors=new[]{Color.Black,Color.Red,Color.Lime,Color.Blue,Color.White,Color.Yellow,Color.Cyan,Color.Magenta};
                for(int i=0;i<palette.Entries.Length;i++)palette.Entries[i]=colors[i%colors.Length];
                brres.GetOrCreateFolder<PLT0Node>().AddChild(new PLT0Node{Name="FittingPreviewTestPalette",Version=1,Format=WiiPaletteFormat.RGB565,Palette=palette});
            }
            brres.ExportChecked(fixturePath);
        }
        using(var mixed=NodeFactory.FromFile(null,fixturePath))
        {
            var model=Walk(mixed).OfType<MDL0Node>().First(m=>{m.Populate();return m.Objects.Length>0&&m.Name.IndexOf("shadow",StringComparison.OrdinalIgnoreCase)<0;});
            var motion=Walk(mixed).OfType<CHR0Node>().Single(c=>c.Name=="FittingMixedMotion");
            var baked=motion.Children.OfType<CHR0EntryNode>().SelectMany(entry=>Enumerable.Range(3,3).Select(channel=>new{Entry=entry,Channel=channel,Storage=PreciseCHR0EntryNode.CaptureStorage(entry)}))
                .Where(item=>item.Storage!=null&&item.Storage.MatchesGroup(item.Entry.Keyframes,1)&&item.Storage[item.Channel]?.Format==AnimDataFormat.L1)
                .Select(item=>new{Bone=item.Entry.Name,item.Channel,Format=item.Storage[item.Channel].Format.ToString(),Payload=item.Storage[item.Channel].ContentKey,Samples=item.Storage[item.Channel].EncodedSampleCount}).ToArray();
            Check(baked.Length>0,"Mixed preview fixture contains actual imported L1 payloads with encoded terminal samples");
            string currentModel=Path.Combine(directory,"current-model.mdl0");model.ExportChecked(currentModel);
            motion.Name="FittingMixedMotionEdited";motion.SignalPropertyChange();
            string State()=>new JavaScriptSerializer().Serialize(new{Content=NativeAnimationOptimization.SourceContentKey(motion),Storage=NativeAnimationOptimization.StorageKey(motion),
                motion.IsDirty,motion.IsBranch,motion.HasChanged,MotionBacking=SharedOptimizationBacking(motion),RootBacking=SharedOptimizationBacking(mixed),
                RootDirty=mixed.IsDirty,RootBranch=mixed.IsBranch,Visual=Walk(mixed).Where(n=>n is MDL0Node||n is TEX0Node||n is PLT0Node).Select(n=>new{Type=n.GetType().Name,n.Name,n.IsDirty,n.IsBranch}).ToArray(),
                Palettes=Walk(mixed).OfType<PLT0Node>().Select(p=>new{p.Name,p.Format,Colors=p.Palette.Entries.Select(c=>c.ToArgb()).ToArray()}).ToArray()});
            string baseline=State();
            Check(motion.IsDirty&&mixed.IsDirty&&NativeFittingPreviewSnapshot.HasVisualDependencies(mixed),"Mixed native context has unsaved animation edits and authentic visual resources");
            using(var pure=NativeAnimationOptimization.CopySource(motion))
                Check(!NativeFittingPreviewSnapshot.HasVisualDependencies(pure),"A pure native motion owner is excluded from visual-context snapshots");
            string contextPath=Path.Combine(directory,"raw-preview-context.brres");
            NativeFittingPreviewSnapshot.WriteRawContext(mixed,contextPath,model,currentModel);
            using(var context=NativeFittingPreviewSnapshot.OpenContext(mixed,contextPath,model,currentModel))
            {
                var copiedModel=Walk(context).OfType<MDL0Node>().Single(m=>m.Name==model.Name);
                Check(!ReferenceEquals(context,mixed)&&!ReferenceEquals(copiedModel,model),"Mixed fitting preview owns an independent native resource and model");
                string modelCopy=Path.Combine(directory,"preview-model.mdl0");copiedModel.ExportChecked(modelCopy);
                Check(FighterProject.HashFile(modelCopy)==FighterProject.HashFile(currentModel),"Mixed preview overlays the exact current MDL0 without changing hierarchy, binds, geometry or weights");
                var originalPalette=Walk(mixed).OfType<PLT0Node>().Single(p=>p.Name=="FittingPreviewTestPalette");var copiedPalette=Walk(context).OfType<PLT0Node>().Single(p=>p.Name==originalPalette.Name);
                Check(copiedPalette.Colors==256&&copiedPalette.Format==WiiPaletteFormat.RGB565&&SharedOptimizationBacking(copiedPalette)==SharedOptimizationBacking(originalPalette),"Clean native palette bytes survive the independent fitting visual context exactly");
                var copiedMotion=Walk(context).OfType<CHR0Node>().Single(c=>c.Name=="FittingMixedMotion");
                Check(baked.All(expected=>{var entry=copiedMotion.Children.OfType<CHR0EntryNode>().Single(e=>e.Name==expected.Bone);var storage=PreciseCHR0EntryNode.CaptureStorage(entry);var curve=storage?[expected.Channel];return storage!=null&&storage.MatchesGroup(entry.Keyframes,1)&&curve!=null&&curve.ContentKey==expected.Payload&&curve.EncodedSampleCount==expected.Samples;}),
                    "Raw mixed context preserves complete L1 payload hashes and terminal sample counts without serializing its current motion");
            }
            Check(State()==baseline,"Mixed fitting preview preserves original exact motion/storage, backing bytes, dirty and branch state");
            using(var cancel=new CancellationTokenSource())
            {
                cancel.Cancel();bool rejected=false;
                try{NativeFittingPreviewSnapshot.WriteRawContext(mixed,Path.Combine(directory,"cancelled.brres"),model,currentModel,cancel.Token);}
                catch(OperationCanceledException){rejected=true;}
                Check(rejected&&State()==baseline,"Cancelled mixed-context snapshot preserves all original native data and state");
            }
            string expectedState=baseline;var texture=Walk(mixed).OfType<PLT0Node>().Single(p=>p.Name=="FittingPreviewTestPalette");
            Check(texture!=null,"Mixed preview fixture always includes the palette needed for dirty-visual rejection coverage");
            {
                texture.Palette.Entries[0]=Color.FromArgb(255,17,34,51);
                texture.SignalPropertyChange();string dirty=State();expectedState=dirty;bool rejected=false;
                try{NativeFittingPreviewSnapshot.WriteRawContext(mixed,Path.Combine(directory,"dirty-texture.brres"),model,currentModel);}
                catch(Exception error)when(error is InvalidOperationException||error is InvalidDataException||error is NotSupportedException){rejected=true;}
                Check(rejected&&State()==dirty,"Uncommitted visual texture data fails closed without rebuilding any original motion");
            }
            File.WriteAllText(Path.Combine(directory,"snapshot-evidence.json"),new JavaScriptSerializer().Serialize(new{ImportedBakedChannels=baked,DirtyTextureCase=true,OriginalStatePreserved=State()==expectedState}));
        }
        Check(NativeAnimationOptimization.SourceContentKey(source)==originalContent&&NativeAnimationOptimization.StorageKey(source)==originalStorage,
            "Mixed fixture preparation leaves the project's authentic source motion and complete storage untouched");
    }
}
