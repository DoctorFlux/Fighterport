using System.Numerics;
using FighterPort.Core;

namespace FighterPort.Rendering;

public sealed record EditorPinHandle(Guid Id,int Bone,string Name,bool Soft,Vector3 Position,Quaternion Rotation)
{
    public override string ToString()=>(Soft?"Soft pin: ":"FK pin: ")+Name;
}

public sealed unsafe partial class SkinnedViewport
{
    public Guid? SelectedPinId {get;private set;}
    public bool PickPinsOnly {get;set;}
    public bool PinsVisible {get;set;}=true;
    Guid? gesturePin;
    EditorPinTarget? pinGestureTarget;
    Vector3 initialPinPosition;
    Quaternion initialPinRotation;
    public EditorPinHandle[] EditorPinHandles(EvaluatedPose? pose=null)
    {
        pose??=Document.Evaluate();
        return Document.CurrentContacts.Where(p=>p.EditorBehavior==EditorPinBehavior.SoftReach).Select(p=>new EditorPinHandle(p.Id,p.Bone,p.Role,true,pose.World[p.Bone].Translation,PoseGeometry.Rotation(pose.World[p.Bone])))
            .Concat(Document.CurrentBonePins.Select(p=>new EditorPinHandle(p.Id,p.Bone,Document.Project.Scene.Bones[p.Bone].Name,false,pose.World[p.Bone].Translation,PoseGeometry.Rotation(pose.World[p.Bone])))).ToArray();
    }
    public EditorPinHandle? SelectedEditorPin=>SelectedPinId is {} id?EditorPinHandles().FirstOrDefault(p=>p.Id==id):null;
    public void ClearEditorPinSelection(){SelectedPinId=null;pinGestureTarget=null;Refresh();}
    public void SynchronizePinSelection(){if(SelectedEditorPin is not {} pin||pin.Bone!=Document.SelectedBone)ClearEditorPinSelection();}
    public bool SelectEditorPin(Guid id)
    {
        if(IsDragging||Document.IsSourcePreview||!PoseEditingEnabled||!PinsVisible)return false;
        var pin=EditorPinHandles().FirstOrDefault(p=>p.Id==id);if(pin is null)return false;
        DisableIK();SelectedPinId=id;Document.SelectedBone=pin.Bone;PoseChanged?.Invoke();Refresh();return true;
    }
    public bool TryPickEditorPin(Vector2 screen)
    {
        if(!PinsVisible||Document.IsSourcePreview||!PoseEditingEnabled)return false;
        var pin=EditorPinHandles().Where(p=>Vector4.Transform(new Vector4(p.Position,1),Camera.ViewProjection((float)Bounds.Width/Math.Max(1,(float)Bounds.Height))).W>0)
            .OrderBy(p=>Vector2.DistanceSquared(screen,Screen(p.Position))).FirstOrDefault();
        return pin is not null&&Vector2.Distance(screen,Screen(pin.Position))<=12&&SelectEditorPin(pin.Id);
    }
    public bool PreviewPinTransform(Vector3 position,Quaternion rotation,Vector3? scale=null)
    {
        if(SelectedEditorPin is not {} pin||gesturePin!=pin.Id||transformEdit is null||!PoseGeometry.Finite(position)||!float.IsFinite(rotation.LengthSquared())||Math.Abs(rotation.LengthSquared()-1)>.001f||scale is {} requestedScale&&(!PoseGeometry.Finite(requestedScale)||requestedScale.X<=0||requestedScale.Y<=0||requestedScale.Z<=0))return false;
        var pose=ikInput!;var scene=Document.Project.Scene;
        if(pin.Soft)
        {
            if(ControlSetup is null){InteractionDiagnostic="Review the existing limb controls before moving this soft pin.";return false;}
            if(scale is {} s&&(s.X<=0||Math.Abs(s.X-s.Y)>.0001f||Math.Abs(s.Y-s.Z)>.0001f)){InteractionDiagnostic="Soft limb pins require uniform positive scale. Use an explicit FK edit for another supported scale policy.";return false;}
            var contacts=Document.CurrentContacts.Select(p=>p.Id==pin.Id?p with{Position=position,Rotation=rotation}:p).ToArray();
            var result=EditorControlPosing.Solve(scene,pose,ControlSetup,new Dictionary<int,Transform>(),contacts);InteractionDiagnostic=string.Join(" · ",result.Diagnostics.Select(d=>d.Message));if(result.Value is null)return false;
            ikKeys=result.Value.Transforms;if(scale is {} size)ikKeys[pin.Bone]=(ikKeys.GetValueOrDefault(pin.Bone)??pose.Local[pin.Bone]) with{Scale=size};
            Document.PreviewTransforms(ikKeys);pinGestureTarget=new(position,rotation);return true;
        }
        int parent=scene.Bones[pin.Bone].Parent;var world=parent<0?Matrix4x4.Identity:pose.World[parent];if(!Matrix4x4.Invert(world,out var inverse)){InteractionDiagnostic="The pin's native parent transform is singular.";return false;}
        var value=PoseGeometry.WorldRotation(scene,pose,pin.Bone,rotation) with{Translation=Vector3.Transform(position,inverse),Scale=scale??pose.Local[pin.Bone].Scale};return PreviewFK(pin.Bone,value);
    }
    void DrawEditorPinMarkers(List<Vertex> lines,EvaluatedPose pose)
    {
        if(!PinsVisible||ReferencePose)return;
        foreach(var pin in EditorPinHandles(pose))
        {
            float size=SceneScale*.065f;var color=SelectedPinId==pin.Id?Vector4.One:pin.Soft?new Vector4(1,.72f,.15f,1):new Vector4(.15f,.85f,1,1);var p=pin.Position;
            // Distinct diamonds and orientation ticks remain visible without bones.
            for(int plane=0;plane<3;plane++){var a=Vector3.Zero;var b=Vector3.Zero;a[plane]=size;b[(plane+1)%3]=size;Line(lines,p+a,p+b,color);Line(lines,p+b,p-a,color);Line(lines,p-a,p-b,color);Line(lines,p-b,p+a,color);}
            var orientation=Matrix4x4.CreateFromQuaternion(pin.Rotation);for(int axis=0;axis<3;axis++){var v=Vector3.Zero;v[axis]=size*1.4f;Line(lines,p,p+Vector3.TransformNormal(v,orientation),color);}
        }
    }
}
