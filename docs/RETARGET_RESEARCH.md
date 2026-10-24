# Retargeting research: body proportions and actor movement

Reviewed 18 October 2026. These are primary-source design references, not new
format integrations or imported implementations.

## Xenoverse tools

XenoKit commit `a28de869eb6b732f1230c9752214e2cc9aeb3e95` evaluates a bone's
animated transform relative to the skeleton carried by its EAN animation. Its
base-bone movement is handled separately. Character body data can also affect
pelvis placement and per-bone scales. See its
[animation evaluator](https://github.com/LazyBone152/XenoKit/blob/a28de869eb6b732f1230c9752214e2cc9aeb3e95/XenoKit/Engine/Animation/AnimationPlayer.cs#L413)
and [body-scale application](https://github.com/LazyBone152/XenoKit/blob/a28de869eb6b732f1230c9752214e2cc9aeb3e95/XenoKit/Engine/Animation/Xv2Skeleton.cs#L218).

This supports treating animation-source rest transforms, target anatomy and
actor movement as separate inputs. It does not establish universal floor IK,
or prove the game's own engine uses the same implementation. The Goku/Gotenks
example remains a useful future adult/child fixture; no such game assets or
Xenoverse reader/writer were added during this work. Game-specific character
flags and body tables are not portable rules for unknown Smash skeletons.

## Engine reference

[Godot's retargeting documentation](https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/retargeting_3d_skeletons.html)
distinguishes bone mapping, rest calibration and position-track normalization.
Its configurable normalization scales movement using a reference bone height.
That is a policy choice: it must not silently multiply authored gameplay travel
when the requested policy is to retain the original route.

## Independent V2 implementation

- Anatomical rotations use bind-world calibration and measured limb proportions.
- Mapped ancestors above the pelvis carry actor movement in adapter world axes
  and units. Equal, unambiguous ancestor paths can match unnamed helper joints.
  Anatomical rest tilt does not rotate the gameplay trajectory.
- Native trajectory translations retain authored cubic curves and discontinuities.
  Different local axes use a fixed linear conversion; contact fitting edits only
  anatomy beneath the trajectory.
- Pelvis displacement is measured relative to that trajectory, avoiding double
  application of leg scaling to movement. IK and skin contacts correct the pose.
- A surface rising from far below the reference plane is not planted merely
  because it crosses that plane. Motion history permits contacts once it clears
  the band, settles, or reaches the plane with credible upright support. Foot
  travel alone does not prove lifting: stable vertical leg extension and
  bilateral near-floor geometry can establish support during motion. Stationary
  locks remain separate. No named idle pose or clip boundary is used.

The current adapter contract uses normalized game axes/units and humanoid rig
roles. Unknown anatomy, missing terrain or props, incompatible simultaneous
contacts and native interpolation limits prevent a defensible promise of
perfect results for every possible model and animation. Acceptance therefore
measures contacts, trajectory, limb offsets, native skin and original-file hashes
on complete motions, alongside independent proportion and coordinate-basis cases.
