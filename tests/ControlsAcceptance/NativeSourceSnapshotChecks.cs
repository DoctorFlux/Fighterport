using BrawlCrate.Studio;
using BrawlLib.SSBB;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;

internal static partial class Program
{
    static void NativeSourceSnapshotChecks(string stage)
    {
        Directory.CreateDirectory(stage);
        foreach (int version in new[] { 4, 5 })
        foreach (int rule in new[] { 0, 1, 2 })
        {
            string file = Path.Combine(stage, "source-v" + version + "-rule" + rule + ".chr0");
            using (var fixture = new CHR0Node { Name="Independent donor metadata",Version=version,FrameCount=9,Loop=true,OriginalPath="procedural/source-snapshot" })
            {
                if(version==5)fixture.UserEntries.Add(new UserDataClass{Name="Source marker",DataType=UserValueType.Int,Entries=new[]{"7"}});
                var entry=fixture.CreateEntry("Exact native identity");
                entry.ScaleCompensateApply=true;entry.ClassicScaleOff=true;
                for(int axis=3;axis<6;axis++)entry.Keyframes.SetFrameValue(axis,0,0,true)._tangent=0;
                foreach(int frame in new[]{0,4,8})entry.Keyframes.SetFrameValue(3,frame,3.125f+frame*.25f,true)._tangent=.25f;
                entry.Keyframes.PreserveInterpolation=true;PreciseCHR0EntryNode.Promote(fixture);fixture.ExportChecked(file);
            }
            var bytes=File.ReadAllBytes(file);int offset=version==4?0x24:0x28;
            bytes[offset]=bytes[offset+1]=bytes[offset+2]=0;bytes[offset+3]=(byte)rule;File.WriteAllBytes(file,bytes);
            string originalHash=FighterProject.HashFile(file);
            using(var source=NodeFactory.FromFile(null,file) as CHR0Node)
            {
                string clean=NativeAnimationOptimization.SourceContentKey(source);
                foreach(bool dirty in new[]{false,true})
                {
                    if(dirty)
                    {
                        var entry=(CHR0EntryNode)source.Children[0];entry.Keyframes.SetFrameValue(3,2,3.789123f,true)._tangent=.1234567f;
                        entry.Keyframes.PreserveInterpolation=false;entry.SignalPropertyChange();
                    }
                    string before=NativeAnimationOptimization.SourceContentKey(source);bool wasDirty=source.IsDirty,wasBranch=source.IsBranch;
                    var liveEntry=(CHR0EntryNode)source.Children[0];bool interpolation=liveEntry.Keyframes.PreserveInterpolation;
                    string output=Path.Combine(stage,"copy-v"+version+"-rule"+rule+"-"+dirty+".chr0");
                    using(var copy=NativeAnimationOptimization.CopySource(source))
                    {
                        Check(NativeAnimationOptimization.SourceContentKey(copy)==before,"Source snapshot preserves complete in-memory metadata/tuples / v"+version+" rule"+rule+" dirty"+dirty);
                        using(var second=NativeAnimationOptimization.CopySource(copy))
                            Check(NativeAnimationOptimization.SourceContentKey(second)==before,"Unrebuilt donor snapshots retain their managed scaling metadata through another independent copy / rule"+rule);
                        if(rule!=0)
                        {
                            bool blockedCopy=false,blockedHash=false;
                            try{using(var ignored=NativeAnimationOptimization.Copy(copy)){} }catch(NotSupportedException){blockedCopy=true;}
                            try{NativeAnimationOptimization.ContentKey(copy);}catch(NotSupportedException){blockedHash=true;}
                            Check(blockedCopy&&blockedHash,"Unrebuilt donor snapshots cannot bypass target optimizer metadata qualification / rule"+rule);
                        }
                        copy.ExportChecked(output);
                    }
                    using(var decoded=NodeFactory.FromFile(null,output) as CHR0Node)
                        Check(decoded!=null&&NativeAnimationOptimization.SourceContentKey(decoded)==before,"Source snapshot independently reopens scaling rule, metadata, flags, indices, values and tangents / v"+version+" rule"+rule+" dirty"+dirty);
                    Check(NativeAnimationOptimization.SourceContentKey(source)==before&&source.IsDirty==wasDirty&&source.IsBranch==wasBranch&&liveEntry.Keyframes.PreserveInterpolation==interpolation&&FighterProject.HashFile(file)==originalHash,
                        "Source snapshot does not rebuild/change live donor, its dirty state or original bytes / v"+version+" rule"+rule+" dirty"+dirty);
                    if(rule!=0)
                    {
                        bool blocked=false;try{using(var ignored=NativeAnimationOptimization.Copy(source)){} }catch(NotSupportedException){blocked=true;}
                        Check(blocked,"Donor metadata preservation does not broaden target optimizer scaling qualification / rule"+rule);
                    }
                }
                Check(clean!=NativeAnimationOptimization.SourceContentKey(source),"Dirty-source fixture genuinely changes the original authored native curve / v"+version+" rule"+rule);
            }
        }
        foreach(int rawVersion in new[]{4,5})
        {
            string path=Path.Combine(stage,"source-v"+rawVersion+"-rule2.chr0");string originalHash=FighterProject.HashFile(path);
            using(var source=NodeFactory.FromFile(null,path) as CHR0Node)
            {
                // Managed Version can change before the original buffer is
                // rebuilt. Its raw version still owns the scaling offset.
                source.UserEntries.Clear();source.Version=rawVersion==4?5:4;
                string before=NativeAnimationOptimization.SourceContentKey(source);
                string output=Path.Combine(stage,"version-switch-raw-v"+rawVersion+".chr0");
                using(var copy=NativeAnimationOptimization.CopySource(source))copy.ExportChecked(output);
                byte[] decodedBytes=File.ReadAllBytes(output);int offset=source.Version==4?0x24:0x28;
                using(var decoded=NodeFactory.FromFile(null,output) as CHR0Node)
                    Check(decoded!=null&&MetadataRead32(decodedBytes,offset)==2&&NativeAnimationOptimization.SourceContentKey(decoded)==before&&
                        FighterProject.HashFile(path)==originalHash,
                        "Source snapshot reads scaling with the raw version offset while writing the changed managed version / raw v"+rawVersion);
            }
            using(var source=NodeFactory.FromFile(null,path) as CHR0Node)
            {
                source.SignalPropertyChange();string upstream=Path.Combine(stage,"native-preserved-v"+rawVersion+".chr0");source.Export(upstream);
                string before=NativeAnimationOptimization.SourceContentKey(source);
                string output=Path.Combine(stage,"preserved-after-normalization-v"+rawVersion+".chr0");
                using(var copy=NativeAnimationOptimization.CopySource(source))copy.ExportChecked(output);
                using(var decoded=NodeFactory.FromFile(null,output) as CHR0Node)
                    Check(decoded!=null&&source.NativeScalingRule==2&&MetadataRead32(File.ReadAllBytes(upstream),rawVersion==4?0x24:0x28)==2&&
                        MetadataRead32(File.ReadAllBytes(output),rawVersion==4?0x24:0x28)==2&&
                        NativeAnimationOptimization.SourceContentKey(decoded)==before&&FighterProject.HashFile(path)==originalHash,
                        "Native rebuild and independent source snapshot retain original opaque scaling metadata without overwriting source bytes / v"+rawVersion);
            }
        }
        string unknown=Path.Combine(stage,"unsupported-rule.chr0");var unsupported=File.ReadAllBytes(Path.Combine(stage,"source-v4-rule0.chr0"));unsupported[0x27]=3;File.WriteAllBytes(unknown,unsupported);
        using(var source=NodeFactory.FromFile(null,unknown) as CHR0Node)
        {
            bool blocked=false;try{using(var ignored=NativeAnimationOptimization.CopySource(source)){} }catch(NotSupportedException){blocked=true;}
            Check(blocked,"Unknown donor scaling metadata rejects without guessing a replacement mode");
        }
    }
}
