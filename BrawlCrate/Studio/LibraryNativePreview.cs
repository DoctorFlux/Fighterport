using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BrawlCrate.Studio
{
    internal static class LibraryNativePreview
    {
        internal static readonly Type[] Types={typeof(CHR0Node),typeof(SRT0Node),typeof(SHP0Node),typeof(PAT0Node),typeof(CLR0Node),typeof(VIS0Node),typeof(SCN0Node)};
        internal static bool Supported(LibraryAnimation clip)
        {
            if(clip==null||clip.FrameCount<1||clip.FrameCount>4096)return false;
            switch(clip.Channel)
            {
                case "CHR0 skeletal":case "SRT0Node":case "SCN0Node":return clip.NativeVersion==4||clip.NativeVersion==5;
                case "SHP0Node":case "PAT0Node":case "VIS0Node":case "CLR0Node":return clip.NativeVersion==3||clip.NativeVersion==4;
                default:return false;
            }
        }
        internal static bool MatchesChannel(NW4RAnimationNode node,string channel)
            =>node!=null&&(channel=="CHR0 skeletal"?node is CHR0Node:node.GetType().Name==channel);
        internal static LibraryAnimation Anchor(LibraryCatalogue data,LibraryAnimation clip)
        {
            if(clip.ModelId!=null&&clip.Channel=="CHR0 skeletal")return clip;
            var mount=data.Mounts.Single(m=>m.Id==clip.MountId);
            var mounts=data.Mounts.ToDictionary(m=>m.Id);
            var models=data.Models.ToDictionary(m=>m.Id);
            var candidates=data.Clips.Where(c=>c.Channel=="CHR0 skeletal"&&c.ModelId!=null&&models.ContainsKey(c.ModelId)&&
                (mount.GameSourceId!=null?mounts[c.MountId].GameSourceId==mount.GameSourceId&&c.SourceCharacter==clip.SourceCharacter:
                    models[c.ModelId].MotionMountIds.Contains(clip.MountId))).ToArray();
            var dominant=candidates.GroupBy(c=>c.ModelId).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key,StringComparer.Ordinal).FirstOrDefault();
            return dominant?.OrderByDescending(c=>c.Name=="Wait1"||c.Name=="Idle").ThenBy(c=>c.Name,StringComparer.Ordinal).FirstOrDefault();
        }
        private static ResourceNode Container(ResourceNode node)
        {for(var parent=node.Parent;parent!=null;parent=parent.Parent)if(parent.ResourceFileType==ResourceType.BRES)return parent;return node.RootNode;}
        internal static NW4RAnimationNode[] Bundle(ResourceNode root,NW4RAnimationNode primary,out string notice)
        {
            var owner=Container(primary);var nodes=BrawlLibrarySource.Resources(root).Select(p=>p.Item1).OfType<NW4RAnimationNode>()
                .Where(n=>n.Name==primary.Name&&ReferenceEquals(Container(n),owner)).ToArray();
            var warnings=new List<string>();var result=new List<NW4RAnimationNode>();
            foreach(var type in Types)
            {
                if(primary.GetType()==type){result.Add(primary);continue;}
                var same=nodes.Where(n=>n.GetType()==type&&n.SupportedVersions.Contains(n.Version)).ToArray();
                if(same.Length==1)result.Add(same[0]);
                else if(same.Length>1)warnings.Add("Ambiguous "+type.Name.Replace("Node","")+" companion; not applied.");
            }
            notice=string.Join(" ",warnings);return result.ToArray();
        }
        internal static void Apply(MDL0Node model,NW4RAnimationNode[] bundle,float frame)
        {
            float Sample(NW4RAnimationNode node)=>frame<=0?0:Math.Min(frame,node.FrameCount+(node.Loop?1:0));
            var chr=bundle.OfType<CHR0Node>().SingleOrDefault();model.ApplyCHR(chr,chr==null?0:Sample(chr));
            foreach(var node in bundle)
            {
                float at=Sample(node);
                if(node is SRT0Node srt)model.ApplySRT(srt,at);
                else if(node is SHP0Node shp)model.ApplySHP(shp,at);
                else if(node is PAT0Node pat)model.ApplyPAT(pat,at);
                else if(node is VIS0Node vis)model.ApplyVIS(vis,at);
                else if(node is CLR0Node clr)model.ApplyCLR(clr,at);
                else if(node is SCN0Node scn)model.ApplySCN(scn,at);
            }
        }
        internal static string Bindings(MDL0Node model,NW4RAnimationNode[] bundle)
        {
            var bones=new HashSet<string>(model.AllBones.Select(b=>b.Name),StringComparer.Ordinal);
            var materials=new HashSet<string>((model.MaterialList??new List<ResourceNode>()).Select(m=>m.Name),StringComparer.Ordinal);
            var vertices=new HashSet<string>((model.VertexList??new List<ResourceNode>()).Select(v=>v.Name),StringComparer.Ordinal);
            var notes=new List<string>();
            foreach(var node in bundle)
            {
                var known=node is CHR0Node||node is VIS0Node?bones:node is SHP0Node?vertices:materials;
                if(node is SCN0Node){notes.Add("SCN0 uses native indexed scene/light bindings.");continue;}
                var missing=node.Children.Where(c=>!known.Contains(c.Name)).Select(c=>c.Name).ToArray();
                if(missing.Length>0)notes.Add(node.GetType().Name.Replace("Node","")+" unmatched native tracks: "+string.Join(", ",missing.Take(12))+(missing.Length>12?" ...":""));
            }
            return string.Join(" ",notes);
        }
    }
}
