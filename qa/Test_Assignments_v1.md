# CRASHVERSE test assignments v1 (Test lead: QA), 2026-10-17
Source cases: Feature_Test_Cases_v1.4.md, Security_Test_Cases_v1.md, M1_Test_Cases_v0.3. PR status: PR_Approvals.md.
Rules: each tester writes results per case (PASS/FAIL/BLOCKED with evidence) and tickets (severity, repro, expected/actual, file:line). Post 'QA:' comments on PRs (comments only, never merge). Report to the Test lead; the Test lead gives final sign-off. A merge needs a green CI emulator gate (Test Automation Engineer).

| Tester | Pair | Cases | Open PRs today |
|---|---|---|---|
| Gameplay Tester | Gameplay Developer | A stars, B Ch1 levels, C L10 Tidewarden, D power-ups, I Free Hint, J combo, K campaign score, N Level Rulings (LR-01..12), M1 core cases | #5, #16 Rules; #12, #13 level data; #19 economy rules logic |
| UI Tester | UI Developer | M Art v2 decisions (ART-01..06), F daily rewards UI, G seasons UI, the UI_UX_Checklist, Reference_Screen_Parity_v3 (screens vs the user's board/Art v2), age gate | #38, #24 age gate; #3 age gate part |
| Physics Tester | Physics Developer | win ≥85% after settle, settle/collapse, debris damage, determinism (50 Hz, seeded RNG), 60/30 FPS perf | #6, #11, #14 |
| Backend Tester | Backend Engineer | E Daily Titan, H leaderboards (incl. LB-30/31), L2 refunds (ST-20..33, deferred for v1), M Economy v1 (EG-01..22, server side), SEC-TC-60..64 | #1, #10 (gem flag), #19 (server side), #3 (/v1 routes) |
| Graphics Tester | Graphics Dev + VFX + 3D Modeler | BUG-23 magenta / missing materials, URP pipeline asset, shader variants, particle budget/overdraw, tri/LOD budgets, impact-feel visuals | #2, #31, #28, #18, #20 |
| Test Automation Engineer | CI | emulator-smoke gate (API 34+36), BUG-24 win line, the magenta renderer check, fail-on-zero tests | #23, #34, #7, #8, #21, #22 (CI/build) |
| Playtester | none | player-experience verdict on each dev build (dev-build-5 now) | none |
