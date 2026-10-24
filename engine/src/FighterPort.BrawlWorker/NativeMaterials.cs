using System;
using System.Collections.Generic;
using System.Linq;
using BrawlLib.Internal;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Graphics;

internal static unsafe partial class Program
{
    static object Color(Vector4 c)=>V4(c._x,c._y,c._z,c._w);
    static float[] RawMatrix(Matrix matrix){var result=new float[16];for(int i=0;i<16;i++)result[i]=((float*)&matrix)[i];return result;}
    static object Material(MDL0MaterialNode m,string path,string hash)
    {
        object program=null;
        try
        {
            foreach(var texture in m.Children.OfType<MDL0MaterialRefNode>())texture._frameState=texture._bindState;
            ShaderGenerator.UsePixelLighting=false;ShaderGenerator.SetTarget(m);
            var vertex=ShaderGenerator.GenVertexShader();var stages=m.ShaderNode==null?null:ShaderGenerator.GenTEVFragShader();
            var fragment=ShaderGenerator.CombineFragShader(ShaderGenerator.GenMaterialFragShader(),stages,m.ActiveShaderStages);
            var colors=new Dictionary<string,object>{{"amb[0]",Color(m.C1AmbientColor)},{"amb[1]",Color(m.C2AmbientColor)},
                {"clr[0]",Color(m.C1MaterialColor)},{"clr[1]",Color(m.C2MaterialColor)},
                {"creg[0]",Color(m.Color0)},{"creg[1]",Color(m.Color1)},{"creg[2]",Color(m.Color2)},
                {"ccreg[0]",Color(m.ConstantColor0)},{"ccreg[1]",Color(m.ConstantColor1)},{"ccreg[2]",Color(m.ConstantColor2)},{"ccreg[3]",Color(m.ConstantColor3)}};
            program=new{VertexSource=vertex,FragmentSource=fragment,Colors=colors,
                Bindings=m.Children.OfType<MDL0MaterialRefNode>().Select(t=>new{Texture=t.Texture,Palette=t.Palette,Coordinate=t.TextureCoordId,WrapS=(int)t.UWrapMode,WrapT=(int)t.VWrapMode,MinFilter=(int)t.MinFilter,MagFilter=(int)t.MagFilter,Matrix=RawMatrix(t.GetTransform(false)),BaseSrt=Enumerable.Range(0,5).Select(i=>t._bindState[i]).ToArray()}).ToArray(),
                Cull=(int)m.CullMode,DepthTest=m.EnableDepthTest,DepthWrite=m.EnableDepthUpdate,DepthCompare=(int)m.DepthFunction,
                Blend=m.EnableBlend,BlendSource=(int)m._blendMode.SrcFactor,BlendDestination=(int)m._blendMode.DstFactor,Subtract=m._blendMode.Subtract,
                LogicOperation=m._blendMode.EnableLogicOp,LogicMode=(int)m._blendMode.LogicOp,Generator="BrawlLib ShaderGenerator d56cbac89dc2cd0e5c3f923e1d03041b16902add LGPL-3.0; native TEV program",
                Limitations=m.Children.OfType<MDL0MaterialRefNode>().Where(t=>t.MapMode!=MappingMethod.TexCoord).Select(t=>"Unverified dynamic texture effect: "+t.MapMode+", slot "+t.Index).Concat(m.IndirectShaderStages>0?new[]{"GX indirect texture stages are not verified"}:new string[0]).ToArray()};
        }
        catch(Exception e){Diagnostics.Add("Material shader unavailable: "+m.Name+" / "+e.Message);}
        finally{ShaderGenerator.ClearTarget();}
        return new{Name=m.Name,Textures=m.Children.OfType<MDL0MaterialRefNode>().Select(t=>t.Texture).ToArray(),Native=Resource(m,path,hash),Program=program};
    }
    static object[] TextureImages(ResourceNode modelRoot,ResourceNode motionRoot,MDL0Node model)
    {
        var nodes=Walk(modelRoot).Concat(motionRoot==null?Enumerable.Empty<ResourceNode>():Walk(motionRoot)).ToArray();
        var images=nodes.OfType<TEX0Node>().GroupBy(t=>t.Name).ToDictionary(g=>g.Key,g=>g.First());
        var palettes=nodes.OfType<PLT0Node>().GroupBy(t=>t.Name).ToDictionary(g=>g.Key,g=>g.First());
        var output=new Dictionary<string,object>();
        foreach(var image in images.Values){var decoded=Texture(image);if(decoded!=null)output[image.Name]=decoded;}
        Action<string,string> add=(texture,palette)=>{if(string.IsNullOrEmpty(texture)||string.IsNullOrEmpty(palette))return;string key=texture+"::"+palette;if(output.ContainsKey(key))return;if(images.TryGetValue(texture,out var image)&&palettes.TryGetValue(palette,out var pal)){var decoded=Texture(image,pal,key);if(decoded!=null)output[key]=decoded;}else Diagnostics.Add("Unresolved texture/palette variant: "+key);};
        if(model.MaterialList!=null)foreach(var material in model.MaterialList.Cast<MDL0MaterialNode>())foreach(var binding in material.Children.OfType<MDL0MaterialRefNode>())add(binding.Texture,binding.Palette);
        foreach(var pat in nodes.OfType<PAT0Node>())foreach(var material in pat.Children)foreach(var binding in material.Children.OfType<PAT0TextureNode>())foreach(var key in binding.Children.OfType<PAT0TextureEntryNode>())
        {
            var baseMaterial=model.MaterialList?.Cast<MDL0MaterialNode>().FirstOrDefault(m=>m.Name==material.Name);var original=baseMaterial?.Children.OfType<MDL0MaterialRefNode>().ElementAtOrDefault(binding.TextureIndex);
            add(string.IsNullOrEmpty(key.Texture)?original?.Texture:key.Texture,string.IsNullOrEmpty(key.Palette)?original?.Palette:key.Palette);
        }
        return output.Values.ToArray();
    }
}
