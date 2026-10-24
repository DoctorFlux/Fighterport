using Avalonia.Input;
using System.Numerics;

namespace FighterPort.Rendering;

public enum ViewerCameraAction { Pan, Rotate, Zoom, Roll }
public enum ViewerCameraMode { Flycam, Turntable }

// Original implementation of the pinned model-viewer input behavior. This
// controller only changes the camera; no document/evaluation/Undo dependency.
public sealed class ViewerCameraInput(OrbitCamera camera)
{
    public ViewerCameraMode Mode {get;set;}=ViewerCameraMode.Flycam;
    public ViewerCameraAction RightMouse {get;set;}=ViewerCameraAction.Pan;
    public ViewerCameraAction MiddleMouse {get;set;}=ViewerCameraAction.Zoom;
    public ViewerCameraAction ControlRightMouse {get;set;}=ViewerCameraAction.Rotate;
    public ViewerCameraAction ControlAltRightMouse {get;set;}=ViewerCameraAction.Roll;
    public float RotationScale {get;set;}=.4f;
    public float TranslationScale {get;set;}=.05f;
    public float ZoomScale {get;set;}=2.5f;
    public float Multiplier {get;private set;}=1;
    public float ViewDistance {get;private set;}
    public bool InvertPanX {get;set;}
    public bool InvertPanY {get;set;}
    public bool InvertRotateX {get;set;}
    public bool InvertRotateY {get;set;}
    public bool WrapCursor {get;set;}=true;
    public ViewerCameraAction Action(bool middle,KeyModifiers modifiers)=>middle?MiddleMouse:
        modifiers.HasFlag(KeyModifiers.Control)?modifiers.HasFlag(KeyModifiers.Alt)?ControlAltRightMouse:ControlRightMouse:RightMouse;
    public void Drag(float right,float up,bool middle,KeyModifiers modifiers)
    {
        var action=Action(middle,modifiers);
        if(camera.Orthographic&&action is not (ViewerCameraAction.Roll or ViewerCameraAction.Rotate)){right*=20;up*=20;}
        if(modifiers.HasFlag(KeyModifiers.Shift)){right*=16;up*=16;}
        switch(action)
        {
            case ViewerCameraAction.Pan:Translate(new((InvertPanX?right:-right)*TranslationScale,(InvertPanY?up:-up)*TranslationScale,0));break;
            case ViewerCameraAction.Zoom:Translate(new(0,0,-up*.01f));break;
            case ViewerCameraAction.Rotate:Pivot((InvertRotateY?-up:up)*RotationScale,(InvertRotateX?right:-right)*RotationScale);break;
            case ViewerCameraAction.Roll:camera.RotateDegrees(0,0,-up*RotationScale*Multiplier);break;
        }
    }
    public void Wheel(float detents,KeyModifiers modifiers)
    {
        if(detents==0)return;
        float z=detents*(modifiers==KeyModifiers.Shift?32:1);Zoom(-z*ZoomScale);
        if(modifiers==KeyModifiers.Alt)Multiplier=Math.Clamp(z<0?Multiplier/.9f:Multiplier*.9f,.000001f,1000000);
    }
    public bool Key(Key key,KeyModifiers modifiers)
    {
        if(modifiers.HasFlag(KeyModifiers.Alt)||modifiers.HasFlag(KeyModifiers.Meta))return false;
        bool shift=modifiers.HasFlag(KeyModifiers.Shift),control=modifiers.HasFlag(KeyModifiers.Control);
        float rotation=RotationScale*(shift?32:4),translation=TranslationScale*(shift?128:8);
        switch(key)
        {
            case Avalonia.Input.Key.Up:case Avalonia.Input.Key.NumPad8:if(control)Pivot(-rotation,0);else Translate(new(0,translation,0));break;
            case Avalonia.Input.Key.Down:case Avalonia.Input.Key.NumPad2:if(control)Pivot(rotation,0);else Translate(new(0,-translation,0));break;
            case Avalonia.Input.Key.Right:case Avalonia.Input.Key.NumPad6:if(control)Pivot(0,rotation);else Translate(new(translation,0,0));break;
            case Avalonia.Input.Key.Left:case Avalonia.Input.Key.NumPad4:if(control)Pivot(0,-rotation);else Translate(new(-translation,0,0));break;
            case Avalonia.Input.Key.Add:case Avalonia.Input.Key.OemPlus:Zoom(-ZoomScale*(shift?32:2));break;
            case Avalonia.Input.Key.Subtract:case Avalonia.Input.Key.OemMinus:Zoom(ZoomScale*(shift?32:2));break;
            default:return false;
        }
        return true;
    }
    void Translate(Vector3 delta)=>camera.TranslateLocal(delta*Multiplier);
    void Pivot(float x,float y)=>camera.RotateDegrees(x*Multiplier,y*Multiplier,0,Mode==ViewerCameraMode.Flycam?0:ViewDistance);
    void Zoom(float amount){camera.Zoom(amount);ViewDistance+=amount;}
    public void ResetDistance()=>ViewDistance=0;
}
