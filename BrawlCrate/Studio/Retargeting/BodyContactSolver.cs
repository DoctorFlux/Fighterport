using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BrawlCrate.Studio.Retargeting
{
    public sealed partial class ContactMotion
    {
        internal const string EndOrientationPolicy="coupled-end-world-orientation-1";
        internal const string EstablishedLockPolicy="coupled-established-locks-1";
        private sealed class ContactRow
        {
            internal SkinPoint Point;
            internal Vector3 Axis;
            internal float Error,Weight,DerivativeWeight;
        }

        // Solve active skin contacts together. Only hip translation and joint
        // rotations vary, so limb lengths and authored root channels are retained.
        private void FitBodyContacts(RigPose pose,Dictionary<string,ContactGoal> controls,Dictionary<string,Tuple<float,float>> nearFeet=null,bool allowEndOnly=false)
        {
            if(!allowEndOnly && !controls.Any(c=>!IsEndContact(c.Key) && c.Value.Support>0.001f))return;
            var active=controls.Select(c=>new KeyValuePair<string,ContactGoal>(c.Key,new ContactGoal{
                Support=nearFeet!=null && nearFeet.ContainsKey(c.Key)?Math.Max(c.Value.Support,nearFeet[c.Key].Item1):c.Value.Support,
                Lock=c.Value.Lock,Marker=c.Value.Marker,Anchor=c.Value.Anchor})).Where(c=>c.Value.Support>0.001f).ToArray();
            int pelvis=pose.Rig.Roles["pelvis"];
            // Analytical contact preparation has already chosen the palm/sole
            // orientation (including authored rolls and partial flattening).
            // Keep that goal throughout the coupled solve, rather than letting
            // whichever skin vertex happens to be lowest rotate the end joint.
            var endOrientations=active.Where(c=>IsEndContact(c.Key)).ToDictionary(c=>pose.Rig.Roles[c.Key],c=>pose.Orientation[pose.Rig.Roles[c.Key]]);
            // A supported head or arm can require torso articulation even when
            // the donor's trunk itself is slightly above the ground.
            int[] allJoints=active.SelectMany(c=>JointControls(pose.Rig,c.Key))
                .Concat(new[]{"pelvis","spine","chest","neck"}.Select(r=>pose.Rig.Roles[r]))
                .Where(j=>plan.Mapping.Any(m=>m.TargetIndex==j)&&!endOrientations.ContainsKey(j)).Distinct().OrderBy(i=>i).ToArray();
            var digits=new HashSet<int>(pose.Rig.Roles.Where(r=>r.Key.Contains(".finger") || r.Key.EndsWith(".toe",StringComparison.Ordinal)).Select(r=>r.Value));
            int[] joints=allJoints.Where(j=>!digits.Contains(j)).ToArray();
            int count=3+joints.Length*3;
            var controlForBone=Enumerable.Repeat(-1,pose.Rig.Joints.Length).ToArray();
            for(int i=0;i<joints.Length;i++)controlForBone[joints[i]]=i;
            float length=Length(pose.Rig.Rest,"left",false);
            var reference=pose.World.Select(m=>m.Translation).ToArray();
            var desiredHeights=active.ToDictionary(c=>c.Key,c=>pose.Rig.Surfaces[c.Key].Clearance(pose)*HeightRemainder(c.Value.Support));
            if(nearFeet!=null)foreach(var foot in nearFeet.Where(f=>f.Value.Item1>0.001f))
                desiredHeights[foot.Key]=pose.Rig.Surfaces[foot.Key].Clearance(pose)*(1-foot.Value.Item1)+foot.Value.Item2*foot.Value.Item1;
            const float poseWeight=0.005f;
            float penetration=active.Where(c=>!IsEndContact(c.Key)).Select(c=>pose.Rig.Surfaces[c.Key].Clearance(pose)).DefaultIfEmpty(0).Min();
            if(penetration<0)
            {
                pose.SetWorldPoint(pelvis,pose.Point(pelvis)-pose.Rig.GroundNormal*Math.Max(penetration,-length*0.2f));
                foreach(var contact in active.Where(c=>IsEndContact(c.Key)))
                {
                    bool hand=contact.Key.EndsWith(".hand",StringComparison.Ordinal);string side=contact.Key.Split('.')[0];
                    float clearance=pose.Rig.Surfaces[contact.Key].Clearance(pose);
                    plan.SolveLimb(pose,new RetargetPose{Pose=pose},side,hand,pose.Point(contact.Key)-pose.Rig.GroundNormal*clearance*contact.Value.Support);
                }
            }
            int stalled=0;
            for(int iteration=0;iteration<180;iteration++)
            {
                BodySolveIterations++;
                var rows=new List<ContactRow>();
                bool contactsSatisfied=true;
                bool supportedContactsSatisfied=true;
                bool bodyFeasible=true;
                bool unresolvedEnds=false;
                float precision=Math.Max(0.00001f,length*0.0001f);
                foreach(var contact in active)
                {
                    ContactSurface surface=pose.Rig.Surfaces[contact.Key];ContactGoal goal=contact.Value;
                    SkinPoint lowest=surface.LowestPoint(pose,out float minimum);
                    float clearance=minimum-pose.Rig.GroundHeight;
                    float error=clearance-desiredHeights[contact.Key],weight=goal.Support*(IsEndContact(contact.Key)?20:10);
                    contactsSatisfied&=Math.Abs(error)*goal.Support<=precision;
                    if(goal.Support>0.99f && !IsEndContact(contact.Key))supportedContactsSatisfied&=Math.Abs(error)<=length*0.005f;
                    if(goal.Support>0.99f && IsEndContact(contact.Key) && Math.Abs(error)>length*0.005f)unresolvedEnds=true;
                    if(!IsEndContact(contact.Key))bodyFeasible&=Math.Abs(error)*goal.Support<=length*(0.001f+0.015f*(1-goal.Support));
                    rows.Add(new ContactRow{Point=lowest,Axis=pose.Rig.GroundNormal,Error=error,Weight=weight*PenetrationWeight(clearance,length),
                        DerivativeWeight=weight*(PenetrationWeight(clearance,length)+error*PenetrationDerivative(clearance,length))});
                    if(goal.Marker!=null && goal.Lock>0.001f)
                    {
                        Vector3 delta=goal.Marker.Evaluate(pose)-goal.Anchor;
                        Vector3 x=Vector3.Normalize(Vector3.Cross(pose.Rig.GroundNormal,Math.Abs(pose.Rig.GroundNormal.Z)>0.9f?Vector3.UnitX:Vector3.UnitZ));
                        Vector3 z=Vector3.Cross(x,pose.Rig.GroundNormal);
                        Vector3 planar=delta-pose.Rig.GroundNormal*Vector3.Dot(delta,pose.Rig.GroundNormal);
                        contactsSatisfied&=planar.Length()*goal.Lock<=precision*2;
                        if(goal.Lock>0.99f && !IsEndContact(contact.Key))supportedContactsSatisfied&=planar.Length()<=length*0.0005f;
                        if(goal.Lock>0.99f && IsEndContact(contact.Key) && planar.Length()>length*0.0005f)unresolvedEnds=true;
                        if(!IsEndContact(contact.Key))bodyFeasible&=planar.Length()*goal.Lock<=length*(goal.Lock>0.99f?0.0005f:0.01f);
                        float lockWeight=ContactLockWeight(contact.Key,goal);
                        rows.Add(new ContactRow{Point=goal.Marker,Axis=x,Error=Vector3.Dot(delta,x),Weight=lockWeight,DerivativeWeight=lockWeight});
                        rows.Add(new ContactRow{Point=goal.Marker,Axis=z,Error=Vector3.Dot(delta,z),Weight=lockWeight,DerivativeWeight=lockWeight});
                    }
                }
                if(iteration>=4 && bodyFeasible && !unresolvedEnds)break;
                // Solve the main chains first. Digits keep their authored FK
                // unless measured end contact requires extra shaping freedom.
                if(iteration==32 && unresolvedEnds && joints.Length!=allJoints.Length)
                {
                    joints=allJoints;count=3+joints.Length*3;
                    controlForBone=Enumerable.Repeat(-1,pose.Rig.Joints.Length).ToArray();
                    for(int i=0;i<joints.Length;i++)controlForBone[joints[i]]=i;
                    continue;
                }
                // Stop when the measured contacts have converged. Continuing
                // to spend the full iteration budget on the small pose penalty
                // needlessly moves an already-supported body and slows baking.
                if(contactsSatisfied)break;
                Vector3[] positions=pose.World.Select(m=>m.Translation).ToArray();
                var normal=new double[count,count];var rhs=new double[count];
                foreach(var row in rows)
                {
                    var jacobian=new double[count];
                    float influence=0;
                    foreach(var skin in row.Point.Weights)if(Descendant(pose.Rig,skin.Joint,pelvis))influence+=skin.Weight;
                    jacobian[0]=row.Axis.X*influence*row.DerivativeWeight;
                    jacobian[1]=row.Axis.Y*influence*row.DerivativeWeight;
                    jacobian[2]=row.Axis.Z*influence*row.DerivativeWeight;
                    var skinPositions=row.Point.Weights.Select(w=>Vector3.Transform(w.Local,pose.World[w.Joint])).ToArray();
                    for(int joint=0;joint<joints.Length;joint++)
                    {
                        Vector3 derivative=Vector3.Zero;
                        for(int w=0;w<skinPositions.Length;w++)
                            if(Descendant(pose.Rig,row.Point.Weights[w].Joint,joints[joint]))
                                derivative+=Vector3.Cross(ContactLever(pose.Rig,positions,row.Point.Weights[w].Joint,skinPositions[w],joints[joint],endOrientations),row.Axis)*row.Point.Weights[w].Weight;
                        jacobian[3+joint*3]=derivative.X*row.DerivativeWeight;
                        jacobian[4+joint*3]=derivative.Y*row.DerivativeWeight;
                        jacobian[5+joint*3]=derivative.Z*row.DerivativeWeight;
                    }
                    AddContactEquation(normal,rhs,jacobian,row.Error*row.Weight);
                }
                // A small positional penalty retains the authored pose where
                // contacts do not require a change, rather than favoring one limb.
                double priorWeight=poseWeight*poseWeight;
                var columns=new Vector3[count];var indices=new int[count];
                for(int bone=0;bone<pose.Rig.Joints.Length;bone++)
                {
                    Vector3 error=positions[bone]-reference[bone];
                    int used=0;
                    if(Descendant(pose.Rig,bone,pelvis))
                    {
                        columns[0]=Vector3.UnitX;columns[1]=Vector3.UnitY;columns[2]=Vector3.UnitZ;
                        indices[used++]=0;indices[used++]=1;indices[used++]=2;
                    }
                    for(int joint=0;joint<joints.Length;joint++)
                    {
                        if(!Descendant(pose.Rig,bone,joints[joint]))continue;
                        Vector3 offset=ContactLever(pose.Rig,positions,bone,positions[bone],joints[joint],endOrientations);
                        int index=3+joint*3;
                        columns[index]=new Vector3(0,-offset.Z,offset.Y);
                        columns[index+1]=new Vector3(offset.Z,0,-offset.X);
                        columns[index+2]=new Vector3(-offset.Y,offset.X,0);
                        indices[used++]=index;indices[used++]=index+1;indices[used++]=index+2;
                    }
                    for(int a=0;a<used;a++)
                    {
                        int i=indices[a];rhs[i]-=Vector3.Dot(columns[i],error)*priorWeight;
                        for(int b=0;b<=a;b++)
                        {
                            int j=indices[b];double value=Vector3.Dot(columns[i],columns[j])*priorWeight;
                            normal[i,j]+=value;if(i!=j)normal[j,i]+=value;
                        }
                    }
                }
                for(int i=0;i<count;i++)normal[i,i]+=0.0001;
                var descent=Enumerable.Range(0,count).Select(i=>rhs[i]/normal[i,i]).ToArray();
                double[] step=SolveContactSystem(normal,rhs);
                var previous=(JointTransform[])pose.Local.Clone();double loss=BodyContactLoss(pose,active,reference,poseWeight,desiredHeights,length),acceptedLoss=loss;bool improved=false,converged=false;
                for(int direction=0;direction<2 && !improved;direction++)
                {
                if(direction==1)step=descent;
                Vector3 translation=new Vector3((float)step[0],(float)step[1],(float)step[2]);
                var rotations=new Vector3[joints.Length];float maximumAngle=0;
                for(int i=0;i<joints.Length;i++)
                {
                    rotations[i]=new Vector3((float)step[3+i*3],(float)step[4+i*3],(float)step[5+i*3]);
                    maximumAngle=Math.Max(maximumAngle,rotations[i].Length());
                }
                float scale=Math.Min(1,Math.Min(0.12f/Math.Max(0.000001f,maximumAngle),length*0.08f/Math.Max(0.000001f,translation.Length())));
                // At a change of the lowest skin vertex the Gauss-Newton step
                // can miss descent. A diagonal gradient step crosses that corner.
                for(int attempt=0;attempt<12;attempt++)
                {
                    ApplyBodyStep(pose,previous,pelvis,controlForBone,translation,rotations,scale,endOrientations);
                    double candidateLoss=BodyContactLoss(pose,active,reference,poseWeight,desiredHeights,length);
                    if(candidateLoss<loss){acceptedLoss=candidateLoss;improved=true;break;}
                    scale*=0.5f;
                }
                if(improved && Math.Max(translation.Length(),maximumAngle)*scale<0.00001f)converged=true;
                }
                if(!improved){Array.Copy(previous,pose.Local,previous.Length);pose.Update();break;}
                // Partially supported skin patches can have competing soft
                // goals. Stop after sustained negligible objective improvement
                // instead of spending the entire budget on that compromise.
                stalled=loss-acceptedLoss<=Math.Max(1e-10,loss*0.0005)?stalled+1:0;
                if(stalled>=3 && supportedContactsSatisfied)break;
                if(converged)break;
            }
            // Finish the coupled body solve with exact two-bone end constraints.
            // This preserves palm/sole orientation and segment lengths while
            // removing the small end drift left by competing soft body goals.
            ProjectBodyEnds(pose,active,desiredHeights,endOrientations);
            var supportedBody=active.Where(c=>!IsEndContact(c.Key) && c.Value.Support>0.99f).ToArray();
            for(int pass=0;pass<4 && supportedBody.Length>0;pass++)
            {
                float maximum=0,shift=0,total=0;
                foreach(var contact in supportedBody)
                {
                    float error=desiredHeights[contact.Key]-pose.Rig.Surfaces[contact.Key].Clearance(pose);
                    float weight=contact.Value.Support*contact.Value.Support;
                    maximum=Math.Max(maximum,Math.Abs(error));shift+=error*weight;total+=weight;
                }
                if(maximum<=length*0.001f)break;
                shift=RigMath.Clamp(shift/total,-length*0.05f,length*0.05f);
                var original=(JointTransform[])pose.Local.Clone();
                double loss=BodyContactLoss(pose,active,reference,poseWeight,desiredHeights,length);bool improved=false;
                for(int attempt=0;attempt<6;attempt++)
                {
                    Array.Copy(original,pose.Local,original.Length);pose.Update();
                    pose.SetWorldPoint(pelvis,pose.Point(pelvis)+pose.Rig.GroundNormal*shift);
                    ProjectBodyEnds(pose,active,desiredHeights,endOrientations);
                    if(BodyContactLoss(pose,active,reference,poseWeight,desiredHeights,length)<loss){improved=true;break;}
                    shift*=0.5f;
                }
                if(!improved){Array.Copy(original,pose.Local,original.Length);pose.Update();break;}
            }
            // A skinned minimum is piecewise smooth: its active vertex can
            // change across a small rotation. A bounded coordinate search can
            // cross that corner when the linearized solve leaves body support
            // unsatisfied. Evaluate the complete skin and the same pose prior.
            float searchAngle=0.12f;
            for(int pass=0;pass<6 && active.Any(c=>!IsEndContact(c.Key) && c.Value.Support>0.99f &&
                Math.Abs(pose.Rig.Surfaces[c.Key].Clearance(pose)-desiredHeights[c.Key])>length*0.005f);pass++)
            {
                bool improved=false;
                foreach(int joint in allJoints.Where(j=>!digits.Contains(j)))foreach(Vector3 axis in new[]{Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ})
                {
                    var before=(JointTransform[])pose.Local.Clone();Quaternion orientation=pose.Orientation[joint];
                    double bestLoss=BodyContactLoss(pose,active,reference,poseWeight,desiredHeights,length);JointTransform[] best=null;
                    foreach(float sign in new[]{-1f,1f})
                    {
                        Array.Copy(before,pose.Local,before.Length);pose.Update();
                        pose.SetWorldRotation(joint,Quaternion.CreateFromAxisAngle(axis,searchAngle*sign)*orientation);
                        ProjectBodyEnds(pose,active,desiredHeights,endOrientations);
                        double loss=BodyContactLoss(pose,active,reference,poseWeight,desiredHeights,length);
                        if(loss<bestLoss){bestLoss=loss;best=(JointTransform[])pose.Local.Clone();}
                    }
                    Array.Copy(best??before,pose.Local,before.Length);pose.Update();
                    if(best!=null)improved=true;
                }
                if(!improved)searchAngle*=0.5f;
            }
        }
        private static Vector3 ContactLever(Rig rig,Vector3[] positions,int bone,Vector3 point,int joint,Dictionary<int,Quaternion> ends)
        {
            // Holding an end's world orientation cancels its inherited angular
            // change, but its pivot still moves with the ancestor. Skin below
            // that end therefore uses the end pivot's lever, with every weight.
            int first=-1;
            foreach(int end in ends.Keys)
                if(Descendant(rig,bone,end)&&Descendant(rig,end,joint)&&(first<0||Descendant(rig,first,end)))first=end;
            return (first<0?point:positions[first])-positions[joint];
        }
        private void ProjectBodyEnds(RigPose pose,KeyValuePair<string,ContactGoal>[] active,Dictionary<string,float> desiredHeights,Dictionary<int,Quaternion> orientations)
        {
            var original=(JointTransform[])pose.Local.Clone();
            float before=EstablishedContactError(pose,active,desiredHeights);
            foreach(var end in orientations.OrderBy(p=>p.Key))pose.SetWorldRotation(end.Key,end.Value);
            var report=new RetargetPose{Pose=pose};
            for(int pass=0;pass<3;pass++)foreach(var contact in active.Where(c=>IsEndContact(c.Key)))
            {
                ContactGoal goal=contact.Value;
                Vector3 delta=pose.Rig.GroundNormal*(desiredHeights[contact.Key]-pose.Rig.Surfaces[contact.Key].Clearance(pose))*
                    (1-(float)Math.Pow(1-goal.Support,1.0/3));
                if(goal.Marker!=null && goal.Lock>0)
                {
                    Vector3 planar=goal.Anchor-goal.Marker.Evaluate(pose);
                    planar-=pose.Rig.GroundNormal*Vector3.Dot(planar,pose.Rig.GroundNormal);
                    delta+=planar*(1-(float)Math.Pow(1-goal.Lock,1.0/3));
                }
                string side=contact.Key.Split('.')[0];
                plan.SolveLimb(pose,report,side,contact.Key.EndsWith(".hand",StringComparison.Ordinal),pose.Point(contact.Key)+delta);
            }
            // End projection must not trade an established body marker for a
            // slightly better foot. Keep the jointly solved pose when this
            // optional polishing step increases the worst hard-contact error.
            if(EstablishedContactError(pose,active,desiredHeights)>before)
            {Array.Copy(original,pose.Local,original.Length);pose.Update();}
        }
        private static float EstablishedContactError(RigPose pose,KeyValuePair<string,ContactGoal>[] active,Dictionary<string,float> desiredHeights)
        {
            float maximum=0;
            foreach(var contact in active)
            {
                if(contact.Value.Support>0.99f)maximum=Math.Max(maximum,Math.Abs(pose.Rig.Surfaces[contact.Key].Clearance(pose)-desiredHeights[contact.Key]));
                if(contact.Value.Lock>0.99f&&contact.Value.Marker!=null)maximum=Math.Max(maximum,PlanarLockError(pose,contact.Value));
            }
            return maximum;
        }
        private static void ApplyBodyStep(RigPose pose,JointTransform[] original,int pelvis,int[] controlForBone,Vector3 translation,Vector3[] rotations,float scale,Dictionary<int,Quaternion> orientations)
        {
            Array.Copy(original,pose.Local,original.Length);
            // Body controls descend from the pelvis. Restore its unchanged
            // ancestors, then apply all world-space increments in one FK pass.
            // Recalculating every descendant after each control is redundant.
            for(int i=0;i<pelvis;i++)pose.UpdateJoint(i);
            int parent=pose.Rig.Joints[pelvis].Parent;
            Vector3 delta=translation*scale;
            if(parent>=0)
            {
                if(!Matrix4x4.Invert(pose.World[parent],out Matrix4x4 inverse))throw new System.IO.InvalidDataException("A pose has a singular parent transform.");
                delta=Vector3.TransformNormal(delta,inverse);
            }
            pose.Local[pelvis].Translation+=delta;
            for(int bone=pelvis;bone<original.Length;bone++)
            {
                int control=controlForBone[bone];
                if(orientations.TryGetValue(bone,out Quaternion required))
                {
                    int ancestor=pose.Rig.Joints[bone].Parent;
                    pose.Local[bone].Rotation=Quaternion.Normalize(ancestor<0?required:Quaternion.Inverse(pose.Orientation[ancestor])*required);
                }
                else if(control>=0)
                {
                    float angle=rotations[control].Length();
                    if(angle>=0.000001f)
                    {
                        int ancestor=pose.Rig.Joints[bone].Parent;
                        Quaternion world=ancestor<0?pose.Local[bone].Rotation:Quaternion.Normalize(pose.Orientation[ancestor]*pose.Local[bone].Rotation);
                        world=Quaternion.CreateFromAxisAngle(rotations[control]/angle,angle*scale)*world;
                        pose.Local[bone].Rotation=Quaternion.Normalize(ancestor<0?world:Quaternion.Inverse(pose.Orientation[ancestor])*world);
                    }
                }
                pose.UpdateJoint(bone);
            }
        }
        private static float PenetrationWeight(float height,float length)=>1+9*Smooth(0,length*0.005f,Math.Max(0,-height));
        private static float PenetrationDerivative(float height,float length)
        {
            float width=length*0.005f,t=-height/width;
            return t>0 && t<1?-54*t*(1-t)/width:0;
        }
        private static double BodyContactLoss(RigPose pose,KeyValuePair<string,ContactGoal>[] active,Vector3[] reference,float poseWeight,Dictionary<string,float> desiredHeights,float length)
        {
            double loss=0;
            foreach(var contact in active)
            {
                float clearance=pose.Rig.Surfaces[contact.Key].Clearance(pose);
                float h=(clearance-desiredHeights[contact.Key])*contact.Value.Support*(IsEndContact(contact.Key)?20:10)*PenetrationWeight(clearance,length);loss+=h*h;
                if(contact.Value.Marker==null)continue;
                Vector3 delta=contact.Value.Marker.Evaluate(pose)-contact.Value.Anchor;
                delta-=pose.Rig.GroundNormal*Vector3.Dot(delta,pose.Rig.GroundNormal);
                float lockWeight=ContactLockWeight(contact.Key,contact.Value);
                loss+=delta.LengthSquared()*lockWeight*lockWeight;
            }
            for(int i=0;i<reference.Length;i++)loss+=Vector3.DistanceSquared(pose.Point(i),reference[i])*poseWeight*poseWeight;
            return loss;
        }
        // An established body marker is a stationary contact too, not merely
        // a weak pose preference. Share the end-contact priority and reserve
        // half the existing positional bound before native representation.
        private static float ContactLockWeight(string role,ContactGoal goal)
            =>goal.Lock*(IsEndContact(role)?40:1+39*Smooth(0.8f,0.99f,goal.Lock));
        private static bool Descendant(Rig rig,int joint,int ancestor)=>rig.Ancestors[joint][ancestor];
        private static void AddContactEquation(double[,] normal,double[] rhs,double[] jacobian,double residual)
        {
            int[] nonzero=Enumerable.Range(0,rhs.Length).Where(i=>Math.Abs(jacobian[i])>1e-12).ToArray();
            for(int a=0;a<nonzero.Length;a++)
            {
                int i=nonzero[a];rhs[i]-=jacobian[i]*residual;
                for(int b=0;b<=a;b++){int j=nonzero[b];double value=jacobian[i]*jacobian[j];normal[i,j]+=value;if(i!=j)normal[j,i]+=value;}
            }
        }
        internal static double[] SolveContactSystem(double[,] matrix,double[] rhs)
        {
            int n=rhs.Length;
            // Regularized normal equations are positive definite. Cholesky
            // retains the complete coupled pose prior without pivoting a dense
            // matrix on every iteration. Fall back if roundoff defeats it.
            var lower=new double[n*n];bool positive=true;
            for(int i=0;i<n && positive;i++)for(int j=0;j<=i;j++)
            {
                int row=i*n,other=j*n;
                double value=matrix[i,j];for(int k=0;k<j;k++)value-=lower[row+k]*lower[other+k];
                if(i==j){if(value<=0 || double.IsNaN(value) || double.IsInfinity(value)){positive=false;break;}lower[row+j]=Math.Sqrt(value);}
                else lower[row+j]=value/lower[other+j];
            }
            if(positive)
            {
                var solution=new double[n];
                for(int i=0;i<n;i++){int row=i*n;double value=rhs[i];for(int j=0;j<i;j++)value-=lower[row+j]*solution[j];solution[i]=value/lower[row+i];}
                for(int i=n-1;i>=0;i--){double value=solution[i];for(int j=i+1;j<n;j++)value-=lower[j*n+i]*solution[j];solution[i]=value/lower[i*n+i];}
                return solution;
            }
            for(int k=0;k<n;k++)
            {
                int pivot=k;for(int i=k+1;i<n;i++)if(Math.Abs(matrix[i,k])>Math.Abs(matrix[pivot,k]))pivot=i;
                if(pivot!=k){for(int j=k;j<n;j++){double v=matrix[k,j];matrix[k,j]=matrix[pivot,j];matrix[pivot,j]=v;}double b=rhs[k];rhs[k]=rhs[pivot];rhs[pivot]=b;}
                for(int i=k+1;i<n;i++){double factor=matrix[i,k]/matrix[k,k];for(int j=k+1;j<n;j++)matrix[i,j]-=factor*matrix[k,j];rhs[i]-=factor*rhs[k];}
            }
            var result=new double[n];for(int i=n-1;i>=0;i--){double value=rhs[i];for(int j=i+1;j<n;j++)value-=matrix[i,j]*result[j];result[i]=value/matrix[i,i];}
            return result;
        }
    }
}
