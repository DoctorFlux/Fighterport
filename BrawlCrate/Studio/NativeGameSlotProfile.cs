using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace BrawlCrate.Studio
{
    public sealed class NativeGameSlotEntry {public string Entry {get;set;}public string Name {get;set;}public int Samples {get;set;}public int Version {get;set;}public bool Loop {get;set;}}
    public sealed class NativeGameSlotProfile
    {
        public string Id {get;set;}public string SourceAsset {get;set;}public string SourceSha256 {get;set;}public string Family {get;set;}="Brawl native motion";public string Build {get;set;}="Unverified";
        public NativeGameSlotEntry[] Entries {get;set;}
        internal void Validate()
        {
            if(!Guid.TryParseExact(Id,"N",out _)||!Guid.TryParseExact(SourceAsset,"N",out _)||SourceSha256==null||SourceSha256.Length!=64||!SourceSha256.All(Uri.IsHexDigit)||Family!="Brawl native motion"||Build!="Unverified"||Entries==null||Entries.Length<1||Entries.Length>1024||Entries.Any(e=>e==null||string.IsNullOrWhiteSpace(e.Entry)||e.Entry.Length>16384||string.IsNullOrWhiteSpace(e.Name)||e.Name.Length>256||e.Samples<1||e.Samples>100000||e.Version!=4&&e.Version!=5)||Entries.Select(e=>e.Entry).Distinct().Count()!=Entries.Length)throw new InvalidDataException("Invalid source-bound Brawl motion slot inventory.");
        }
        internal static NativeGameSlotProfile Capture(FighterAsset asset)
        {
            if(asset.Generated)throw new InvalidOperationException("Choose a registered original Brawl motion PAC/PCS/BRRES template, not a generated animation.");
            string before=FighterProject.HashFile(asset.WorkingPath);if(!string.Equals(before,asset.SourceSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("This template's working revision changed. Register its authentic original independently before profiling it.");
            var entries=new List<NativeGameSlotEntry>();using(var root=NodeFactory.FromFile(null,asset.WorkingPath))
            {
                if(!(root is ARCNode)&&!(root is BRRESNode))throw new NotSupportedException("Game slot inventories require an actual native Brawl motion container.");
                void Walk(ResourceNode node,string key){if(node is CHR0Node clip)entries.Add(new NativeGameSlotEntry{Entry=key,Name=clip.Name,Samples=clip.FrameCount,Loop=clip.Loop,Version=clip.Version});for(int i=0;i<node.Children.Count;i++)Walk(node.Children[i],key+"/"+i);}
                Walk(root,"root");
            }
            if(FighterProject.HashFile(asset.WorkingPath)!=before)throw new InvalidOperationException("The motion template changed during profiling.");var result=new NativeGameSlotProfile{Id=Guid.NewGuid().ToString("N"),SourceAsset=asset.Id,SourceSha256=before,Entries=entries.ToArray()};result.Validate();return result;
        }
    }
}
