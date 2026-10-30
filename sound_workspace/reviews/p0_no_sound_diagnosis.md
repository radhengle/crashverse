# P0: dev-build-5 has no sound — diagnosis (Sound Manager, 2026-10-17 01:15 IST)
Read-only: gh + a temporary worktree off origin/main (22162b5), since removed. Nothing was merged or pushed.

## 1. Build source
- The dev-build-5 release/tag is 7c6c48f (run 38945003682), the head of **PR #23 dev/touch-fix** (open, not merged). 7c6c48f is not an ancestor of main.
- **PR #32 audio/library-v1** (draft, head e7befb8) is **NOT merged**. Neither main nor 7c6c48f has any .ogg/.wav, Resources/Music or Resources/SFX.
- PR #18 (PHYS_* events, ImpactAudioProfile) is also still open. main has no Scripts/Physics/ and no PHYS_* emitters.

## 2. Audio playback code on main (and 7c6c48f — identical for audio)
| Piece | State | Path |
|---|---|---|
| MusicPlayer (Resources.Load("Music/"+id), dual base/layer/stinger sources, AnimEventBus subscriber) | EXISTS | Assets/_Project/Scripts/Audio/MusicPlayer.cs, MusicCore.cs |
| Music started at boot | EXISTS: PlayBase("sunnyville_map_loop") | Scripts/Core/GameBootstrap.cs:103 |
| Missing clip handling | Silent: clip==null → StopBase, no log | MusicPlayer.cs PlayBase |
| Music import rules | EXISTS | Scripts/Editor/MusicImportRules.cs |
| SFX router/manager (AnimEventBus + PHYS_* → sfx ids) | **MISSING** | — |
| SFX player / voice pool / ImpactAudioProfile consumer | **MISSING** | — |
| AudioMixer asset (.mixer) | **MISSING**. MusicPlayer's baseGroup/layerGroup/stingerGroup are null (routes to the master, OK) | — |
| AudioListener | **MISSING in the shipped scene**. Bootstrap.unity "Main Camera" has only Transform + Camera. GameBootstrap.cs:37 adds a listener only when Camera.main == null, and the scene camera is tagged MainCamera, so no listener is ever created. | Assets/_Project/Scenes/Bootstrap.unity, GameBootstrap.cs:37 |
| Haptics | MISSING (also playtest item 7) | — |

## 3. PR #32 health
- Mergeable / CLEAN against main (`git merge-tree` shows no conflicts). 670 files, +8048, all additive.
- Contents are **assets only**: Resources/Music (26 .ogg) + Resources/SFX/** (~300 clips) + docs. **No code** (no SFX router/player, no mixer, no listener fix).
- .meta validity: every asset and folder has a .meta, every guid is 32-hex, **0 duplicate GUIDs** across the merged tree, and the importers are right (AudioImporter ×325, DefaultImporter ×17 for folders). Music loadType 2 = Streaming (matches the MusicPlayer warning check). SFX use 0/1 (DecompressOnLoad/CompressedInMemory), 15 metas have no loadType (check). No LFS: blobs are real (e.g. 10.9 KB ogg). It should import and build.
- Every music track id MusicPlayer asks for has a matching file (sunnyville_map_loop.ogg etc.). The repo-root audio/*.md/csv files are outside Assets, so they need no meta.

## 4. Build settings that could strip audio
- `ProjectSettings/AudioManager.asset` **is not committed**. Unity generates the default (Disable Unity Audio = off), so it doesn't strip audio, but it isn't pinned either. Commit it with m_DisableAudio: 0 (and DSP buffer "Best latency" for SFX).
- BuildScript.cs: scenes from EditorBuildSettings, IL2CPP ARMv7|ARM64. No Resources exclusion, no stripping of audio. .gitignore has no audio exclusion.
- The emulator can't verify audio (pcm_writei I/O errors in the HAL), so QA needs a device check.

## Root cause
1. No audio assets are in the build. PR #32 isn't merged and dev-build-5 came from the PR #23 branch, so MusicPlayer silently finds no clips.
2. Even with assets, the scene has **no AudioListener** (bootstrap fallback never triggers), so nothing would be heard.
3. No SFX playback code exists at all (no router, pool or mixer).

## Fix list
**Sound Manager**
- Take PR #32 out of draft and get it merged (it's clean and the metas are valid). Fix the 15 SFX metas with no loadType.
- Write the SFX router spec/table (AnimEventBus names + PHYS_* → sfx ids) as data (ScriptableObject/CSV) for Gameplay Dev. Build the AudioMixer (Master/Music{Base,Layers,Stingers}/SFX{Destruction,UI,Boss…}/Duck snapshots) as a .mixer asset, or spec it for Developer.
- Land the ImpactAudioProfile patch once PR #18 merges.
**Gameplay Developer**
- AudioListener: add it to the Main Camera in Bootstrap.unity, or change GameBootstrap.cs:37 to ensure one exists (`if (FindObjectOfType<AudioListener>()==null) Camera.main.gameObject.AddComponent<AudioListener>()`).
- Implement SfxRouter (subscribe AnimEventBus.Raised + PHYS_*), an SfxPlayer voice pool (12/24/32), priorities and cooldowns per ImpactAudioProfile, and Resources.Load of SFX/**.
- Wire the mixer groups into MusicPlayer and SfxPlayer. Log a warning when a music/SFX clip is missing (no silent failure). Add haptics.
**Developer (build/CI)**
- Build dev-build-6 from main after merging #23, #32 (+ the router PR), not from a feature branch.
- Commit ProjectSettings/AudioManager.asset (audio enabled).
- Add a CI APK assertion: at least N AudioClips in the build (or Resources/Music+SFX present), plus an EditMode test that Bootstrap has an AudioListener and that every MusicTracks id loads.
