using FighterPort.Core;
using FighterPort.Rendering;
using System.Numerics;

internal static class CompanionLayerChecks
{
    public static void Run(FighterProject fixture,string directory,Action<bool,string> check)
    {
        string path=Path.Combine(directory,"original-motion.pac");var source=new NativeResourceRef(path,"root/2/4/0/7","original-fixture-hash","CHR0",4);
        var clip=fixture.Clips[0] with{Name="Signal [root/2/4/0/7]",Source=new(Guid.NewGuid(),source,null,fixture.Scene.SkeletonRevision,new(60)){NativeName="Signal"}};
        NativeAnimationLayer Layer(string kind,string entry,string name="Signal",string? hash=null)=>new(Guid.NewGuid(),name,kind,30,true,new(30),new(path,entry,hash??source.Sha256,kind,4),[],"Original fixture");
        var vis=Layer("VIS0","root/2/4/3/1");var elsewhere=Layer("VIS0","root/8/4/3/1");var srt=Layer("SRT0","root/2/4/2/5");var pat=Layer("PAT0","root/2/4/4/2");var clr=Layer("CLR0","root/2/4/1/0");var shp=Layer("SHP0","root/2/4/5/0");var scn=Layer("SCN0","root/2/4/6/0");
        var other=clip with{Id=Guid.NewGuid(),Name="Other [root/2/4/0/8]",Source=clip.Source! with{NativeName="Other",Resource=source with{Entry="root/2/4/0/8"}}};
        var project=fixture with{Clips=[clip,other],NativeLayers=[vis,elsewhere,srt,pat,clr,shp,scn,Layer("VIS0","root/2/4/3/9",hash:"different-source")]};
        var doc=new EditorDocument(project);check(doc.Layer("VIS0")?.Id==vis.Id,"Same-name VIS0 automatically pairs with CHR0 in the closest native archive branch");
        check(new[]{"VIS0","SRT0","PAT0","CLR0","SHP0"}.All(k=>doc.Layer(k) is not null)&&doc.Layer("SCN0") is null,"Morph, visibility, texture and color companions run simultaneously with bone animation while scene animation stays skipped");
        doc.Clock.Playing=true;doc.Tick(.25);check(doc.Clock.Frame==15&&doc.LayerFrame(vis)==7.5&&doc.LayerFrame(srt)==7.5,"Concurrent bone/visibility/texture layers share authentic elapsed time across timebases");
        doc.SetLayer("VIS0",elsewhere.Id);doc.Seek(13.125);check(doc.Layer("VIS0")?.Id==elsewhere.Id&&doc.LayerFrame(elsewhere)==6.5625,"Manual layer override survives fractional scrubbing");
        doc.SelectClip(1);check(doc.ActiveLayers.Count==0,"Changing to an unmatched CHR0 clears stale automatic companions");
        doc.SelectClip(0);doc.AutoMatchNativeLayers=false;doc.SetLayer("VIS0",elsewhere.Id);doc.SelectClip(1);check(doc.Layer("VIS0")?.Id==elsewhere.Id,"Disabling name matching preserves explicitly selected layers across clips");
        doc.AutoMatchNativeLayers=true;doc.SelectClip(0);check(doc.Layer("VIS0")?.Id==vis.Id,"Re-enabling automatic matching restores the closest authentic companion");
        var duplicate=Layer("VIS0","root/2/4/3/6");doc.Replace(project with{NativeLayers=[vis,duplicate,srt]});
        check(doc.Layer("VIS0") is null&&doc.LayerDiagnostics.Any(d=>d.Code=="AMBIGUOUS_COMPANION"&&d.Message.Contains(vis.Resource.Entry)&&d.Message.Contains(duplicate.Resource.Entry)),"Equally close duplicate companion names report exact conflicting entries");
        check(doc.Layer("SRT0")?.Id==srt.Id,"An ambiguous visibility family does not block other unambiguous companions");
        doc.Replace(project with{NativeLayers=[elsewhere]});check(doc.Layer("VIS0") is null&&doc.LayerDiagnostics.Any(d=>d.Code=="COMPANION_CONTAINER"),"A unique same-name layer in another character/article container requires explicit selection");
        var colorTrack=new LayerTrack("OriginalMaterial",0,[new([new(0,3)],Interpolation.Step),new([new(0,128)],Interpolation.Step),new([new(0,12)],Interpolation.Step),new([new(0,127)],Interpolation.Step)]){ColorMask=0x00F00FFF};
        var mixed=NativeMaterialSampling.Color(new Vector4(165,195,86,224)/255,colorTrack,.375);
        check(Vector4.Distance(mixed,new Vector4(163,131,94,127)/255)<.000001f,"Concurrent CLR0 preserves masked register bits and applies stepped RGBA channels");
        var pattern=new LayerTrack("OriginalMaterial",0,[]){TextureSwaps=[new(0,"OriginalA",null),new(5,"OriginalB","PaletteB")]};
        check(NativeMaterialSampling.Texture(pattern,4.999)?.Texture=="OriginalA"&&NativeMaterialSampling.Texture(pattern,5)?.Texture=="OriginalB"&&NativeMaterialSampling.Texture(pattern,5)?.Palette=="PaletteB","Concurrent PAT0 switches texture and palette exactly at held native key times");
    }
}
