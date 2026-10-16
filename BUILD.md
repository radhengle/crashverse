# CRASHVERSE — Build guide (M1)

**Engine:** Unity **2022.3.50f1** LTS (pinned in `ProjectSettings/ProjectVersion.txt`)  
**Render:** URP (`com.unity.render-pipelines.universal` in `Packages/manifest.json`)  
**Target:** Android (IL2CPP, ARM64+ARMv7 in PlayerSettings stub)

Unity Editor is **not** available on the agent box and requires a licence — do not expect local APKs from CI agents without secrets.

---

## Phone-only: get an APK from GitHub Actions

1. Create a GitHub repo and push this project (needs your go-ahead; Developer will not push without it).
2. In the repo **Settings → Secrets and variables → Actions**, add:
   - `UNITY_LICENSE` — contents of a Unity licence / `.ulf` activation file (see [GameCI docs](https://game.ci/docs/github/activation))
   - `UNITY_EMAIL` — Unity account email
   - `UNITY_PASSWORD` — Unity account password
3. Open **Actions → Build Android APK → Run workflow** (or push to `main`).
4. When the job finishes, open the run → **Artifacts → `crashverse-android-apk`**.
5. On your phone’s browser (GitHub.com → same artifact), download the zip, unpack the `.apk`, and install (allow unknown sources / install from Files).

Personal access: artifact download works in the mobile GitHub site or via email notification link if configured.

### Activating a Unity licence for GameCI

```bash
# On a machine with Docker, one-time:
# Follow https://game.ci/docs/github/activation
# You will get a .ulf licence file — paste into UNITY_LICENSE secret.
```

---

## Desktop: local Unity build

1. Install **Unity Hub** + Editor **2022.3.50f1** (Android Build Support + OpenJDK + Android SDK & NDK).
2. Open `/workspace/crashverse` (or your clone) in that Editor. Let URP packages restore.
3. Menu **Crashverse → Create Default Material & Level Assets** (optional; runtime defaults also work).
4. Open scene `Assets/_Project/Scenes/Bootstrap.unity` (AutoBootstrap also spawns systems if the component GUID remaps).
5. **File → Build Settings → Android → Switch Platform → Build**.
6. Output an APK; copy to phone via USB, Drive, or QR from a local HTTP share.

### Editor play mode

Press Play on `Bootstrap`. You get Sunnyville arena, greybox Wrecker (x=-20), Clockwork Tower (~x=10), aim (drag) + FIRE, 5 shots, win @ 85% objective mass, stars from moves remaining.

---

## Project layout (engineering)

```
Assets/_Project/
  Scripts/{Core,Destruction,Weapons,Level,UI,Perf,Editor}
  Scenes/Bootstrap.unity
  Data/{Materials,Levels}   # created via Editor menu or runtime
  ArtReference/             # copies of M1 art briefs/moodboards
design/M1_Level_Design_Contract_v0.1.md
qa/M1_Test_Cases_v0.2.md
```

Namespaces: `Crashverse.Core`, `.Destruction`, `.Weapons`, `.Level`, `.UI`, `.Perf`.

## M2 additions (Design Pack v1, Art v2, Addendum v1.1)

- Single scene (`Bootstrap.unity`) → `GameBootstrap` builds services (`Core/AppServices.cs`), the uGUI router and the 3D island map.
  `GameplayDirector` swaps between map and a run; everything is built in code, so there are no prefabs to wire.
- UI: `UI/UIFramework.cs` (DpCanvasScaler, SafeArea, router + Android back), `UI/UIFactory.cs`, `UI/Screens.cs`. Theme tokens are in
  `UI/UITheme.cs`; **Crashverse ▸ Create UI Theme** bakes `Resources/UITheme.asset`. Import Lilita One / Nunito ExtraBold into
  `Assets/_Project/Fonts/` first, or the built-in font is used.
- Content: `python3 Tools/ContentGen/gen_content.py` regenerates all JSON (levels, layouts, Titan templates, season, store).
- Tests: `~/.dotnet/dotnet run --project Tools/Tests` (55 cases) covers logic (stars SG-01/24, backend/test clock, season, combo/score/hint, store validator, Wrecker v2 dims).
  `dotnet build Tools/CompileCheck` compiles all runtime scripts against minimal Unity stubs. This is **not** a substitute for a Unity compile.
- Editor-only tasks still open: URP Volume/post effects, font import, first open in Unity to import the generated .meta files.
