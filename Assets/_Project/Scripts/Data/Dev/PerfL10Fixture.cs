#if CRASHVERSE_DEV || DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Generic;
using Crashverse.Backend;

namespace Crashverse.Data.Dev
{
    /// <summary>
    /// DEV-ONLY perf fixture for the L10 full-budget collapse (vfx/PERF_TEST_L10.md, physics/PERF_L10_STAGING.md).
    /// Engine-free and deterministic: the same Ch1 L10 inputs always produce the same layout, level and scripted inputs.
    /// The fixture contains GLASS on purpose. Glass is Neon (ch3+) only in shipped content; this layout lives only in the
    /// dev perf scene (Scenes/Dev/L10_PerfFullBudget), is never written to Resources/Data/Layouts, and its id carries the
    /// "dev_" prefix that the no-glass-in-Ch1 content test excludes. Never reference this from release code.
    /// </summary>
    public static class PerfL10Fixture
    {
        public const string BaseLevelId = "c1_l10";
        public const string LayoutId = DevPrefix + "perf_l10_fullbudget";
        public const string DevPrefix = "dev_";

        /// <summary>Fixed sim seed ("L10PERF1"), fed to SimRunner.BeginRun via RunContext.seed → SimRng / DebrisPool.</summary>
        public const long Seed = 0x4C31305045524631L;

        // Scripted shot (fixed aim + power). Ticks are SimRunner ticks from BeginRun (50 Hz).
        public const int ArmTick = 25, FireTick = 50;
        public const float AimYawDeg = 0f, AimElevationDeg = 8f;
        /// <summary>"Power" = launch speed (m/s), pinned so a wrecker.json retune can't change the perf shot.</summary>
        public const float LaunchSpeed = 28f;
        public const string PowerUpId = "chain_reactor";
        public const int PowerUpSlot = 0; // Core slot
        public const string ArmInput = "arm", FireInput = "fire";

        // Curtain wall in front of the boss (local x relative to structureBase; the Wrecker fires along +x).
        // Tuned after CI run 38947452856 (2x10x8: breaks bunched, saturated_ms 200): taller and narrower so falling-time spread
        // staggers the landing breaks. Starting values; see physics/PERF_L10_STAGING.md.
        public const int Layers = 2, Columns = 6, Rows = 16;
        public const float PieceW = 0.9f, PieceH = 0.55f, PieceD = 0.9f, Gap = 0.05f;
        public const float CurtainX = -3.2f, LayerStep = 1.0f;
        /// <summary>Row materials bottom→top. Row 0 is the explosive fuse that Chain Reactor sets off.</summary>
        public static readonly string[] RowMats = { "wood", "glass", "brick", "glass", "wood", "steel", "glass", "brick", "glass", "wood", "glass", "brick", "glass", "wood", "glass", "wood" };

        /// <summary>Scripted inputs, tick-ordered (fixed-point aim via InputRecorder, like live play).</summary>
        public static InputEvent[] Script() => new[]
        {
            new InputEvent { tick = ArmTick, type = ArmInput, x = PowerUpSlot, y = 0 },
            new InputEvent { tick = FireTick, type = FireInput, x = InputRecorder.ToFixed(AimYawDeg), y = InputRecorder.ToFixed(AimElevationDeg) },
        };

        /// <summary>Ch1 L10 entry with the boss rules removed (so the collapse, not boss phases, decides the run) and glass allowed.</summary>
        public static LevelEntry Level(LevelEntry l10)
        {
            if (l10 == null) throw new ArgumentNullException(nameof(l10));
            return new LevelEntry
            {
                id = DevPrefix + l10.id + "_perf", number = l10.number, island = l10.island, name = l10.name + " (PERF)", layout = LayoutId,
                objective = "DESTROY_STRUCTURE", shots = l10.shots, winThreshold = 1.01f, // never "win" mid-capture
                materials = new[] { "wood", "brick", "steel", "glass" }, teaches = "", mastery = l10.mastery,
                forced = new SlotRule[0], lockedSlots = new string[0], requires = "", bossId = "", reward = new Reward[0],
                star3Pct = l10.star3Pct, star2Pct = l10.star2Pct, starCapIfPowerupUsed = l10.starCapIfPowerupUsed, islandKillY = l10.islandKillY,
                hintTarget = "", hintYaw = 0f, hintPitch = 0f,
            };
        }

        /// <summary>L10 blocks (copied, ids prefixed "l10_") + the mixed-material curtain. Pure function of the input.</summary>
        public static LayoutData Layout(LayoutData l10)
        {
            if (l10 == null || l10.blocks == null) throw new ArgumentNullException(nameof(l10));
            var list = new List<BlockData>();
            foreach (var b in l10.blocks)
            {
                var c = Copy(b); c.id = "l10_" + b.id;
                if (b.sup != null) { c.sup = new string[b.sup.Length]; for (int i = 0; i < b.sup.Length; i++) c.sup[i] = "l10_" + b.sup[i]; }
                list.Add(c);
            }
            float z0 = -(Columns - 1) * (PieceD + Gap) * 0.5f;
            for (int l = 0; l < Layers; l++)
                for (int r = 0; r < Rows; r++)
                    for (int col = 0; col < Columns; col++)
                    {
                        string mat = RowMats[r % RowMats.Length];
                        list.Add(new BlockData
                        {
                            id = CurtainId(l, r, col), mat = mat,
                            p = new[] { CurtainX + l * LayerStep, PieceH * 0.5f + r * (PieceH + Gap), z0 + col * (PieceD + Gap) },
                            s = new[] { PieceW, PieceH, PieceD },
                            sup = r == 0 ? new string[0] : new[] { CurtainId(l, r - 1, col) },
                            joint = false, role = r == 0 ? "fuse" : "", nonObj = false, color = "", explosive = r == 0,
                            indestructible = false, spinAxis = new float[0], pivot = new float[0], spinDps = 0f,
                        });
                    }
            float zSpan = (Columns - 1) * (PieceD + Gap);
            for (int i = 0; i < DropPanes; i++)
            {
                int col = i % Columns; // held by layer-0 fuse block in this column
                list.Add(new BlockData
                {
                    id = DropId(i), mat = "glass",
                    p = new[] { CurtainX + (i % Layers) * LayerStep, DropHeight(i), z0 + zSpan * ((i * 7) % DropPanes) / (DropPanes - 1f) },
                    s = new[] { 0.7f, 0.12f, 0.7f },
                    sup = new[] { CurtainId(0, 0, col) },
                    joint = false, role = "drop", nonObj = true, color = "", explosive = false,
                    indestructible = false, spinAxis = new float[0], pivot = new float[0], spinDps = 0f,
                });
            }
            for (int i = 0; i < AnvilPairs; i++)
            {
                int side = i % 2 == 0 ? 1 : -1, k = i / 2; // alternate sides so neighbouring landing times are far apart
                float x = AnvilX0 + (k % AnvilCols) * AnvilStep, z = side * (AnvilZ0 + (k / AnvilCols) * AnvilStep);
                list.Add(new BlockData
                {
                    id = AnvilId(i), mat = "glass", p = new[] { x, AnvilH * 0.5f, z }, s = new[] { 0.7f, AnvilH, 0.7f },
                    sup = new string[0], joint = false, role = "anvil", nonObj = true, color = "", explosive = false, // grounded: kinematic until broken
                    indestructible = false, spinAxis = new float[0], pivot = new float[0], spinDps = 0f,
                });
                list.Add(new BlockData
                {
                    id = HammerId(i), mat = "steel", p = new[] { x, AnvilH + HammerH * 0.5f + HammerFall(i), z }, s = new[] { 0.5f, HammerH, 0.5f },
                    sup = new[] { CurtainId(0, 0, i % Columns) }, joint = false, role = "hammer", nonObj = true, color = "", explosive = false,
                    indestructible = false, spinAxis = new float[0], pivot = new float[0], spinDps = 0f,
                });
            }
            return new LayoutData { id = LayoutId, blocks = list.ToArray() };
        }

        /// <summary>
        /// Hammer/anvil pairs (tuning after CI run 38950649389: the fuse burst ends in window 9, the curtain tail is patchy).
        /// Each steel hammer hangs from a fuse block over a grounded glass anvil, beside the curtain (|z| ≥ 5.2 m: > 2.5 m explosion radius from every fuse, off the
        /// shot line). When the fuses go, hammer i lands after HammerT(i) s and breaks its anvil. Debris damage (ruling #7a)
        /// = 0.5 × 4 kg × (v − 2 m/s) × 1.5 (glass) ≥ 12 HP needs v ≥ 6 m/s, i.e. ≥ 0.61 s of fall, so a break by falling can't
        /// land earlier than ~0.6 s after the fuse (windows 10–14 can't be filled by gravity drops; see PERF_L10_STAGING.md).
        /// HammerT spans [0.7, 2.2] s: 60 pairs over 1.5 s = 4 breaks per 100 ms on top of the drop panes and rubble.
        /// </summary>
        public const int AnvilPairs = 60, AnvilCols = 6;
        public const float HammerT0 = 0.7f, HammerT1 = 2.2f, AnvilH = 0.12f, HammerH = 0.25f, AnvilX0 = -5.2f, AnvilZ0 = 5.2f, AnvilStep = 0.8f;
        public static float HammerT(int i) => HammerT0 + (HammerT1 - HammerT0) * i / (AnvilPairs - 1);
        public static float HammerFall(int i) { float t = HammerT(i); return 0.5f * G * t * t; }
        public static string AnvilId(int i) => $"perf_anvil_{i:00}";
        public static string HammerId(int i) => $"perf_hammer_{i:00}";

        /// <summary>
        /// Drop panes (tuning after CI run 38949077290: the fuse burst gives ~110 breaks in 2 windows, then a patchy tail).
        /// Glass panes float over the curtain, each held by one fuse block; when the fuse goes they fall onto the dynamic rubble.
        /// Heights h = g t^2 / 2 spread the landing times evenly over [DropT0, DropT1] s, so breaks keep coming at about
        /// DropPanes / (DropT1 - DropT0) per second (48 / 1.2 s = 4 per 100 ms). Ground has no rigidbody, so panes must land on rubble.
        /// </summary>
        public const int DropPanes = 48;
        public const float DropT0 = 0.9f, DropT1 = 2.1f, G = 9.81f;
        public static float DropHeight(int i) { float t = DropT0 + (DropT1 - DropT0) * i / (DropPanes - 1); return 0.5f * G * t * t; }
        public static string DropId(int i) => $"perf_drop_{i:00}";

        public static string CurtainId(int layer, int row, int col) => $"perf_L{layer}_R{row:00}_C{col}";
        public static bool IsDevLayout(string id) => id != null && id.StartsWith(DevPrefix, StringComparison.Ordinal);

        static BlockData Copy(BlockData b) => new BlockData
        {
            id = b.id, mat = b.mat, p = (float[])b.p?.Clone(), s = (float[])b.s?.Clone(), sup = (string[])b.sup?.Clone(), joint = b.joint, role = b.role,
            nonObj = b.nonObj, color = b.color, explosive = b.explosive, indestructible = b.indestructible,
            spinAxis = (float[])b.spinAxis?.Clone(), pivot = (float[])b.pivot?.Clone(), spinDps = b.spinDps,
        };
    }

    /// <summary>Engine-free capture stats for the perf run: break-rate saturation and a determinism hash of the PHYS_* stream.</summary>
    public sealed class PerfL10Stats
    {
        /// <summary>VFX break-FX limiter: 3 per 100 ms = 3 per 5 ticks at 50 Hz.</summary>
        public const int WindowTicks = 5, BreaksPerWindow = 3;
        readonly SortedDictionary<int, int> _breaksPerWindow = new SortedDictionary<int, int>();
        ulong _hash = 14695981039346656037UL; // FNV-1a 64
        public int Breaks { get; private set; }
        public int Events { get; private set; }
        public int PeakDebris { get; private set; }
        public int CollapseStarts { get; private set; }
        public int CollapseEnds { get; private set; }
        public readonly SortedDictionary<string, int> BreaksByMaterial = new SortedDictionary<string, int>(StringComparer.Ordinal);
        /// <summary>NOTE: read by VFX by reflection (vfx/perf-l10-integration). Keep the name and type stable.</summary>
        public ulong Hash => _hash;
        /// <summary>First cannon PHYS_IMPACT of the scripted shot (tick, height in local metres, curtain row). -1 = no hit yet.</summary>
        public int HitTick { get; private set; } = -1;
        public float HitY { get; private set; }
        public int HitRow => HitTick < 0 ? -1 : (int)Math.Floor(HitY / (PerfL10Fixture.PieceH + PerfL10Fixture.Gap));
        public bool Hit => HitTick >= 0;

        /// <summary>Record the first cannon impact (source Cannon = 0). y is world height (ground at 0).</summary>
        public void OnCannonImpact(int tick, float y) { if (HitTick < 0) { HitTick = tick; HitY = y; } }

        public void Reset()
        {
            _breaksPerWindow.Clear(); BreaksByMaterial.Clear(); _hash = 14695981039346656037UL;
            Breaks = Events = PeakDebris = CollapseStarts = CollapseEnds = 0; HitTick = -1; HitY = 0f;
        }

        /// <summary>Fold one PHYS_* event (kind as its enum byte) into the hash; count breaks per window from the fire tick.</summary>
        public void OnEvent(int tick, int kind, int pieceId, int cascadeId, string materialA, int fireTick)
        {
            Events++;
            Mix(tick); Mix(kind); Mix(pieceId); Mix(cascadeId);
            if (kind == 2) // ImpactKind.Break
            {
                Breaks++;
                int w = Math.Max(0, tick - fireTick) / WindowTicks;
                _breaksPerWindow[w] = _breaksPerWindow.TryGetValue(w, out var n) ? n + 1 : 1;
                BreaksByMaterial[materialA ?? "?"] = BreaksByMaterial.TryGetValue(materialA ?? "?", out var m) ? m + 1 : 1;
            }
            else if (kind == 4) CollapseStarts++;
            else if (kind == 5) CollapseEnds++;
        }

        public void SampleDebris(int active) { if (active > PeakDebris) PeakDebris = active; }

        /// <summary>Longest run of consecutive windows with ≥ BreaksPerWindow breaks, in ms (target ≥ 1000).</summary>
        public int SaturatedMs
        {
            get
            {
                int best = 0, run = 0, prev = int.MinValue;
                foreach (var kv in _breaksPerWindow)
                {
                    if (kv.Value >= BreaksPerWindow) { run = kv.Key == prev + 1 ? run + 1 : 1; prev = kv.Key; if (run > best) best = run; }
                    else { run = 0; prev = int.MinValue; }
                }
                return best * WindowTicks * 20;
            }
        }

        public int MaterialsBroken => BreaksByMaterial.Count;

        /// <summary>Breaks per 100 ms window from the shot, first n windows ("3.0.12..."), for tuning the fixture.</summary>
        public string WindowHistogram(int n = 40)
        {
            var sb = new System.Text.StringBuilder();
            for (int w = 0; w < n; w++) { if (w > 0) sb.Append('.'); sb.Append(_breaksPerWindow.TryGetValue(w, out var c) ? c : 0); }
            return sb.ToString();
        }

        void Mix(int v) { unchecked { for (int i = 0; i < 4; i++) { _hash ^= (byte)(v >> (8 * i)); _hash *= 1099511628211UL; } } }

        public string Summary(int run, long seed, string tier) =>
            $"PERFL10_SUMMARY,run={run},seed=0x{seed:X16},tier={tier},hit={(Hit ? 1 : 0)},hit_tick={HitTick},hit_y={HitY:0.00},hit_row={HitRow},hash={_hash:X16},events={Events},breaks={Breaks},saturated_ms={SaturatedMs}," +
            $"peak_debris={PeakDebris},collapse_start={CollapseStarts},collapse_end={CollapseEnds},mats={string.Join("|", Mats())},windows={WindowHistogram()}";

        IEnumerable<string> Mats() { foreach (var kv in BreaksByMaterial) yield return kv.Key + ":" + kv.Value; }
    }
}
#endif
