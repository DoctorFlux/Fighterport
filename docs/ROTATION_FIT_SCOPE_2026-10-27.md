# Rotation fitting continuation — frozen checks

Within the single incomplete G1/M2, improve local boundary rotation paths on
existing native XYZ bones. The reported bone is HipN, but numerical code must
not use bone, character, animation, file or frame identities as exceptions.

The supplied Fall angles (-0.078132, -0.081514, -0.5721804) and fitted angles
(179.9219, -179.9185, -180.5722) are orientation equivalent within screenshot
rounding. The defect to investigate is the intervening path, not a requirement
that equivalent Euler labels match. Test original Next Sonic AttackAirLw on Mario,
with Fall frame 1 and joins at display frames 5 and 35, plus existing directions.

Initial experimental checks (retained as the rejected experiment):

- Pure-axis frozen endpoints follow shortest orientation smoothstep; moving
  endpoints preserve angular velocity and timebase. Both boundaries, sparse bind
  fallback, coupled Euler families, paired keys, gimbal configurations, multiple
  turns and renamed/permuted fixtures must be exercised independently.
- Endpoint orientation tolerance remains 0.01 degrees; original retained keys,
  tangents and protected controls remain exact. Authored middle and source travel
  are unchanged. Existing floor, pin and contact bounds are unchanged.
- Native CHR0 has integral key indices. Compile orientation curves offline to
  integer native Hermite keys. Check approximation at 15 fractional witnesses
  per native interval, maximum 0.25 degrees. This is a separate representation
  bound, not endpoint/contact acceptance or a continuous-time certificate.
- Bound added rotation keys and witness work to two million each. Reject
  nonfinite, discontinuous/unrepresentable or excessive work before publication;
  do not silently substitute a different path. Disabling smooth rotations keeps
  the legacy authored-turn Euler policy. Legacy receipts remain readable.
- Preserve native Undo, current animation ownership, source bytes, Save/export,
  full previewer and original Next. No new skeleton, contact solver, playback
  work, game adapter, persistent state or project schema version.

Primary mathematical references: [Shoemake, quaternion curves](https://www.cs.cmu.edu/~kiranb/animation/p245-shoemake.pdf)
and [SciPy rotation spline documentation](https://docs.scipy.org/doc/scipy/reference/generated/scipy.spatial.transform.RotationSpline.html).
Use original implementation and independent native matrix oracles. Test privately
in the background; inspect fresh captures. No broad all-character/contact/game
claim follows from these bounded checks.

## Native representation finding and revised implementation boundary

The experimental orientation-space cubic failed the unchanged 0.25-degree
representation gate on authentic HipN: 23.017 degrees for 1–3 and 18.593 degrees
for 1–5, with the original attack join velocity. It is rejected; do not publish
that curve, loosen its gate or silently replace it. Some procedural pole cases
also cannot be represented accurately by integral keys. That experiment cannot
be the default implementation.

Before implementing its replacement, freeze a native-representable alternative:
search both complete endpoint Euler families and the neighboring whole-turn
offsets (54 candidates). Evaluate the actual native Hermite path's physical local
angular travel by Simpson integration at at least 64 intervals and at least 16
intervals per frame. Deterministically retain the nearest branch on ties. Select
the least measured travel, including the legacy branch as a candidate. Keep all
original joins/tangents/middle and the exact reference endpoint orientation.
Limit this offline candidate review to two million evaluations. No extra baked
keys, SO(3) approximation certificate, changed contact gate or global-optimum
claim. Disabling smooth rotation retains the authored-turn policy.

The native branch search passes the full authentic workflow but leaves HipN's
path unchanged (169.201 entry / 124.765 exit degrees in the sealed Next fixture).
It only improves one other bone. Before extending implementation, freeze one
additional native FK candidate: split at an integral middle key whose orientation
lies on the shortest spherical endpoint path, with its smoothstep derivative.
Search equivalent midpoint families using native Hermite curves on both sides.
Accept only lower measured angular travel with no increase in measured peak
angular speed; otherwise retain the original single-segment candidate. This is
one native construction, not a bake of the rejected SO(3) cubic or a certificate
of approximation to it. Shared joins and their authored keys/tangents remain exact;
insert at most three additional axis keys per window. Count all candidate work
against the same two-million-evaluation budget. Independently check dense actual
native samples, including peak speed and C1 middle-key tangents, before acceptance.

Independent dense native matrix samples must show travel no greater than the
legacy branch within integration error, exact endpoint orientation (0.01 degrees),
unchanged middle/controls, and an improvement in the reported authentic case.
Paired sides, moving references, sparse bind fallback, scale extremes, shortest
single-axis paths, ties, naming/storage permutations and invalid inputs remain
required. Record rejected experiments separately from admitted evidence.
