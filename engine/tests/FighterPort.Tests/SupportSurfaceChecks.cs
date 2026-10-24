using System.Numerics;
using System.Text.Json;
using FighterPort.Core;

internal static class SupportSurfaceChecks
{
    public static void Run(Action<bool,string> check)
    {
        var timing=new RetargetTiming();
        check(Enumerable.Range(0,240).All(f=>timing.TargetFrame(f,60,60)==f&&timing.SourceFrame(f,60,60)==f),"Identity timing preserves every integer boundary exactly without a seconds round-trip");
        check(timing.TargetFrame(31.125,60,60)==31.125&&timing.SourceFrame(31.125,60,60)==31.125,"Identity timing preserves genuine fractional keys and contact times without snapping");
        check(timing.TargetFrame(31,60,30)==15.5&&timing.SourceFrame(15.5,60,30)==31,"Unequal rational frame rates preserve an independently expected half-frame boundary");
        var derived=new RetargetTiming{TrimStartSeconds=.1,ExportSpeed=1.5};
        check(derived.TargetFrame(24.5625,60,60)==12.375&&derived.SourceFrame(12.375,60,60)==24.5625,"Trim and export speed use the same invertible source/target boundary map");
        var floor=new SupportSurface(Vector3.UnitY,0);
        check(floor.Penetration(new(5,-2,3))==2&&floor.Penetration(new(5,2,3))==0,"A supplied floor rates below-surface geometry and leaves above-surface geometry clear");
        var ledge=new SupportSurface(Vector3.UnitY,0,new(Vector3.UnitX,0));
        check(ledge.Penetration(new(-2,-4,0))==0,"The exterior below a supplied ledge is empty space, not an infinite floor");
        check(ledge.TopOffset(new(-2,-4,0))==new Vector3(-2,-4,0),"Clear hanging space still has horizontal and vertical separation from the bounded platform top");
        check(ledge.TopOffset(new(2,0,0))==Vector3.Zero&&ledge.TopOffset(new(-3,0,0))==new Vector3(-3,0,0),"A body on the top is supported; equal height outside the edge is not support");
        var pose=new EvaluatedPose([Transform.Identity],[Matrix4x4.Identity],[Matrix4x4.Identity]);
        var pin=new ContactPin(Guid.NewGuid(),"Test support",0,new(-2,0,0),Vector3.Zero,Quaternion.Identity,false,"Explicit bounded top"){PlaneNormal=Vector3.UnitY,AllowedSurface=ledge};
        check(ContactGeometry.Residual(pin,pose)==new Vector3(-2,0,0),"A ground contact outside the platform fails even at exactly the floor height");
        check(ContactGeometry.Residual(pin with{LocalPoint=new(2,0,0)},pose)==Vector3.Zero,"A ground contact on the supplied platform top has zero separation");
        var contradictory=pin with{LocalPoint=new(-1,0,0),Position=new(-2,0,0),PlaneNormal=null};
        check(ContactGeometry.Residual(contradictory,pose)==Vector3.UnitX&&ContactGeometry.AllowedSurfaceResidual(contradictory,pose)==-Vector3.UnitX,"Opposing fixed-anchor and allowed-region violations remain separate and cannot cancel into a false pass");
        SkinSurfacePoint Point(Vector3 p)=>new(p,Vector4.Zero,new(1,0,0,0));
        var cornerRegion=new SupportRegion("Unfamiliar region",0,[Point(new(0,.001f,0)),Point(new(-.3f,-.2f,0)),Point(new(-.1f,.2f,0))]);
        var topRegion=cornerRegion with{Points=cornerRegion.Points.Select(p=>p with{Position=p.Position+new Vector3(2,0,0)}).ToArray()};
        var first=SurfaceContacts.SupportPatch(cornerRegion,pose,ledge,.02f);var second=SurfaceContacts.SupportPatch(topRegion,pose,ledge,.02f);
        check(first.Corner&&first.Separation<.002f&&!second.Corner&&second.Separation<.002f,"The same unidentified region transitions from the corner onto the top using skin geometry alone");
        check(SurfaceContacts.SupportPatch(cornerRegion with{Points=cornerRegion.Points.Select(p=>p with{Position=p.Position+Vector3.UnitY}).ToArray()},pose,ledge,.02f).Separation>.7f,"A stationary region in midair is separated from the supplied support");
        var buried=new SupportRegion("Unidentified volume",0,[Point(new(0,-2,0)),Point(Vector3.Zero),Point(Vector3.UnitY)]);
        check(SurfaceContacts.SupportPatch(buried,pose,floor,.02f).Separation==2,"A floor cannot be validated by a near-zero vertex inside an otherwise buried skin region");
        var buriedSupport=pin with{SurfaceRegion=[Point(new(2,0,0)),Point(new(2,-2,0)),Point(new(-1,-4,0))]};
        check(ContactGeometry.SupportSeparation(buriedSupport,pose)==2,"Independent target-support acceptance detects the buried underside despite a near-zero upper vertex, while allowing exterior hanging skin");
        var boundary=pin with{SurfaceRegion=[Point(new(-.0002f,.0002f,0)),Point(new(2,1,0))]};
        check(ContactGeometry.SupportSeparation(boundary,pose,.0004f)<.0004f,"A complete boundary distance within the frozen bound is accepted without an exact-zero footprint test");
        check(ContactGeometry.SupportSeparation(boundary with{SurfaceRegion=[Point(new(-.0005f,.0002f,0)),Point(new(2,1,0))]},pose,.0004f)>.0004f,"A boundary distance outside the frozen bound remains a failure");
        check(ContactGeometry.SupportSeparation(pin with{SurfaceRegion=[Point(new(-.0002f,-2,0)),Point(new(2,0,0))]},pose,.0004f)==0,"Deep exterior hanging skin is excluded even when its horizontal wall distance is small");
        var moved=new ContactReference(Guid.NewGuid(),"Moving support","Independent translation fixture"){Motion=[new(0,new(5,3,0),Quaternion.Identity)]};
        check(ContactGeometry.Residual(pin with{Reference=moved,LocalPoint=new(3,3,0)},pose)==new Vector3(-2,0,0),"Support-local top bounds follow the same moving frame as the contact anchor");
        var restoredPin=JsonSerializer.Deserialize<ContactPin>(JsonSerializer.Serialize(pin,ProjectStore.Json),ProjectStore.Json)!;
        check(restoredPin.AllowedSurface==pin.AllowedSurface&&ContactGeometry.Valid(restoredPin,1),"Allowed top support survives project serialization without changing an authored anchor");
        check(!ContactGeometry.Valid(pin with{PlaneNormal=Vector3.UnitZ},1),"An incompatible plane/allowed-top combination is rejected rather than silently dropping the plane constraint");
        check(ledge.Penetration(new(2,-3,0))==2&&ledge.Correction(new(2,-3,0))==new Vector3(-2,0,0),"The 90-degree wall independently bounds penetration and its nearest exit");
        check(ledge.Penetration(new(4,-1,0))==1&&ledge.Correction(new(4,-1,0))==Vector3.UnitY,"Inside the platform near its top, clearance uses the top surface");
        check(ledge.Penetration(new(2,1,0))==0&&ledge.Penetration(new(0,-3,0))==0,"Above-top and boundary points remain clear without clipping the hanging phase");
        var transformed=new SupportSurface(Vector3.UnitZ,2,new(-Vector3.UnitX,3));
        check(transformed.Penetration(new(-4,0,1))==1&&transformed.Penetration(new(-2,0,-4))==0,"Support geometry accepts a rotated frame and a nonzero edge offset");
        bool rejected=false;try{new SupportEdge(Vector3.UnitY,0).Validate(Vector3.UnitY);}catch(InvalidDataException){rejected=true;}
        check(rejected,"A non-perpendicular 90-degree edge is rejected rather than silently rotated");
        string old=JsonSerializer.Serialize(new GroundContactSettings(),ProjectStore.Json);
        check(!old.Contains("ExplicitSurface")&&!old.Contains("Edge"),"Unspecified context keeps the historical serialized defaults and source-proof representation");
        var settings=new GroundContactSettings{ExplicitSurface=true,SourceFloor=2,TargetFloor=3,SourceEdge=new(Vector3.UnitX,4),TargetEdge=new(-Vector3.UnitX,5)};
        var reopened=JsonSerializer.Deserialize<GroundContactSettings>(JsonSerializer.Serialize(settings,ProjectStore.Json),ProjectStore.Json)!;
        check(reopened==settings,"Explicit source and target support coordinates survive serialization independently");
    }
}
