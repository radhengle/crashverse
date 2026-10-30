# Joint break policy (physics-owned)

**Code:** `Assets/_Project/Scripts/Physics/Core/JointBreakPolicy.cs` (`Crashverse.Sim.JointBreakPolicy`, engine-free).
**CI:** `Tools/ci/check_joint_break_policy.py` (unity-tests.yml fast-fail step). **Tests:** `Physics/Tests/JointBreakPolicyTests.cs`,
`Scripts/Editor/Tests/Gameplay/JointBreakPolicyPhysicsTests.cs`.

## Values

| | Value |
|---|---|
| `Joint.breakForce` (every structural joint) | `+Infinity` (`float.PositiveInfinity` == `Mathf.Infinity`) |
| `Joint.breakTorque` | `+Infinity` |
| Kit `JNT_` `break_force_N` / `break_torque_Nm` blank (missing, 0, negative, NaN) | → `+Infinity` |
| Kit explicit finite value | **ignored** → `+Infinity` (kept on `ImportedPhysicsHints` as data; import validator warns) |

Why ignore and not clamp: any finite threshold is a force-break path that Level Designer's solvability sim does not model;
a clamp is still one. One rule, no exceptions.

## How a joint goes away (only these)

1. **Damage:** HP reaches 0 (Contract §3, steel-joint 2× on direct hits) → `DestructibleBlock.Break()` removes the piece's joints.
2. **Support gone:** when a support breaks, collapses or falls out of bounds, each dependent drops its joint to that support
   (`DetachJointsTo`), then `SupportRule` decides: collapse only when ALL supports are gone (progressive via `StructuralIntegrity`).
   Without this an unbreakable joint would let a falling support drag a still-supported dependent, or hang a collapsed piece.
3. Removal is tick-exact: `JointRemoval` queues it and `SimRunner` flushes with `DestroyImmediate` at tick begin and just before the
   step (no end-of-frame `Destroy`, which lands after a device-dependent number of ticks).

`OnJointBreak` (50% HP splash) is now unreachable for structural joints. `MaterialDefinition.jointBreakForce/Torque` is legacy data, not applied.

## Where joints come from

Kit prefabs carry no Unity joints (`CrashversePrefabBuilder`: "no physics components"); `JNT_` empties are naming/validation only and
their `break_*` user properties land on `ImportedPhysicsHints`. The only runtime joints are the level-layout support joints
(`LevelBuilder` → `DestructibleBlock.AddSupport`, one `FixedJoint` per `sup` entry). Both paths go through this policy.

## Unchanged (ranked lock)

50 Hz / 0.02 s step, 6 solver iterations, sleep threshold 0.08 (`QualityController`, `SimTime`, `TimeManager.asset`). Checked by CI.

## Behaviour change vs before

Previously support joints had finite material thresholds (glass 250, wood 450, brick 900, stone 1500, steel 2200 N) and a snap applied 50% HP.
Because blocks are kinematic while supported these rarely fired, except where a collapsed support hung on a dependent's joint. Ranked
replays recorded before this change may differ (already the case after chunked destruction).
