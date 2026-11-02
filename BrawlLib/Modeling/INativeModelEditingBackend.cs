using BrawlLib.Internal;
using BrawlLib.OpenGL;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
namespace BrawlLib.Modeling
{
    public interface INativeModelEditingBackend : IDisposable
    {
        List<DrawCallBase> DrawCalls {get;}
        Box Bounds {get;}
        void Sample(MDL0Node model,CHR0Node animation,float displayFrame);
        // A format must qualify its native hierarchy/skin semantics before the
        // shared rig solver or geometry-dependent optimization may consume it.
        void ValidateSharedRigEvaluation();
        FighterPort.NativeEvaluation.NativeRigData CaptureEvaluation();
        INativeModelEditingBackend CreateSnapshotBackend(MDL0Node model);
        bool CanEditAnimationBone(string name);
        void PrepareAnimationChannel(CHR0EntryNode entry,int channel);
        void UpdatePose(MDL0Node model);
    }
}
