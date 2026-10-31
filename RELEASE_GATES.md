# CRASHVERSE — Product Completion & Release Gates

**Working branch:** `pm/season1-king-phase1` (PR #58)  
**Purpose:** One living acceptance contract for turning the current Unity project into a coherent, installable, release-quality Android game. A green build is necessary, never sufficient.

## Product target

Ship a polished Android destruction-puzzle game with a clear first-session experience, satisfying physics, readable targets, a coherent Kingdom of Ironvale visual identity, ten well-tuned campaign levels, a meaningful finale, reliable progression, and production-safe online services. The game must remain enjoyable offline for campaign play; online/ranked/reward features must fail safely when the service is unavailable.

Season 1 commercial rule: real-money purchases remain disabled. Diamonds are earn-only. Do not expose fake purchase buttons or imply a payment succeeded.

## Release gates (all required)

### G0 — Build integrity and deterministic rules
- [ ] Unity Editor version, URP packages, Android SDK/NDK and scripting backend are pinned and documented.
- [ ] Logic tests, Unity EditMode/PlayMode tests, release guards, compile checks, server tests and APK/AAB workflows pass on the same PR head.
- [ ] Attempt seed is preserved end-to-end as 64-bit; simulation-critical random choices use the shared deterministic PRNG.
- [ ] Campaign level JSON, authoring source, generated data, hints, simulation fixtures and server validation agree.
- [ ] No stale generated content or duplicate/conflicting level definitions.

### G1 — Core gameplay
- [ ] Cold start → age gate → profile/onboarding → map → briefing → gameplay → results → map works with touch on real Android hardware.
- [ ] Aim, trajectory, fire, reload, hit detection, material damage, support collapse, debris, objective percentage, win/fail, retry and pause are reliable.
- [ ] No dead taps, soft locks, stuck projectiles, unbounded debris, invisible collision blockers or unrecoverable runs.
- [ ] Retry is deterministic for the same attempt seed and ordered inputs.
- [ ] Accessibility options cover motion/shake, audio levels, vibration and readable UI.

### G2 — Campaign and game design
- [ ] L1–L10 each have a distinct teaching objective, readable target silhouette, fair shot budget, valid hint and at least one reproducible winning solution.
- [ ] Star bands, mastery, power-up caps, rewards and server-side validation use identical level data.
- [ ] Every level is play-tested; automated solvability is supporting evidence, not a substitute for play.
- [ ] L10 Palace is the Chapter 1 finale; King Grimhold is a separate, explicitly scoped Hard Level milestone. Do not describe the Palace as a boss fight.
- [ ] Daily Boss content has clear entry, attempt, result, reward and reset behaviour.

### G3 — Visual and audio quality
- [ ] Compare actual in-game captures for L1–L10 and Daily Boss against approved concept art at matching camera framing.
- [ ] Consistent art direction: environment, buildings, cannon, materials, lighting, sky/water, UI, VFX and transitions.
- [ ] Destruction remains legible: hit feedback, cracks, material-specific debris, collapse, hidden-core exposure and detonation.
- [ ] SFX/music/haptics are present, balanced, licensed, respect settings and are not placeholders.
- [ ] Android texture compression, shader variants, materials, fonts and safe areas are verified in release builds.

### G4 — UI, UX and retention loop
- [ ] Production screens for home/map, level briefing, HUD, pause, win/fail/results, profile, settings, inventory/loadout, rewards and daily content.
- [ ] No emoji/icon placeholders, debug labels, test clocks, fake bots or QA controls in release.
- [ ] Touch targets, typography, contrast, loading, empty, offline, error and confirmation states are consistent.
- [ ] First-time user can understand the objective, aim/fire and result without developer help.

### G5 — Backend, identity and data
- [ ] Production backend is deployed with real persistence, migrations, backups, monitoring and environment separation.
- [ ] Authentication/account linking, guest upgrade, session/token lifecycle, account deletion and recovery are tested end-to-end.
- [ ] Campaign progress, wallet, rewards, daily reset, seasons, leaderboards and ranked attempts are server-authoritative.
- [ ] Replay/input validation and anti-tamper protections are verified; client-submitted score/reward values are never trusted.
- [ ] Offline campaign queue/reconciliation is idempotent and has explicit conflict/failure handling.
- [ ] No release path silently falls back to fake/local services for online features.

### G6 — Monetisation and economy
- [ ] Season 1 stays earn-only: real-money purchase entry points are hidden/disabled, not simulated.
- [ ] Currency/reward grants are idempotent, server-authoritative, auditable and protected against replay/duplicate claims.
- [ ] Economy is balanced against realistic session length and progression; no pay-to-win or misleading price surfaces.

### G7 — Security, privacy and compliance
- [ ] Secrets are not in source or client builds; API transport and server auth are production-grade.
- [ ] Age gate, privacy disclosures, consent, data minimisation, deletion and retention behaviour are verified.
- [ ] Release artifact contains no dev symbols, QA panels, local test backend, test clock or debug endpoints.
- [ ] Third-party asset licences, fonts, audio and trademarks are documented.

### G8 — Performance and release operations
- [ ] Profile representative low-, mid- and high-tier Android devices; target stable 60 FPS on agreed mid-tier hardware and graceful quality scaling on low-tier devices.
- [ ] Measure cold start, scene transition, memory, thermal behaviour, draw calls, GC allocations, physics cost and worst-case destruction.
- [ ] Test offline/poor network, app pause/resume, process death, low storage and interrupted requests.
- [ ] Signed AAB built from the reviewed commit; install/update tested through Play internal testing.
- [ ] Crash/ANR monitoring, versioning, release notes, rollback plan and support contact are ready.

## Working method

1. Keep PR #58 as the integration branch; make focused commits grouped by workstream.
2. Before each change, trace the gameplay/data/UI/backend call path and identify all consumers (Unity, tools, server, tests, generated content).
3. Add or update tests with every behaviour change. Never update a test merely to bless an unverified behaviour.
4. Run targeted checks first, then the complete CI suite. Review the actual failing logs.
5. For visual changes, attach before/after captures and device details. Do not claim visual acceptance from compilation.
6. Keep this document and the PR body current. Record blockers explicitly; never mark a gate complete without evidence.
7. Do not merge or call the game complete until all release gates are met.

## Known baseline risks to close

- PR #58 has a large visual/content scope and needs full L1–L10 gameplay and device acceptance.
- The hidden core is gameplay, not only presentation; seed, exposure, detonation, blast and score effects require tests.
- The Chapter 1 L10 Tidewarden encounter is intentionally retired in favour of a structural Palace finale; King Grimhold remains a separate Phase 2 feature.
- Existing README, QA notes and master checklist contain historical/stale statements and must be reconciled against the actual branch.
- A passing server test suite does not prove a production deployment, real authentication provider, persistent production database or live client integration.
- Emulator smoke tests do not prove real-device touch, visual quality, performance, thermal behaviour or Play Store readiness.
