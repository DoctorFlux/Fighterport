using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
namespace BrawlCrate.Studio
{
    [Flags]
    public enum NativeGameOperation
    {
        None=0, Inventory=1, ModelRead=2, MotionRead=4, SourcePreview=8,
        TargetEdit=16, RetargetSource=32, NativeModelWrite=64, NativeMotionWrite=128,
        MaterialPreview=256, TextureRead=512, ContainerWrite=1024, OriginalSourceExport=2048
    }
    // The workspace routes by a registered provider, never by treating a saved
    // path or another game's format as evidence of native support. New adapters
    // supply native behavior behind this same source/preview/export boundary.
    public interface INativeGameSourceAdapter
    {
        string[] Games { get; }
        string[] ImageExtensions { get; }
        string[] SourceProviders { get; }
        string NativeProvider { get; }
        NativeGameOperation Operations { get; }
        void Validate(LibraryGameRequest request,LibraryCatalogue catalogue,CancellationToken cancellation);
        LibraryGamePlan Prepare(LibraryGameRequest request,LibraryCatalogue catalogue,string scratch,CancellationToken cancellation,Action<int,string> progress);
        void Verify(LibraryGamePlan plan,CancellationToken cancellation);
    }
    public interface INativeSourceExportAdapter
    {
        void ExportOriginal(LibraryCatalogue catalogue,LibraryAnimation clip,LibraryModel model,string destination,CancellationToken cancellation);
    }
    public interface INativeSourcePreview : IDisposable
    {
        BrawlLib.OpenGL.IRenderedObject RenderTarget { get; }
        void ApplyDisplayFrame(float frame);
    }
    public static class NativeGameAdapters
    {
        private static readonly ReadOnlyCollection<INativeGameSourceAdapter> adapters=Array.AsReadOnly(new INativeGameSourceAdapter[]{new BrawlSources(),new MeleeSources()});
        public static IReadOnlyList<INativeGameSourceAdapter> All=>adapters;
        public static INativeGameSourceAdapter ForGame(string game)=>adapters.SingleOrDefault(a=>a.Games.Contains(game,StringComparer.Ordinal));
        public static INativeGameSourceAdapter ForSource(string game,string provider)=>adapters.SingleOrDefault(a=>a.Games.Contains(game,StringComparer.Ordinal)&&a.SourceProviders.Contains(provider,StringComparer.Ordinal));
        public static INativeGameSourceAdapter ForNative(string provider)=>adapters.SingleOrDefault(a=>a.NativeProvider==provider);
        public static bool CanIndex(LibraryGameRequest request)=>ForGame(request.Game) is INativeGameSourceAdapter adapter&&(Directory.Exists(request.Path)||adapter.ImageExtensions.Contains(Path.GetExtension(request.Path??""),StringComparer.OrdinalIgnoreCase));
        private sealed class BrawlSources:INativeGameSourceAdapter
        {
            public string[] Games=>new[]{"Brawl","Project M","Project+"};
            public string[] ImageExtensions=>new[]{".iso",".wbfs"};
            public string[] SourceProviders=>new[]{"brawl-folder-v1",BrawlDiscImage.Provider,"dolphin-brawl-disc-v1"};
            public string NativeProvider=>BrawlLibrarySource.Provider;
            public NativeGameOperation Operations=>NativeGameOperation.Inventory|NativeGameOperation.ModelRead|NativeGameOperation.MotionRead|NativeGameOperation.SourcePreview|NativeGameOperation.TargetEdit|NativeGameOperation.RetargetSource|NativeGameOperation.NativeMotionWrite|NativeGameOperation.MaterialPreview|NativeGameOperation.TextureRead;
            public void Validate(LibraryGameRequest r,LibraryCatalogue c,CancellationToken t)=>BrawlGameSource.ValidateGameSource(r,t);
            public LibraryGamePlan Prepare(LibraryGameRequest r,LibraryCatalogue c,string s,CancellationToken t,Action<int,string> p)=>BrawlGameSource.Prepare(r,s,t,p);
            public void Verify(LibraryGamePlan p,CancellationToken t)
            {
                var discs=new Dictionary<string,BrawlDiscImage>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach(var mount in p.Mounts)
                    {
                        t.ThrowIfCancellationRequested();
                        if(mount.OriginPath!=null)
                        {
                            if(!discs.TryGetValue(mount.OriginPath,out var disc)){disc=new BrawlDiscImage(mount.OriginPath,t);discs.Add(mount.OriginPath,disc);}
                            var info=new FileInfo(mount.OriginPath);if(info.Length!=mount.OriginBytes||info.LastWriteTimeUtc.Ticks!=mount.OriginWriteTicks)throw new InvalidDataException("The original game source changed during preparation.");
                            NativeSourceScratch.Verify(disc,mount,t);
                        }
                        else using(var held=AnimationLibrary.LockSource(mount.LocalPath))if(held.Length!=mount.Bytes||FighterProject.HashFile(mount.LocalPath)!=mount.ContainerSha256)throw new InvalidDataException("The original native source changed during preparation.");
                    }
                }
                finally{foreach(var disc in discs.Values)disc.Dispose();}
            }
        }
        private sealed class MeleeSources:INativeGameSourceAdapter,INativeSourceExportAdapter
        {
            public string[] Games=>new[]{"Melee"};
            public string[] ImageExtensions=>new[]{".iso",".gcm"};
            public string[] SourceProviders=>new[]{FighterPort.Adapters.Melee.MeleeSource.DiscProvider,FighterPort.Adapters.Melee.MeleeSource.FolderProvider};
            public string NativeProvider=>MeleeGameSource.NativeProvider;
            public NativeGameOperation Operations=>NativeGameOperation.Inventory|NativeGameOperation.ModelRead|NativeGameOperation.MotionRead|NativeGameOperation.SourcePreview|NativeGameOperation.TextureRead|NativeGameOperation.OriginalSourceExport;
            public void Validate(LibraryGameRequest r,LibraryCatalogue c,CancellationToken t)=>MeleeGameSource.Validate(r,c,t);
            public LibraryGamePlan Prepare(LibraryGameRequest r,LibraryCatalogue c,string s,CancellationToken t,Action<int,string> p)=>MeleeGameSource.Prepare(r,c,t,p);
            public void Verify(LibraryGamePlan p,CancellationToken t)=>MeleeGameSource.VerifyPlan(p,t);
            public void ExportOriginal(LibraryCatalogue c,LibraryAnimation a,LibraryModel m,string d,CancellationToken t)=>MeleeGameSource.ExportOriginal(c,a,m,d,t);
        }
    }
}
