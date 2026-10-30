using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Crashverse.Sim.Tests
{
    /// <summary>EditMode: chunked destruction core (physics/Chunked_Destruction.md). Engine-free.</summary>
    public class ChunkedDestructionTests
    {
        const int Mid = ChunkBudget.TierMid, Low = ChunkBudget.TierLow, High = ChunkBudget.TierHigh;

        static ChunkSpec[] Layout(ChunkKind k, int sx, int sy, int sz, int blockId, int tier, out int n)
        {
            var buf = new ChunkSpec[32];
            n = ChunkLayout.Generate(k, sx, sy, sz, ChunkLayout.SeedFor(blockId), ChunkBudget.ChunksFor(k, tier), buf);
            return buf;
        }

        static ulong Hash(ChunkSpec[] c, int n)
        {
            ulong h = 1469598103934665603UL;
            void Mix(int v) { unchecked { h ^= (uint)v; h *= 1099511628211UL; } }
            for (int i = 0; i < n; i++) { Mix(c[i].cx); Mix(c[i].cy); Mix(c[i].cz); Mix(c[i].sx); Mix(c[i].sy); Mix(c[i].sz); Mix((int)c[i].kind); Mix(c[i].order); Mix(c[i].delayTicks); }
            return h;
        }

        // ───────────── layout determinism ─────────────

        [Test] public void Layout_SameSeed_IsIdentical_AndPinned()
        {
            var a = Layout(ChunkKind.StoneBlock, 3400, 400, 2000, StableId.Of("slabL"), Mid, out int na);
            var b = Layout(ChunkKind.StoneBlock, 3400, 400, 2000, StableId.Of("slabL"), Mid, out int nb);
            Assert.AreEqual(na, nb);
            Assert.AreEqual(Hash(a, na), Hash(b, nb));
            // Golden: pins integer layout output so any change to the generator (or a platform difference) fails CI.
            Assert.AreEqual(8, na);
            Assert.AreEqual(GoldenSlabL, Hash(a, na), "golden layout hash for slabL 3.4×0.4×2.0 m stone");
        }
        internal const ulong GoldenSlabL = 14562312337329458971UL;

        [Test] public void Layout_DifferentBlocks_DifferentCuts()
        {
            var a = Layout(ChunkKind.StoneBlock, 4000, 2400, 400, StableId.Of("wallA"), Mid, out int na);
            var b = Layout(ChunkKind.StoneBlock, 4000, 2400, 400, StableId.Of("wallB"), Mid, out int nb);
            Assert.AreEqual(na, nb, "same size → same grid");
            Assert.AreNotEqual(Hash(a, na), Hash(b, nb), "jittered cuts differ per block id");
        }

        [TestCase(ChunkKind.StoneBlock, 3400, 400, 2000)]
        [TestCase(ChunkKind.StoneBlock, 400, 2400, 4000)]
        [TestCase(ChunkKind.RoofTile, 3600, 300, 2400)]
        [TestCase(ChunkKind.TimberBeam, 400, 2800, 400)]
        [TestCase(ChunkKind.SteelPlate, 300, 3000, 1200)]
        [TestCase(ChunkKind.GlassShard, 2000, 1500, 80)]
        public void Layout_TilesTheBlockExactly_AndRespectsMinSize(ChunkKind k, int sx, int sy, int sz)
        {
            var c = Layout(k, sx, sy, sz, 12345, Mid, out int n);
            Assert.Greater(n, 1);
            Assert.LessOrEqual(n, ChunkBudget.ChunksFor(k, Mid));
            long vol = 0;
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(k, c[i].kind);
                vol += (long)c[i].sx * c[i].sy * c[i].sz;
                Assert.Greater(c[i].sx, 0); Assert.Greater(c[i].sy, 0); Assert.Greater(c[i].sz, 0);
                if (sx >= 2 * ChunkLayout.MinChunkMm) Assert.GreaterOrEqual(c[i].sx, Math.Min(sx, ChunkLayout.MinChunkMm), "x ≥ min");
                Assert.LessOrEqual(Math.Abs(c[i].cx) * 2, sx); Assert.LessOrEqual(Math.Abs(c[i].cy) * 2, sy); Assert.LessOrEqual(Math.Abs(c[i].cz) * 2, sz);
            }
            Assert.AreEqual((long)sx * sy * sz, vol, "chunks tile the block volume exactly (no gaps, no overlap)");
        }

        [Test] public void Layout_KindShapes()
        {
            // roof: the thin axis (y) is never cut → tiles
            var r = Layout(ChunkKind.RoofTile, 3600, 300, 2400, 7, Mid, out int nr);
            for (int i = 0; i < nr; i++) Assert.AreEqual(300, r[i].sy, "roof tiles keep full thickness");
            // timber: beams stay long along the longest axis (at most 2 pieces along it)
            ChunkLayout.Grid(ChunkKind.TimberBeam, 400, 2800, 400, ChunkBudget.ChunksFor(ChunkKind.TimberBeam, Mid), out _, out int ny, out _);
            Assert.LessOrEqual(ny, 2);
            var t = Layout(ChunkKind.TimberBeam, 400, 2800, 400, 7, Mid, out int nt);
            for (int i = 0; i < nt; i++) Assert.Greater(t[i].sy, Math.Max(t[i].sx, t[i].sz), "beam is longest along the member");
            // steel: a few plates
            Layout(ChunkKind.SteelPlate, 300, 3000, 1200, 7, Mid, out int ns);
            Assert.LessOrEqual(ns, 3);
        }

        [Test] public void Layout_TierBudgets()
        {
            Layout(ChunkKind.StoneBlock, 4000, 2400, 2000, 9, Low, out int low);
            Layout(ChunkKind.StoneBlock, 4000, 2400, 2000, 9, Mid, out int mid);
            Layout(ChunkKind.RoofTile, 4000, 300, 4000, 9, High, out int high);
            Assert.LessOrEqual(low, ChunkBudget.PerBreak(Low));
            Assert.LessOrEqual(mid, ChunkBudget.PerBreak(Mid));
            Assert.LessOrEqual(high, ChunkBudget.PerBreak(High));
            Assert.Greater(mid, low);
            Assert.AreEqual(4, ChunkBudget.PerBreak(Low)); Assert.AreEqual(8, ChunkBudget.PerBreak(Mid)); Assert.AreEqual(12, ChunkBudget.PerBreak(High));
        }

        // ───────────── release order inside a piece ─────────────

        [Test] public void ReleaseOrder_NearestFirst_DelaysMonotonic_Deterministic()
        {
            var c = Layout(ChunkKind.StoneBlock, 4000, 2400, 400, 99, Mid, out int n);
            var scratch = new int[n];
            ChunkLayout.AssignReleaseOrder(c, n, -2000, 1200, 0, ChunkKindRules.ReleaseSpreadTicks(ChunkKind.StoneBlock), scratch);
            ulong h1 = Hash(c, n);
            var byOrder = new ChunkSpec[n];
            for (int i = 0; i < n; i++) byOrder[c[i].order] = c[i];
            long Prev(ChunkSpec s) { long dx = s.cx + 2000, dy = s.cy - 1200, dz = s.cz; return dx * dx + dy * dy + dz * dz; }
            for (int r = 1; r < n; r++)
            {
                Assert.GreaterOrEqual(Prev(byOrder[r]), Prev(byOrder[r - 1]), "nearest to the hit first");
                Assert.GreaterOrEqual(byOrder[r].delayTicks, byOrder[r - 1].delayTicks);
            }
            Assert.AreEqual(0, byOrder[0].delayTicks);
            Assert.AreEqual(ChunkKindRules.ReleaseSpreadTicks(ChunkKind.StoneBlock), byOrder[n - 1].delayTicks);
            ChunkLayout.AssignReleaseOrder(c, n, -2000, 1200, 0, ChunkKindRules.ReleaseSpreadTicks(ChunkKind.StoneBlock), scratch);
            Assert.AreEqual(h1, Hash(c, n), "same hit → same order");
        }

        [Test] public void ReleaseOrder_GlassIsInstant()
        {
            var c = Layout(ChunkKind.GlassShard, 2000, 1500, 80, 5, Mid, out int n);
            ChunkLayout.AssignReleaseOrder(c, n, 0, 0, 0, ChunkKindRules.ReleaseSpreadTicks(ChunkKind.GlassShard), new int[n]);
            for (int i = 0; i < n; i++) Assert.AreEqual(0, c[i].delayTicks);
        }

        // ───────────── progressive collapse order (fixed seed) ─────────────

        /// <summary>Stack of pieces 0..n-1, each supported only by the one below. Piece 0 breaks at tick 0.</summary>
        static List<(int piece, int tick)> RunStack(long seed, MaterialType[] mats)
        {
            int n = mats.Length;
            var gone = new bool[n];
            var sched = new CollapseSchedule(n);
            var due = new int[n];
            var log = new List<(int, int)>();
            gone[0] = true; log.Add((0, 0));
            for (int tick = 0; tick < 500 && log.Count < n; tick++)
            {
                int k = sched.PopDue(tick, due);
                for (int i = 0; i < k; i++) { gone[due[i]] = true; log.Add((due[i], tick)); }
                for (int p = 1; p < n; p++)
                {
                    if (gone[p] || sched.IsScheduled(p)) continue;
                    if (SupportRule.ShouldCollapse(1, gone[p - 1] ? 0 : 1))
                        sched.Schedule(p, tick + CollapseRules.DelayTicks(mats[p], StableId.Of("stack_" + p), seed));
                }
            }
            return log;
        }

        static readonly MaterialType[] Tower = { MaterialType.Brick, MaterialType.Brick, MaterialType.Wood, MaterialType.Stone, MaterialType.Wood, MaterialType.Steel };

        [Test] public void Collapse_IsProgressive_BottomUp_AndDeterministic()
        {
            var a = RunStack(0x4C31305045524631L, Tower);
            var b = RunStack(0x4C31305045524631L, Tower);
            Assert.AreEqual(Tower.Length, a.Count, "every unsupported piece eventually collapses");
            CollectionAssert.AreEqual(a, b, "same seed → same collapse order and ticks");
            for (int i = 1; i < a.Count; i++)
            {
                Assert.AreEqual(i, a[i].piece, "storey by storey, from the break upward");
                Assert.GreaterOrEqual(a[i].tick - a[i - 1].tick, CollapseRules.BaseDelayTicks, "never all in one tick");
            }
            Assert.AreEqual(GoldenTowerTicks, string.Join(",", a.ConvertAll(x => x.tick)), "golden collapse ticks for the fixed seed");
        }
        internal const string GoldenTowerTicks = "0,5,14,19,27,37"; // brick,brick,wood,stone,wood,steel

        [Test] public void Collapse_DelayRules()
        {
            for (int id = 1; id < 200; id++)
            {
                int d = CollapseRules.DelayTicks(MaterialType.Brick, id, 42);
                Assert.That(d, Is.InRange(CollapseRules.BaseDelayTicks + 2, CollapseRules.BaseDelayTicks + 4));
                Assert.AreEqual(d, CollapseRules.DelayTicks(MaterialType.Brick, id, 42));
            }
            Assert.Greater(CollapseRules.SagTicks(MaterialType.Steel), CollapseRules.SagTicks(MaterialType.Wood));
            Assert.Greater(CollapseRules.SagTicks(MaterialType.Wood), CollapseRules.SagTicks(MaterialType.Brick));
            Assert.AreEqual(0, CollapseRules.SagTicks(MaterialType.Glass));
        }

        [Test] public void CollapseSchedule_OrdersByTickThenIndex_Dedupes_AndCaps()
        {
            var s = new CollapseSchedule(3);
            Assert.IsTrue(s.Schedule(5, 10)); Assert.IsTrue(s.Schedule(2, 10)); Assert.IsTrue(s.Schedule(7, 8));
            Assert.IsTrue(s.Schedule(5, 9), "re-schedule keeps the earlier tick");
            Assert.IsFalse(s.Schedule(9, 1), "full → caller collapses at once");
            var o = new int[3];
            Assert.AreEqual(0, s.PopDue(7, o));
            Assert.AreEqual(1, s.PopDue(8, o)); Assert.AreEqual(7, o[0]);
            Assert.AreEqual(2, s.PopDue(10, o)); Assert.AreEqual(5, o[0]); Assert.AreEqual(2, o[1]);
            Assert.AreEqual(0, s.Count);
        }

        [Test] public void LandingBreak_Thresholds()
        {
            Assert.IsFalse(CollapseRules.BreaksOnLanding(MaterialType.Brick, 3999));
            Assert.IsTrue(CollapseRules.BreaksOnLanding(MaterialType.Brick, 4000));
            Assert.IsTrue(CollapseRules.BreaksOnLanding(MaterialType.Wood, 6000));
            Assert.IsTrue(CollapseRules.BreaksOnLanding(MaterialType.Stone, 6000));
            Assert.IsFalse(CollapseRules.BreaksOnLanding(MaterialType.Steel, 20000), "steel bends, never self-breaks");
            Assert.IsFalse(CollapseRules.BreaksOnLanding(MaterialType.Glass, 20000));
        }

        // ───────────── cap enforcement ─────────────

        [TestCase(25)] [TestCase(60)] [TestCase(90)] [TestCase(DebrisRules.RankedSimCap)]
        public void Ledger_NeverExceedsCap(int cap)
        {
            var l = new ChunkLedger(cap + ChunkBudget.FadeHeadroom, cap);
            for (int i = 0; i < cap + ChunkBudget.FadeHeadroom; i++) l.AddSlot();
            var demoted = new int[l.Capacity];
            for (int tick = 0; tick < 300; tick++)
            {
                for (int k = 0; k < 4; k++) // 4 chunks per tick: far over any cap
                {
                    l.MakeRoom(tick, demoted);
                    int s = l.TakeFree(out _);
                    Assert.GreaterOrEqual(s, 0, "a slot is always available (fading slots are reused)");
                    l.Activate(s, tick, tick + (k % 3), ChunkBudget.MaxDynamicTicks);
                    Assert.LessOrEqual(l.SimCount, cap);
                }
                for (int s = 0; s < l.Slots; s++) l.Advance(s, tick, (tick + s) % 2 == 0);
                Assert.LessOrEqual(l.SimCount, cap);
            }
        }

        [Test] public void Ledger_VictimOrder_FrozenThenSettledThenOldest()
        {
            var l = new ChunkLedger(8, 3) { FreezeAfterTicks = 2 };
            for (int i = 0; i < 8; i++) l.AddSlot();
            l.Activate(0, 0, 0, 0); l.Activate(1, 0, 0, 0); l.Activate(2, 0, 0, 0);
            l.Advance(2, 1, true); l.Advance(2, 2, true); // slot 2 frozen
            Assert.AreEqual(ChunkState.Frozen, l.State(2));
            Assert.AreEqual(2, l.PickVictim(), "frozen rubble goes first even though it is newest");
            l.StartFade(2, 3);
            l.Activate(3, 3, 3, 0);
            l.Advance(1, 4, true); // slot 1 settling
            Assert.AreEqual(1, l.PickVictim(), "then the oldest settled");
            l.Advance(1, 5, false);
            Assert.AreEqual(0, l.PickVictim(), "then the oldest");
        }

        [Test] public void RankedCap_IsTierIndependent()
        {
            Assert.AreEqual(60, DebrisRules.SimCap(true, 25));
            Assert.AreEqual(60, DebrisRules.SimCap(true, 90));
            Assert.AreEqual(25, DebrisRules.SimCap(false, 25));
        }

        // ───────────── sleep / freeze timeout ─────────────

        [Test] public void Ledger_HeldReleaseFreezeFadeRecycle()
        {
            var l = new ChunkLedger(2, 2);
            l.AddSlot();
            l.Activate(0, 100, 104, 0);
            Assert.AreEqual(ChunkState.Held, l.State(0));
            for (int t = 100; t < 104; t++) Assert.AreEqual(ChunkTransition.None, l.Advance(0, t, false));
            Assert.AreEqual(ChunkTransition.Release, l.Advance(0, 104, false));
            int tick = 105;
            for (int i = 0; i < ChunkBudget.FreezeAfterTicks - 1; i++) Assert.AreEqual(ChunkTransition.None, l.Advance(0, tick++, true));
            Assert.AreEqual(ChunkTransition.None, l.Advance(0, tick++, false), "movement resets the settle timer");
            for (int i = 0; i < ChunkBudget.FreezeAfterTicks - 1; i++) Assert.AreEqual(ChunkTransition.None, l.Advance(0, tick++, true));
            Assert.AreEqual(ChunkTransition.Freeze, l.Advance(0, tick, true), "frozen after FreezeAfterTicks settled ticks");
            Assert.AreEqual(ChunkState.Frozen, l.State(0));
            int frozenAt = tick;
            Assert.AreEqual(ChunkTransition.None, l.Advance(0, frozenAt + l.RubbleLifeTicks - 1, false));
            Assert.AreEqual(ChunkTransition.StartFade, l.Advance(0, frozenAt + l.RubbleLifeTicks, false), "rubble fades after its lifetime");
            for (int i = 0; i < ChunkBudget.FadeTicks - 1; i++) Assert.AreEqual(ChunkTransition.None, l.Advance(0, 0, false));
            Assert.AreEqual(ChunkTransition.Recycle, l.Advance(0, 0, false));
            Assert.AreEqual(ChunkState.Free, l.State(0));
        }

        [Test] public void Ledger_RestlessChunkFadesAtMaxDynamic()
        {
            var l = new ChunkLedger(1, 1); l.AddSlot();
            l.Activate(0, 0, 0, 0);
            for (int t = 1; t < ChunkBudget.MaxDynamicTicks; t++) Assert.AreEqual(ChunkTransition.None, l.Advance(0, t, false));
            Assert.AreEqual(ChunkTransition.StartFade, l.Advance(0, ChunkBudget.MaxDynamicTicks, false));
        }

        [Test] public void FreezeTimeout_Values()
        {
            Assert.AreEqual(25, ChunkBudget.FreezeAfterTicks, "0.5 s at 50 Hz");
            Assert.AreEqual(1000, ChunkBudget.RubbleLifeTicks(Mid));
            Assert.Less(ChunkBudget.RubbleLifeTicks(Low), ChunkBudget.RubbleLifeTicks(Mid));
            Assert.Greater(ChunkBudget.RubbleLifeTicks(High), ChunkBudget.RubbleLifeTicks(Mid));
        }

        // ───────────── material → chunk kind ─────────────

        [Test] public void MaterialMapping_EveryChunkKind()
        {
            Assert.AreEqual(ChunkKind.StoneBlock, ChunkKindRules.For(MaterialType.Stone, false));
            Assert.AreEqual(ChunkKind.StoneBlock, ChunkKindRules.For(MaterialType.Brick, false));
            Assert.AreEqual(ChunkKind.StoneBlock, ChunkKindRules.For(MaterialType.Terrain, false));
            Assert.AreEqual(ChunkKind.TimberBeam, ChunkKindRules.For(MaterialType.Wood, false));
            Assert.AreEqual(ChunkKind.RoofTile, ChunkKindRules.For(MaterialType.Wood, true), "wooden roof → shingle tiles");
            Assert.AreEqual(ChunkKind.RoofTile, ChunkKindRules.For(MaterialType.Brick, true));
            Assert.AreEqual(ChunkKind.RoofTile, ChunkKindRules.For(MaterialType.Stone, true));
            Assert.AreEqual(ChunkKind.GlassShard, ChunkKindRules.For(MaterialType.Glass, false));
            Assert.AreEqual(ChunkKind.GlassShard, ChunkKindRules.For(MaterialType.Glass, true));
            Assert.AreEqual(ChunkKind.SteelPlate, ChunkKindRules.For(MaterialType.Steel, true));
        }

        [Test] public void MaterialMapping_FractureClass_RoofIsStoneTiles()
        {
            Assert.AreEqual(ChunkKind.RoofTile, ChunkKindRules.ForFractureClass(" ROOF ", MaterialType.Wood, out var m));
            Assert.AreEqual(MaterialType.Stone, m, "Roof maps to Stone (FractureClassRules, PR #54)");
            Assert.AreEqual(ChunkKind.StoneBlock, ChunkKindRules.ForFractureClass("Stone", MaterialType.Wood, out m)); Assert.AreEqual(MaterialType.Stone, m);
            Assert.AreEqual(ChunkKind.TimberBeam, ChunkKindRules.ForFractureClass("wood", MaterialType.Brick, out m)); Assert.AreEqual(MaterialType.Wood, m);
            Assert.AreEqual(ChunkKind.GlassShard, ChunkKindRules.ForFractureClass("Glass", MaterialType.Wood, out m));
            Assert.AreEqual(ChunkKind.SteelPlate, ChunkKindRules.ForFractureClass("Steel", MaterialType.Wood, out m));
            Assert.AreEqual(ChunkKind.StoneBlock, ChunkKindRules.ForFractureClass("Brick", MaterialType.Wood, out m)); Assert.AreEqual(MaterialType.Brick, m);
            Assert.AreEqual(ChunkKind.StoneBlock, ChunkKindRules.ForFractureClass("", MaterialType.Brick, out m), "empty → caller's material");
        }

        [Test] public void MaterialMapping_OnlyGlassIsPresentationOnly()
        {
            foreach (ChunkKind k in Enum.GetValues(typeof(ChunkKind)))
                Assert.AreEqual(k == ChunkKind.GlassShard, ChunkKindRules.PresentationOnly(k), k.ToString());
            Assert.IsTrue(ChunkKindRules.IsRoofRole(" Roof"));
            Assert.IsFalse(ChunkKindRules.IsRoofRole("roofline"));
        }

        // ───────────── levels 1-10 (incl. the Tidewarden boss) ─────────────

        static readonly string[] Chapter1 = { "c1_l01_lookout_hut", "c1_l02_stilt_tower", "c1_l03_bakery", "c1_l04_windmill", "c1_l05_twin_cottages",
                                              "c1_l06_granary", "m1_clockwork_tower", "c1_l08_market_arcade", "c1_l09_lighthouse_gauntlet", "c1_l10_tidewarden" };

        static string LayoutsDir()
        {
            string dir = Directory.GetCurrentDirectory();
            for (int i = 0; i < 6 && dir != null; i++, dir = Path.GetDirectoryName(dir))
            {
                string p = Path.Combine(dir, "Assets", "_Project", "Resources", "Data", "Layouts");
                if (Directory.Exists(p)) return p;
            }
            return null;
        }

        static readonly Regex BlockRx = new Regex("\"id\":\\s*\"(?<id>[^\"]*)\",\\s*\"mat\":\\s*\"(?<mat>[^\"]*)\".*?\"s\":\\s*\\[(?<s>[^\\]]*)\\].*?\"role\":\\s*\"(?<role>[^\"]*)\".*?\"indestructible\":\\s*(?<ind>true|false)", RegexOptions.Singleline);

        [Test] public void Levels1To10AndBoss_EveryDestructibleBlockHasAChunkLayout_WithinBudget()
        {
            string dir = LayoutsDir();
            Assert.IsNotNull(dir, "layout folder");
            var buf = new ChunkSpec[32];
            foreach (var name in Chapter1)
            {
                string json = File.ReadAllText(Path.Combine(dir, name + ".json"));
                int blocks = 0, worst = 0;
                foreach (Match m in BlockRx.Matches(json))
                {
                    if (m.Groups["ind"].Value == "true") continue;
                    string mat = m.Groups["mat"].Value;
                    var t = mat == "wood" ? MaterialType.Wood : mat == "steel" ? MaterialType.Steel : mat == "glass" ? MaterialType.Glass : mat == "stone" ? MaterialType.Stone : MaterialType.Brick;
                    var s = m.Groups["s"].Value.Split(',');
                    int sx = ChunkLayout.ToMm(float.Parse(s[0], System.Globalization.CultureInfo.InvariantCulture));
                    int sy = ChunkLayout.ToMm(float.Parse(s[1], System.Globalization.CultureInfo.InvariantCulture));
                    int sz = ChunkLayout.ToMm(float.Parse(s[2], System.Globalization.CultureInfo.InvariantCulture));
                    var kind = ChunkKindRules.For(t, ChunkKindRules.IsRoofRole(m.Groups["role"].Value));
                    int n = ChunkLayout.Generate(kind, sx, sy, sz, ChunkLayout.SeedFor(StableId.Of(m.Groups["id"].Value)), ChunkBudget.ChunksFor(kind, Mid), buf);
                    Assert.GreaterOrEqual(n, 2, $"{name}/{m.Groups["id"].Value} ({kind} {sx}×{sy}×{sz} mm) breaks into chunks");
                    Assert.LessOrEqual(n, ChunkBudget.PerBreak(Mid));
                    long vol = 0; for (int i = 0; i < n; i++) vol += (long)buf[i].sx * buf[i].sy * buf[i].sz;
                    Assert.AreEqual((long)sx * sy * sz, vol, $"{name}/{m.Groups["id"].Value} tiles exactly");
                    blocks++; worst = Math.Max(worst, n);
                }
                Assert.Greater(blocks, 5, name + " has destructible blocks");
            }
        }
    }
}
