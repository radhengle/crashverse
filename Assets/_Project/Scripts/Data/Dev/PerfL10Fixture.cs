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
        public const int Layers = 2, Columns = 8, Rows = 10;
        public const float PieceW = 0.9f, PieceH = 0.55f, PieceD = 0.9f, Gap = 0.05f;
        public const float CurtainX = -3.2f, LayerStep = 1.0f;
        /// <summary>Row materials bottom→top. Row 0 is the explosive fuse that Chain Reactor sets off.</summary>
        public static readonly string[] RowMats = { "wood", "glass", "brick", "glass", "wood", "steel", "glass", "brick", "glass", "wood" };

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
            return new LayoutData { id = LayoutId, blocks = list.ToArray() };
        }

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
        public ulong Hash => _hash;

        public void Reset()
        {
            _breaksPerWindow.Clear(); BreaksByMaterial.Clear(); _hash = 14695981039346656037UL;
            Breaks = Events = PeakDebris = CollapseStarts = CollapseEnds = 0;
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

        void Mix(int v) { unchecked { for (int i = 0; i < 4; i++) { _hash ^= (byte)(v >> (8 * i)); _hash *= 1099511628211UL; } } }

        public string Summary(int run, long seed, string tier) =>
            $"PERFL10_SUMMARY,run={run},seed=0x{seed:X16},tier={tier},hash={_hash:X16},events={Events},breaks={Breaks},saturated_ms={SaturatedMs}," +
            $"peak_debris={PeakDebris},collapse_start={CollapseStarts},collapse_end={CollapseEnds},mats={string.Join("|", Mats())}";

        IEnumerable<string> Mats() { foreach (var kv in BreaksByMaterial) yield return kv.Key + ":" + kv.Value; }
    }
}
#endif
