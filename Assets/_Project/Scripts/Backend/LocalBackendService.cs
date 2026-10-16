using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Crashverse.Data;
using UnityEngine;

namespace Crashverse.Backend
{
    /// <summary>
    /// Local stand-in for the authoritative server. Reads bundled JSON (ContentDatabase), keeps its own
    /// "server DB" file (separate from the campaign save) and a deterministic fake leaderboard.
    /// QA: the server clock can be overridden (SetTestClock / SetTestOffset); the override is persisted so it
    /// survives app restarts. Device clock changes have no effect: only ServerNow is used (SG-20).
    /// Limitation: because it is on-device, reinstall wipes it; a real server is required for reinstall-proof ranked/streaks.
    /// </summary>
    public class LocalBackendService : IBackendService
    {
        [Serializable] class Sub { public string playerId; public string name; public int score; public long ticks; public bool friend; }
        [Serializable] class Board { public string key; public int type; public bool processed; public List<Sub> subs = new List<Sub>(); }
        [Serializable] class EventBest { public string levelId; public int stars; }
        [Serializable] class TicketRec { public string token; public string dayKey; public int shotBudget; public bool used; }

        [Serializable]
        class ServerDb
        {
            public long testFixedUnixMs = -1; public long testOffsetMs; // QA clock override
            public AccountState account = new AccountState();
            public Wallet wallet = new Wallet();
            public List<InboxItem> inbox = new List<InboxItem>();
            public List<string> grantedFirstClears = new List<string>();
            public List<TicketRec> tickets = new List<TicketRec>();
            public List<RankedStatus> ranked = new List<RankedStatus>();
            public int streakIndex; public string lastClaimDayKey = "";
            public string seasonId = ""; public int seasonXp; public int bonusChests; public bool premium;
            public List<int> claimedFree = new List<int>(); public List<int> claimedPremium = new List<int>();
            public List<EventBest> eventBest = new List<EventBest>();
            public List<string> titanClearDays = new List<string>();
            public List<Board> boards = new List<Board>();
            public bool seasonEndProcessed;
        }

        readonly ContentDatabase _db;
        readonly IClock _deviceClock;
        readonly string _path;
        ServerDb _s;
        public bool SimulateOffline { get; set; }
        public bool IsOnline => !SimulateOffline;
        public const int BotCount = 240;

        public LocalBackendService(ContentDatabase db, IClock clock = null, string path = null)
        {
            _db = db;
            _deviceClock = clock ?? new SystemClock();
            _path = path ?? Path.Combine(Application.persistentDataPath, "local_server_db.json");
            Load();
            if (string.IsNullOrEmpty(_s.account.playerId))
            {
                _s.account.playerId = "guest_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                _s.account.displayName = "Guest Wrecker";
                Save();
            }
        }

        // ---------------- QA test clock ----------------
        public DateTime ServerNow
        {
            get
            {
                if (_s.testFixedUnixMs >= 0)
                    return DateTimeOffset.FromUnixTimeMilliseconds(_s.testFixedUnixMs).UtcDateTime;
                return _deviceClock.UtcNow.AddMilliseconds(_s.testOffsetMs);
            }
        }
        /// <summary>Freeze server time at an exact UTC instant (QA).</summary>
        public void SetTestClock(DateTime utc) { _s.testFixedUnixMs = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds(); Save(); }
        /// <summary>Run server time at real speed with an offset (QA).</summary>
        public void SetTestOffset(TimeSpan offset) { _s.testFixedUnixMs = -1; _s.testOffsetMs = (long)offset.TotalMilliseconds; Save(); }
        public void AdvanceTestClock(TimeSpan by)
        {
            if (_s.testFixedUnixMs >= 0) _s.testFixedUnixMs += (long)by.TotalMilliseconds; else _s.testOffsetMs += (long)by.TotalMilliseconds;
            Save();
        }
        public void ClearTestClock() { _s.testFixedUnixMs = -1; _s.testOffsetMs = 0; Save(); }
        public bool TestClockActive => _s.testFixedUnixMs >= 0 || _s.testOffsetMs != 0;

        // ---------------- persistence ----------------
        void Load()
        {
            try { _s = File.Exists(_path) ? JsonUtility.FromJson<ServerDb>(File.ReadAllText(_path)) : null; }
            catch { _s = null; }
            if (_s == null) _s = new ServerDb();
        }
        void Save() { File.WriteAllText(_path, JsonUtility.ToJson(_s)); }

        void Online() { if (SimulateOffline) throw new BackendException("offline"); Maintenance(); }
        static long Unix(DateTime d) => new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)).ToUnixTimeSeconds();

        // ---------------- account / wallet ----------------
        public Task<DateTime> GetServerTimeUtc() { Online(); return Task.FromResult(ServerNow); }
        public Task<AccountState> GetAccount() { Online(); return Task.FromResult(_s.account); }

        /// <summary>Fake OAuth link. SG-19: guest scores on currently open boards become visible.</summary>
        public Task<AccountState> LinkAccount(string provider)
        {
            Online();
            _s.account.linked = true; _s.account.provider = provider;
            _s.account.displayName = provider + " Wrecker";
            foreach (var b in _s.boards)
                if (!IsClosed(b)) foreach (var x in b.subs) if (x.playerId == _s.account.playerId) x.name = _s.account.displayName;
            Save();
            return Task.FromResult(_s.account);
        }

        public Task<Wallet> GetWallet() { Online(); return Task.FromResult(_s.wallet); }

        public Task<bool> ConsumeExtraCharge()
        {
            Online();
            if (_s.wallet.extraCharges <= 0) return Task.FromResult(false);
            _s.wallet.extraCharges--; Save();
            return Task.FromResult(true);
        }

        public Task<List<InboxItem>> GetInbox() { Online(); return Task.FromResult(_s.inbox.FindAll(i => !i.claimed && i.expiresUnixUtc > Unix(ServerNow))); }

        public Task<Wallet> ClaimInbox(string itemId)
        {
            Online();
            var it = _s.inbox.Find(i => i.id == itemId);
            if (it != null && !it.claimed && it.expiresUnixUtc > Unix(ServerNow)) { it.claimed = true; Grant(it.reward); Save(); }
            return Task.FromResult(_s.wallet);
        }

        /// <summary>SG-07: chest/unlock/gate granted on first clear only (server-tracked).</summary>
        public Task<Wallet> GrantCampaignReward(string levelId, Reward[] rewards)
        {
            Online();
            if (rewards != null && !_s.grantedFirstClears.Contains(levelId))
            {
                _s.grantedFirstClears.Add(levelId);
                foreach (var r in rewards) Grant(r);
                Save();
            }
            return Task.FromResult(_s.wallet);
        }

        void Grant(Reward r)
        {
            if (r == null) return;
            switch (r.type)
            {
                case "coins": _s.wallet.coins += r.amount; break;
                case "shard": _s.wallet.shards += r.amount; break;
                case "charge": _s.wallet.extraCharges += r.amount; break;
                case "cosmetic": if (!_s.wallet.cosmetics.Contains(r.id)) _s.wallet.cosmetics.Add(r.id); break;
                case "powerup": if (!_s.wallet.powerUps.Contains(r.id)) _s.wallet.powerUps.Add(r.id); break;
                case "chest": // id like "coins:300;shard:3"
                    foreach (var part in (r.id ?? "").Split(';'))
                    {
                        var kv = part.Split(':');
                        if (kv.Length == 2 && int.TryParse(kv[1], out var n)) Grant(new Reward { type = kv[0], amount = n });
                    }
                    break;
            }
        }

        // ---------------- Daily Titan ----------------
        /// <summary>Deterministic rotation from handcrafted templates (4 arenas × 6 rules), keyed on the UTC day.</summary>
        public static DailyTitanConfig BuildDailyConfig(TitanTemplates t, DateTime serverUtc)
        {
            int day = UtcDay.Index(serverUtc);
            var arena = t.arenas[Mod(day, t.arenas.Length)];
            var rule = t.rules[Mod(day * 5 + day / t.arenas.Length, t.rules.Length)];
            return new DailyTitanConfig
            {
                dayKey = UtcDay.Key(serverUtc), archetype = t.archetype, name = t.name, arenaId = arena.id, arenaName = arena.name,
                layout = arena.layout, ruleId = rule.id, ruleText = rule.text, mod = rule.mod,
                shots = Math.Max(1, t.baseShots + rule.mod.shotDelta), mastery = t.mastery, completionRewards = t.completionRewards,
                topPercent = t.topPercent, topPercentBonusShards = t.topPercentBonusShards, maxDamageScore = t.maxDamageScore,
                perShotLeftBonus = t.perShotLeftBonus, resetsAtUnixUtc = Unix(UtcDay.NextReset(serverUtc))
            };
        }
        static int Mod(int a, int n) => ((a % n) + n) % n;

        /// <summary>Optional server push: a JSON file named daily_titan_override_{dayKey}.json in persistentData replaces the rotation without a build.</summary>
        public Task<DailyTitanConfig> GetDailyTitan()
        {
            Online();
            var now = ServerNow;
            var overridePath = Path.Combine(Path.GetDirectoryName(_path) ?? ".", $"daily_titan_override_{UtcDay.Key(now)}.json");
            if (File.Exists(overridePath))
            {
                var o = JsonUtility.FromJson<DailyTitanConfig>(File.ReadAllText(overridePath));
                if (o != null) { o.dayKey = UtcDay.Key(now); o.resetsAtUnixUtc = Unix(UtcDay.NextReset(now)); return Task.FromResult(o); }
            }
            return Task.FromResult(BuildDailyConfig(_db.Titan, now));
        }

        RankedStatus Status(string dayKey)
        {
            var st = _s.ranked.Find(r => r.dayKey == dayKey);
            if (st == null) { st = new RankedStatus { dayKey = dayKey, status = "available" }; _s.ranked.Add(st); }
            return st;
        }

        public Task<RankedStatus> GetRankedStatus() { Online(); return Task.FromResult(Status(UtcDay.Key(ServerNow))); }

        /// <summary>One ranked attempt per UTC day; consumed when the run starts.</summary>
        public Task<RankedTicket> BeginRankedRun()
        {
            Online();
            var now = ServerNow; var key = UtcDay.Key(now);
            var st = Status(key);
            if (st.attemptUsed) throw new BackendException("Ranked attempt already used today");
            st.attemptUsed = true; st.status = "in_progress";
            var cfg = BuildDailyConfigOrOverride(now);
            var t = new RankedTicket { token = Guid.NewGuid().ToString("N"), dayKey = key, issuedUnixUtc = Unix(now), shotBudget = cfg.shots };
            _s.tickets.Add(new TicketRec { token = t.token, dayKey = key, shotBudget = cfg.shots });
            Save();
            return Task.FromResult(t);
        }
        DailyTitanConfig BuildDailyConfigOrOverride(DateTime now) => GetDailyTitan().Result;

        /// <summary>Deterministic FNV-1a checksum (stand-in for a server HMAC; not secure, stub only).</summary>
        public static string Sign(RankedResult r)
        {
            string s = $"{r.token}|{r.damageScore}|{r.shotsLeft}|{r.killed}|{r.partial}";
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            return h.ToString("x8");
        }

        /// <summary>
        /// SG-10/SG-22: a run belongs to the UTC day it started; accepted while serverNow &lt;= nextReset + 15:00.000 (inclusive);
        /// later submissions are rejected and refunded as practice. SG-11: partial results (app killed) are accepted the same way.
        /// </summary>
        public Task<SubmitResponse> SubmitRanked(RankedResult r)
        {
            Online();
            var now = ServerNow;
            var tk = _s.tickets.Find(t => t.token == r.token);
            if (tk == null) return Task.FromResult(new SubmitResponse { status = SubmitStatus.RejectedInvalid, message = "Unknown run token" });
            if (tk.used) return Task.FromResult(new SubmitResponse { status = SubmitStatus.RejectedDuplicate, message = "Already submitted" });
            var st = Status(tk.dayKey);
            var t = _db.Titan;
            int maxScore = t.maxDamageScore + t.perShotLeftBonus * tk.shotBudget;
            bool valid = r.signature == Sign(r) && r.damageScore >= 0 && r.damageScore <= t.maxDamageScore &&
                         r.shotsLeft >= 0 && r.shotsLeft <= tk.shotBudget;
            if (!valid) return Task.FromResult(new SubmitResponse { status = SubmitStatus.RejectedInvalid, message = "Failed validation" });
            tk.used = true;
            var dayStart = DateTime.ParseExact(tk.dayKey, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
            var deadline = dayStart.AddDays(1).AddMinutes(t.rankedGraceMinutes);
            if (now > deadline)
            {
                st.status = "refunded_practice"; Save();
                return Task.FromResult(new SubmitResponse { status = SubmitStatus.RefundedAsPractice, message = "Submitted after the grace window; counted as practice" });
            }
            int score = TitanScore(r.damageScore, r.shotsLeft, r.killed, t);
            if (score > maxScore) score = maxScore;
            st.submitted = true; st.score = score; st.status = r.partial ? "submitted_partial" : "submitted";
            AddSub(BoardType.DailyTitan, tk.dayKey, score, now);
            if (r.killed && !_s.titanClearDays.Contains(tk.dayKey))
            {
                _s.titanClearDays.Add(tk.dayKey);
                _s.seasonXp += _db.Season.xpPerTitanClear;
                foreach (var cr in t.completionRewards) Grant(cr);
                AddSub(BoardType.Season, SeasonKey(now), _s.seasonXp, now, replace: true);
            }
            Save();
            return Task.FromResult(new SubmitResponse { status = SubmitStatus.Accepted, finalScore = score, message = "Ranked score accepted" });
        }

        /// <summary>DP §3: damage dealt (0..10000) + 500 per shot left, bonus only on a kill.</summary>
        public static int TitanScore(int damageScore, int shotsLeft, bool killed, TitanTemplates t)
        {
            int d = Math.Max(0, Math.Min(t.maxDamageScore, damageScore));
            return d + (killed ? t.perShotLeftBonus * Math.Max(0, shotsLeft) : 0);
        }

        // ---------------- Daily rewards ----------------
        public Task<DailyRewardState> GetDailyRewardState()
        {
            Online();
            var now = ServerNow;
            return Task.FromResult(new DailyRewardState
            {
                streakIndex = _s.streakIndex, lastClaimDayKey = _s.lastClaimDayKey,
                canClaimToday = _s.lastClaimDayKey != UtcDay.Key(now), nextResetUnixUtc = Unix(UtcDay.NextReset(now)),
                days = _db.DailyRewards.days
            });
        }

        /// <summary>SG-14: one claim per UTC day; missed days pause the streak, it never resets. Loops after day 7.</summary>
        public Task<Wallet> ClaimDailyReward()
        {
            Online();
            var key = UtcDay.Key(ServerNow);
            if (_s.lastClaimDayKey == key) throw new BackendException("Already claimed today");
            var days = _db.DailyRewards.days;
            Grant(days[_s.streakIndex % days.Length]);
            _s.streakIndex = (_s.streakIndex + 1) % days.Length;
            _s.lastClaimDayKey = key;
            Save();
            return Task.FromResult(_s.wallet);
        }

        // ---------------- Season / events ----------------
        DateTime SeasonStart => DateTime.Parse(_db.Season.startUtc, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal);
        string SeasonKey(DateTime now) => _db.Season.id;
        string WeekKey(DateTime now) { int w = WeekIndex(now); return _db.Season.id + "_w" + w; }
        int WeekIndex(DateTime now) => (int)Math.Floor((now - SeasonStart).TotalDays / _db.Season.weekDays);
        DateTime SeasonEnd => SeasonStart.AddDays(_db.Season.weekDays * _db.Season.weeks.Length);

        public static int TierForXp(SeasonData s, int xp)
        {
            int tier = 0;
            foreach (var t in s.tiers) if (xp >= t.xp) tier = t.tier;
            return tier;
        }

        /// <summary>SG-16: past tier 30, one bonus chest per extra 300 XP, max 10.</summary>
        public static int BonusChests(SeasonData s, int xp)
        {
            int cap = s.tiers[s.tiers.Length - 1].xp;
            if (xp <= cap) return 0;
            return Math.Min(s.bonusChestCap, (xp - cap) / s.bonusChestEveryXp);
        }

        public Task<SeasonState> GetSeasonState()
        {
            Online();
            return Task.FromResult(BuildSeasonState());
        }

        SeasonState BuildSeasonState()
        {
            var now = ServerNow; var s = _db.Season;
            int w = WeekIndex(now);
            var st = new SeasonState
            {
                seasonId = s.id, name = s.name, xp = _s.seasonXp, tier = TierForXp(s, _s.seasonXp), weekIndex = w,
                weekEventId = w >= 0 && w < s.weeks.Length ? s.weeks[w].id : "",
                weekEndsUnixUtc = Unix(SeasonStart.AddDays((w + 1) * s.weekDays)), seasonEndsUnixUtc = Unix(SeasonEnd),
                premium = _s.premium, bonusChests = BonusChests(s, _s.seasonXp),
                claimedFree = new List<int>(_s.claimedFree), claimedPremium = new List<int>(_s.claimedPremium)
            };
            return st;
        }

        /// <summary>SG-23: event stars = best single run, capped at 3, never summed; XP only for stars above the previous best.</summary>
        public Task<SeasonState> ReportEventStars(string eventLevelId, int stars)
        {
            Online();
            var s = _db.Season;
            stars = Math.Max(0, Math.Min(s.eventStarCapPerLevel, stars));
            var best = _s.eventBest.Find(e => e.levelId == eventLevelId);
            if (best == null) { best = new EventBest { levelId = eventLevelId }; _s.eventBest.Add(best); }
            if (stars > best.stars)
            {
                _s.seasonXp += (stars - best.stars) * s.xpPerEventStar;
                best.stars = stars;
                var now = ServerNow;
                AddSub(BoardType.WeeklyEvent, WeekKey(now), WeeklyStars(now), now, replace: true);
                AddSub(BoardType.Season, SeasonKey(now), _s.seasonXp, now, replace: true);
            }
            Save();
            return Task.FromResult(BuildSeasonState());
        }

        public int BestEventStars(string levelId) => _s.eventBest.Find(e => e.levelId == levelId)?.stars ?? 0;

        int WeeklyStars(DateTime now)
        {
            int w = WeekIndex(now); var s = _db.Season;
            if (w < 0 || w >= s.weeks.Length) return 0;
            int sum = 0;
            foreach (var l in s.weeks[w].levels) sum += BestEventStars(l.id);
            return sum;
        }

        public Task<int> SubmitEventScore(string eventLevelId, int stars) { ReportEventStars(eventLevelId, stars); return Task.FromResult(WeeklyStars(ServerNow)); }

        public Task<SeasonState> ClaimSeasonTier(int tier, bool premium)
        {
            Online();
            var s = _db.Season;
            if (tier < 1 || tier > s.tiers.Length) throw new BackendException("Bad tier");
            if (TierForXp(s, _s.seasonXp) < tier) throw new BackendException("Tier not reached");
            if (premium && !_s.premium) throw new BackendException("Premium track (cosmetic only) not owned");
            var list = premium ? _s.claimedPremium : _s.claimedFree;
            if (!list.Contains(tier)) { list.Add(tier); var t = s.tiers[tier - 1]; Grant(premium ? t.premium : t.free); Save(); }
            return Task.FromResult(BuildSeasonState());
        }

        // ---------------- Leaderboards ----------------
        string CurrentKey(BoardType b, DateTime now) => b == BoardType.DailyTitan ? UtcDay.Key(now) : b == BoardType.WeeklyEvent ? WeekKey(now) : SeasonKey(now);
        string PreviousKey(BoardType b, DateTime now) => b == BoardType.DailyTitan ? UtcDay.Key(now.AddDays(-1)) :
            b == BoardType.WeeklyEvent ? _db.Season.id + "_w" + (WeekIndex(now) - 1) : "s0_previous";

        Board GetBoard(BoardType type, string key)
        {
            var b = _s.boards.Find(x => x.type == (int)type && x.key == key);
            if (b != null) return b;
            b = new Board { key = key, type = (int)type };
            var rng = new System.Random(StableSeed(key + type));
            int max = type == BoardType.DailyTitan ? 12000 : type == BoardType.WeeklyEvent ? 15 : 4500;
            var baseT = new DateTime(2026, 1, 1).Ticks;
            for (int i = 0; i < BotCount; i++)
                b.subs.Add(new Sub { playerId = "bot_" + i, name = BotName(i), score = rng.Next(0, max), ticks = baseT + rng.Next(0, 1000000) * 10000L, friend = i % 17 == 0 });
            _s.boards.Add(b);
            return b;
        }

        static int StableSeed(string s) { int h = 17; foreach (char c in s) h = h * 31 + c; return h; }
        static readonly string[] A = { "Brick", "Gear", "Tide", "Ember", "Sky", "Rust", "Crane", "Bolt", "Rivet", "Storm" };
        static readonly string[] N = { "Breaker", "Smith", "Runner", "Wrecker", "Pilot", "Fox", "Hammer", "Spark" };
        static string BotName(int i) => A[i % A.Length] + N[(i / A.Length) % N.Length] + (i % 7 == 0 ? "TheVeryLongNamedDemolisher" : (i % 100).ToString());

        void AddSub(BoardType type, string key, int score, DateTime now, bool replace = false)
        {
            var b = GetBoard(type, key);
            var me = b.subs.Find(x => x.playerId == _s.account.playerId);
            if (me == null) b.subs.Add(new Sub { playerId = _s.account.playerId, name = _s.account.displayName, score = score, ticks = now.Ticks });
            else if (replace || score > me.score) { me.score = score; me.ticks = now.Ticks; }
        }

        bool IsClosed(Board b)
        {
            var now = ServerNow;
            if (b.type == (int)BoardType.DailyTitan) return b.key != UtcDay.Key(now);
            if (b.type == (int)BoardType.WeeklyEvent) return b.key != WeekKey(now);
            return b.key != SeasonKey(now) || now >= SeasonEnd;
        }

        /// <summary>Sort: score desc, ties by earlier submission (DP §3).</summary>
        static List<Sub> Ranked(Board b, bool friendsOnly, string me)
        {
            var l = b.subs.FindAll(x => !friendsOnly || x.friend || x.playerId == me);
            l.Sort((x, y) => x.score != y.score ? y.score.CompareTo(x.score) : x.ticks.CompareTo(y.ticks));
            return l;
        }

        public Task<LeaderboardPage> GetLeaderboard(BoardType board, BoardPeriod period, bool friendsOnly)
        {
            Online();
            var now = ServerNow;
            var key = period == BoardPeriod.Current ? CurrentKey(board, now) : PreviousKey(board, now);
            var b = GetBoard(board, key);
            var me = _s.account.playerId;
            bool guest = !_s.account.linked;
            var all = Ranked(b, friendsOnly, me);
            var page = new LeaderboardPage { board = board, periodKey = key, closed = IsClosed(b), fetchedUnixUtc = Unix(now) };
            int rank = 0;
            foreach (var x in all)
            {
                bool isMe = x.playerId == me;
                if (isMe && guest) { page.meIsGuestHidden = true; continue; } // guests stored, not listed until linked
                rank++;
                var e = new LeaderboardEntry { rank = rank, playerId = x.playerId, name = x.name, score = x.score, submittedTicks = x.ticks, isFriend = x.friend, isMe = isMe };
                if (isMe) page.me = e;
                if (rank <= 100) page.top.Add(e);
            }
            page.totalEntries = rank;
            Save();
            return Task.FromResult(page);
        }

        // ---------------- scheduled jobs (run lazily on each call) ----------------
        /// <summary>SG-12: top-10% bonus once a daily board closes (+grace); SG-17: unclaimed season tiers to inbox for 30 days.</summary>
        void Maintenance()
        {
            var now = ServerNow; var t = _db.Titan;
            foreach (var b in _s.boards)
            {
                if (b.type != (int)BoardType.DailyTitan || b.processed) continue;
                var start = DateTime.ParseExact(b.key, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
                if (now <= start.AddDays(1).AddMinutes(t.rankedGraceMinutes)) continue;
                b.processed = true;
                var all = Ranked(b, false, _s.account.playerId);
                int idx = all.FindIndex(x => x.playerId == _s.account.playerId);
                if (idx >= 0 && idx < Math.Ceiling(all.Count * t.topPercent))
                    _s.inbox.Add(new InboxItem { id = "titan_top_" + b.key, source = "Daily Titan top 10% (" + b.key + ")",
                        reward = new Reward { type = "shard", amount = t.topPercentBonusShards }, expiresUnixUtc = Unix(now.AddDays(30)) });
            }
            if (!_s.seasonEndProcessed && now >= SeasonEnd)
            {
                _s.seasonEndProcessed = true;
                int tier = TierForXp(_db.Season, _s.seasonXp);
                for (int i = 1; i <= tier; i++)
                    if (!_s.claimedFree.Contains(i))
                        _s.inbox.Add(new InboxItem { id = "season_tier_" + i, source = "Unclaimed season tier " + i, reward = _db.Season.tiers[i - 1].free,
                            expiresUnixUtc = Unix(now.AddDays(_db.Season.unclaimedInboxDays)) });
            }
        }
    }
}
