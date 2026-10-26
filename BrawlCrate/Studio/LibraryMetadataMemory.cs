using System;
using System.Collections.Generic;
using System.Linq;

namespace BrawlCrate.Studio
{
    internal static class LibraryMetadataMemory
    {
        // XML repeats source/build/association evidence per resource. Reuse
        // equal strings within a snapshot without retaining a global intern pool.
        // Values and serialization remain exact; this never touches native data.
        internal static void Compact(LibraryCatalogue data)
        {
            if(data?.Mounts==null||data.Models==null||data.Clips==null||data.GameLocations==null)return;
            var shared=new Dictionary<string,string>(StringComparer.Ordinal);
            string Share(string value)
            {
                if(value==null||value.Length>8192)return value;
                if(shared.TryGetValue(value,out string known))return known;
                if(shared.Count<4096)shared.Add(value,value);
                return value;
            }
            Dictionary<string,string> mounts=new Dictionary<string,string>(StringComparer.Ordinal),models=new Dictionary<string,string>(StringComparer.Ordinal),sources=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var row in data.GameLocations.Where(r=>r?.Id!=null))
            {sources[row.Id]=row.Id;row.Game=Share(row.Game);row.GameVersion=Share(row.GameVersion);row.Provider=Share(row.Provider);}
            foreach(var row in data.Mounts.Where(r=>r?.Id!=null))
            {
                mounts[row.Id]=row.Id;if(row.GameSourceId!=null&&sources.TryGetValue(row.GameSourceId,out string source))row.GameSourceId=source;
                row.Game=Share(row.Game);row.GameVersion=Share(row.GameVersion);row.Platform=Share(row.Platform);row.Provider=Share(row.Provider);row.BuildEvidence=Share(row.BuildEvidence);
                row.OriginPath=Share(row.OriginPath);row.OriginSha256=Share(row.OriginSha256);row.OriginPartition=Share(row.OriginPartition);
            }
            foreach(var row in data.Models.Where(r=>r?.Id!=null))
            {
                models[row.Id]=row.Id;if(row.MountId!=null&&mounts.TryGetValue(row.MountId,out string mount))row.MountId=mount;
                if(row.MotionMountIds!=null)for(int i=0;i<row.MotionMountIds.Count;i++)if(row.MotionMountIds[i]!=null&&mounts.TryGetValue(row.MotionMountIds[i],out string motion))row.MotionMountIds[i]=motion;
                row.Character=Share(row.Character);row.RestInterpretation=Share(row.RestInterpretation);
            }
            foreach(var row in data.Clips.Where(r=>r!=null))
            {
                if(row.MountId!=null&&mounts.TryGetValue(row.MountId,out string mount))row.MountId=mount;
                if(row.ModelId!=null&&models.TryGetValue(row.ModelId,out string model))row.ModelId=model;
                row.SourceCharacter=Share(row.SourceCharacter);row.Channel=Share(row.Channel);row.TimebaseEvidence=Share(row.TimebaseEvidence);row.Interpolation=Share(row.Interpolation);
                row.AssociationEvidence=Share(row.AssociationEvidence);row.AssociationIssue=Share(row.AssociationIssue);row.UnboundSourceTracks=Share(row.UnboundSourceTracks);row.Tags=Share(row.Tags);
            }
        }
    }
}
