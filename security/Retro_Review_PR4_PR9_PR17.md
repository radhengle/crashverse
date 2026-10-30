# Retro-review: PR #4, #9 and #17 (radhengle/crashverse, merged)

- **Reviewer:** Security Admin
- **Date:** 2026-10-16
- **Method:** read-only throughout.
  - `git fetch` of `origin/main` and the merge commits.
  - `gh pr view` and `gh api` GET calls.
  - Nothing was posted, committed or pushed.

**Merge commits reviewed**

| PR | Merge commit | Files changed |
|---|---|---|
| #4 | `9b5d750` | `CONTRIBUTING.md` |
| #17 | `39c76fa` | `.github/workflows/android-apk.yml`, `Assets/_Project/Scripts/Editor/BuildScript.cs`, `ProjectSettings/ProjectSettings.asset`, `Tools/ci/check_release_guards.py` |
| #9 | `22162b5` (current `main` head) | `.github/workflows/unity-tests.yml`, `Packages/manifest.json` |

**Context found during the review (important):** `gh api repos/radhengle/crashverse/branches/main/protection` returns **HTTP 403: "Upgrade to GitHub Pro or make this repository public to enable this feature."** The repository lists no environments. On the current plan, a private repo can have neither branch protection or rulesets, nor environments with required reviewers. Three things therefore rely on bots following the rules rather than on GitHub enforcement:
- the "no direct pushes to `main`" rule in CONTRIBUTING;
- the `unity` and `production` environments planned in PR #1 (M4 and S7);
- any required-review gate.

## Verdicts

| PR | Verdict |
|---|---|
| #9 | **Acceptable, follow-ups required.** The workflow is well built: SHA pins, least-privilege `permissions`, no `pull_request_target`, no forwarded credentials. But Unity account credentials reach PR-controlled code (H1). |
| #17 | **Acceptable.** Nothing weakens hardening or the release guard, and the guard gained IL2CPP and ABI checks. There are follow-ups on release signing and stripping, plus a package-name mismatch with the server. |
| #4 | **Changes needed (documentation).** It does not cover secrets handling, signed commits, enforcing branch protection, or vulnerability reporting, and there is no `SECURITY.md`. |

## PR #9: Unity compile and EditMode tests in CI (`.github/workflows/unity-tests.yml` at `22162b5`)

**What is good**
- **Action pinning.** Every action is pinned to a full commit SHA:
  - `free-disk-space` (line 26)
  - `checkout` (line 37)
  - `cache` (line 50)
  - `unity-test-runner` (line 59)
  - `upload-artifact` (line 73)

  I checked that `0ff419b913a3630032cbe0de48a0099b5a9f0ed9` is exactly the `v4.3.1` tag of `game-ci/unity-test-runner`.
- **Permissions.** Workflow-level `permissions: contents: read` (lines 10-11). The job adds only `checks: write` (lines 21-23), which the test report needs.
- **Trigger.** `on: pull_request` (lines 5-8), with **no** `pull_request_target`. Fork PRs get neither the secrets nor a write token.
- **Credentials.** `persist-credentials: false` (line 40).
- **Ordering.** The fast fail-step runs before the Unity step (lines 42-47).
- **Concurrency** is cancelled per PR (lines 13-15).
- **Dependency.** `com.unity.test-framework` 1.1.33 is an official Unity package (`Packages/manifest.json`).

**Findings**

**H1 (High): Unity account credentials are exposed to code the PR controls.**
- **Where:** `unity-tests.yml:57-63`. `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD` are set on the test-runner step, which compiles and runs EditMode test code and editor scripts from the PR branch.
- **Why it matters:** fork PRs don't get these secrets, but every same-repo branch does, and every bot can push branches (CONTRIBUTING rule 1). A buggy or compromised bot, or a malicious test, `[InitializeOnLoad]` editor script or package added in a PR, could read the environment inside the container and exfiltrate the Unity login. The comment on line 2, "only available to same-repo branches", is true, but it doesn't make this safe.
- **Fix:**
  1. Use a **dedicated Unity account** for CI, with no other assets or org rights. Rotate its password now if it was ever shared.
  2. Gate the job:
     - `if: github.event.pull_request.head.repo.full_name == github.repository`, so it skips cleanly on forks; and
     - require an approval label (`if: contains(github.event.pull_request.labels.*.name, 'ci-unity')`) that only the owner applies; or
     - run Unity tests on `push` to `main` plus `workflow_dispatch` only, and keep the dotnet logic tests on PRs.
  3. Once the plan supports it, move the secrets into a protected `unity` environment with required reviewers. That is not available on the current plan.

**M1 (Medium): CI does not run on branches pushed directly to `main`.**
- **Where:** the workflow triggers on `pull_request` only (lines 5-8). There is no branch protection to enforce it (see context), so a direct push to `main` skips Unity tests entirely.
- **Fix:** add `push: branches: [main]`, and make "EditMode test results" a required check when protection becomes available.

**L1 (Low): version comments are vague.**
- **Where:** lines 37, 50 and 73 say only `# v4`.
- **Fix:** write the exact version, for example `# v4.2.2`. Add `.github/dependabot.yml` with `package-ecosystem: github-actions` so SHA bumps are reviewed PRs.

**L2 (Low): the cache.**
- **Where:** lines 49-55. PR-scoped caches can't be read by `main`, so cross-branch poisoning is limited.
- **Fix:** keep the `Library-EditMode-` prefix separate from the Android build cache, as it is now. Don't widen `restore-keys` to `Library-`.

**L3 (Low): artifacts.**
- **Where:** `test-results` is uploaded with `if: always()` and kept 14 days (lines 71-78). Unity logs can include machine and licensing details.
- **Fix:** upload only `*.xml` results, not full editor logs. Keep the 14-day retention.

**L4 (Low): token and a third-party action.**
- **Where:** `githubToken: ${{ secrets.GITHUB_TOKEN }}` goes to a third-party action (line 68). It is scoped to `contents: read` plus `checks: write`, which is acceptable.
- `jlumbroso/free-disk-space` (line 26) is third-party. It is SHA-pinned and runs before any secret is in the environment. Acceptable.

## PR #17: arm64 fix (`39c76fa`)

**What is good**
- **Build settings.** IL2CPP and `ARMv7 | ARM64` are set in code (`Assets/_Project/Scripts/Editor/BuildScript.cs:23-25`). ProjectSettings uses the correct `AndroidTargetArchitectures: 3` key (`ProjectSettings.asset` hunk near line 30).
- **Release guard.** It now fails if ABIs are not 3 or the scripting backend is not IL2CPP (`Tools/ci/check_release_guards.py:54-57`). That strengthens it.
- **CI assertion.** CI checks that `lib/arm64-v8a/libil2cpp.so` and `libunity.so` are in the APK (`.github/workflows/android-apk.yml:108-115`).
- **Release behaviour unchanged.** Release builds still strip `CRASHVERSE_DEV` and are non-Development (`BuildScript.cs:21-22`, `:31`). `ReleaseBuildGuard` still blocks a release that would fall back to `LocalBackendService` (`BuildScript.cs:47-61`).
- Nothing in #17 weakens hardening or the release guard.

**Findings**

**M2 (Medium): release builds are debug-signed APKs.**
- **Where:**
  - `BuildScript.cs:26-32` builds an `.apk` with no keystore configuration, so Unity signs with the debug key.
  - Play requires an AAB.
  - `android-apk.yml:117-123` uploads dev and release builds under the same artifact name, `crashverse-android-apk`.
- **Fix:**
  - Add a separate release job. It should be tag- or dispatch-only, in a protected environment once one is available.
  - Build an AAB (`EditorUserBuildSettings.buildAppBundle = true`) signed with the **upload key**. Decode the keystore from secrets into a temp file and delete it afterwards. Use Play App Signing.
  - Name artifacts per profile.
  - Don't upload release builds as long-lived artifacts.

**M3 (Medium): stripping and minification are not pinned.**
- **Where:** `BuildScript.cs:23-25` pins the backend and ABIs, but not:
  - `PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.High)`, or at least Medium with a `link.xml`;
  - `PlayerSettings.SetIl2CppCompilerConfiguration(..., Il2CppCompilerConfiguration.Release)`;
  - `PlayerSettings.Android.minifyRelease = true`.

  `ProjectSettings.asset` has no stripping entries, so the Unity defaults apply.
- **Fix:** set these for the release profile in `BuildScript`, and add matching checks to `check_release_guards.py`. They raise the effort needed to reverse-engineer the APK. They are not a security boundary, because the server is authoritative.

**M4 (Medium): the package name doesn't match the server.**
- **Where:** the client's `applicationIdentifier` is `com.crashverse.game` (`ProjectSettings/ProjectSettings.asset:26-27`). The server in PR #1 expects `PackageName = "com.radhengle.crashverse"` (`server/src/Crashverse.Server/Domain/Models.cs`, `ServerOptions`).
- **Impact:** Play Integrity (`packageName` / `requestPackageName`) and Play Developer API checks would reject every genuine client. People tend to "fix" that by loosening the check.
- **Fix:** decide the final Play package name (owner). Set it in one place, and make the server read it from config. Add a CI check that the two match.

**L5 (Low): the release profile can be triggered by any writer.**
- **Where:** `android-apk.yml` `workflow_dispatch` `profile: release` can be run by anyone with write access, with the Unity secrets. There are no environment reviewers on this plan.
- **Fix:** same as H1. Once available, use a protected environment.

**L6 (Low): ARMv7 is still shipped.**
- This is fine for reach, but adds attack surface and size. Consider ARM64-only when the minimum SDK allows.

**Note: `environment: unity` is not on `main`.** It arrives with PR #1 (not merged). Until then, `android-apk.yml:91-97` uses repository secrets.

## PR #4: CONTRIBUTING.md (`9b5d750`)

**What it covers:** worktree and branch hygiene, PRs only, no force-pushes, Developer merges, local checks before a PR (`CONTRIBUTING.md:5-11`), and "don't touch workflow secrets" (line 13).

**Findings**

**M5 (Medium): no vulnerability-reporting route.**
- **Where:** there is no `SECURITY.md` in the repository, and CONTRIBUTING doesn't mention one.
- **Fix:** add `SECURITY.md`. Report privately to [[security contact]] or through GitHub private vulnerability reporting, and never in public issues or PRs. Include a response target, a scope (client, server, CI) and a safe-harbour line.

**M6 (Medium): "PRs only" and "no force-push" are not enforced.**
- **Where:** `CONTRIBUTING.md:6-7`. Branch protection is unavailable on the current plan (HTTP 403).
- **Fix:**
  - **Owner:** upgrade to GitHub Pro or Team, or make the repo public. Then enable a ruleset on `main`:
    - require PRs and 1 or more approvals;
    - require status checks (`server-ci`, `Unity compile + EditMode tests`, `Release guards`);
    - block force-pushes and deletions;
    - require linear history;
    - optionally require signed commits.
  - **Until then:** say in CONTRIBUTING that these rules are policy only, and that the owner audits `git log --first-parent main` for direct pushes.

**M7 (Medium): no secrets-handling guidance.**
- **Where:** line 13 only says don't touch workflow secrets.
- **Fix:** add a "Secrets" section:
  - never commit keys, keystores (`*.jks`, `*.keystore`), `.ulf`/Unity licence files, `.env` files, service-account JSON or tokens;
  - never print secrets in CI logs or PR text;
  - use Secret Manager or GitHub secrets only;
  - if a secret is committed, tell the owner, rotate it first, then clean history;
  - turn on secret scanning and push protection where the plan allows.

  Also extend `.gitignore` to cover those patterns.

**L7 (Low): no signed commits or bot identity.**
- **Fix:** each bot commits with its own identity, using SSH or GPG signing if available. Add `Co-authored-by` or the bot name in the trailer.

**L8 (Low): changes to security-sensitive files need a second reviewer.**
- **Where:** line 7 says Developer merges everything.
- **Fix:** changes to `.github/**`, `Tools/ci/**`, `server/**/Security*`, `Packages/manifest.json` and `ProjectSettings/**` also need Security Admin review. Add a `CODEOWNERS` file, enforceable once protection exists.

## Owner-only items
1. **GitHub plan.** Upgrade to Pro or Team, or make the repo public, to get branch protection or rulesets, required checks, and environments with required reviewers. Without this, M6, H1 option 3, and PR #1's M4 and S7 stay policy-only.
2. **Unity account.** Use a dedicated CI-only Unity account. Rotate its password, and update the three secrets.
3. **Play package name.** Decide the final name (M4).
4. **Release signing.** Create the upload keystore (Play App Signing) and store it only as protected secrets (M2).
5. **Security contact.** Provide a security contact for `SECURITY.md` (M5).

---

## Ready-to-post comments (not posted)

> These are for the parent agent or the user to post. The PRs are already merged, so post them as PR comments or open follow-up issues.

### PR #9

**Security retro-review: OK with follow-ups**

Nice work. These look good:
- every action is SHA-pinned (I checked that `unity-test-runner@0ff419b…` is exactly v4.3.1);
- `permissions: contents: read` plus only `checks: write`;
- no `pull_request_target`, and `persist-credentials: false`;
- fork PRs get no secrets.

Follow-ups:
1. **High:** `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD` (lines 57-63) are visible to editor and test code from any same-repo PR branch. Please:
   - add `if: github.event.pull_request.head.repo.full_name == github.repository`;
   - gate the Unity step behind an owner-applied label, or move it to `push: main` and `workflow_dispatch`;
   - use a dedicated CI-only Unity account.

   We can't use a protected environment on the current plan.
2. **Medium:** add `push: branches: [main]`, because nothing enforces PR-only merges today.
3. **Low:**
   - write exact versions in the pin comments (`# v4` → `# v4.x.y`);
   - add Dependabot for `github-actions`;
   - upload only the test XML, not editor logs.

### PR #17

**Security retro-review: OK**

IL2CPP and ARMv7|ARM64 are set in code, and the release guard now enforces ABI 3 and IL2CPP. The CI assertion for arm64 `libil2cpp.so` and `libunity.so` is a good addition. Nothing weakens the release guard or hardening.

Follow-ups:
- **Medium:** release builds are debug-signed APKs (`BuildScript.cs:26-32`). Add a release-only job that builds a signed AAB with the upload key and Play App Signing, and name artifacts per profile (`android-apk.yml:117-123`).
- **Medium:** pin stripping for release in `BuildScript`:
  - `ManagedStrippingLevel.High` (or Medium plus `link.xml`);
  - IL2CPP `Release` configuration;
  - `minifyRelease`.

  Add matching guard checks.
- **Medium:** `applicationIdentifier` is `com.crashverse.game` (`ProjectSettings.asset:26-27`), but the server expects `com.radhengle.crashverse`. Play Integrity and Play API package checks would reject real clients. Pick one name (owner), make the server read it from config, and add a CI check that they match.

### PR #4

**Security retro-review: please extend**

The workflow rules are clear. Please add:
1. **`SECURITY.md`** with a private reporting route ([[security contact]] or GitHub private vulnerability reporting), a response target and scope. No public issues for vulnerabilities.
2. **A "Secrets" section:**
   - never commit keys, keystores, Unity licence files, `.env` files, service-account JSON or tokens;
   - never print secrets in logs or PRs;
   - if one leaks, rotate first and then clean history.

   Extend `.gitignore` to cover these files.
3. **Branch protection.** Note that "PRs only / no force-push" isn't enforced: the API returns 403 on this plan. Once the owner upgrades, add a `main` ruleset with required PRs, required checks, no force-push, and optionally signed commits.
4. **Bot identity and review.** Each bot should commit under its own identity, signed if possible. Add a `CODEOWNERS` entry requiring Security review for `.github/**`, `Tools/ci/**`, `Packages/manifest.json`, `ProjectSettings/**` and server security code.
