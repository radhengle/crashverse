# integration/v2 status (Dev lead running checklist; resume from here)
Updated: 2026-10-30 23:50 IST.

## Merges
- [x] wip/graphics a11b6b3, wip/vfx 453fba5, wip/anim 7ed1a90, wip/sound 30b01eb
- [x] wip/levels c2e9945 (levels/s2_s4_on_hold/ excluded)
- [x] physics/chunked-destruction 3770394, physics/joint-break-policy 3806245 (sim version bumped: sim-3-jointpolicy)
- [x] 3d-modeler/realistic-v1 8c30262, wip/art 0e3e109
- [x] PR #57 rev 9.3 (3169c0b; GitHub shows MERGED)
- [x] release/docs-v1 c08c3f2 (8 docs files)

## Tasks
- [x] Hide unfinished features (FeatureFlags; map = Ch1 L1-10 + Daily Boss)
- [x] MED: locked node under the star counter (map viewport inset under header)
- [x] Cannon camera behind cannon; per-device top margin max(0.06,(safeTop+(104+8)dp*dpi/160)/H); hint arcs below HUD; 35%-width check only w/h<0.7
- [x] V2-01 assetsPaths (configChanges + compileSdk 36 + pinned aapt2 8.7.3); 10x API34 (1 google_apis) + API36 smoke
- [x] 4 previously failing tests fixed (+ foliage instancing postprocessor)
- [x] Loader gaps 1,2,3,5,6,7 + modelOffset; gap 4 (hero slot destructible) superseded: no level uses heroSlot fbx, buildings are block modules
- [x] Mockup side-by-side in /workspace/compare/ (compare_*.png + hud_L1-10_boss_1080x2340_api36.png, run 41090876169)
- [x] #55 comment + re-review request (issuecomment-6529338227)

## CI
- 097cf3a: APK (dev+release+11 emulator), AAB, EditMode, server-ci all green (run 41090876169)
- 6f25553+: smoke [CAM] gate fix; PR run pending

## Old draft PRs (2026-10-30 night)
- #3 dev/developer-wip: folded music loop data only (76673af: loop table = shipped .ogg, Tidewarden 144 BPM, Titan 150 BPM). Age bands already in (f3fc100). Skipped: InstallId / ageToken / /v1 routes (online backend; offline v1 uses OfflineBackendService; its InstallId used security-crypto, which Security rejected), 2.4 Hz shutter (superseded by the 2 Hz tell spec).
- #10 dev/gem-pack-flag: already folded (03d6001).
- #21 release/apk-hardening: already folded (b7702a7 backup/dataExtractionRules, 9444737 check_apk asserts + portrait); root txt dumps not taken.
- #24 art/age-gate-v2 and #38 ui-dev/age-gate-final: not folded. Age gate restyle is outside the L1-10/boss scope for dev-build-8. PrivacyUrlBuildGuard would fail every release build until Security supplies the privacy URL. #38's year picker anchor (current-18) conflicts with Security's neutral anchor (#52).
- [ ] close comments on #3 #10 #21 #24 #38 (branches kept)
- [ ] CI green on the final head
