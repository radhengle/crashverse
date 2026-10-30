# QA PR Approvals (deep pass), radhengle/crashverse, 2026-10-16 ~23:15 IST
Nothing posted (the user hasn't approved posting). Each comment below is ready to post. Comment URL: none yet.
Local runs: Tools/Tests (console runner), Tools/CompileCheck and check_release_guards.py at each head, in a worktree at /workspace/qa_pr_<n>. Server tests for #1 ran without the emulator. A QA LogicTests project doesn't exist in qa/, so there was nothing to fix there.
Global gate: emulator-smoke exists only in #23's android-apk.yml. No other PR can pass the CI emulator flow until #23 merges and is green. "EditMode test results" is skipping on every PR, so the Unity check only proves the code compiles.

| Order | PR | Head | Local | CI | Verdict | Tickets |
|---|---|---|---|---|---|---|
| 1 | #23 | 30983a8 | 791/0 tests, compile OK, guards OK, scripts syntax OK | EditMode in progress; APK build for 55eda51 in progress; emulator-smoke has never run green; no run yet at 30983a8 | Approve with changes (pending CI) | T23-1: green emulator-smoke API 34+36 at the final head with shots 01-08. T23-2: PlayMode FirstRunTapTests actually run and pass in CI (now ExecuteEvents-based). Done: v1-sig gate fix (55eda51), .pyc removed, BuildScript blocks release with dev key, Continue-enabled assert (flow.sh l.34-36), win/fail RESULT assert, year list opens at now-18 (V-02). Conflicts: #21, #8 (android-apk.yml). |
| 2 | #5 | 249dddb | 790/0 | no CI | Approve with changes | T5-1: needs CI to run (branch not triggering). Moves WinRule/StarRules into Rules (no duplicate types). |
| 3 | #6 | 9e84760 | 795/0 | compile pass | Approve with changes | T6-1: emulator flow after #23 merges. |
| 4 | #14 | 4312ef3 | 799/0 | compile pass | Approve with changes | Merges cleanly with #11. T14-1: emulator flow. |
| 5 | #11 | 2b4d1c9 | 798/0 | compile pass | Approve with changes | T11-1: emulator flow. |
| 6 | #18 | 53f9361 | 798/0 | none (base = #6 branch) | Approve with changes | T18-1: retarget to main after #6, CI green. |
| 7 | #20 | d586b0c | 804/0 | none (base = #18) | Approve with changes | Same as #18. |
| 8 | #2 | dacf81d | see Update 23:20 | | Approve with changes | superseded, see update below |
| 9 | #12 | 7211c17 | 859/0 | compile pass | Approve with changes | L07 = m1_clockwork_tower via chapter1.json (correction noted). Merges cleanly with #13. Merge before #1 and #13. |
| 10 | #13 | e273d63 (moved) | 834/**2 FAIL** | none | Request changes | T13-1: tests "shots L10 expected 7 got 8" and "star3 L10 expected 0.6 got 0.375". The data follows the LR ruling (8 shots), so the tests are stale. Update them to the LR values. |
| 11 | #16 | 8db3ad0 | 792/0 | none | Approve with changes | T16-1: CI; after #5. |
| 12 | #19 | f4a9b29 | 901/0 | compile pass | Approve with changes | Merge with #10; run EG cases on device. |
| 13 | #10 | b1c3407 | 794/0 | compile pass | Approve | Gem packs hidden when the flag is off; unknown flags count as off. |
| 14 | #7 | c3a2639 | 789/0 | compile pass | Approve | targetSdk 35 plus a guard. |
| 15 | #21 | 5923a52 | 789/0 | compile pass | Approve with changes | T21-1: conflicts with #23; rebase after it. |
| 16 | #8 | c172375 | 789/0 | compile pass | Approve with changes | T8-1: conflicts with #23; rebase. Needs the user's upload keystore secret. |
| 17 | #1 | 4a83daf | see Update 23:20 | | Approve (conditional) | superseded, see update below |
| 18 | #22 | b7071fc | 789/0 | compile pass | Approve | Suggest "incl. emulator-smoke" in the CI-green rule. |
| 19 | #3 | 25537db | 823/0 | compile pass | Request changes | T3-1: conflicts with #1 (/v1, AgeBands); rebase after #1. WIP scope. Music .ogg files not in Resources. |
| 20 | #24 | cae55b3 | 789/0 | compile pass | Approve with changes | Merges cleanly with #23. T24-1: emulator screenshots of the new age gate vs Art v2. |
| — | #15 | 542ba26 | doc | none | Approve | Doc only. |

## Ready-to-post comments
- #23: `QA: Approve with changes @30983a8. Local: 791/0 tests, compile + release guards OK. Fixed since 9306946: v1 sig gate, .pyc removed, dev-key release guard, Continue-enabled (BUG-20) + win/fail asserts, year list at now-18. Before merge: (T23-1) emulator-smoke green on API 34 and 36 at final head with screenshots 01-08; (T23-2) PlayMode FirstRunTapTests run and pass in CI. Note conflicts with #21/#8 in android-apk.yml.`
- #5: `QA: Approve with changes @249dddb. 790/0 local, compile OK. CI did not run on this branch; needs a green run, then emulator flow after #23.`
- #6 / #14 / #11: `QA: Approve with changes @<sha>. Local tests pass (795/799/798), compile OK, CI compile green. Merge in order #6→#14→#11 (clean merges); each must pass emulator-smoke after #23 lands.`
- #18 / #20: `QA: Approve with changes @<sha>. Local tests pass. Stacked PR has no CI; retarget to main after its base merges and get CI + emulator-smoke green.`
- #2: `QA: Request changes @44e9ec5. Local 791/0, but no CI on the branch and no particle prefabs yet (only shaders + VfxDirector). Needs CI and the impact/dust prefabs.`
- #12: `QA: Approve with changes @7211c17. 859/0 local, CI compile green. Merge before #13 and #1 (they depend on L10=8 shots). Emulator flow after #23.`
- #13: `QA: Request changes @e273d63. Local Tools/Tests: 2 failures, "shots L10 expected 7 got 8" and "star3 L10 expected 0.6 got 0.375". The data matches the LR ruling (8 shots), so update the stale expectations. No CI run on the branch.`
- #16: `QA: Approve with changes @8db3ad0. 792/0 local. No CI; needs green CI after #5.`
- #19 + #10: `QA: Approve (#10 @b1c3407) / Approve with changes (#19 @f4a9b29, 901/0 local). Merge together; EG cases still need a device/emulator pass.`
- #7: `QA: Approve @c3a2639. targetSdk 35 + guard; 789/0 local, CI green.`
- #21 / #8: `QA: Approve with changes @<sha>. Tests pass; conflicts with #23 in android-apk.yml, so rebase after #23. #8 also needs the upload keystore secret.`
- #1: `QA: Request changes @515ee12. BE-06 fix verified (Leaderboards.cs:23). Local server tests: 3 failures. BE02_LevelData_MatchesLevelRulings60ba5d1 fails (L10 shots 7 vs 8) since eb0bcb4 restored main's chapter1.json; this depends on #12. ReplayMismatch_GoesToShadowBoard_NoBan_ClawsBackProvisionalFirstClear (SecurityFindingsTests.cs:320) and FirstClearRewards_OverDailyCap_AreDeferredUntilValidated also fail. Emulator/Unity CI still pending. Conflicts with #3.`
- #22: `QA: Approve @b7071fc. Suggest the CI-green condition name emulator-smoke explicitly.`
- #3: `QA: Request changes @25537db. Conflicts with #1 (/v1, AgeBands); rebase after #1. Music .ogg still not in Resources.`
- #24: `QA: Approve with changes @cae55b3. Clean merge with #23; need emulator screenshots of the new age gate compared with Art v2.`
- #15: `QA: Approve @542ba26 (doc only).`

dev-build-5: not published yet. BUG-20 can't be confirmed on a build until #23's emulator-smoke produces screenshots.

## Update 23:20 IST: #2 / #31 / #28 (VFX stack) and #1 re-review
| PR | Head | Local | CI | Verdict | Tickets |
|---|---|---|---|---|---|
| #2 VFX core base | dacf81d | 791/0, compile + vfx compile_check OK | compile pass | Approve with changes | Re-reviewed as the base, so the "no particles" blocker is dropped. T2-1 perf: the GlossyPBR High pass multiplies about 8 multi_compile sets (SSAO, box projection, probe blending, soft shadows, cascades, lightmap, fog, instancing), so hundreds of variants. Strip SSAO, box projection and probe blending on mobile URP assets, or use shader_feature, and attach a variant count. The Low SubShader (hard shadows only) is good. |
| #31 particles + damage | 864da5f (on #2 head) | 799/0, compile OK | none (base = #2 branch) | Approve with changes | Budget: 150 impact lane (Low 75), oldest culled, Low-tier layers off, maxParticleSize 0.5 screen, no lights, collision or sub-emitters. Fits 60/30. T31-1: retarget after #2 so CI and VfxCoreTests run. T31-2: alpha-blended dust is the overdraw risk. Measure GPU frame time on a mid device at full budget (L10 collapse) and lower the dust count on Mid if needed. T31-3: vfx/compile_check/tests has Program.cs but no csproj, so it can't run locally. |
| #28 import pipeline | 821c46c (based on old #2 fcbf46f) | 798/0, compile OK | none | Approve with changes | Merges cleanly with the #2 head and #31; the apparent cubemap deletion is only base drift. T28-1: rebase on dacf81d. T28-2: ImportPipelineTests (editor) must run in CI; there's no local runner. T28-3 perf: the validator must fail imports over the tri budgets (Wrecker 12k, island 10k, kit 1.5k) and require LODs. |
| #1 backend | 4a83daf | server 182 pass / 0 fail / 7 skipped (Firestore emulator-only); Tools/Tests 791/0 | server test + emulator pass; Unity EditMode 38940558948 in progress | Approve (conditional on the Unity run going green) | chapter1.json and season_s1.json are identical to 60ba5d1. BE-06 fixed. LB-30 asserts reachedUnixMs = recalc time on a drop (and updates on a rise). LB-31 asserts the earlier reacher ranks first after a tie, in both the campaign path and the Titan replay correction. The earlier 3 failures came from the reverted data. Conflicts with #3. |

Ready-to-post (supersedes the earlier #2 and #1 texts):
- #2: `QA: Approve with changes @dacf81d. Reviewed as the shader/VFX core base (particles in #31, import in #28). 791/0 local, compile + vfx compile_check OK, CI compile green. Perf: GlossyPBR High pass multiplies ~8 multi_compile sets; strip SSAO/box projection/probe blending for mobile or use shader_feature, and attach a variant count.`
- #31: `QA: Approve with changes @864da5f. Particle budget (150, Low 75, oldest culled, Low-tier layers off, 0.5-screen size cap, no lights/collision) fits 60/30 FPS. Before merge: retarget after #2 so CI + VfxCoreTests run; measure GPU frame time on a mid device at full budget (L10 collapse); alpha-blended dust is the overdraw risk, so lower dust count on Mid if needed.`
- #28: `QA: Approve with changes @821c46c. Rebase on #2 head dacf81d (merges cleanly with #2 and #31). Need CI/EditMode ImportPipelineTests green. Validator should fail imports over tri budgets (Wrecker 12k, island 10k, kit 1.5k) and require LODs.`
- #1: `QA: Approve @4a83daf, conditional on Unity EditMode run 38940558948 passing. chapter1/season_s1 match 60ba5d1 exactly. Local: server 182 pass/0 fail/7 Firestore-emulator skips (green in CI emulator job), Tools/Tests 791/0. BE-06 verified; LB-30 asserts the recalc timestamp; LB-31 asserts tie ranking (campaign + Titan replay). Rebase #3 after this.`

## Posted 23:20 IST (user-approved, gh pr comment, comments only)
#13 re-reviewed @12e1199: 852/0 local, guards OK, clean merge with #12; CI run 38941298441 pending, so posted as Approve with changes (becomes Approve once green). Heads that moved since review: #2 dacf81d→b787517 and #28 821c46c→85de6b5 (comments note the reviewed head).

| PR | Comment URL |
|---|---|
| #23 | https://github.com/radhengle/crashverse/pull/23#issuecomment-6221333842 |
| #5 | https://github.com/radhengle/crashverse/pull/5#issuecomment-6221334777 |
| #6 | https://github.com/radhengle/crashverse/pull/6#issuecomment-6221335642 |
| #14 | https://github.com/radhengle/crashverse/pull/14#issuecomment-6221336370 |
| #11 | https://github.com/radhengle/crashverse/pull/11#issuecomment-6221337226 |
| #18 | https://github.com/radhengle/crashverse/pull/18#issuecomment-6221338057 |
| #20 | https://github.com/radhengle/crashverse/pull/20#issuecomment-6221338903 |
| #2 | https://github.com/radhengle/crashverse/pull/2#issuecomment-6221339659 |
| #31 | https://github.com/radhengle/crashverse/pull/31#issuecomment-6221340686 |
| #28 | https://github.com/radhengle/crashverse/pull/28#issuecomment-6221341446 |
| #12 | https://github.com/radhengle/crashverse/pull/12#issuecomment-6221342262 |
| #13 | https://github.com/radhengle/crashverse/pull/13#issuecomment-6221343406 |
| #16 | https://github.com/radhengle/crashverse/pull/16#issuecomment-6221344350 |
| #19 | https://github.com/radhengle/crashverse/pull/19#issuecomment-6221345290 |
| #10 | https://github.com/radhengle/crashverse/pull/10#issuecomment-6221346066 |
| #7 | https://github.com/radhengle/crashverse/pull/7#issuecomment-6221347194 |
| #21 | https://github.com/radhengle/crashverse/pull/21#issuecomment-6221348184 |
| #8 | https://github.com/radhengle/crashverse/pull/8#issuecomment-6221349300 |
| #1 | https://github.com/radhengle/crashverse/pull/1#issuecomment-6221350511 |
| #22 | https://github.com/radhengle/crashverse/pull/22#issuecomment-6221351535 |
| #3 | https://github.com/radhengle/crashverse/pull/3#issuecomment-6221352440 |
| #24 | https://github.com/radhengle/crashverse/pull/24#issuecomment-6221353472 |
| #15 | https://github.com/radhengle/crashverse/pull/15#issuecomment-6221354430 |

## Update 00:35 IST, 17 Oct (posted)
| PR | Head | Verdict | Blockers | Comment URL |
|---|---|---|---|---|
| #34 EditMode false-green fix | 7e3553a | Approve with changes | Gate proven: 4 tests ran, 1 failed, job red. The failure is real: activeInputHandler on main = Input System only (BUG-20 class). Merge after/with #23 and get it green. Shared fail-on-0 step with #23. | https://github.com/radhengle/crashverse/pull/34#issuecomment-6222422236 |
| #23 BUG-20 | 7c6c48f | Approve with changes | BUG-20 verified on API 34+36. BUG-24: win path untested (needs a hard win assert). Unity Edit+PlayMode job segfault (exit 139), 0 tests. BUG-23 magenta filed High (separate). | https://github.com/radhengle/crashverse/pull/23#issuecomment-6222423648 |
| #1 backend | b8abe02 | **Approve** | none (CI Unity 20/20, server-ci + emulator green; local 182/0) | https://github.com/radhengle/crashverse/pull/1#issuecomment-6222424945 |
| #13 Ch1 levels | 12e1199 | **Approve** | CI 38941298441 green; merge after #12 | https://github.com/radhengle/crashverse/pull/13#issuecomment-6222426344 |
| #2 VFX core | b787517 | **Approve** | Variant strip done. Note: no URP pipeline asset assigned. | https://github.com/radhengle/crashverse/pull/2#issuecomment-6222427749 |
| #28 import | 723ccb3 | Approve with changes | Rebased, budgets enforced; needs a CI run | https://github.com/radhengle/crashverse/pull/28#issuecomment-6222428962 |

New bugs: BUG-23 (High) magenta procedural meshes, no material assigned (WorldMapBuilder.cs:34/38, ArenaBuilder), will show on device. BUG-24 (Medium) smoke win path is only a warning. Details in DevBuild5_RunThrough.md.
- PR #52 @be6ad77 (dev-build-6): APPROVE WITH CHANGES — https://github.com/radhengle/crashverse/pull/52#issuecomment-6380984344
- PR #52 @6cf622e (dev-build-6 rebuilt, vc56): APPROVE WITH CHANGES (C-2/C-4/C-7/aim P0 fixed; C-1/C-3 API36 screenshot still Score 0/2★; C-9 publish clobbered reviewed dev-build-6, tag at be6ad77) — https://github.com/radhengle/crashverse/pull/52#issuecomment-6381865363
| — | V-02 | 2026-10-24 | PM ruling | — | Updated | Year list opens at current−50 (was −18). New tag per build, no overwrites. |
- dev-build-7 @7d359e6 (main, vc62): GO for phone test / NO-GO release (targetSdk 34, 4 KB libs, debuggable). No PR, no comment. Report qa/DevBuild7_RunThrough.md
- PR #55 @eb0126f (integration/v2, vc65): BLOCK. V2-01: the app terminated itself at the API 34 age gate (onDestroy + self SIGKILL, not a flake). R-1/R-2 met, R-3 APK met but no signed AAB. Report: qa/PR55_Review.md — https://github.com/radhengle/crashverse/pull/55#issuecomment-6384489623
- PR #55 @603ccc0 (re-review, vc75): NO-GO. V2-01: only 3/3 green API 34 runs (need >=10); root cause is a CONFIG_ASSETS_PATHS (0x80000000) relaunch, which Unity turns into a self-kill. Masked in CI (AOSP no-GMS image + settle wait), not fixed in the app. Gem packs, social login, HUD circles, meta tests (14), release launch, R-1/R-2 all PASS. Map lock overlap MED. Signed AAB not produced. Evidence: qa/pr55b/ — https://github.com/radhengle/crashverse/pull/55#issuecomment-6524426044
