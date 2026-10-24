using FighterPort.Core;

internal static class NativeMaterialChecks
{
    public static void Run(FighterProject project,Action<bool,string> check)
    {
        var scene=project.Scene;
        var active=scene.Meshes.SelectMany(m=>m.DrawCalls).Where(d=>d.DefaultVisible).Select(d=>d.Material).Distinct().ToArray();
        check(active.Length>0&&active.All(name=>scene.Materials.Single(m=>m.Name==name).Program is not null),"Private visible native draw calls retain generated TEV programs and material state");
        check(scene.Materials.Where(m=>active.Contains(m.Name)).SelectMany(m=>m.Program!.Bindings).All(b=>b.Matrix.Length==16&&b.Matrix.Any(x=>x!=0)&&b.BaseSrt.Length==5),"Private material bind texture matrices are initialized instead of collapsing UVs to zero");
        check(scene.Meshes.SelectMany(m=>m.Vertices).All(v=>v.UVSets.Length==8),"Private native facepoints preserve all eight UV sets for multi-texture materials");
        var wait=project.Clips.Single(c=>c.Source?.NativeName=="Wait1");var doc=new EditorDocument(project);doc.SelectClip(Array.IndexOf(project.Clips,wait));
        string family=scene.Name.Contains("Mario",StringComparison.OrdinalIgnoreCase)?"SRT0":"VIS0";
        var layer=doc.Layer(family);
        check(layer is not null&&layer.Name=="Wait1"&&layer.Resource.ContainerEntry==wait.Source!.Resource.ContainerEntry&&layer.Tracks.Length>0,$"Private Wait1 CHR0 automatically selects its decoded {family} in the same native container");
        doc.Seek(12.375);check(layer is not null&&doc.Clock.Frame==12.375&&doc.LayerFrame(layer)==12.375,$"Private {family} and bone animation scrub together at an authentic fractional sample");
        if(family!="SRT0")return;
        foreach(string name in new[]{"EyeL","EyeR"})
        {
            var bindings=scene.Materials.Single(m=>m.Name==name).Program!.Bindings;
            check(bindings.Select(b=>b.Texture).SequenceEqual(new[]{"FitMario_EyeUpper","FitMario_EyeLower","FitMario_EyeIris","FitMario_EyeWhite"})&&bindings.Select(b=>b.Coordinate).SequenceEqual(new[]{0,1,2,3}),$"Mario {name} retains its four authentic texture layers and distinct UV coordinates");
        }
        check(scene.Meshes.SelectMany(m=>m.DrawCalls).Where(d=>d.VisibilityBone=="EyeYellowM").All(d=>!d.DefaultVisible),"Mario alternate yellow eyes retain native default draw visibility");
    }
}
