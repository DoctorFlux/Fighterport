// Adapted from BrawlLib/Internal/Matrix34.cs, revision d56cbac89dc2cd0e5c3f923e1d03041b16902add.
// Copyright BrawlLib authors: Bryan Moulton, BlackJax96, libertyernie, Sammi Husky;
// BrawlCrate contributors soopercool101, NuclearNecrosis, Simon381. LGPL-3.0.
// Modified: managed System.Numerics output, general direct SRT equations only.
using System.Numerics;

namespace FighterPort.Rendering;

public static class BrawlTextureTransform
{
    public static Matrix4x4 Evaluate(float sx,float sy,float degrees,float tx,float ty,int mode)
    {
        float sin=(float)Math.Sin(degrees*(MathF.PI/180)),cos=(float)Math.Cos(degrees*(MathF.PI/180));
        float a=sx*cos,b=sx*sin,c=sy*cos,d=sy*sin,x,y;
        if(mode==1){x=b-a*tx-b*ty;y=-c-d*tx+c*ty+1;b=-b;}
        else if(mode==2){x=-a*(tx+.5f)+b*(ty-.5f)+.5f;y=d*(tx+.5f)+c*(ty-.5f)+.5f;d=-d;}
        else{x=sx*(-.5f*cos-.5f*sin+.5f-tx);y=sy*(-.5f*cos+.5f*sin-.5f+ty)+1;d=-d;}
        var result=new Matrix4x4(a,d,0,0,b,c,0,0,0,0,1,0,x,y,0,1);
        // The pinned upstream pure-Maya-rotation branch writes its T offset to d[6].
        // Preserve that observable compatibility behavior rather than silently substituting general SRT.
        if(mode==0&&sx==1&&sy==1&&tx==0&&ty==0&&degrees!=0){result.M42=0;result.M32=.5f-.5f*cos+.5f*sin;}
        return result;
    }
}
