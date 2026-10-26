using BrawlCrate;
using BrawlCrate.UI;
using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;

internal static partial class Program
{
    sealed class CancelNativeWrite:MemoryStream
    {
        readonly CancellationTokenSource cancel;
        internal CancelNativeWrite(CancellationTokenSource cancel){this.cancel=cancel;}
        public override void Write(byte[] buffer,int offset,int count){base.Write(buffer,offset,count);cancel.Cancel();}
    }
    static async Task WorkspaceDirectSources(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Direct sources need the isolated desktop.");
        string input=args[Array.IndexOf(args,"--library-index")+1],folder=Path.Combine(output,"direct-library"),scratch=Path.Combine(output,"transient");
        Directory.CreateDirectory(folder);Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",folder);Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_TEMP_ROOT",scratch);
        var serializer=new XmlSerializer(typeof(LibraryCatalogue));LibraryCatalogue data;
        using(var stream=File.OpenRead(input))data=(LibraryCatalogue)serializer.Deserialize(stream);
        foreach(var mount in data.Mounts)mount.LocalPath=Path.Combine(output,"never-extracted",mount.Id+".pac");
        using(var stream=File.Create(Path.Combine(folder,"catalogue.xml")))serializer.Serialize(stream,data);
        var library=new AnimationLibrary(folder);string catalogue=NextLibraryFiles(library);
        foreach(string character in new[]{"mario","sonic","snake"})
        {
            var item=library.Browse(new LibraryQuery{Character=character,Text="Wait1",Channel="CHR0 skeletal"}).First(i=>i.Clip.Name=="Wait1"&&i.State==LibrarySourceState.Available);
            int reads=BrawlDiscImage.DiagnosticNativeReads;string stagedModel,stagedMotion;
            using(var source=library.Open(item.Clip.Id))
            {
                stagedModel=source.ModelLock.Name;stagedMotion=source.MotionLock.Name;
                Check(BrawlDiscImage.DiagnosticNativeReads-reads==new[]{item.Model.MountId,item.Clip.MountId}.Distinct().Count(),"Direct "+character+" Wait1 reads only its exact model and motion packages");
                Check(!File.Exists(item.Motion.LocalPath)&&source.ModelPath==stagedModel&&File.Exists(stagedModel)&&source.Model.Name==item.Model.Name&&source.Animation.Name=="Wait1","Legacy "+character+" source opens without an extracted cache, using a temporary native lease");
                Check(FighterProject.HashFile(stagedModel)==source.Reference.ModelContainerSha256&&FighterProject.HashFile(stagedMotion)==source.Reference.MotionContainerSha256&&BrawlLibrarySource.ResourceHash(source.Animation)==item.Clip.ResourceSha256,"Direct package bytes and selected native resource match independently recorded "+character+" hashes");
                reads=BrawlDiscImage.DiagnosticNativeReads;int hashes=FighterProject.DiagnosticFileHashes,opens=AnimationLibrary.DiagnosticOpens,bakes=BrawlRetargeter.DiagnosticBakes;
                for(int sample=0;sample<60;sample++)source.Model.ApplyCHR(source.Animation,1+(sample%Math.Max(1,source.Animation.FrameCount)));
                Check(reads==BrawlDiscImage.DiagnosticNativeReads&&hashes==FighterProject.DiagnosticFileHashes&&opens==AnimationLibrary.DiagnosticOpens&&bakes==BrawlRetargeter.DiagnosticBakes,"Prepared "+character+" sampling performs no disc/package reads, hashing, source loading or baking");
                bool locked=false;try{using(var exclusive=new FileStream(data.GameLocations.Single().Path,FileMode.Open,FileAccess.Read,FileShare.None)){} }catch(IOException){locked=true;}
                Check(locked,"The original image remains read-locked throughout the "+character+" native lease");
            }
            Check(!File.Exists(stagedModel)&&!File.Exists(stagedMotion)&&(!Directory.Exists(scratch)||!Directory.EnumerateFiles(scratch,"*",SearchOption.AllDirectories).Any()),"Closing "+character+" removes its temporary native packages");
        }
        Check(NextLibraryFiles(library)==catalogue&&!Directory.Exists(Path.Combine(output,"never-extracted")),"Reading original disc sources neither changes the catalogue nor recreates an extracted fighter tree");
        var mario=library.Browse(new LibraryQuery{Character="mario",Text="Wait1",Channel="CHR0 skeletal"}).First(i=>i.Clip.Name=="Wait1"&&i.State==LibrarySourceState.Available);
        using(var cancel=new CancellationTokenSource())using(var image=new BrawlDiscImage(data.GameLocations.Single().Path,cancel.Token))using(var stream=new CancelNativeWrite(cancel))
        {
            bool cancelled=false;try{image.CopyNativeFile(mario.Motion.VirtualPath,stream);}catch(OperationCanceledException){cancelled=true;}
            Check(cancelled&&stream.Length>0&&stream.Length<=256*1024,"Disc native reads stop at the next bounded block after cancellation");
        }
        // Damage only isolated metadata. Neither the original image nor its
        // independently verified payloads are modified by negative probes.
        var selectedMount=data.Mounts.Single(m=>m.Id==mario.Model.MountId);string hash=selectedMount.ContainerSha256;
        selectedMount.ContainerSha256=new string('0',64);GameLibraryPublishMetadata(library,data);
        bool bad=false;try{using(var source=library.Open(mario.Clip.Id)){} }catch(InvalidDataException){bad=true;}
        Check(bad&&(!Directory.Exists(scratch)||!Directory.EnumerateFiles(scratch,"*",SearchOption.AllDirectories).Any()),"Changed native payload expectations reject before preview and clean temporary files");
        selectedMount.ContainerSha256=hash;GameLibraryPublishMetadata(library,data);
        string origin=selectedMount.OriginPath;selectedMount.OriginPath=Path.Combine(output,"missing-disc.wbfs");GameLibraryPublishMetadata(library,data);
        bad=false;try{using(var source=library.Open(mario.Clip.Id)){} }catch(InvalidOperationException){bad=true;}
        Check(bad,"A missing original disc rejects even when historical extracted-cache metadata exists");
        selectedMount.OriginPath=origin;GameLibraryPublishMetadata(library,data);
        ModelForm form=null;
        try
        {
            using(var startup=MainForm.StartViewerApplication())
            {
                form=(ModelForm)Field(MainForm.Instance,"_fighterForm");form.ClientSize=new Size(1700,950);
                Call(form,"ImportFighterModel",args[0]);Call(form,"ImportFighterAnimations",new[]{new[]{args[1]}});await SimpleControlsReady(form);
                Call(form,"BuildLibraryPane");Call(form,"ToggleAnimationLibrary");
                var sonic=library.Browse(new LibraryQuery{Character="sonic",Text="Wait1",Channel="CHR0 skeletal"}).First(i=>i.Clip.Name=="Wait1"&&i.State==LibrarySourceState.Available);
                await GameLibrarySelect(form,sonic.Clip.Id);SimpleControl<Button>(form,"libraryPreviewOriginal").PerformClick();
                Check(((LibrarySourceLease)Field(form,"_libraryLease")).Reference.ClipId==sonic.Clip.Id,"The small source viewport loads original Sonic Wait1 directly from the disc");
                await SourceFitPixels(form,Path.Combine(output,"direct-source-sonic-geometry.png"));
                await GameLibraryCostumeLayouts(form,output,true);
                int reads=BrawlDiscImage.DiagnosticNativeReads,opens=AnimationLibrary.DiagnosticOpens;
                SimpleControl<Button>(form,"libraryOriginalPlay").PerformClick();await Task.Delay(150);Call(form,"PauseSourceOperation");
                Check(reads==BrawlDiscImage.DiagnosticNativeReads&&opens==AnimationLibrary.DiagnosticOpens,"Original source playback retains prepared disc data without loading more packages");
                bool transferred=false;
                using(Modal("Skeleton Mapping",dialog=>{
                    var grid=dialog.Controls.OfType<DataGridView>().Single();
                    Check(grid.Rows.Count>=17,"The source mapping review exposes the existing native humanoid-role controls");
                    CaptureDialog(dialog,output,"direct-source-mapping-review.png");
                    dialog.Controls.OfType<Button>().Single(b=>b.Text=="Use Mapping").PerformClick();
                }))
                using(OnRetargetReady(form,dialog=>transferred=true))SimpleControl<Button>(form,"libraryUseOnTarget").PerformClick();
                await SimpleControlsReady(form);
                Check(transferred&&form.FighterProject.Animations.Any(a=>a.Retarget?.Library?.ClipId==sonic.Clip.Id),"Original Next retargets disc-backed Sonic Wait1 to the editable Mario target");
                Call(form,"OpenLinkedComparison");
                reads=BrawlDiscImage.DiagnosticNativeReads;int hashes=FighterProject.DiagnosticFileHashes,bakes=BrawlRetargeter.DiagnosticBakes;
                form.FighterEditor.SetFrame(2);form.FighterEditor.UpdateModel(2.5f);
                Check(BrawlDiscImage.DiagnosticNativeReads==reads&&FighterProject.DiagnosticFileHashes==hashes&&BrawlRetargeter.DiagnosticBakes==bakes,"Linked comparison samples prepared source data without reading the disc, hashing or solving");
                await CaptureSimpleWorkspace(form,Path.Combine(output,"direct-linked-comparison.png"));Call(form,"CloseLinkedComparison");
                await GameLibrarySelect(form,mario.Clip.Id);SimpleControl<Button>(form,"libraryPreviewOriginal").PerformClick();
                var costume=library.GetCostumes(mario.Clip.Id).First(c=>c.Available&&!c.IsCanonical);Call(form,"SelectLibraryCostume",costume.ModelId);SimpleControl<Button>(form,"libraryPreviewOriginal").PerformClick();
                form=await GameLibraryOpenTarget(form,library,mario.Clip.Id,costume.ModelId,output,true);
            }
        }
        finally{if(form!=null&&!form.IsDisposed)SimpleDisposeForm(form);}
        Check(!Directory.Exists(scratch)||!Directory.EnumerateFiles(scratch,"*",SearchOption.AllDirectories).Any(),"Closing native source and target workflows releases every transient disc package");
    }
}
