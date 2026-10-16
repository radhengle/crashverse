# Audio Sync Table — Animation Spec v1 (for Sound Manager)
Frame = @60 fps from the start of the named state/trigger (negative = before). ms is authoritative; on 30-fps devices fire on the nearest frame ≥ ms. Events are raised via `AnimEventBus.Raise(name, payload)` (animation events or tween callbacks). Voice limits: impacts 4, debris 6, UI 3.

| Event | Trigger frame / ms | Source state | Description |
|---|---|---|---|
| ANIM_WRECKER_IDLE_FIDGET | 0 / 0 | Fidget | wheel creak, gold glint ting |
| ANIM_WRECKER_AIM_START | 0 / 0 | AimEnter | servo hum start (loop) |
| ANIM_WRECKER_AIM_TICK | per 5° of yaw/pitch | Aim | ratchet tick (throttle 60 ms) |
| ANIM_WRECKER_AIM_CLAMP | 0 / 0 | clamp hit | soft stop clunk |
| ANIM_WRECKER_AIM_END | 0 / 0 | AimCancel | servo hum stop |
| ANIM_WRECKER_FIRE_ANTICIPATE | 0 / 0 at FIRE tap = -4 / -66 before launch | FireAnticipation | breech clack (spawn delayed 66 ms — accepted by Developer) |
| ANIM_WRECKER_FIRE | 0 / 0 at launch (= +4 f / 66 ms after FIRE tap) | Recoil start | cannon boom + muzzle flash; cam shake |
| ANIM_WRECKER_MUZZLE_SMOKE | 2 / 33 | Recoil | air whoosh tail |
| ANIM_WRECKER_RECOIL_LAND | 12 / 200 | Recoil end | carriage thud, wheel roll |
| ANIM_WRECKER_REARM | 0 / 0 | ReArm | **no sound** (visual pulse only) |
| ANIM_WRECKER_READY | 0 / 0 when physics settled AND ≥1.0 s since shot | Ready | ready click; barrel settle + FIRE button glow; FIRE unlocks |
| ANIM_WRECKER_SLOT_OUT | 0 / 0 | SlotSwap | pop-out |
| ANIM_WRECKER_SLOT_IN | 9 / 150 | SlotSwap | click-in + slot-colour chime |
| ANIM_WRECKER_WIN / _FAIL | 0 / 0 | Win/Fail | hop boings / droop sigh |
| ANIM_PROJ_LAUNCH | 0 / 0 | Spawn | projectile whistle loop start |
| ANIM_PROJ_SPLIT_POINT | apex or 70% predicted flight time, whichever first | Flight | Splitstrike split cue (only with Splitstrike armed) |
| IMPACT_WOOD / BRICK / STEEL / STEEL_JOINT / TERRAIN | 0 / 0 | Impact | material hit (velocity → volume) |
| BREAK_WOOD / BREAK_BRICK / BREAK_STEEL_JOINT | 0 / 0 | piece HP 0 | snap / crumble / metal pop |
| EXPLODE_CRATE (alias, deprecated: EXPLODE_BARREL) | 6 / 100 | after swell | explosion |
| BREAK_STEEL | 0 / 0 | non-joint steel HP 0 | metal tear |
| DAMAGE_STEP | 0 / 0 | darken+crack step applied (payload material, step 1/2) | material crack |
| DEBRIS_BURST | 0 / 0 | DebrisPool spawn (payload material, count) | debris skitter |
| COLLAPSE_CREAK | 0–9 / 0–150 | support loss | creak |
| COLLAPSE_LAND | 0 / 0 | segment ground contact (param `material`: wood / brick / steel) | heavy thud per material |
| DEBRIS_FALL_OFF | 0 / 0 | kill-plane cross | falling whoosh |
| PU_ARM_<NAME> | 0 / 0 | PowerArmed | slot-colour arm sting |
| PU_SHOCKWAVE_IMPACT / _RING | 0 / 0, 0 / 0 | Trigger | bass thump + ring sweep (400 ms) |
| PU_SPLIT | 0 / 0 at split point | Trigger | split zing ×3 (not raised if the shot hits before the split point) |
| PU_GRAVITY_OPEN / LOOP / BURST | 0 / 0; 9 / 150 loop; 90 / 1500 | Active | vortex open, hum loop, implode+burst |
| PU_CHAIN_PULSE / PU_CHAIN_LINK | 0 / 0; +5 f (80 ms) per link | Trigger | zap per target |
| PU_PHASE_PASS / PU_PHASE_HIT | per pass-through / 3rd hit | Flight | phase shimmer / solid hit |
| PU_TIME_START / WARN / END | 0 / 0 **on arm**; 210 / 3500; 240 / 4000 real time (or END at impact of a shot fired during the slow; WARN skipped then) | Active | time crack, ticking, shatter |
| PU_DISARM | 0 / 0 | Armed → Disarm | power-down blip (not for Time Fracture) |
| BOSS_TIDE_INTRO | 0 / 0 | Intro | rise, lantern ignite |
| BOSS_TIDE_ARMS_RISE | 60 / 1000 after Intro start | Intro arms spread (1000–1800 ms, ease-out-back) | arms rise / chain creak |
| BOSS_TIDE_JOINT_HIT / JOINT_BREAK | 0 / 0 | P1 | clang / metal snap |
| BOSS_TIDE_ARMS_DROP | 0 / 0 | PhaseChange | arms fall crash (+36 f land) |
| BOSS_TIDE_ARMS_LAND | 36 / 600 | PhaseChange | arms hit sea-stack/water |
| BOSS_TIDE_CHEST_OPEN | 42 / 700 | PhaseChange | plates grind open |
| BOSS_TIDE_PHASE2 | 75 / 1250 | PhaseChange | core reveal flare |
| BOSS_TIDE_TELL | 0 / 0 (pulses at 0, 25, 50 f) | Tell | warning hum on each pulse (2.4 Hz = 144 BPM beat) |
| BOSS_TIDE_SHUTTER_CLOSE / OPEN | 0 / 0 = tween START (sound file carries its thunk at +300 ms / 18 f, end of tween) ; 0 / 0 | shutter | heavy brick slam / grind |
| BOSS_TIDE_CORE_HIT | 0 / 0 | CoreHit | glass crack + roar |
| BOSS_TIDE_DEFEAT | 0 / 0 | Defeat | collapse, clip length 4.0 s (confirmed) |
| BOSS_TITAN_INTRO / HIT / STAGE / DEFEAT | 0 / 0 | Titan states | lava rise, iron clang (flinch), stagger groan (at 25/50/75%), meltdown |
| BOSS_TITAN_RANKED_START | 0 / 0 | ranked only, before Intro | ranked sting |
| BOSS_TITAN_RULE | 0 / 0 after Intro (loop start) | Rule layer | day-rule ambience (e.g. heated-joint sizzle) |
| UI_BTN_PRESS | 0 / 0 | button (payload `type`: confirm / back / error) | click variant |
| UI_TAB / UI_TOGGLE | 0 / 0 | tab / toggle | tab swipe / switch click |
| UI_PANEL_IN / OUT | 0 / 0 | panel | swoosh up/down |
| UI_STAR_1 / 2 / 3 | 9 / 150 each (peak), stagger 18 f | win | star ding, rising pitch |
| UI_MASTERY_TICK | 0 / 0 when ✔ completes (achieved only) | Level Complete mastery row | gold ping |
| UI_COMBO_UP | 0 / 0 | combo ≥x3 | pop, pitch + N |
| UI_COMBO_TIER | 0 / 0 | x5/x10/x20 | bigger fanfare stinger |
| UI_SCORE_TICK | every 3 f / 50 ms, max 12 | count-up | ticker |
| UI_COIN_LAND | per coin 42 f / 700 ms (throttle 2/100 ms) | coin fly | coin clink |
| UI_CHEST_SHAKE / OPEN | 0 / 0; 24 / 400 | reward | rattle / burst |
| UI_CLAIM | 0 / 0 | daily | stamp |
| UI_NODE_UNLOCK | 12 / 200 (lock pop) | map | unlock chime |
| UI_MARKER_HOP | each hop landing during marker travel (3 hops) | map | hop boing |
| UI_GATE_OPEN | 0 / 0 | map | padlock + wood creak |
| UI_CHARGE_SPEND | 0 / 0 at FIRE with power-up armed | HUD power-up button | badge decrement tick |
| UI_SHOT_SPEND | 0 / 0 | HUD | soft tick |
| UI_THRESHOLD_HIT | fill reaches win tick (level `winThreshold`, default 85%) | HUD | success sting with tick pop |
| UI_WIN | 0 / 0 | win seq | fanfare + confetti |
| UI_FAIL | 0 / 0 | fail screen | soft fail sting |
| UI_HINT_BUTTON_APPEAR | 18 / 300 after Retry, only from 2nd fail in a row | fail screen | hint pop |
| UI_SEASON_TIER_UP | 0 / 0 | season track | tier fanfare |
| UI_SHARD_GAIN | 36 / 600 (land) | reward | shard chime |
| UI_EXTRA_CHARGE | 42 / 700 (land) | daily reward | charge zap |
| UI_DAMAGE_TICK | per hit, throttle 50 ms; also during kill shot-bonus count-up | Titan damage counter (0–10,000) | counter tick |
| UI_HINT_SHOW | 0 / 0 | hint | soft sparkle |
| CAM_SLOWMO_START / END | 0 / 0; 36 / 600 real (big collapse) or 30 / 500 real (boss phase break) | slow-mo | low-pass on / off; not raised in Titan ranked or with Reduce Motion |
