# Per-release checklist
Copy it for each release. **[APPROVAL]** = needs PM + owner go-ahead.

## Build
- [ ] Flavour is right: `test` (local backend, no ranked/purchases, TEST label) for internal/closed. `release` (hosted backend) for production only, **GATE: hosting approved**
- [ ] No real-money items, no BILLING permission (v1)
- [ ] versionName/versionCode bumped (code > every previous upload), CHANGELOG updated, git tag
- [ ] `workflow_dispatch` profile=`release`: guards job green (B1 `check_release_guards.py`, release and dev compile checks, 62 logic tests)
- [ ] Output is a signed **AAB** (upload key from protected-env secrets), target SDK meets Play's minimum (35), ARM64
- [ ] Not a Development Build, `CRASHVERSE_DEV` off, no QA gear/`QADebugModal`
- [ ] `aapt2 dump permissions`: no AD_ID, permissions match DATA_SAFETY_DRAFT
- [ ] Production backend is reachable from the build (or it fails closed, as designed)
## Test
- [ ] Install from the internal track on a real phone. Run the QA smoke test (age gate, L1–L10, boss, deletion flow, offline behaviour)
- [ ] No crashes or ANRs in the pre-launch report
## Console
- [ ] Data Safety, content rating, target audience and ads declaration still accurate (re-check if SDKs changed)
- [ ] Store listing, screenshots and What's new updated
- [ ] **[APPROVAL]** Promote / send for review. Use a staged rollout for production
## After release
- [ ] Watch Android vitals and reviews for 48 h. Halt the rollout if the crash rate rises
- [ ] Back up the keystore (no change) and archive the AAB + mapping/symbols
