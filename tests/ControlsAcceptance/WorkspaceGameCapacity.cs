using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Serialization;

internal static partial class Program
{
    // Frozen capacity witness: four 25,934-clip / 2,688-model rosters,
    // independent identities and resource-aware memory and atomic-write boundaries.
    // These are metadata clones, not actual Project M game acceptance.
    static Task WorkspaceGameCapacity(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Capacity acceptance requires the isolated desktop.");
        string index=args[Array.IndexOf(args,"--library-index")+1];
        int hashes=FighterProject.DiagnosticFileHashes,opens=AnimationLibrary.DiagnosticOpens,bakes=BrawlRetargeter.DiagnosticBakes;
        var all=new LibraryCatalogue{Generation=1,LastOperation="Procedural four-roster capacity fixture"};
        var serializer=new XmlSerializer(typeof(LibraryCatalogue));
        for(int instance=0;instance<4;instance++)
        {
            LibraryCatalogue copy;using(var input=File.OpenRead(index))copy=(LibraryCatalogue)serializer.Deserialize(input);
            Check(copy.Clips.Count==25934&&copy.Models.Count==2688&&copy.Mounts.Count==986,"Capacity input has the frozen complete native-roster metadata counts: "+instance);
            var mounts=copy.Mounts.ToDictionary(m=>m.Id,m=>Guid.NewGuid().ToString("N"));
            var models=copy.Models.ToDictionary(m=>m.Id,m=>Guid.NewGuid().ToString("N"));
            var source=copy.GameLocations.Single();source.Id=Guid.NewGuid().ToString("N");
            source.Game=instance==0?"Brawl":"Project M";source.Label="Full roster metadata fixture "+instance;
            source.GameVersion="Procedural capacity fixture; not actual mod content";
            source.Path=Path.Combine(output,"metadata-source-"+instance);Directory.CreateDirectory(source.Path);
            source.Provider="brawl-folder-v1";source.IsDirectory=true;source.Bytes=0;source.ToolPath=source.ToolSha256=null;
            foreach(var mount in copy.Mounts)
            {
                mount.Id=mounts[mount.Id];mount.GameSourceId=source.Id;mount.Game=source.Game;mount.GameVersion=source.GameVersion;
                mount.OriginPath=mount.OriginSha256=null;mount.OriginBytes=mount.OriginWriteTicks=0;
            }
            foreach(var model in copy.Models)
            {model.Id=models[model.Id];model.MountId=mounts[model.MountId];model.MotionMountIds=model.MotionMountIds.Select(id=>mounts[id]).ToList();}
            foreach(var clip in copy.Clips)
            {clip.Id=Guid.NewGuid().ToString("N");clip.MountId=mounts[clip.MountId];if(clip.ModelId!=null)clip.ModelId=models[clip.ModelId];}
            all.GameLocations.Add(source);all.Mounts.AddRange(copy.Mounts);all.Models.AddRange(copy.Models);all.Clips.AddRange(copy.Clips);
        }
        string folder=Path.Combine(output,"four-roster-library");Directory.CreateDirectory(folder);
        string file=Path.Combine(folder,"catalogue-00000000000000000001.xml");
        using(var stream=File.Create(file))serializer.Serialize(stream,all);
        Check(new FileInfo(file).Length>64L*1024*1024,"Four-roster serialized metadata exceeds the old byte limit without imposing a new fixed byte ceiling");
        long plainBytes=new FileInfo(file).Length;
        string[] ids=all.GameLocations.Select(s=>s.Id).ToArray();string witnessClipId=all.Clips[0].Id;
        all=null;GC.Collect();GC.WaitForPendingFinalizers();
        var library=new AnimationLibrary(folder);var data=library.Read();
        Check(data.Clips.Count==103736&&data.Models.Count==10752&&data.Mounts.Count==3944&&data.GameLocations.Count==4,"Brawl and three independent full-roster metadata instances reopen together beyond the former clip limit");
        var originalIds=data.Clips.Select(c=>c.Id).OrderBy(id=>id).ToArray();data=null;
        foreach(string id in ids)
        {
            var rows=library.Browse(new LibraryQuery{GameSourceId=id});
            Check(rows.Count==25934&&rows.All(r=>r.Motion.GameSourceId==id),"Exact source scope retains all clips without crossing another full roster: "+id);
        }
        library.RenameSavedGameSource(ids[2],"Second complete PM metadata instance");
        Check(library.Read().GameLocations.Single(s=>s.Id==ids[2]).Label=="Second complete PM metadata instance"&&library.ClipCount==103736,"A full-roster instance rename persists atomically above the old size limit");
        library.RemoveSavedGameSource(ids[1]);
        Check(library.ClipCount==77802&&library.Read().Clips.Any(c=>c.Id==witnessClipId),"Removing one full roster preserves the other three instances beyond the former 50,000-clip limit");
        library.UndoLastUpdate();
        Check(library.Read().Clips.Select(c=>c.Id).OrderBy(id=>id).SequenceEqual(originalIds),"Library Undo restores all 103,736 exact clip identities");
        Check(AnimationLibrary.MaximumClips==int.MaxValue,"The legacy API no longer imposes a fixed global animation count");
        const long MiB=1024L*1024;
        Check(LibraryCapacity.XmlAllowance(1024*MiB,4096*MiB,4096*MiB)==128*MiB&&
            LibraryCapacity.XmlAllowance(256*MiB,4096*MiB,4096*MiB)==0&&
            LibraryCapacity.XmlAllowance(4096*MiB,384*MiB,4096*MiB)==32*MiB,
            "Independent numeric budgets reserve editor memory and adapt to virtual and physical headroom");
        bool rejected=false;try{LibraryCapacity.CheckRead(long.MaxValue);}catch(InvalidDataException){rejected=true;}
        Check(rejected,"An unsafe memory request is rejected without allocating its declared size");
        using(var bytes=new MemoryStream())using(var quota=new LibraryQuotaWriter(bytes,3))
        {
            quota.Write(new byte[2],0,2);rejected=false;try{quota.Write(new byte[2],0,2);}catch(IOException){rejected=true;}
            Check(rejected&&bytes.Length==2,"Resource quotas stop before a write that would exceed headroom");
        }
        Check(library.IndexPath.EndsWith(".xml.gz",StringComparison.Ordinal)&&new FileInfo(library.IndexPath).Length<plainBytes/2,
            "Large catalogues persist as compact metadata instead of copying game assets");
        string corrupt=Path.Combine(output,"corrupt-compact");Directory.CreateDirectory(corrupt);
        byte[] compact=File.ReadAllBytes(library.IndexPath);compact[compact.Length-8]^=0xff;File.WriteAllBytes(Path.Combine(corrupt,"catalogue.xml"),compact);
        rejected=false;try{new AnimationLibrary(corrupt).Read();}catch(InvalidDataException){rejected=true;}
        Check(rejected,"Corrupt compact metadata CRC is rejected and its original file is preserved");
        long generation=library.Read().Generation;string blocked=Path.Combine(folder,"catalogue-"+(generation+1).ToString("D20")+".xml.gz");Directory.CreateDirectory(blocked);
        try
        {
            rejected=false;try{library.RenameSavedGameSource(ids[2],"Failed publication label");}catch(IOException){rejected=true;}
            Check(rejected&&library.Read().Generation==generation&&library.Read().GameLocations.Single(l=>l.Id==ids[2]).Label!="Failed publication label"&&!Directory.EnumerateFiles(folder,".library-*.tmp").Any(),
                "Failed compact-index publication preserves the previous catalogue and removes its candidate");
        }
        finally{Directory.Delete(blocked);}
        Check(FighterProject.DiagnosticFileHashes==hashes&&AnimationLibrary.DiagnosticOpens==opens&&BrawlRetargeter.DiagnosticBakes==bakes,"Capacity queries, edits and Undo load/hash/bake no native asset");
        File.WriteAllText(Path.Combine(output,"capacity-memory.txt"),"Peak working set bytes: "+Process.GetCurrentProcess().PeakWorkingSet64);
        return Task.CompletedTask;
    }
}
