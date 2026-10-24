using System.Numerics;

namespace FighterPort.Core;

public static class ProceduralFixture
{
    // Original geometry, rig and curves authored for this repository. No game assets.
    public static FighterProject Create(float proportions=1)
    {
        Bone[] bones=[B("Root",-1,0,0,0), B("Pelvis",0,0,1.05f,0),B("Spine",1,0,.34f,0),B("Head",2,0,.48f,0),
            B("Arm.L",2,-.30f,.27f,0),B("Forearm.L",4,-.42f*proportions,0,0),B("Hand.L",5,-.36f,0,0),
            B("Arm.R",2,.30f,.27f,0),B("Forearm.R",7,.42f*proportions,0,0),B("Hand.R",8,.36f,0,0),
            B("Thigh.L",1,-.17f,-.1f,0),B("Shin.L",10,0,-.46f,0),B("Foot.L",11,0,-.43f,0),
            B("Thigh.R",1,.17f,-.1f,0),B("Shin.R",13,0,-.46f,0),B("Foot.R",14,0,-.43f,0)];
        var scene=new Scene("Atelier / original skinned mannequin",bones,[]);
        var bind=AnimationService.Evaluate(scene,null,0);
        var vertices=new List<Vertex>(); var indices=new List<uint>();
        void Box(int joint,Vector3 center,Vector3 size,Vector4 color)
        {
            Vector3[] ns=[Vector3.UnitX,-Vector3.UnitX,Vector3.UnitY,-Vector3.UnitY,Vector3.UnitZ,-Vector3.UnitZ];
            foreach(var n in ns)
            {
                var u=MathF.Abs(n.Y)>.5f?Vector3.UnitX:Vector3.Normalize(Vector3.Cross(Vector3.UnitY,n)); var v=Vector3.Cross(n,u);
                uint start=(uint)vertices.Count;
                foreach(var (x,y) in new[]{(-1,-1),(1,-1),(1,1),(-1,1)})
                {
                    var local=center+(n+u*x+v*y)*size*.5f;
                    var p=Vector3.Transform(local,bind.World[joint]);
                    var normal=Vector3.Normalize(Vector3.TransformNormal(n,bind.World[joint]));
                    vertices.Add(new(p,normal,new((x+1)/2f,(y+1)/2f),new(joint,0,0,0),new(1,0,0,0),color));
                }
                indices.AddRange([start,start+1,start+2,start,start+2,start+3]);
            }
        }
        var teal=new Vector4(.17f,.78f,.73f,1); var dark=new Vector4(.12f,.20f,.27f,1); var light=new Vector4(.8f,.88f,.90f,1);
        Box(1,new(0,.08f,0),new(.48f,.32f,.29f),dark); Box(2,new(0,.17f,0),new(.55f,.44f,.32f),teal);
        Box(3,new(0,.12f,0),new(.32f,.35f,.30f),light); Box(3,new(0,.14f,.157f),new(.25f,.10f,.02f),dark);
        Box(2,new(0,.43f,0),new(.18f,.13f,.18f),dark);
        for(int j=4;j<=9;j++){float length=j%3==1?.42f*proportions:j%3==2?.36f:.14f;Box(j,new((j<=6?-1:1)*length*.5f,0,0),new(length,.15f,.17f),j%3==1?teal:light);}
        foreach(int j in new[]{10,11,13,14}) {float length=j is 10 or 13?.46f:.43f;Box(j,new(0,-length*.5f,0),new(.19f,length,.22f),j is 10 or 13?dark:teal);}
        foreach(int j in new[]{12,15}) Box(j,new(0,-.015f,.09f),new(.23f,.15f,.40f),dark);
        scene=scene with { Meshes=[new("Mannequin",vertices.ToArray(),indices.ToArray())] };
        Curve Wave(float amplitude)=>new([new(0,0,amplitude/8,amplitude/8),new(15,amplitude),new(30,0,-amplitude/8,-amplitude/8),new(45,-amplitude),new(60,0,amplitude/8,amplitude/8)]);
        BoneTrack Track(string b,int channel,Curve c) { var a=new Curve?[9]; a[channel]=c; return new(b,a); }
        AnimationClip Clip(string name,int n,bool loop,params BoneTrack[] tracks)=>new(Guid.NewGuid(),name,n,new(60),loop,tracks);
        var idle=Clip("Idle · breathing",60,true,Track("Spine",4,Wave(5)),Track("Arm.L",5,new([new(0,12)])),Track("Arm.R",5,new([new(0,-12)])));
        var wave=Clip("Signal · arm wave",60,true,Track("Arm.R",5,Wave(65)),Track("Forearm.R",4,Wave(45)),Track("Head",4,Wave(-14)));
        var move=Clip("Strike · 24 frames",24,false,Track("Arm.R",4,new([new(0,0),new(7,-90),new(11,70),new(17,40),new(23,0)])),Track("Spine",4,new([new(0,0),new(11,35),new(23,0)])));
        var fall=Clip("Fall · airborne",60,true,Track("Thigh.L",3,Wave(25)),Track("Thigh.R",3,Wave(-25)),Track("Arm.L",5,new([new(0,-25)])),Track("Arm.R",5,new([new(0,25)])));
        return new(1,Guid.NewGuid(),"Untitled Fighter",scene,[wave,idle,move,fall]);
    }
    static Bone B(string n,int parent,float x,float y,float z)=>new(n,parent,Transform.Identity with {Translation=new(x,y,z)});
}
