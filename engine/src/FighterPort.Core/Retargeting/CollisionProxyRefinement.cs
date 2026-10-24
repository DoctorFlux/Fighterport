namespace FighterPort.Core;

// Rest-space extrema are only seeds: skin blending can put other vertices
// below them. Add actual deformed violators before accepting a sparse solve.
// Deterministic ordering and a bounded active set keep Jacobians small. Every
// accepted pose is checked against the complete skin before sparse acceptance.
internal static class CollisionProxyRefinement
{
    public static ContactClearance? Refine(TargetMotionPlan plan,double frame,EvaluatedPose pose)
    {
        if(!plan.HasGround(frame)||plan.Clearance is not {} plane)return plan.Clearance;
        var legs=plan.Rig.Binding.Chains.Where(c=>c.Role.StartsWith("Foot.",StringComparison.Ordinal)).Select(c=>plan.Rig.ChainLength(c.Role)).ToArray();
        float threshold=.5f*(legs.Length>0?legs.Average():plan.Rig.CharacteristicLength)*RetargetAcceptance.PenetrationLegFraction;
        var existing=plane.Points.Select(p=>(p.Mesh,p.Vertex)).ToHashSet();
        var missing=plan.Rig.SkinPoints.Select(p=>(Point:p,Depth:plane.Surface.Penetration(ContactGeometry.Point(p,pose))))
            .Where(p=>p.Depth>threshold&&!existing.Contains((p.Point.Mesh,p.Point.Vertex)))
            .OrderByDescending(p=>p.Depth).ThenBy(p=>p.Point.Mesh).ThenBy(p=>p.Point.Vertex).Take(16).Select(p=>p.Point).ToArray();
        if(missing.Length==0)return plane;
        if(plane.Points.Length+missing.Length>512)
        {
            // Capacity belongs to a pose, not to the union of every collider
            // encountered earlier in the action. Retire inactive cuts rather
            // than rejecting a new support phase with hundreds of stale cuts.
            // Refine still scans ALL skin after projection, including retired
            // points; an unmeasured pose can never pass through this path.
            var points=plan.Rig.SkinPoints.OrderByDescending(p=>plane.Surface.Penetration(ContactGeometry.Point(p,pose))).ThenBy(p=>p.Mesh).ThenBy(p=>p.Vertex).Take(512).ToArray();
            // Array/reference inequality is not evidence of a newly represented
            // collider. At capacity the same vertices can merely change order.
            // Preserve the original set so a projection stall cannot restart
            // indefinitely without adding a missing geometric constraint.
            if(existing.SetEquals(points.Select(p=>(p.Mesh,p.Vertex))))return plane;
            return plane with{Points=points};
        }
        return plane with{Points=plane.Points.Concat(missing).ToArray()};
    }
}
