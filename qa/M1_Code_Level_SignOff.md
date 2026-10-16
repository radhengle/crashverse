# CRASHVERSE: M1 code-level QA sign-off
**Build:** github.com/radhengle/crashverse origin/main @ **0d60e07**
**Date:** 16 Oct 2026 (IST). **QA verdict:** code-level sign-off **GRANTED**: 0 open Blockers, 0 open Highs, 0 open Med/Low code bugs.
Scope: code and logic only. Device, APK, Unity and real-server checks are open dependencies (below) and still need sign-off before release.

## Suites and counts (on 0d60e07)
| Suite | Result |
|---|---|
| Developer tests, `dotnet run --project Tools/Tests` | 514 passed, 0 failed (62 cases) |
| CompileCheck | 0 errors, 0 warnings |
| QA LogicTests (NUnit, /workspace/qa_automation/LogicTests) | 188: 187 passed, 0 failed, 1 skipped (BUG-18, WON'T FIX) |
| QA harness (/workspace/qa_harness_0d60e07) | 61 passed, 0 failed |
| Unity PlayMode/EditMode, visual parity | Not run (open dependency) |

Report trail: Build_0ccfa9b, Build_6e1ecac, Build_40f5286, Build_d109b07 and Build_0d60e07 QA reports.

## Closed tickets (fixed and verified)
| Ticket | Summary | Fixed in |
|---|---|---|
| SEC-01 | Ranked journal re-signed by the client; now server HMAC per-shot receipts | 6e1ecac |
| BUG-01 | csproj gitignored, so Developer tests couldn't run | 6e1ecac |
| BUG-03 | Device clock drove resets; now network server clock, blocked when unsynced | 6e1ecac |
| BUG-04 | Queued Titan kill lost its bonus | 6e1ecac |
| BUG-05 | Score floor drift | 6e1ecac |
| BUG-06 | Season never rolled over; unclaimed and premium tiers now go to the inbox | 6e1ecac |
| BUG-07 | Fake previous-season archive | 6e1ecac |
| BUG-08 | Hint only in campaign; now in events and Titan practice, never ranked | 6e1ecac |
| BUG-09 | Star thresholds rounded to whole percent | 6e1ecac |
| BUG-10 | Combo window tolerance | 6e1ecac |
| BUG-11 | Sagged piece lost its combo point (logic verified; in-scene check is in Unity) | 6e1ecac |
| BUG-12 | Misleading clock comment | 6e1ecac |
| SC-13 | Campaign leaderboard (sum of validated bests, all-time, earlier timestamp wins ties, practice and events excluded) | 6e1ecac → 40f5286 → d109b07 |
| BUG-13 | Server accepted a client-sent campaign total | 40f5286 |
| BUG-14 | Offline campaign wins lost; now queued and idempotent | 40f5286 |
| BUG-15 | `seasonEndProcessed` never set | 40f5286 |
| Online cap | Online per-shot damage trusted up to 10,000; now 5000 per shot online and offline | 40f5286 |
| BUG-16 | Leaderboard timestamp refreshed on non-improving wins | d109b07 |
| BUG-17 | 84.99% accepted as a win; now exact ≥ 8500 bp (WinRule) | d109b07 |
| BUG-19 | Mastery % goals used a float epsilon; now WinRule bp | 0d60e07 |

Spec gaps SG-00 to SG-36 are all closed (GA / ADD-GA answers), and SG-01, SG-21 to SG-24 and SG-34 to SG-36 are regression-tested. QA-UI-001 to 007 pass at code level.

## Won't fix
- **BUG-18:** campaign progress from pre-40f5286 saves isn't migrated to the server. PM decision (16 Oct 2026): nothing has been released. Developer documented these saves as unsupported.

## Info (no action required)
- StarRules uses exact decimal comparisons rather than the WinRule helper. There's no tolerance, and the results are equivalent for every current threshold. PM may choose to unify them.

## Open dependencies (not covered by this sign-off)
1. **APK / device:** QA-UI-001/002 (safe area, dpi) and QA-UI-003/004 interaction, HINT-16a (new device), performance and thermal checks, Android back gesture, the M1 test cases (all BLOCKED without an APK).
2. **Unity Editor:** PlayMode/EditMode suites (/workspace/qa_automation/UnityTests), physics checks (BUG-11 in-scene, SG-21 settle, CMB physics cases), the visual parity compare against the SMASH CITY reference board and Art v2 (/workspace/qa_automation/visual).
3. **Real backend** (currently SERVER-DEPENDENT stubs):
   - authoritative server time (placeholder host `www.google.com/generate_204`)
   - server-side validation of campaign stats and mastery (replay)
   - Titan shot simulation instead of the 5000 per-shot cap
   - HMAC key and run state held on the server rather than in a local file
   - premium purchase flow (currently `GrantPremiumPassStub`)
   - real leaderboards and friends instead of bots
4. **Reference board conflicts C-01 to C-14** (Reference_Screen_Parity_v1.md, parity 39%), plus the proposal to reopen and close VM-04/10/11. All are waiting for a PM decision; QA does not pick a side.
