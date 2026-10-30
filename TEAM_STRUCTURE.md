# CRASHVERSE team structure (17 Oct 2026)

PM (Project manager) = overall approver. Only spending money goes to the user.

## Dev lead: Developer
Owns integration of the whole game: merge order, main stays green and playable, CI/build/release, architecture, and resolving cross-PR conflicts. Reviews every dev PR for fit.

## Test lead: QA
Owns the test plan, final sign-off on every PR and build, and coordinates all testers. Nothing merges without the paired tester + QA + Security Admin.

## Dev / tester pairs
| Developer | Area | Paired tester |
|---|---|---|
| Gameplay Developer | aim/fire feel, shake, haptics, VFX/SFX hooks, tutorial, win/fail, dev aim hook | Gameplay Tester |
| UI Developer | all screens per Art v2 | UI Tester |
| Physics Developer | destruction sim, materials, perf | Physics Tester |
| Backend Engineer | local backend, contracts, server | Backend Tester |
| Graphics Developer | URP asset, quality tiers, materials, shaders, lighting | Graphics Tester (also covers VFX & 3D Modeler output) |
| Developer (lead) | CI, build, core, integration | QA |

Test Automation Engineer: CI emulator gate (all testers use its screenshots/videos).
Playtester: player-experience verdict on each build.
Content owners (unchanged): Game designer, Level Designer, Art & UI, 3D Modeler, VFX & Tech Artist, Animator, Sound Manager, Security Admin, Release Manager.

## Flow
Dev opens PR -> paired tester tests immediately -> QA signs off -> Security Admin -> Developer merges in order -> CI emulator gate -> Playtester reviews build.
