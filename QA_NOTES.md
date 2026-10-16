# CRASHVERSE — QA notes (M1 vertical slice)

**Level id:** `m1_clockwork_tower`  
**Contract:** `design/M1_Level_Design_Contract_v0.1.md`  
**Test cases:** `qa/M1_Test_Cases_v0.2.md` (AC-01…AC-12)

## What to test first

1. Cold launch → HUD shows **Shots 5/5**, Clockwork Tower, DESTROY_STRUCTURE.
2. Drag to aim, release without FIRE → moves still **5** (aim cancel free).
3. FIRE once → moves **4**; projectile flies from booster (+0.2 forward).
4. Wood wing breaks on solid hit (HP 30); brick tougher (60); steel face spongy (120); **steel ring-joints** take **2×** damage.
5. Breaking a steel ring drops the segment above (support graph).
6. Reach ≥**85%** objective mass, wait settle (~0.75s quiet) → **TOWER DOWN** + stars.
7. Exhaust 5 shots under 85% → fail + **RETRY** (full 5, clean structure, no leftover debris).
8. Stars on win only: 0–1 left → 1★; 2–3 → 2★; 4–5 → 3★. Best stars persist across retries.
9. Quality Mid/Low toggle: 60 vs 30 target frame rate; debris capped / fades.
10. Give Up → fail path.

## Known stubs / limitations

| Item | Status |
|---|---|
| uGUI art-directed HUD | IMGUI placeholder (shots / % / stars / fire / retry) |
| ScriptableObject assets on disk | Runtime defaults used; Editor menu can bake `.asset` files |
| URP asset / renderer in scene | Package referenced; full URP pipeline asset may need Hub create on first open |
| Trajectory prediction line | Gizmo in editor only |
| Explosive barrel | Decorative NonObjective (no chain reaction yet) |
| Audio / particles / camera shake | Not in M1 |
| Wrecker modules gameplay | Visual Core/Module/Booster only; default projectile damage |
| Soft-lock auto-fail | Partial — rely on 0 moves fail for M1 |
| Real device perf AC-11/12 | Needs APK + hardware; CI blocked on Unity licence secrets |
| GitHub repo / push | **Not done** — needs user approval |

## Build blockers for QA APK

1. **Unity licence** for GameCI (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`).
2. **GitHub repository** + secrets + workflow run (Developer will not push without go-ahead).
3. No Unity Editor on the agent box — cannot produce a local APK here.

## Bug reports

Use the template in `qa/M1_Test_Cases_v0.2.md`. Route all bugs through Project Manager.

## M2: QA tickets addressed

| ID | Fix |
|---|---|
| QA-UI-001/002 | `DpCanvasScaler` (scale = dpi/160, min 320dp width) + `SafeArea`; tap targets ≥48dp and text ≥12sp enforced by UITheme/UIFactory (unit-tested) |
| QA-UI-003 | `AimController` ignores pointers over UI (EventSystem) |
| QA-UI-004 | Android back: closes modal → pause in a level → back a screen → confirm-quit at map root |
| QA-UI-005 / SG-01 / SG-24 | Stars from % shots left with per-level thresholds, integer maths; 4/5/6/7-shot tables tested |
| QA-UI-006 | `campaign_save.json` = campaign progress only; wallet/rewards/ranked/season via `IBackendService` |
| QA-UI-007 | Live-ops screens (Titan, Daily, Season/Event, Leaderboards) |
| SG-20/21/22/23 | Server UTC only; islandKillY; inclusive 15-min grace; event stars = best single run |

## M2: how to test live-ops
Map ▸ ⚙ opens QA Tools: advance the server test clock +1h/+1d/+7d, jump to 23:59:50 UTC, clear the clock, toggle the backend offline.
Times are shown in device local time with a zone label (e.g. IST) and reset at 00:00 UTC (05:30 IST).

## M2: stubbed
- IAP (the store shows validated SKUs; buying shows a toast). Account linking is a local flag. Friends on boards are fake bots.
- Real server: implement `IBackendService` and set `BackendLocator.Current`. Request signing is an FNV checksum, not HMAC.
- Audio/VFX polish, post effects, final fonts and icons (emoji placeholders), map art beyond greybox islands.
