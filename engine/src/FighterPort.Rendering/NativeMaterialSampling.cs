using System.Numerics;
using FighterPort.Core;

namespace FighterPort.Rendering;

public static class NativeMaterialSampling
{
    public static string? ColorUniform(int target)=>target switch{0=>"clr[0]",1=>"clr[1]",2=>"amb[0]",3=>"amb[1]",>=4 and <=6=>$"creg[{target-4}]",>=7 and <=10=>$"ccreg[{target-7}]",_=>null};
    public static Vector4 Color(Vector4 original,LayerTrack track,double frame)
    {
        Vector4 result=default;int[] shifts=[16,8,0,24];
        for(int c=0;c<4;c++){int before=(int)MathF.Round(original[c]*255)&255;int mask=(int)(track.ColorMask>>shifts[c])&255;int value=(int)(track.Curves[c]?.Sample(frame)??0);result[c]=((before&mask)|value)/255f;}
        return result;
    }
    public static TextureSwap? Texture(LayerTrack? track,double frame)=>track?.TextureSwaps.LastOrDefault(k=>k.Frame<=frame)??track?.TextureSwaps.FirstOrDefault();
}
