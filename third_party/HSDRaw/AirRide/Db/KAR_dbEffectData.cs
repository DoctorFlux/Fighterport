using System.IO;

namespace HSDRaw.AirRide.Db
{
    public class KAR_dbEffect : HSDAccessor
    {
        public override int TrimmedSize => 0x10;

        public string Kind { get => _s.GetString(0x00); set => _s.SetString(0x00, value); }

        public uint EffectID { get => _s.GetUInt32(0x04); set => _s.SetUInt32(0x04, value); }

        public string Name { get => _s.GetString(0x08); set => _s.SetString(0x08, value); }

        public uint x0C { get => _s.GetUInt32(0x04); set => _s.SetUInt32(0x04, value); }

        public override string ToString()
        {
            return $"{EffectID}: ({Kind}) {Name}";
        }
    }

    public class KAR_dbEffectData : HSDAccessor
    {
        public override int TrimmedSize => 0x08;

        public HSDArrayAccessor<KAR_dbEffect> Entries { get => _s.GetReference<HSDArrayAccessor<KAR_dbEffect>>(0x00); set => _s.SetReference(0x00, value); }

        // The last entry is 4 floats at this offset
        public uint FloatsOffset { get => _s.GetUInt32(0x04); set => _s.SetUInt32(0x04, value); }

        public void DumpToFile(string filePath)
        {
            using (var s = new FileStream(filePath, FileMode.Create))
            using (var w = new StreamWriter(s))
            {
                foreach (var e in Entries.Array)
                {
                    w.WriteLine(e.ToString());
                }
            }
        }
    }
}
