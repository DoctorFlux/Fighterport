namespace FighterPort.Core;

// Reviewed semantic roles, never native names or animation-dependent guesses.
public static class BoundaryReferenceControls
{
    public const string Policy="reference controls with source-authored motion-position control retained";
    public static string[] Resolve(Scene scene,ControlRigSetup? setup)
    {
        if(setup is null||setup.SkeletonFingerprint!=AnimationControls.Fingerprint(scene))
            throw new InvalidDataException("Reference control fitting requires the exact reviewed control rig.");
        var ancestors=AnimationControls.ProtectedMotionBones(scene,setup).ToHashSet();
        var roles=setup.BoneRoles.Where(r=>r.Reviewed&&r.SemanticRole is "SceneRoot" or "RootMotion" or "RootOrientation" or "BodyOrientation").ToArray();
        if(ancestors.Count==0||roles.Length!=ancestors.Count||roles.Select(r=>r.SemanticRole).Distinct(StringComparer.Ordinal).Count()!=roles.Length||
            roles.Select(r=>r.Bone).Distinct().Count()!=roles.Length||roles.Any(r=>!ancestors.Contains(r.Bone)||string.IsNullOrWhiteSpace(r.Evidence))||
            roles.Count(r=>r.SemanticRole=="RootMotion")!=1||roles.Length<2||setup.UnassignedBoneRoles?.Any(r=>roles.Any(v=>v.SemanticRole==r))==true)
            throw new InvalidDataException("Reference control fitting requires unambiguous reviewed ancestor roles and exactly one motion-position control. No name-based fallback is allowed.");
        return roles.Where(r=>r.SemanticRole!="RootMotion").OrderBy(r=>r.Bone).Select(r=>scene.Bones[r.Bone].Name).ToArray();
    }
}
