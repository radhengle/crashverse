# CRASHVERSE Art v2: Age gate + under-13 block (QA V-01/V-02/V-03)
Canvas 1080×2340 portrait (360×780 dp, 3 px = 1 dp). Safe area: top 158 px (130 px scaled to 2340 height, 52.7 dp) plus 30 px sides and 72 px bottom (24 dp). The runtime uses Screen.safeArea; these numbers are the mockup minimums. Build script: `src/build.py` (PIL). Conventions follow v1/ui_system.md and v2/OVERLAYS.md. **Everything except the background is a PIL placeholder. The engine builds it as live UI. Never bake it into the art.**

## Background (V-03)
`slices/bg_age_gate_1080x2340.png`: Canva text-to-image (media MAHYMGFlZ98), original art with floating islands, sky and a navy fade at the bottom. Raw source: `src/bg_canva_raw.jpg` (944×1680), scaled to 2340 high and centre-cropped. For other aspects, use an anchor top-centre and cover-scale.

## Layout (px / dp, from the top-left)
| Element | Rect px (x,y,w,h) | dp |
|---|---|---|
| Title logo (`title_logo.png`, Lilita One 190 + extrude) | centred, y 218, w ≤980 | y 73 |
| Panel (9-slice) | 60,760,960,1180 | 20,253,320,393 |
| Ribbon "BEFORE YOU PLAY" (violet, gold 6 px border) | 240,714,600,96 | 80,238,200,32 |
| Heading "Enter your birth month and year" (Rubik Medium 46) | centre y 910 | 303 |
| Sub "This helps us set up the right game for you." (Rubik 36 = 12 sp) | centre y 975 | 325 |
| Month field | 108,1060,420,168 | 36,353,140,56 |
| Year field | 552,1060,420,168 | 184,353,140,56 |
| Error text slot (Rubik Medium 38, #FF6B6B, with a ! icon) | centre y 1290, h 60 reserved | 430 |
| Continue button | 108,1400,864,150 (+14 lip) | 36,467,288,50 |
| Hint "Choose both to continue" (36, only while disabled) | centre y 1610 | 537 |
| Privacy Policy link (38, underlined, tap area ≥144 px tall) | centre y 1820 | 607 |
| Year bottom sheet | y 1180 → bottom, full width, 30 px radius | 393 |
| Sheet rows | 960×144 each (48 dp), list area y 1400 → 2248 | |

## Colours
Navy panel gradient #1E2852→#0E122A, field fill #0A0E22 (92%), gold frame #FFDE78→#AA701E, gold #F4BE40, orange #FF7A1A (enabled CTA, top #FFAA46), cyan #22D3EE (focused field, pressed row tint at 24% alpha), violet #8B5CF6 (ribbon), charcoal #262A36, disabled CTA #5C6480→#424862 with text #AAB0C4, error #FF6B6B, body #FFFFFF, muted #C8D2EB, placeholder #8C96B9.

## Fonts
Lilita One (titles, buttons and values, 54–66 px) and Rubik (body, Medium/Regular/Bold, 36 px minimum = 12 sp). Field labels are Rubik Bold 36.

## States (mockups/)
1. `01_empty_disabled`: both fields read "Tap to choose", the CTA is disabled, and the hint is shown.
2. `02_year_picker_open`: a bottom sheet "BIRTH YEAR" over a 66% navy dim. The Year field is focused (cyan). **2008 is in the centre row with no highlight, tick or bold.**
3. `03_both_set_enabled`: March / 2009, gold borders, orange CTA enabled.
4. `04_error`: fields with a red border and an error line (example: a future date, Dec 2026). The CTA stays disabled until the value changes.
5. `05_under13_block`: medallion, "THANKS FOR STOPPING BY!", "CRASHVERSE isn't available for your age right now. We hope to see you again when you're older!", and a Privacy Policy link for grown-ups. **There's no back, retry or edit control.**

## Behaviour notes
- **No preselection, ever.** Fields start empty and no picker row is selected on open. Neutral copy only: don't hint at a "right" age, and there's no default.
- **Scroll anchor (V-02):** the year list (current year down to current year minus 100, descending) opens scrolled so that *currentYear − 18* (2008 in 2026) sits centred in the viewport. The anchor is computed from the device date, not hard-coded. Centring is scroll position only and is not a selection. Selection happens only on a row tap, which closes the sheet. Reopening after a choice shows that row in the `selected` state, scrolled to it.
- Month picker: the same sheet, with 12 rows (January–December), opening at the top and nothing selected.
- Continue is disabled until both month and year are set (alpha-free disabled art, not just transparency). Tap targets: fields 168 px, rows 144 px, CTA 150 px, close 96 px plus padding to 144.
- Errors: a future month/year, or a service failure ("Something went wrong. Please try again."). Errors render in the reserved slot, so the layout doesn't jump.
- Under 13: persist the block flag locally. On relaunch, go straight to the block screen and never show the gate again on this install. Store only the age band (`under13` / `13to15` / `16to17` / `adult`); never store the birth month/year. Security Admin owns the final retention wording.
- Reduce motion: the sheet appears with a fade instead of a slide.

## Asset list (slices/)
| File | Size | Use |
|---|---|---|
| bg_age_gate_1080x2340.png | 1080×2340 | background |
| title_logo.png | ≤980 wide, RGBA | title (stand-in; the official transparent wordmark follows in a later commit on this branch) |
| panel_9slice_960x1180.png | 1000×1220 (20 px shadow pad) | panel; 9-slice borders 64 px |
| panel_9slice_src_240.png | 280×280 | compact 9-slice source, borders 64 px |
| frame_gold_960x1180.png | 1000×1220 | gold frame only, transparent centre |
| btn_continue_{enabled,pressed,disabled}_864x164.png | 864×164 (150 + 14 lip) | CTA states; label is live text |
| picker_field_{empty,filled,focused,error}_420x168.png | 420×168 | field states (sample text baked; engine uses the frame + live text) |
| picker_row_{normal,pressed,selected}_960x144.png | 960×144 | list rows (sample "2008") |
| block_medallion_300.png | 300×300 | block screen emblem |

## Sign-offs (PM, 2026-10-16)
- Why-we-ask copy: **APPROVED**.
- Data: only the age band is stored (under13/13to15/16to17/adult), never the birth date. Final wording: Security Admin.
- Logo: `title_logo.png` is a stand-in; the official transparent wordmark lands in a later commit on `art/age-gate-v2`.
- Review flow: QA + Security review, then Developer merges.
