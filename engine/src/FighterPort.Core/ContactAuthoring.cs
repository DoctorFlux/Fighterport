using System.Numerics;

namespace FighterPort.Core;

public sealed record ContactBakeOptions(string Action,string Bone,int Start,int End,int Blend,int AnchorFrame,float[] Offset,float[] Angles,bool HoldRotation=true);
public sealed record ContactBakeReceipt(string Version,ContactBakeOptions Options,int FirstChanged,int LastChanged,string[] Bones,float PositionBound,float RotationBound,string[] Limits,int CurveEvaluations=0,int CurveSweeps=0);
public sealed record ContactBakeResult(AnimationClip Clip,ContactFrame[] Contacts,BoneTrack[] Changed,ContactBakeReceipt Receipt);
public static class ContactAuthoring
{
    public const string Version="reviewed-contact-bake-1";
    internal static float AngleError(Quaternion a,Quaternion b)
    {
        double dot=(double)a.X*b.X+(double)a.Y*b.Y+(double)a.Z*b.Z+(double)a.W*b.W;
        double aa=(double)a.X*a.X+(double)a.Y*a.Y+(double)a.Z*a.Z+(double)a.W*a.W,bb=(double)b.X*b.X+(double)b.Y*b.Y+(double)b.Z*b.Z+(double)b.W*b.W;
        return (float)(2*Math.Acos(Math.Clamp(Math.Abs(dot)/Math.Sqrt(aa*bb),0,1))*180/Math.PI);
    }
    public static ContactBakeResult Prepare(FighterProject project,AnimationClip clip,ControlRigSetup rig,ContactBakeOptions options,string[] protectedBones,AnimationClip? reference=null)
    {
        bool release=options.Action=="Release";if(!release&&options.Action!="Place")throw new InvalidDataException("Choose Place or Release.");
        int last=clip.SampleCount-1,begin=release?options.Start:options.Start-options.Blend,finish=release?options.Start+options.Blend:options.End+options.Blend;
        if(options.Blend<2||begin<0||finish>last||options.Start>options.End||options.Start<0||options.AnchorFrame<0||options.AnchorFrame>last||finish-begin>240)throw new InvalidDataException("Use at least two blend samples, a valid hold interval, and at most 240 edited samples inside the native duration.");
        if(options.Offset.Length!=3||options.Angles.Length!=3||options.Offset.Concat(options.Angles).Any(v=>!float.IsFinite(v)))throw new InvalidDataException("Contact offsets and rotations must be finite.");
        if(rig.SkeletonFingerprint!=AnimationControls.Fingerprint(project.Scene))throw new InvalidDataException("Contact controls belong to another rig revision.");
        var scene=project.Scene;int bone=Array.FindIndex(scene.Bones,b=>b.Name==options.Bone);var chain=rig.Chains.SingleOrDefault(c=>c.Effector==bone&&c.Reviewed)??throw new NotSupportedException("Choose an effector on an explicitly reviewed existing IK chain.");
        var joints=new[]{chain.Root,chain.Middle,chain.Effector};if(joints.Any(rig.PreservedHelperBones.Contains))throw new NotSupportedException("Contact baking cannot rewrite preserved native helper joints.");
        if(project.NativeLayers.Any(l=>l.Kind!="SCN0"))throw new NotSupportedException("Composed companion channels need a synchronized contact adapter.");
        var contacts=project.Contacts.Where(c=>c.Clip==clip.Id).ToArray();
        if(contacts.Any(c=>c.SkeletonFingerprint!=rig.SkeletonFingerprint))throw new InvalidDataException("A contact interval belongs to another rig revision.");
        if(release&&(reference is null||reference.SampleCount!=clip.SampleCount||reference.Rate!=clip.Rate||!contacts.Any(c=>c.Pins.Any(p=>p.Bone==bone))))throw new InvalidDataException("Release needs the frozen original motion and an authored contact on this effector.");
        if(project.Moves.SelectMany(m=>m.Events).Any(p=>p.Start<=(release?last:finish)&&p.End>=begin))throw new InvalidDataException("Contact authoring intersects a protected game event.");
        var anchor=AnimationService.Evaluate(scene,clip,options.AnchorFrame);var radians=new Vector3(options.Angles[0],options.Angles[1],options.Angles[2])*(MathF.PI/180);
        var rotation=Quaternion.Normalize(Quaternion.Concatenate(PoseGeometry.Rotation(anchor.World[bone]),Quaternion.CreateFromYawPitchRoll(radians.Y,radians.X,radians.Z)));
        var target=new ContactPin(Guid.NewGuid(),chain.Role,bone,Vector3.Zero,anchor.World[bone].Translation+new Vector3(options.Offset[0],options.Offset[1],options.Offset[2]),rotation,options.HoldRotation,"Reviewed editor contact"){Pole=AnimationControls.DefaultPole(scene,anchor,chain)};
        float unit=PoseGeometry.Scale(scene,rig),positionBound=unit*.001f;const float rotationBound=.25f;
        var samples=new Dictionary<int,Transform[]>();
        float Smooth(float t){t=Math.Clamp(t,0,1);return t*t*(3-2*t);}
        Transform[] SolveAt(double frame)
        {
            var original=AnimationService.Evaluate(scene,clip,frame);var locks=new Dictionary<int,Transform>();
            foreach(string name in protectedBones){int index=Array.FindIndex(scene.Bones,b=>b.Name==name);if(index<0)throw new InvalidDataException("Unknown native support dependency.");if(!joints.Contains(index))locks[index]=original.Local[index];}
            var existing=contacts.Where(c=>c.Contains(frame)).SelectMany(c=>c.Pins).Where(p=>p.Bone!=bone).Select(p=>p.EditorMotion?.Resolve(scene,p,frame,clip.Rate)??p).ToArray();
            if(release)
            {
                var other=AnimationService.Evaluate(scene,reference,frame);float weight=Smooth((float)((frame-begin)/options.Blend));
                foreach(int joint in joints){var a=original.Local[joint];var b=other.Local[joint];var delta=b.Rotation-a.Rotation;delta-=new Vector3(MathF.Round(delta.X/360),MathF.Round(delta.Y/360),MathF.Round(delta.Z/360))*360;locks[joint]=a with{Rotation=a.Rotation+delta*weight};}
            }
            var seed=original;
            if(!release)
            {
                float weight=frame<options.Start?Smooth((float)((frame-begin)/options.Blend)):frame>options.End?1-Smooth((float)((frame-options.End)/options.Blend)):1;
                var goal=target with{Position=Vector3.Lerp(original.World[bone].Translation,target.Position,weight),Rotation=Quaternion.Slerp(PoseGeometry.Rotation(original.World[bone]),rotation,weight),Pole=Vector3.Lerp(AnimationControls.DefaultPole(scene,original,chain),target.Pole!.Value,weight)};
                // Seed each sample from the same reviewed geometric branch. A free
                // DLS solve can meet individual endpoints with different elbow rolls,
                // leaving a path that native Hermite channels cannot interpolate.
                var ik=AnimationControls.Solve(scene,original,rig,chain,goal.Position,goal.Pole.Value);
                if(ik.Value is null)throw new InvalidOperationException("Contact frame "+(frame+1)+": "+string.Join("; ",ik.Diagnostics.Select(d=>d.Message)));
                var seedValues=original.Local.Select((t,i)=>(t,i)).ToDictionary(v=>v.i,v=>v.t);foreach(var pair in ik.Value.Transforms)seedValues[pair.Key]=pair.Value;
                seed=AnimationService.Evaluate(scene,null,0,seedValues);
                // Fix the otherwise free upper-segment roll with the current
                // elbow plane. Minimal swing alone puts all plane change into
                // the middle joint and can cross a native Euler singularity.
                var oldA=original.World[chain.Root].Translation;var oldB=original.World[chain.Middle].Translation;var oldC=original.World[bone].Translation;
                var newA=seed.World[chain.Root].Translation;var newB=seed.World[chain.Middle].Translation;
                var normal=Vector3.Cross(oldB-oldA,oldC-oldB);var wanted=Vector3.Cross(newB-newA,goal.Position-newB);
                if(normal.LengthSquared()>1e-12f&&wanted.LengthSquared()>1e-12f)
                {
                    var oldRotation=PoseGeometry.RotationMatrix(original.World[chain.Root]);var newRotation=PoseGeometry.RotationMatrix(seed.World[chain.Root]);
                    var carried=Vector3.TransformNormal(normal,Matrix4x4.Transpose(oldRotation)*newRotation);
                    var roll=AnimationControls.DirectionRotation(carried,wanted);var upperRotation=Quaternion.Concatenate(PoseGeometry.Rotation(seed.World[chain.Root]),roll);
                    seedValues[chain.Root]=PoseGeometry.WorldRotation(scene,seed,chain.Root,upperRotation);seedValues[chain.Middle]=original.Local[chain.Middle];seed=AnimationService.Evaluate(scene,null,0,seedValues);
                    var turn=AnimationControls.DirectionRotation(seed.World[bone].Translation-seed.World[chain.Middle].Translation,goal.Position-seed.World[chain.Middle].Translation);
                    seedValues[chain.Middle]=PoseGeometry.WorldRotation(scene,seed,chain.Middle,Quaternion.Concatenate(PoseGeometry.Rotation(seed.World[chain.Middle]),turn));seed=AnimationService.Evaluate(scene,null,0,seedValues);
                }
                if(goal.HoldRotation){seedValues[bone]=PoseGeometry.WorldRotation(scene,seed,bone,goal.Rotation);seed=AnimationService.Evaluate(scene,null,0,seedValues);}
                existing=[..existing,goal];
                // The analytic single-chain result already has a complete proof.
                // Normalize the quaternion dot in double before acos: a float
                // dot can report .068 degrees for identical orientations, above
                // the existing solver's .001 degree endpoint bound.
                if(existing.Length==1&&Vector3.Distance(seed.World[bone].Translation,goal.Position)<=unit*.0001f&&(!goal.HoldRotation||AngleError(PoseGeometry.Rotation(seed.World[bone]),goal.Rotation)<=.001f))return frame==begin||frame==finish?original.Local.ToArray():seed.Local;
            }
            var solved=ContactSolver.Solve(scene,seed,rig,locks,existing);
            if(solved.Value is null)throw new InvalidOperationException("Contact frame "+(frame+1)+": "+string.Join("; ",solved.Diagnostics.Select(d=>d.Message)));
            var local=seed.Local.ToArray();foreach(var value in solved.Value.Transforms)local[value.Key]=value.Value;
            if(frame==begin||!release&&frame==finish)local=original.Local.ToArray();
            if(Enumerable.Range(0,local.Length).Any(i=>!joints.Contains(i)&&local[i]!=original.Local[i]))throw new NotSupportedException("Other authored constraints require a coupled edit outside the selected existing chain; no animation was changed.");
            return local;
        }
        var between=new Dictionary<double,Transform[]>();
        for(int frame=begin;frame<=finish;frame++)
        {
            samples[frame]=SolveAt(frame);
            if(frame<finish)for(int quarter=1;quarter<4;quarter++)between[frame+quarter*.25]=SolveAt(frame+quarter*.25);
        }
        float[] FitSlopes(int joint,int channel,float startSlope,float endSlope)
        {
            int count=finish-begin+1;var diagonal=new double[count];var upper=new double[count];var rhs=new double[count];
            for(int frame=begin;frame<finish;frame++)for(int quarter=1;quarter<4;quarter++)
            {
                double u=quarter*.25,a=u*(1-u)*(1-u),b=-u*u*(1-u);int index=frame-begin;
                double fixedValue=(1-u)*(1-u)*(1+2*u)*samples[frame][joint].Channels()[channel]+u*u*(3-2*u)*samples[frame+1][joint].Channels()[channel];
                double residual=between[frame+u][joint].Channels()[channel]-fixedValue;
                diagonal[index]+=a*a;diagonal[index+1]+=b*b;upper[index]+=a*b;rhs[index]+=a*residual;rhs[index+1]+=b*residual;
            }
            // The two exterior derivatives remain authentic. Fit shared interior
            // derivatives to solved quarter frames, using native integral knots.
            rhs[1]-=upper[0]*startSlope;upper[0]=0;rhs[count-2]-=upper[count-2]*endSlope;upper[count-2]=0;
            diagonal[0]=1;rhs[0]=startSlope;diagonal[count-1]=1;rhs[count-1]=endSlope;
            for(int i=1;i<count;i++){double factor=upper[i-1]/diagonal[i-1];diagonal[i]-=factor*upper[i-1];rhs[i]-=factor*rhs[i-1];}
            var result=new float[count];for(int i=count-1;i>=0;i--)result[i]=(float)((rhs[i]-(i+1<count?upper[i]*result[i+1]:0))/diagonal[i]);
            if(result.Any(v=>!float.IsFinite(v)))throw new InvalidOperationException("Native contact derivatives are nonfinite.");return result;
        }
        var changed=new List<BoneTrack>();
        foreach(int joint in joints)
        {
            var name=scene.Bones[joint].Name;var original=clip.Tracks.SingleOrDefault(t=>t.Bone==name)??new(name,new Curve?[9]);if(original.Rotation is not null)throw new NotSupportedException("Native contact baking needs Euler channel curves.");var curves=original.Channels.ToArray();
            for(int channel=3;channel<6;channel++)
            {
                var basis=curves[channel];var fittedSlopes=FitSlopes(joint,channel,BoundaryFitting.Derivative(basis,begin),BoundaryFitting.Derivative(release?reference!.Tracks.SingleOrDefault(t=>t.Bone==name)?.Channels[channel]:basis,finish));
                var keys=(basis?.Keys??[]).Where(k=>k.Frame<begin||k.Frame>finish).ToList();
                for(int frame=begin;frame<=finish;frame++)
                {
                    float value=samples[frame][joint].Channels()[channel];float slope=fittedSlopes[frame-begin];
                    keys.Add(new(frame,value,slope,slope));
                }
                if(release){keys.RemoveAll(k=>k.Frame>finish);var referenceCurve=reference!.Tracks.SingleOrDefault(t=>t.Bone==name)?.Channels[channel];keys.AddRange((referenceCurve?.Keys??[]).Where(k=>k.Frame>finish));if(referenceCurve is null&&finish<last)keys.Add(new(last,scene.Bones[joint].Rest.Channels()[channel]));}
                // Exterior curve derivatives belong to their authentic originals.
                var atBegin=basis?.Keys.Where(k=>k.Frame==begin).ToArray()??[];if(atBegin.Length>0)keys[keys.FindIndex(k=>k.Frame==begin)]=keys.First(k=>k.Frame==begin) with{InTangent=atBegin[0].InTangent};
                curves[channel]=new(keys.OrderBy(k=>k.Frame).ToArray(),Interpolation.Hermite,basis?.LoopFrameLimit);
            }
            changed.Add(original with{Channels=curves});
        }
        var output=clip with{Id=Guid.NewGuid(),Revision=Guid.NewGuid(),Name=clip.Name+(release?" (released)":" (contact)"),Tracks=clip.Tracks.Where(t=>changed.All(c=>c.Bone!=t.Bone)).Concat(changed).ToArray(),NativeWrite=NativeWriteMode.NewResource};
        var edited=new List<ContactFrame>();
        foreach(var contact in project.Contacts)
        {
            if(contact.Clip!=clip.Id){edited.Add(contact);continue;}double end=contact.EndFrame??contact.Frame,editEnd=release?last:finish;var selected=contact.Pins.Where(p=>p.Bone==bone).ToArray();var others=contact.Pins.Where(p=>p.Bone!=bone).ToArray();
            if(selected.Length==0||end<begin||contact.Frame>editEnd){edited.Add(contact with{Clip=output.Id});continue;}
            if(others.Length>0)edited.Add(contact with{Clip=output.Id,Pins=others});
            if(contact.Frame<begin)edited.Add(contact with{Id=Guid.NewGuid(),Clip=output.Id,Pins=selected,EndFrame=begin,EndExclusive=true});
            if(end>editEnd)edited.Add(contact with{Id=Guid.NewGuid(),Clip=output.Id,Pins=selected,Frame=editEnd+1});
        }
        if(!release)edited.Add(new(Guid.NewGuid(),output.Id,options.Start,rig.SkeletonFingerprint,[target]){EndFrame=options.End});
        var outputContacts=edited.Where(c=>c.Clip==output.Id).ToArray();var projection=ContactCurveProjection.Refine(scene,output,changed.ToArray(),outputContacts,rig,begin,finish,positionBound,rotationBound);
        Validate(scene,output,rig,outputContacts,positionBound,rotationBound,begin,release?last:finish);
        var receipt=new ContactBakeReceipt(Version,options,begin,release?last:finish,changed.Select(t=>t.Bone).ToArray(),positionBound,rotationBound,["Reviewed existing-chain bone contacts only; no automatic ground/surface inference or game-state acceptance.","Place blends from/to the current motion; Release restores selected chain rotations into its frozen original motion after the blend.","Native support paths outside the selected chain remain unchanged. Existing constraints outside supported anatomy can reject."],projection.Evaluations,projection.Sweeps);
        return new(output,edited.ToArray(),changed.ToArray(),receipt);
    }
    public static void Validate(Scene scene,AnimationClip clip,ControlRigSetup rig,ContactFrame[] contacts,float positionBound,float rotationBound,int begin,int end)
    {
        for(double frame=begin;frame<=end;frame+=.25)
        {
            var pose=AnimationService.Evaluate(scene,clip,frame);
            foreach(var pin in contacts.Where(c=>c.Contains(frame)).SelectMany(c=>c.Pins))
            {
                var goal=pin.EditorMotion?.Resolve(scene,pin,frame,clip.Rate)??pin;
                if(goal.SurfacePoints.Length>0||goal.SurfaceRegion.Length>0||goal.Reference is not null||goal.Clearance is not null||goal.EditorBehavior==EditorPinBehavior.SoftReach)throw new NotSupportedException("Native contact bake verification requires fixed reviewed bone-point contacts; preserve other authored contexts separately.");
                float positionError=Vector3.Distance(Vector3.Transform(goal.LocalPoint,pose.World[goal.Bone]),goal.Position),angleError=goal.HoldRotation?AngleError(PoseGeometry.Rotation(pose.World[goal.Bone]),goal.Rotation):0;
                if(positionError>positionBound||angleError>rotationBound)throw new InvalidOperationException("Native-quarter-frame contact bound failed at frame "+(frame+1)+": position "+positionError+" / "+positionBound+", orientation "+angleError+" / "+rotationBound+" degrees. No result was applied.");
            }
        }
    }
}
