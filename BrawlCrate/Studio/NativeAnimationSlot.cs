using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BrawlCrate.Studio
{
    // Custom editor intent, not a verified game's runtime animation table.
    public sealed class NativeAnimationSlot
    {
        public string Id {get;set;}
        public bool Required {get;set;}
        public bool Protected {get;set;}
        public string AnimationKey {get;set;}
        public string AnimationSha256 {get;set;}
        public string ModelSha256 {get;set;}
        public string ProfileId {get;set;}public string NativeEntry {get;set;}
        internal NativeAnimationSlot Copy()=>(NativeAnimationSlot)MemberwiseClone();
        internal static void Validate(IReadOnlyCollection<NativeAnimationSlot> slots)
        {
            bool Hash(string s)=>s!=null&&s.Length==64&&s.All(Uri.IsHexDigit);
            if(slots==null||slots.Count>1024||slots.Any(s=>s==null||string.IsNullOrWhiteSpace(s.Id)||s.Id.Length>80||s.Id.Any(char.IsControl)||s.ProfileId!=null&&(!Guid.TryParseExact(s.ProfileId,"N",out _)||string.IsNullOrEmpty(s.NativeEntry)||s.NativeEntry.Length>16384)||s.AnimationKey!=null&&(s.AnimationKey.Length==0||s.AnimationKey.Length>16384||!Hash(s.AnimationSha256)||!Hash(s.ModelSha256)))||slots.Select(s=>s.Id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=slots.Count)throw new InvalidDataException("Slots need unique names of 1–80 characters and exact native assignment hashes (maximum 1024 slots).");
        }
    }
}
