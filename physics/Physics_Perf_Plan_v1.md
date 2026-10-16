# CRASHVERSE Physics & Performance Plan v1

Owner: Physics & Performance Developer. Scope: `Assets/Scripts/Physics/**`, sim tick/runner, pooling, quality tiers. Work on `physics/*` branches, PR into `main`, no force pushes.
Surveyed from the local checkout `/workspace/crashverse` (branch `vfx/v1-foundation`, origin `radhengle/crashverse`, which the GitHub connector can't reach: 404) on 2026-10-16.

## 1. Repo state
- Branches: `main`, `vfx/v1-foundation` (current, has uncommitted VFX/audio work), `backend/server-v0`. Gameplay code lives in `Assets/_Project/Scripts/*`. `Assets/Scripts/Physics/` doesn't exist yet. **Decision needed:** create it there as agreed, or use `Assets/_Project/Scripts/Physics/` to match the existing layout.
- Packages: URP `com.unity.render-pipelines.universal` 14.0.11. No URP pipeline `.asset` is checked in; the only setup is `Assets/_Project/Scripts/Editor/CreateDefaultAssets.cs`.
- There are **no `.asmdef` files at all.** `Crashverse.Rules` hasn't been created yet: `server/README.md` says "There is no `Crashverse.Rules` folder yet", and `server/.../Crashverse.Server.csproj` links client sources directly. We won't touch it; Rules code stays wherever it is.

## 2. Findings
- `ProjectSettings/TimeManager.asset`: Fixed Timestep 0.02 (also on `main`), Max Allowed 0.1.
- **Determinism break:** `Assets/_Project/Scripts/Perf/QualityController.cs` `ApplyLow()` sets `Time.fixedDeltaTime = 0.0333333f` and solver iterations to 4. High uses 8, Mid 6. Physics outcome therefore depends on the device tier.
- `ProjectSettings/PhysicsManager.asset`: `m_AutoSimulation: 1` (no script-driven stepping), solver iterations 6, bounce threshold 2.
- Tick: `Assets/_Project/Scripts/Core/GameSession.cs:46` works the tick out from `Time.fixedTime` (float round). Fire gating happens in `Update` with `Time.deltaTime` (line 127).
- Inputs: `Assets/_Project/Scripts/Backend/ServerContracts.cs:40` `InputEvent {int tick; string type; int x, y}`. The seed is `AttemptToken.seed` (long). Server replay is in `server/src/Crashverse.Server/Domain/InputRules.cs`.
- Events: `AnimEventBus` is a static class in `Assets/_Project/Scripts/Feel/FeelCore.cs:45`. Its consumers are `VfxDirector`, `MusicPlayer`, `FeelDirector`, `Projectile` and `FireController`.
- Destruction: in `Assets/_Project/Scripts/Destruction/`:
  - `DestructibleBlock` covers HP damage, `Break()`, `FixedJoint` supports and `OnJointBreak`, plus `Explosion` (static).
  - `MaterialDefinition` holds the float joint break force/torque values (450/800/900/2200).
  - `StructuralIntegrity` and `DestructionTracker` run in FixedUpdate; the tracker's combo uses `Time.time`.
  - `KillPlane`/`MovingPart` are also in this folder.
- There is no `_frag_##` or `UCX_` handling anywhere.
- Pooling: `Destruction/DebrisPool.cs` does exist (a basic debris pool). However:
  - It uses `UnityEngine.Random` (lines 59-60, 127-128).
  - Lifetime ticks in `Update` with `Time.deltaTime` (line 95-100).
  - Projectiles still use `Instantiate`/`Destroy` (`Projectile.cs:143`, `GameSession.cs:62`).
  - The VFX pool is in `VFX/VfxDirector`.
- Other determinism risks:
  - `Projectile.Update` runs life and age on `Time.deltaTime`.
  - `FeelDirector` uses `Random.value`, though that's cosmetic only.
  - Damage and joint values are floats in PhysX (an accepted risk, see §3).
- Quality tiers: `ProjectSettings/QualitySettings.asset` has Low (no shadows, no MSAA) and Mid (shadows, 2x MSAA), and possibly High. They're switched by `QualityController`. The High comment says "90 debris rigidbodies (OQ-D1)".

## 3. Determinism contract
- Sim state is a pure function of (long seed, InputEvents sorted by tick). The step is fixed at 0.02 on every tier. Randomness comes from one seeded PRNG per run, with streams split for spawn, debris and VFX. Cosmetics may use their own unseeded RNG.
- Per `backend/Backend_Plan_v1.md` D3, the server checks replay within a tolerance. Integer basis-point scoring stays in the Rules code. PhysX floats are accepted for the visuals but must not touch the score except through events counted at a tick.
- All sim events go through `AnimEventBus`, stamped with their tick.

## 4. Prioritized first tasks
1. **physics/fixed-step-lock**: remove the `fixedDeltaTime` and solver-iteration changes from `QualityController` (tiers may only change rendering, debris caps and targetFrameRate). Add an editor/CI assert that `TimeManager` = 0.02.
2. **physics/sim-runner**: `SimRunner` with an integer tick counter, `Physics.simulationMode = Script` with `Physics.Simulate(0.02f)`, and a tick-indexed `InputEvent` queue (it rejects anything out of order). Replace the `GameSession` tick calculation and its Update-based fire gating.
3. **physics/seeded-rng**: `SimRng` (PCG/xorshift64 seeded from `AttemptToken.seed`). Swap out `UnityEngine.Random` in `DebrisPool`. Move `Projectile` life/age to ticks.
4. **physics/pooling**: generalize `DebrisPool` into a pre-warmed `Pool<T>` for projectiles, debris and fragments, sized per tier (Low/Mid/High caps, High=90). No Instantiate/Destroy during play.
5. **physics/fracture-import**: an importer/prefab builder for `<Piece>_frag_##` with `UCX_` convex colliders. `DestructibleBlock.Break()` swaps in pooled fragments and raises break/collapse on `AnimEventBus`.
6. **physics/replay-test**: a headless test (extend `Tools/Tests/Tests.cs` and the server `InputRules`) that checks same seed plus same inputs gives the same event log and score within tolerance. Also an on-device IL2CPP ARM64 perf capture per tier.

## 5. Open questions
- Path: resolved, `Assets/_Project/Scripts/Physics/` (lead decision, 2026-10-16).
- Who creates the `Crashverse.Rules` asmdef, and when? (Without asmdefs, physics code compiles into Assembly-CSharp.)
- The URP pipeline assets per tier need committing (only an editor generator exists today).

## 6. Status
- Tasks 1-3 (fixed-step lock, SimRunner, seeded RNG) plus deterministic DebrisPool: branch `physics/fixed-step-lock`.
- `Crashverse.Rules` asmdef (WinRule, StarRules): branch `physics/rules-asmdef`.
- Sim code uses namespace `Crashverse.Sim`, not `Crashverse.Physics`. That name would shadow `UnityEngine.Physics` inside every `Crashverse.*` namespace.
- Debris and determinism (PR #6 review):
  - Debris is not purely visual. Chunks sit on the fixed `Debris` layer (9) and collide with pieces (Default/Structure) and with each other, never with projectiles. `SimLayers.ApplyRankedMatrix` re-applies these rows of the matrix at the start of every ranked run.
  - In ranked runs the colliding debris cap is a single tier-independent `DebrisRules.RankedSimCap = 60` (the Mid baseline), and chunk counts use the Mid tier. Tier caps of 25/60/90 apply only outside ranked play.
  - At every `SimRunner.BeginRun` (fresh start and Retry, tracked by `RunId` and `RunStarted`), DebrisPool recycles everything, rebuilds the free list in creation order, and re-seeds from `DebrisRules.RngSeed(RunContext.seed)`.
