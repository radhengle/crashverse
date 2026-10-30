using System;

namespace Crashverse.Sim
{
    // Engine-free chunked destruction model (physics/Chunked_Destruction.md). Integer millimetres and integer ticks only:
    // the same block id + size gives the same chunk layout on every device, in CI and on the server. No UnityEngine,
    // no System.Random, no wall clock. Nothing here allocates after construction.

    /// <summary>Chunk geometry family. Append only (values appear in PHYS_CHUNK_* payloads).</summary>
    public enum ChunkKind : byte { StoneBlock = 0, RoofTile = 1, TimberBeam = 2, GlassShard = 3, SteelPlate = 4 }

    /// <summary>Material → chunk kind. Geometry only; the event payload keeps the source MaterialType for VFX/audio.</summary>
    public static class ChunkKindRules
    {
        /// <summary>
        /// Glass → GlassShard (presentation only), Steel → SteelPlate, any other roof → RoofTile (terracotta tiles on stone,
        /// shingles on wood), Wood → TimberBeam, Brick/Stone/Terrain → StoneBlock.
        /// </summary>
        public static ChunkKind For(MaterialType m, bool isRoof)
        {
            if (m == MaterialType.Glass) return ChunkKind.GlassShard;
            if (m == MaterialType.Steel) return ChunkKind.SteelPlate;
            if (isRoof) return ChunkKind.RoofTile;
            return m == MaterialType.Wood ? ChunkKind.TimberBeam : ChunkKind.StoneBlock;
        }

        /// <summary>Imported fractureClass (FractureClassRules: Roof → Stone + roof flag). Empty/unknown → fallback material.</summary>
        public static ChunkKind ForFractureClass(string fractureClass, MaterialType fallback, out MaterialType material)
        {
            FractureClassRules.Resolve(fractureClass, out material, fallback);
            return For(material, FractureClassRules.IsRoof(fractureClass));
        }

        /// <summary>Layout JSON role "roof" (trimmed, ordinal, case-insensitive).</summary>
        public static bool IsRoofRole(string role) =>
            role != null && string.Equals(role.Trim(), "roof", StringComparison.OrdinalIgnoreCase);

        /// <summary>Glass shards never enter the sim: no rigidbody, no collider, not counted against any cap.</summary>
        public static bool PresentationOnly(ChunkKind k) => k == ChunkKind.GlassShard;

        /// <summary>Chunks in a full layout (Mid/ranked). Mirrors the fractured kit ranges (models/validation_fracture.txt) scaled for greybox blocks.</summary>
        public static int LayoutTarget(ChunkKind k) => k switch
        {
            ChunkKind.StoneBlock => 8, ChunkKind.RoofTile => 9, ChunkKind.TimberBeam => 6, ChunkKind.SteelPlate => 3, _ => 10, // glass
        };

        /// <summary>Ticks over which a broken piece's chunks are released, nearest-to-impact first (glass: all at once).</summary>
        public static int ReleaseSpreadTicks(ChunkKind k) => k switch
        {
            ChunkKind.StoneBlock => 8, ChunkKind.RoofTile => 10, ChunkKind.TimberBeam => 6, ChunkKind.SteelPlate => 4, _ => 0,
        };
    }

    /// <summary>One chunk: centre offset and size in mm in the source block's local frame, plus release rank/delay.</summary>
    [Serializable]
    public struct ChunkSpec
    {
        public int cx, cy, cz;   // centre, mm, relative to block centre
        public int sx, sy, sz;   // full size, mm
        public ChunkKind kind;
        public int order;        // release rank (0 = first)
        public int delayTicks;   // ticks after the break before this chunk is released
    }

    /// <summary>Per-tier chunk budgets. Ranked always uses Mid (tier-independent sim).</summary>
    public static class ChunkBudget
    {
        public const int TierLow = 0, TierMid = 1, TierHigh = 2;
        /// <summary>Max sim chunks from one broken piece.</summary>
        public static int PerBreak(int tier) => tier <= TierLow ? 4 : tier >= TierHigh ? 12 : 8;
        /// <summary>Presentation-only glass shards per pane.</summary>
        public static int GlassShards(int tier) => tier <= TierLow ? 5 : tier >= TierHigh ? 14 : 10;
        /// <summary>Concurrent presentation shards (all panes).</summary>
        public static int GlassShardPool(int tier) => tier <= TierLow ? 24 : tier >= TierHigh ? 72 : 48;
        /// <summary>Consecutive settled ticks before a chunk is frozen (kinematic rubble). 25 = 0.5 s.</summary>
        public const int FreezeAfterTicks = 25;
        /// <summary>Hard limit on a chunk staying dynamic (rolling / bouncing): fades out. 400 = 8 s.</summary>
        public const int MaxDynamicTicks = 400;
        /// <summary>How long frozen rubble stays before fading. Ranked = Mid.</summary>
        public static int RubbleLifeTicks(int tier) => tier <= TierLow ? 500 : tier >= TierHigh ? 1500 : 1000;
        /// <summary>Fade-out length (presentation, collider already off). 30 = 0.6 s.</summary>
        public const int FadeTicks = 30;
        /// <summary>Pool slots beyond the sim cap, for chunks that are fading out.</summary>
        public const int FadeHeadroom = 16;
        public static int ChunksFor(ChunkKind k, int tier) =>
            ChunkKindRules.PresentationOnly(k) ? GlassShards(tier) : Math.Min(ChunkKindRules.LayoutTarget(k), PerBreak(tier));
    }

    /// <summary>
    /// Deterministic box / voronoi-lite chunk layout. Splits the block into an nx×ny×nz grid shaped per kind (stone: near-cubic
    /// blocks; roof: thin tiles, thinnest axis kept whole; timber: long beams along the longest axis; steel: a few plates), with
    /// seeded cut jitter re-drawn per row so courses are staggered like masonry bond. Volumes tile the block exactly.
    /// </summary>
    public static class ChunkLayout
    {
        public const long Salt = 0x43484E4B; // "CHNK"
        public const int MinChunkMm = 120;   // never cut thinner than 12 cm (stability with 6 solver iterations)
        public const int JitterPermille = 220; // cut planes move up to ±22% of a cell

        /// <summary>Layout seed: block id only (never the run seed), so layouts are identical across runs, Retry and server.</summary>
        public static long SeedFor(int stableBlockId) => unchecked(((long)stableBlockId * 0x9E3779B1L) ^ Salt);

        public static int ToMmSigned(float metres) => (int)Math.Round(metres * 1000f, MidpointRounding.AwayFromZero);

        public static int ToMm(float metres) => metres <= 0f ? 0 : (int)Math.Round(metres * 1000f, MidpointRounding.AwayFromZero);

        /// <summary>Fills <paramref name="buffer"/> and returns the chunk count (≥1 for a non-empty block). No allocation.</summary>
        public static int Generate(ChunkKind kind, int sxMm, int syMm, int szMm, long seed, int maxChunks, ChunkSpec[] buffer)
        {
            if (buffer == null || buffer.Length == 0 || sxMm <= 0 || syMm <= 0 || szMm <= 0) return 0;
            int target = Math.Max(1, Math.Min(Math.Min(ChunkKindRules.LayoutTarget(kind), maxChunks), buffer.Length));
            if (ChunkKindRules.PresentationOnly(kind)) target = Math.Max(1, Math.Min(maxChunks, buffer.Length));
            Grid(kind, sxMm, syMm, szMm, target, out int nx, out int ny, out int nz);
            int count = 0;
            // rows along Y; X cuts re-jittered per Y row, Z cuts per (Y,X) cell → staggered courses.
            for (int iy = 0; iy < ny; iy++)
            {
                Cut(syMm, ny, iy, 0, out int y0, out int y1); // rows: no jitter, keeps courses level
                for (int ix = 0; ix < nx; ix++)
                {
                    int jx0 = ix == 0 ? 0 : Jitter(seed, sxMm, nx, iy * 31 + ix);
                    int jx1 = ix == nx - 1 ? 0 : Jitter(seed, sxMm, nx, iy * 31 + ix + 1);
                    // the shared cut between ix and ix+1 must match: draw once per (row, cut) via a stateless mix
                    Cut(sxMm, nx, ix, 0, out int x0, out int x1); x0 += jx0; x1 += jx1;
                    for (int iz = 0; iz < nz; iz++)
                    {
                        int jz0 = iz == 0 ? 0 : Jitter(seed, szMm, nz, 1000 + (iy * 31 + ix) * 31 + iz);
                        int jz1 = iz == nz - 1 ? 0 : Jitter(seed, szMm, nz, 1000 + (iy * 31 + ix) * 31 + iz + 1);
                        Cut(szMm, nz, iz, 0, out int z0, out int z1); z0 += jz0; z1 += jz1;
                        if (count >= buffer.Length) return count;
                        buffer[count++] = new ChunkSpec
                        {
                            kind = kind,
                            sx = x1 - x0, sy = y1 - y0, sz = z1 - z0,
                            cx = (x0 + x1) / 2 - sxMm / 2, cy = (y0 + y1) / 2 - syMm / 2, cz = (z0 + z1) / 2 - szMm / 2,
                            order = count - 1, delayTicks = 0,
                        };
                    }
                }
            }
            return count;
        }

        /// <summary>Grid shape per kind; product ≤ target. Integer greedy split of the axis with the largest cell, ties X, Y, Z.</summary>
        public static void Grid(ChunkKind kind, int sx, int sy, int sz, int target, out int nx, out int ny, out int nz)
        {
            int nX = 1, nY = 1, nZ = 1;
            int thin = sx <= sy && sx <= sz ? 0 : sy <= sz ? 1 : 2;
            int longest = sx >= sy && sx >= sz ? 0 : sy >= sz ? 1 : 2;
            bool lockThin = kind == ChunkKind.RoofTile || kind == ChunkKind.GlassShard || kind == ChunkKind.SteelPlate;
            for (int guard = 0; guard < 64; guard++)
            {
                int best = -1; long bestNum = 0, bestDen = 1;
                for (int a = 0; a < 3; a++)
                {
                    if (lockThin && a == thin) continue;
                    if (kind == ChunkKind.TimberBeam && a == longest && N(a, nX, nY, nZ) >= 2) continue; // beams stay long
                    int n = N(a, nX, nY, nZ);
                    int da = a == 0 ? sx : a == 1 ? sy : sz;
                    if (da / (n + 1) < MinChunkMm) continue;
                    // cell length d[a]/n; compare d[a]*bestDen vs bestNum*n without division
                    if (best < 0 || (long)da * bestDen > bestNum * n) { best = a; bestNum = da; bestDen = n; }
                }
                if (best < 0) break;
                int nb = N(best, nX, nY, nZ);
                long product = (long)nX * nY * nZ / nb * (nb + 1);
                if (product > target) break;
                if (best == 0) nX++; else if (best == 1) nY++; else nZ++;
            }
            nx = nX; ny = nY; nz = nZ;
        }

        static int N(int a, int x, int y, int z) => a == 0 ? x : a == 1 ? y : z;

        static void Cut(int size, int n, int i, int jitter, out int a, out int b)
        {
            a = (int)((long)size * i / n);
            b = (int)((long)size * (i + 1) / n);
        }

        /// <summary>Stateless per-cut jitter (so both neighbours of a cut agree): splitmix of the layout seed and the cut key.</summary>
        static int Jitter(long seed, int size, int n, int key)
        {
            if (n <= 1) return 0;
            int cell = size / n;
            int max = Math.Max(0, Math.Min(cell * JitterPermille / 1000, (cell - MinChunkMm) / 2));
            if (max <= 0) return 0;
            ulong h = Mix(unchecked((ulong)seed ^ ((ulong)(uint)key * 0x9E3779B97F4A7C15UL)));
            return (int)(h % (ulong)(2 * max + 1)) - max;
        }

        /// <summary>splitmix64 finaliser. Pure integer.</summary>
        public static ulong Mix(ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>
        /// Progressive release inside one broken piece: chunks nearest the impact point go first, then outward, ties by index.
        /// delay = rank × spread / (count − 1). Integer squared distances only. No allocation (insertion sort, count ≤ buffer).
        /// </summary>
        public static void AssignReleaseOrder(ChunkSpec[] chunks, int count, int hitXmm, int hitYmm, int hitZmm, int spreadTicks, int[] scratch)
        {
            if (chunks == null || count <= 0 || scratch == null || scratch.Length < count) return;
            for (int i = 0; i < count; i++) scratch[i] = i;
            for (int i = 1; i < count; i++)
            {
                int v = scratch[i]; long dv = Dist2(chunks[v], hitXmm, hitYmm, hitZmm);
                int j = i - 1;
                while (j >= 0)
                {
                    long dj = Dist2(chunks[scratch[j]], hitXmm, hitYmm, hitZmm);
                    if (dj < dv || (dj == dv && scratch[j] < v)) break;
                    scratch[j + 1] = scratch[j]; j--;
                }
                scratch[j + 1] = v;
            }
            for (int r = 0; r < count; r++)
            {
                int c = scratch[r];
                chunks[c].order = r;
                chunks[c].delayTicks = count <= 1 || spreadTicks <= 0 ? 0 : r * spreadTicks / (count - 1);
            }
        }

        static long Dist2(in ChunkSpec c, int x, int y, int z)
        {
            long dx = c.cx - x, dy = c.cy - y, dz = c.cz - z;
            return dx * dx + dy * dy + dz * dz;
        }
    }

    /// <summary>
    /// Progressive collapse between pieces: a piece that has lost ALL its supports (SupportRule) does not drop on the same
    /// tick; it is scheduled at now + DelayTicks, so a tower comes down storey by storey. Integer ticks, stable order.
    /// </summary>
    public static class CollapseRules
    {
        public const int BaseDelayTicks = 3; // 60 ms per support level minimum
        public const long Salt = 0x434F4C4C;  // "COLL"
        /// <summary>Material "sag": wood bends before it lets go, steel holds longest, masonry drops fast, glass instantly.</summary>
        public static int SagTicks(MaterialType m) => m switch
        {
            MaterialType.Wood => 4, MaterialType.Brick => 2, MaterialType.Stone => 2, MaterialType.Steel => 6, _ => 0,
        };
        /// <summary>A piece falling after support loss breaks itself into chunks when it lands at ≥ 4 m/s (≈ 0.8 m drop).</summary>
        public const int LandBreakMmPerS = 4000;
        /// <summary>Steel and glass never self-break on landing (steel bends; glass already shatters on any break).</summary>
        public static bool BreaksOnLanding(MaterialType m, int speedMmPerS) =>
            speedMmPerS >= LandBreakMmPerS && (m == MaterialType.Wood || m == MaterialType.Brick || m == MaterialType.Stone);

        /// <summary>Base + sag + 0..2 ticks of seeded jitter (run seed + stable piece id), so identical runs collapse identically.</summary>
        public static int DelayTicks(MaterialType m, int stablePieceId, long runSeed)
        {
            ulong h = ChunkLayout.Mix(unchecked((ulong)(runSeed ^ Salt) ^ ((ulong)(uint)stablePieceId << 17)));
            return BaseDelayTicks + SagTicks(m) + (int)(h % 3UL);
        }
    }

    /// <summary>Fixed-capacity collapse queue keyed by piece index. Pops due pieces ordered by (dueTick, pieceIndex).</summary>
    public sealed class CollapseSchedule
    {
        readonly int[] _piece, _due;
        int _count;
        public CollapseSchedule(int capacity) { _piece = new int[Math.Max(1, capacity)]; _due = new int[_piece.Length]; }
        public int Count => _count;
        public int Capacity => _piece.Length;

        public bool IsScheduled(int pieceIndex)
        {
            for (int i = 0; i < _count; i++) if (_piece[i] == pieceIndex) return true;
            return false;
        }

        /// <summary>false when full (caller collapses immediately). Re-scheduling an already queued piece keeps the earlier tick.</summary>
        public bool Schedule(int pieceIndex, int dueTick)
        {
            for (int i = 0; i < _count; i++) if (_piece[i] == pieceIndex) { if (dueTick < _due[i]) _due[i] = dueTick; return true; }
            if (_count >= _piece.Length) return false;
            _piece[_count] = pieceIndex; _due[_count] = dueTick; _count++;
            return true;
        }

        /// <summary>Writes pieces with due ≤ now into <paramref name="output"/> in (due, index) order; returns how many.</summary>
        public int PopDue(int nowTick, int[] output)
        {
            int n = 0;
            while (output != null && n < output.Length)
            {
                int best = -1;
                for (int i = 0; i < _count; i++)
                {
                    if (_due[i] > nowTick) continue;
                    if (best < 0 || _due[i] < _due[best] || (_due[i] == _due[best] && _piece[i] < _piece[best])) best = i;
                }
                if (best < 0) break;
                output[n++] = _piece[best];
                _count--; _piece[best] = _piece[_count]; _due[best] = _due[_count];
            }
            return n;
        }

        public void Clear() => _count = 0;
    }

    public enum ChunkState : byte { Free = 0, Held = 1, Dynamic = 2, Frozen = 3, Fading = 4 }
    public enum ChunkTransition : byte { None = 0, Release = 1, Freeze = 2, StartFade = 3, Recycle = 4 }

    /// <summary>
    /// Slot ledger for pooled sim chunks. Held (kinematic in place, waiting for its release tick) → Dynamic → Frozen (settled
    /// for FreezeAfterTicks: kinematic static rubble) → Fading (collider off, presentation fade) → Free.
    /// Cap counts every chunk with a collider (Held + Dynamic + Frozen). When a new chunk needs room, the victim is the oldest
    /// Frozen rubble, else the oldest settled Dynamic, else the oldest Dynamic, else the oldest Held; victims fade out.
    /// Fixed arrays, integer ticks, no allocation.
    /// </summary>
    public sealed class ChunkLedger
    {
        readonly ChunkState[] _state;
        readonly long[] _seq;
        readonly int[] _releaseTick, _stateTick, _settled, _dynLimit, _fade;
        long _nextSeq;
        public int Slots { get; private set; }
        public int Capacity => _state.Length;
        public int Cap { get; set; }
        public int FreezeAfterTicks { get; set; } = ChunkBudget.FreezeAfterTicks;
        public int RubbleLifeTicks { get; set; } = ChunkBudget.RubbleLifeTicks(ChunkBudget.TierMid);
        public int FadeTicks { get; set; } = ChunkBudget.FadeTicks;

        public ChunkLedger(int capacity, int cap)
        {
            capacity = Math.Max(1, capacity);
            _state = new ChunkState[capacity]; _seq = new long[capacity];
            _releaseTick = new int[capacity]; _stateTick = new int[capacity]; _settled = new int[capacity];
            _dynLimit = new int[capacity]; _fade = new int[capacity];
            Cap = cap;
        }

        /// <summary>Makes another slot usable (pool prewarm). Returns its index or -1 at capacity.</summary>
        public int AddSlot() => Slots < _state.Length ? Slots++ : -1;

        public ChunkState State(int slot) => _state[slot];
        public int FadeLeft(int slot) => _fade[slot];

        public int SimCount
        {
            get { int n = 0; for (int i = 0; i < Slots; i++) { var s = _state[i]; if (s == ChunkState.Held || s == ChunkState.Dynamic || s == ChunkState.Frozen) n++; } return n; }
        }

        public int CountIn(ChunkState st) { int n = 0; for (int i = 0; i < Slots; i++) if (_state[i] == st) n++; return n; }

        /// <summary>Demotes victims to Fading until one more sim chunk fits under Cap. Writes demoted slots to <paramref name="demoted"/>.</summary>
        public int MakeRoom(int nowTick, int[] demoted)
        {
            int n = 0;
            int guard = Slots;
            while (Cap > 0 && SimCount >= Cap && guard-- > 0)
            {
                int v = PickVictim();
                if (v < 0) break;
                StartFade(v, nowTick);
                if (demoted != null && n < demoted.Length) demoted[n] = v;
                n++;
            }
            return n;
        }

        public int PickVictim()
        {
            int v = Oldest(ChunkState.Frozen, false);
            if (v < 0) v = Oldest(ChunkState.Dynamic, true);
            if (v < 0) v = Oldest(ChunkState.Dynamic, false);
            if (v < 0) v = Oldest(ChunkState.Held, false);
            return v;
        }

        int Oldest(ChunkState st, bool settledOnly)
        {
            int best = -1;
            for (int i = 0; i < Slots; i++)
            {
                if (_state[i] != st || (settledOnly && _settled[i] == 0)) continue;
                if (best < 0 || _seq[i] < _seq[best]) best = i;
            }
            return best;
        }

        /// <summary>A free slot, else the oldest fading one (its fade is cut short: caller recycles it). -1 if every slot holds a sim chunk.</summary>
        public int TakeFree(out bool wasFading)
        {
            wasFading = false;
            for (int i = 0; i < Slots; i++) if (_state[i] == ChunkState.Free) return i;
            int f = Oldest(ChunkState.Fading, false);
            if (f >= 0) { wasFading = true; _state[f] = ChunkState.Free; }
            return f;
        }

        /// <param name="releaseTick">Held until this tick (≤ now = dynamic immediately).</param>
        /// <param name="maxDynamicTicks">Ticks the chunk may stay dynamic before it fades regardless (legacy bursts: their lifetime).</param>
        public void Activate(int slot, int nowTick, int releaseTick, int maxDynamicTicks)
        {
            _seq[slot] = _nextSeq++;
            _releaseTick[slot] = releaseTick;
            _state[slot] = releaseTick > nowTick ? ChunkState.Held : ChunkState.Dynamic;
            _stateTick[slot] = nowTick; _settled[slot] = 0; _fade[slot] = 0;
            _dynLimit[slot] = maxDynamicTicks > 0 ? maxDynamicTicks : ChunkBudget.MaxDynamicTicks;
        }

        public void StartFade(int slot, int nowTick)
        {
            if (_state[slot] == ChunkState.Free || _state[slot] == ChunkState.Fading) return;
            _state[slot] = ChunkState.Fading; _stateTick[slot] = nowTick; _fade[slot] = Math.Max(1, FadeTicks);
        }

        public void Free(int slot) { _state[slot] = ChunkState.Free; _settled[slot] = 0; _fade[slot] = 0; }

        /// <summary>One sim tick for one slot. <paramref name="settledNow"/>: the body is asleep or below the settle speed this tick.</summary>
        public ChunkTransition Advance(int slot, int nowTick, bool settledNow)
        {
            switch (_state[slot])
            {
                case ChunkState.Held:
                    if (nowTick < _releaseTick[slot]) return ChunkTransition.None;
                    _state[slot] = ChunkState.Dynamic; _stateTick[slot] = nowTick; _settled[slot] = 0;
                    return ChunkTransition.Release;
                case ChunkState.Dynamic:
                    _settled[slot] = settledNow ? _settled[slot] + 1 : 0;
                    if (_settled[slot] >= FreezeAfterTicks) { _state[slot] = ChunkState.Frozen; _stateTick[slot] = nowTick; return ChunkTransition.Freeze; }
                    if (nowTick - _stateTick[slot] >= _dynLimit[slot]) { StartFade(slot, nowTick); return ChunkTransition.StartFade; }
                    return ChunkTransition.None;
                case ChunkState.Frozen:
                    if (nowTick - _stateTick[slot] >= RubbleLifeTicks) { StartFade(slot, nowTick); return ChunkTransition.StartFade; }
                    return ChunkTransition.None;
                case ChunkState.Fading:
                    if (--_fade[slot] > 0) return ChunkTransition.None;
                    Free(slot);
                    return ChunkTransition.Recycle;
                default:
                    return ChunkTransition.None;
            }
        }

        public void Clear() { for (int i = 0; i < Slots; i++) Free(i); _nextSeq = 0; }
    }

    /// <summary>
    /// Payload for PHYS_CHUNK_* events (VFX/audio hooks; presentation must never write back into the sim). Raised on
    /// AnimEventBus with ONE reused instance per event name (no allocation): copy the fields you need inside the handler.
    /// </summary>
    public sealed class ChunkEvent
    {
        public int tick;
        public int pieceId;          // StableId of the source block
        public int chunkIndex;       // index inside the piece's layout; -1 for piece-level events
        public int chunkCount;       // chunks in the layout
        public ChunkKind kind;
        public MaterialType material;
        public ImpactTier tier;
        public SimVec3 position, normal;
        public SimVec3 size;         // chunk size in metres (dust volume)
        public float speed;          // m/s at the event (landing speed for PHYS_CHUNK_LAND)
        public bool presentationOnly;
        public bool ranked;
    }

    public static class ChunkEvents
    {
        public const string Release = "PHYS_CHUNK_RELEASE", Land = "PHYS_CHUNK_LAND", Settle = "PHYS_CHUNK_SETTLE",
                            Fade = "PHYS_CHUNK_FADE", GlassShatter = "PHYS_GLASS_SHATTER", CollapseStep = "PHYS_COLLAPSE_STEP";
        /// <summary>Landing qualifies for a dust/impact hook: ≥ 1.5 m/s (integer mm/s, as DebrisDamage).</summary>
        public const int LandMinMmPerS = 1500;
    }
}
