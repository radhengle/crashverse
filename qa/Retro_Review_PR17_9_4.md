# Retro-review: merged PRs #17, #9, #4 (radhengle/crashverse)
Reviewed 16 Oct 2026 IST, read-only, against origin/main 22162b5. Merge times (IST): #4 11:05, #17 11:19, #9 11:29.

## PR #17: arm64 fix (378664c), verdict: PARTIAL PASS
- PASS: `ProjectSettings.asset` changes the miscased `androidTargetArchitectures` key to `AndroidTargetArchitectures: 3` (ARMv7|ARM64). scriptingBackend Android is 1 (IL2CPP). `BuildScript.cs` sets IL2CPP and ARMv7|ARM64 in code. The android-apk.yml step fails the job if `lib/arm64-v8a/libil2cpp.so` or `libunity.so` is missing. check_release_guards.py checks both settings.
- NOT ADDRESSED: **APK-02 targetSdk is still 34** (`AndroidTargetSdkVersion: 34` on main). Play needs 35.
- NOT VERIFIED: no post-merge APK exists on the box. The newest APKs, `/workspace/apk/out2/CRASHVERSE-dev.apk` (06:08 IST) and `CRASHVERSE-dev-audio.apk` (06:12 IST), were built before the merge. Both are still **armeabi-v7a only**, with targetSdk 34 and debuggable=true.
- Tickets:
  - **R17-1 High:** set targetSdk 35 (merge wt/developer-sdk35) and add a guard check for it.
  - **R17-2 High:** build an APK from main ≥39c76fa and hand it to QA to confirm `lib/arm64-v8a/`. APK-01 stays open until then.
  - R17-3 Low: the CI assert only runs in android-apk.yml. Add the arm64 check to the AAB/release job too.

## PR #9: EditMode tests in CI (358aee2, da8243d), verdict: FAIL (false green)
- The job runs the release guards, both CompileCheck builds and `dotnet run --project Tools/Tests` first, and those do fail the job. QA re-ran them locally on main and all pass: guards pass, both builds succeed, Tools/Tests 789 checks / 92 cases.
- **There are no EditMode tests on main.** main has no `.asmdef` files and no `[Test]`/`[UnityTest]` sources. The Rules/Physics test asmdefs named in the commit message aren't merged. So unity-test-runner runs 0 tests and goes green, which proves only that Unity compiles.
- Results upload uses `if-no-files-found: ignore`, so missing results are silent too.
- The workflow is triggered only on `pull_request` and `workflow_dispatch`, not on push to main, so it doesn't run after a merge.
- Separately, Backend admits that 7 Firestore-emulator tests are skipped in server CI (QA saw 7 skipped at PR #1 head 9b5574b). Their fail-on-skip emulator job is noted and not yet verified.
- Tickets:
  - **R9-1 High:** fail the job when the EditMode test count is 0 or any test is skipped/inconclusive. Parse test-results XML (`total>0`, `skipped==0`) or set an expected minimum count.
  - **R9-2 Med:** change upload to `if-no-files-found: error`.
  - R9-3 Med: add a `push: branches: [main]` trigger.
  - R9-4 Med: land the Rules/Physics EditMode asmdefs, then QA re-verifies a red run with a deliberately failing test.
  - R9-5 Med (Backend): emulator job with fail-on-skip; QA will verify that 0 of the 7 are skipped once it lands.

## PR #4: CONTRIBUTING.md (47fdff1), verdict: PASS WITH COMMENTS
- Accurate: all three pre-PR commands exist and pass on main. Branch naming `<team>/<topic>` matches the real branches (dev/contributing, release/arm64, ci/editmode-tests). The `/workspace/wt/<bot>` worktrees match the box. PR-only and no force-push match.
- Gaps:
  - **R4-1 Med:** no QA sign-off step. Add: "PRs touching gameplay, economy, backend or build need a QA verdict (approve or comment) before Developer merges. Release builds need QA sign-off against the regression list."
  - R4-2 Low: no server tests step (`dotnet test server/`, with the emulator for Firestore) for when the backend lands.
  - R4-3 Low: say that CI checks (Unity EditMode, android-apk, server-ci) must be green, as required status checks with branch protection, before merging.
  - R4-4 Low: Developer reviews and merges their own PRs too (#17 and #9 were Developer's). Require a second reviewer for those, such as QA or PM.

## Comment texts (for GitHub)
**#17:** QA retro: ARM64 arch key fix, IL2CPP and the CI arm64-v8a assert look right. Still open: targetSdk is 34 on main (Play needs 35, APK-02), and there's no post-merge APK yet. The latest box APKs (built before the merge) are still armeabi-v7a only. Please set targetSdk 35 with a guard check and share an APK from ≥39c76fa so QA can confirm lib/arm64-v8a.
**#9:** QA retro: false green. main has no EditMode test asmdefs or tests, so the Unity step runs 0 tests and passes. Please fail the job on 0 tests or any skipped, change upload to `if-no-files-found: error`, add a push-to-main trigger, and land the Rules/Physics test asmdefs. The guards, CompileCheck and Tools/Tests steps are good (789/0 locally). Separately noting Backend's 7 skipped emulator tests and the planned fail-on-skip job.
**#4:** QA retro: the commands and branching are accurate (all pass on main). Please add a QA sign-off step before merging gameplay/economy/backend/build PRs, the server test command, required green CI checks, and a second reviewer when Developer authors the PR.

Posting status: see final report.
