using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
namespace FighterPort.Adapters.Melee
{
    // Read the native executable's filename/joint/matanim pointer records.
    // No executable code is run, and no character, offset or revision table is embedded.
    public static class MeleeCostumeBinding
    {
        private sealed class Section {internal uint Address;internal int Offset,Size;}
        public static int FindIndex(byte[] dol,string file,string joint,string canonicalActor,int costumeCount,CancellationToken cancellation=default)
        {
            cancellation.ThrowIfCancellationRequested();
            if(dol==null||dol.Length<256||dol.Length>32*1024*1024||costumeCount<1||costumeCount>256)throw new InvalidDataException("Invalid native costume binding input.");
            var sections=new List<Section>();
            for(int i=0;i<18;i++)
            {
                uint offset=MeleeSource.U32(dol,i*4),size=MeleeSource.U32(dol,0x90+i*4),address=MeleeSource.U32(dol,0x48+i*4);if(size==0)continue;
                if(offset<256||offset>(long)dol.Length-size||(long)address+size>uint.MaxValue||sections.Any(s=>(long)address<s.Address+(long)s.Size&&(long)s.Address<address+(long)size))throw new InvalidDataException("Invalid or overlapping DOL section.");
                sections.Add(new Section{Address=address,Offset=(int)offset,Size=(int)size});
            }
            string ReadString(uint address)
            {
                var section=sections.SingleOrDefault(s=>address>=s.Address&&(long)address<s.Address+(long)s.Size);if(section==null)return null;
                int offset=checked(section.Offset+(int)(address-section.Address)),end=offset,limit=Math.Min(section.Offset+section.Size,offset+512);
                while(end<limit&&dol[end]!=0){if(dol[end]<32||dol[end]>126)return null;end++;}
                return end<limit?Encoding.ASCII.GetString(dol,offset,end-offset):null;
            }
            Tuple<string,string> Record(int offset)
            {
                if(offset<256||offset>dol.Length-12)return null;
                string name=ReadString(MeleeSource.U32(dol,offset)),symbol=ReadString(MeleeSource.U32(dol,offset+4));uint material=MeleeSource.U32(dol,offset+8);
                if(name==null||symbol==null||!name.EndsWith(".dat",StringComparison.OrdinalIgnoreCase)||name.IndexOfAny(new[]{'/', '\\', ':'})>=0||!symbol.EndsWith("_Share_joint",StringComparison.Ordinal))return null;
                if(material!=0&&ReadString(material)!=symbol.Substring(0,symbol.Length-5)+"matanim_joint")return null;
                return Tuple.Create(name,symbol);
            }
            var matches=new List<int>();
            foreach(var section in sections)for(int offset=section.Offset;offset<=section.Offset+section.Size-12;offset+=4)
            {
                if((offset&4095)==0)cancellation.ThrowIfCancellationRequested();
                var record=Record(offset);if(record==null||!string.Equals(record.Item1,file,StringComparison.OrdinalIgnoreCase)||record.Item2!=joint)continue;
                int first=offset,index=0;
                while(index<costumeCount-1&&first-12>=section.Offset)
                {
                    var previous=Record(first-12);if(previous==null||!previous.Item2.StartsWith(canonicalActor,StringComparison.Ordinal))break;first-=12;index++;
                }
                var start=Record(first);
                if(start!=null&&start.Item2==canonicalActor+"_Share_joint")matches.Add(index);
            }
            if(matches.Count!=1)throw new InvalidDataException("No unique exact native filename/joint costume-table binding. Parts require explicit context.");return matches[0];
        }
    }
}
