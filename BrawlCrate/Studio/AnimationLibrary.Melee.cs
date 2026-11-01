using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace BrawlCrate.Studio
{
    public sealed partial class AnimationLibrary
    {
        private void RepairMeleeSource(string sourceId,string path,CancellationToken cancellation,string name)
        {
            var baseline=Read();var source=baseline.GameLocations.Single(l=>l.Id==sourceId);
            var request=new LibraryGameRequest{Game="Melee",GameVersion=source.GameVersion,Path=path,BaseSourceId=source.BaseSourceId,ApproveBuild=true};
            var plan=MeleeGameSource.Prepare(request,baseline,cancellation,null);
            if(plan.Source.Provider!=source.Provider||plan.Source.InventorySha256!=source.InventorySha256||plan.Source.ContainerSha256!=source.ContainerSha256)
                throw new InvalidDataException("Choose the exact original Melee source at its new location. Changed content requires a separate source instance.");
            MeleeGameSource.VerifyPlan(plan,cancellation);
            Update(data=>{
                if(data.Generation!=baseline.Generation)throw new InvalidOperationException("The library changed during source repair. Nothing was relocated.");
                var current=data.GameLocations.Single(l=>l.Id==sourceId);if(name!=null){ValidateSourceName(data,current,name);current.Label=name;}
                var replacement=plan.Mounts.ToDictionary(m=>m.VirtualPath,StringComparer.Ordinal);
                foreach(var mount in data.Mounts.Where(m=>m.GameSourceId==sourceId))
                {
                    var next=replacement[mount.VirtualPath];if(next.ContainerSha256!=mount.ContainerSha256||next.Bytes!=mount.Bytes)throw new InvalidDataException("A native Melee member changed during repair.");
                    mount.LocalPath=next.LocalPath;mount.NativeWriteTicks=next.NativeWriteTicks;mount.OriginPath=next.OriginPath;mount.OriginSha256=next.OriginSha256;mount.OriginBytes=next.OriginBytes;mount.OriginWriteTicks=next.OriginWriteTicks;
                }
                current.Path=plan.Source.Path;data.LastOperation="Repair Melee Source";return Tuple.Create(true,true);
            },cancellation);
        }
    }
}
