using HSDRaw.Melee.Pl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace FighterPort.Adapters.Melee
{
    // Native DOBJ lookup tables, independently interpreted from ftparts.c.
    // These are inspection selections, not execution of fighter-specific game code.
    public sealed class MeleePartVisibility
    {
        private readonly int[][][] groups;
        private readonly int[] hidden;
        private readonly bool[] normalDetail;
        private readonly int displayCount;
        private readonly int[] choices;
        public int GroupCount=>groups.Length;
        public bool IsNormalDetail(int displayObject)=>displayObject>=0&&displayObject<normalDetail.Length&&normalDetail[displayObject];
        public int VariantCount(int group)=>groups[group].Length;
        public int Choice(int group)=>choices[group];
        public string SourceSha256 {get;}
        public string SourceKey {get;}
        private MeleePartVisibility(int[][][] groups,int[] hidden,int count,string hash,string key)
        {this.groups=groups;this.hidden=hidden;displayCount=count;SourceSha256=hash;SourceKey=key;choices=groups.Select(g=>g.Length==0?-1:0).ToArray();
            var high=new HashSet<int>(groups.SelectMany(g=>g).SelectMany(v=>v));
            normalDetail=Enumerable.Range(0,count).Select(i=>!hidden.Contains(i)||high.Contains(i)).ToArray();}
        public void Select(int group,int variant)
        {if(group<0||group>=groups.Length||variant< -1||variant>=groups[group].Length)throw new ArgumentOutOfRangeException();choices[group]=variant;}
        public bool[] Snapshot()
        {
            var visible=Enumerable.Repeat(true,displayCount).ToArray();
            foreach(int index in hidden)visible[index]=false;
            for(int group=0;group<groups.Length;group++)for(int variant=0;variant<groups[group].Length;variant++)foreach(int index in groups[group][variant])visible[index]=variant==choices[group];
            for(int i=0;i<visible.Length;i++)visible[i]&=normalDetail[i];
            return visible;
        }
        public static MeleePartVisibility Create(SBM_PlayerModelLookupTables tables,int count,string hash,string key,int? costumeIndex=null)
        {
            if(tables==null||count<1||count>4096||tables.VisibilityLookupLength<0||tables.VisibilityLookupLength>128)throw new InvalidDataException("Native part visibility context is unavailable or exceeds its bound.");
            var costumes=tables.CostumeVisibilityLookups?.Array;
            if(costumes==null||costumes.Length==0||costumes.Length>256)throw new InvalidDataException("Native costume visibility context is unavailable.");
            int[][][] selected=null;int[] selectedHidden=null;string selectedIdentity=null;
            if(costumeIndex.HasValue&&(costumeIndex<0||costumeIndex>=costumes.Length))throw new InvalidDataException("Native costume index is outside its visibility table.");
            for(int costume=0;costume<costumes.Length;costume++)
            {
                if(costumeIndex.HasValue&&costume!=costumeIndex.Value)continue;
                var high=Read(costumes[costume].HighPoly??costumes[0].HighPoly,tables.VisibilityLookupLength,count);
                var low=Read(costumes[costume].LowPoly??costumes[0].LowPoly,tables.VisibilityLookupLength,count);
                var metal=Read(costumes[costume].MetalMainModel??costumes[0].MetalMainModel,tables.VisibilityLookupLength,count);
                var concealed=low.Concat(metal).SelectMany(g=>g).SelectMany(v=>v).Distinct().OrderBy(v=>v).ToArray();
                string identity=string.Join(";",high.Select(g=>string.Join("/",g.Select(v=>string.Join(",",v)))))+"|"+string.Join(",",concealed);
                if(selectedIdentity!=null&&identity!=selectedIdentity)throw new NotSupportedException("Costume-specific part tables differ. An exact costume-table binding is required; no default table was guessed.");
                selected=high;selectedHidden=concealed;selectedIdentity=identity;
            }
            return new MeleePartVisibility(selected,selectedHidden,count,hash,key);
        }
        private static int[][][] Read(HSDRaw.HSDArrayAccessor<SBM_LookupTable> table,int length,int count)
        {
            if(table==null)return new int[length][][].Select(_=>new int[0][]).ToArray();
            if(table.Length<length)throw new InvalidDataException("Native visibility group table is truncated.");
            var result=new int[length][][];
            for(int i=0;i<length;i++)
            {
                var group=table[i];if(group.Count<0||group.Count>256||group.Count>0&&(group.LookupEntries==null||group.LookupEntries.Length<group.Count))throw new InvalidDataException("Native visibility variant table is invalid.");
                result[i]=new int[group.Count][];
                for(int v=0;v<group.Count;v++)
                {
                    var entry=group.LookupEntries[v];if(entry.Count<0||entry.Count>count)throw new InvalidDataException("Native visibility DOBJ list exceeds the model.");
                    var values=entry.Count==0?new int[0]:entry.Entries.Select(b=>(int)b).ToArray();
                    if(values.Any(index=>index>=count))throw new InvalidDataException("Native visibility references a DOBJ outside the selected model.");result[i][v]=values;
                }
            }
            return result;
        }
    }
}
