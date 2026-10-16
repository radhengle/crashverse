using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Crashverse.Data;
using UnityEngine;

#if CRASHVERSE_DEV // Security Review §1: dev/QA only. Never compiled into release builds.
namespace Crashverse.Backend
{
    /// <summary>
    /// Local stand-in for the authoritative server. Reads bundled JSON (ContentDatabase), keeps its own
    /// "server DB" file (separate from the campaign save) and a deterministic fake leaderboard.
    /// Time (BUG-03/BUG-12): ServerNow comes from an authoritative clock (NetworkServerClock: HTTP Date of the backend host,
    /// advanced monotonically), never from the device clock. QA can override it (SetTestClock / SetTestOffset, persisted).
    /// Ranked integrity (SEC-01): per-run HMAC key and run state live only in this server DB; the client journal is never trusted
    /// beyond what the server already signed, plus a plausibility-checked offline tail.
    /// Limitations of the on-device stub: the DB file is on the device, so reinstall wipes it and a rooted device could edit it.
    /// Reinstall-proof streaks/ranked and real tamper resistance need the real server behind IBackendService.
    /// </summary>
    public class LocalBackendService : IBackendService
    {
        [Serializable] class Sub { public string playerId; public string name; public int score; public long ticks; public bool friend; }
        [Serializable] class Board { public string key; public int type; public bool processed; public List<Sub> subs = new List<Sub>(); }
        [Serializable] class EventBest { public string levelId; public int stars; }
        [Serializable] class LevelBestRec { public string levelId; public int best; }
        [Serializable] class TicketRec
        {
            public string token; public string dayKey; public int shotBudget; public bool used;
            public string key; // SEC-01: per-run HMAC key, server-side only
            public int lastShotIndex; public int lastDamage; public int lastShotsLeft; public bool lastKilled;
        }

        [Serializable]
        class ServerDb
        {
            public long testFixedUnixMs = -1; public long testOffsetMs; // QA clock override
            public AccountState account = new AccountState();
            public string authEmail = ""; public List<string> attempts = new List<string>();
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
            public bool seasonEndProcessed; // BUG-15: set when a season end has been processed (rollover idempotency/diagnostics)
            public List<LevelBestRec> campaignLevels = new List<LevelBestRec>(); // BUG-13: server-held best per level
            public List<string> processedReports = new List<string>();
        }

        readonly ContentDatabase _db;
        readonly IClock _clock; // authoritative server clock, never the device clock (BUG-03)
        public const string DefaultTimeUrl = "https://www.google.com/generate_204";
        readonly string _path;
        ServerDb _s;
        public bool SimulateOffline { get; set; }
        public bool IsOnline => !SimulateOffline;
        public const int BotCount = 240;

        public LocalBackendService(ContentDatabase db, IClock clock = null, string path = null)
        {
            _db = db;
            _clock = clock ?? new NetworkServerClock(NetworkServerClock.HttpDate(DefaultTimeUrl));
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
                return _clock.UtcNow.AddMilliseconds(_s.testOffsetMs);
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

        void Online()
        {
            if (SimulateOffline) throw new BackendException("offline");
            if (_s.testFixedUnixMs < 0 && _clock is ISyncableClock sc && !sc.Synced) throw new BackendException("server time unavailable");
            Maintenance();
        }
        static long Unix(DateTime d) => new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)).ToUnixTimeSeconds();

        // ---------------- account / wallet ----------------
        public async Task<DateTime> GetServerTimeUtc()
        {
            if (SimulateOffline) throw new BackendException("offline");
            if (_clock is ISyncableClock sc) await sc.SyncAsync();
            Online();
            return ServerNow;
        }
        public Task<AccountState> GetAccount() { Online(); return Task.FromResult(_s.account); }

        /// <summary>Fake OAuth link. SG-19: guest scores on currently open boards become visible.</summary>
        public Task<AccountState> LinkAccount(AuthProvider provider, string credential)
        {
            Online();
            if (string.IsNullOrEmpty(credential)) throw new BackendException("missing provider credential");
            if (provider == AuthProvider.X && !Flags.xLoginEnabled) throw new BackendException("X login disabled");
            _s.account.linked = true; _s.account.provider = provider.ToString();
            _s.account.displayName = provider + " Wrecker";
            foreach (var b in _s.boards)
                if (!IsClosed(b)) foreach (var x in b.subs) if (x.playerId == _s.account.playerId) x.name = _s.account.displayName;
            Save();
            return Task.FromResult(_s.account);
        }

        // ---- Email auth stub: stores the email only; the password is checked for policy then discarded (never persisted). ----
        public FeatureFlags Flags = new FeatureFlags(); // dev stub of the server flag set
        public Task<FeatureFlags> GetFeatureFlags() { Online(); return Task.FromResult(Flags); }
        static bool ValidEmail(string e) => !string.IsNullOrEmpty(e) && e.IndexOf('@') > 0 && e.LastIndexOf('.') > e.IndexOf('@');
        AuthSession Session(string email) => new AuthSession { playerId = _s.account.playerId, email = email, sessionToken = "sess_" + Guid.NewGuid().ToString("N"), expiresUnixUtc = Unix(ServerNow.AddHours(1)) };
        public Task<AuthSession> AuthEmailRegister(string email, string password)
        {
            Online();
            if (!Flags.emailAuthEnabled) throw new BackendException("email auth disabled");
            if (!ValidEmail(email)) throw new BackendException("invalid email");
            if (string.IsNullOrEmpty(password) || password.Length < 8) throw new BackendException("password too short"); // TODO(Security Admin): real policy
            _s.authEmail = email.Trim().ToLowerInvariant(); Save();
            return Task.FromResult(Session(_s.authEmail));
        }
        public Task<AuthSession> AuthEmailLogin(string email, string password)
        {
            Online();
            if (!ValidEmail(email) || string.IsNullOrEmpty(password)) throw new BackendException("invalid credentials");
            if (email.Trim().ToLowerInvariant() != _s.authEmail) throw new BackendException("invalid credentials");
            return Task.FromResult(Session(_s.authEmail));
        }
        public Task<bool> RequestPasswordReset(string email) { Online(); return Task.FromResult(true); } // always true: no account enumeration
        public Task<DeleteAccountResponse> DeleteAccount()
        {
            Online();
            var keepClock = (_s.testFixedUnixMs, _s.testOffsetMs);
            _s = new ServerDb { testFixedUnixMs = keepClock.Item1, testOffsetMs = keepClock.Item2 };
            _s.account.playerId = "guest_" + Guid.NewGuid().ToString("N").Substring(0, 8); _s.account.displayName = "Guest Wrecker";
            Save();
            return Task.FromResult(new DeleteAccountResponse { accepted = true, message = "deleted", scheduledUnixUtc = Unix(ServerNow) });
        }
        public Task<AttemptToken> BeginAttempt(string levelId)
        {
            Online();
            var t = new AttemptToken { token = "att_" + Guid.NewGuid().ToString("N"), levelId = levelId, seed = new System.Random().Next(), issuedUnixUtc = Unix(ServerNow), expiresUnixUtc = Unix(ServerNow.AddHours(2)) };
            _s.attempts.Add(t.token); Save();
            return Task.FromResult(t);
        }
        public Task<InputLogResponse> SubmitInputLog(InputLog log)
        {
            Online();
            if (log == null || !_s.attempts.Remove(log.attemptToken)) return Task.FromResult(new InputLogResponse { accepted = false, message = "unknown or used attempt token" });
            Save();
            return Task.FromResult(new InputLogResponse { accepted = true, message = "stored (replay validation is server-side TODO)" });
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
                perShotLeftBonus = t.perShotLeftBonus, resetsAtUnixUtc = Unix(UtcDay.NextReset(serverUtc)),
                hintTarget = arena.hintTarget, hintYaw = arena.hintYaw, hintPitch = arena.hintPitch
            };
        }
        static int Mod(int a, int n) => ((a % n) + n) % n;

        /// <summary>Dev stub of the server's daily config. A live override is a server concern; set DevOverride (QA) to simulate one.
        /// No local-file override path exists in any build (Security Review §3).</summary>
        public DailyTitanConfig DevOverride;
        public Task<DailyTitanConfig> GetDailyTitan()
        {
            Online();
            var now = ServerNow;
            if (DevOverride != null) { DevOverride.dayKey = UtcDay.Key(now); DevOverride.resetsAtUnixUtc = Unix(UtcDay.NextReset(now)); return Task.FromResult(DevOverride); }
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
            var k = new byte[32]; using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create()) rng.GetBytes(k);
            _s.tickets.Add(new TicketRec { token = t.token, dayKey = key, shotBudget = cfg.shots, key = Convert.ToBase64String(k),
                                           lastShotIndex = 0, lastDamage = 0, lastShotsLeft = cfg.shots });
            Save();
            return Task.FromResult(t);
        }
        DailyTitanConfig BuildDailyConfigOrOverride(DateTime now) => GetDailyTitan().Result;

        /// <summary>SEC-01: HMAC-SHA256 over the canonical checkpoint with the run's server-held key.</summary>
        static string Receipt(TicketRec tk, RankedCheckpoint c)
        {
            using (var h = new System.Security.Cryptography.HMACSHA256(Convert.FromBase64String(tk.key)))
            {
                var msg = System.Text.Encoding.UTF8.GetBytes($"{tk.token}|{c.shotIndex}|{c.damageScore}|{c.shotsLeft}|{(c.killed ? 1 : 0)}");
                return Convert.ToBase64String(h.ComputeHash(msg));
            }
        }

        static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int d = 0; for (int i = 0; i < a.Length; i++) d |= a[i] ^ b[i];
            return d == 0;
        }

        /// <summary>Validates one shot against the previous state (index +1, shots -1, damage monotonic and bounded, nothing after a kill).</summary>
        string CheckStep(TicketRec tk, int prevIndex, int prevDamage, bool prevKilled, RankedCheckpoint c, int maxGain)
        {
            var t = _db.Titan;
            if (prevKilled) return "run already finished";
            if (c.shotIndex != prevIndex + 1) return "shot out of order";
            if (c.shotsLeft != tk.shotBudget - c.shotIndex || c.shotsLeft < 0) return "shot count mismatch";
            if (c.damageScore < prevDamage || c.damageScore > t.maxDamageScore) return "damage out of range";
            if (c.damageScore - prevDamage > maxGain) return "implausible damage for one shot";
            return null;
        }

        /// <summary>SEC-01: online per-shot report. Server state advances only through validated steps; returns the server receipt.</summary>
        public Task<RankedCheckpoint> ReportRankedCheckpoint(RankedCheckpoint c)
        {
            Online();
            var tk = _s.tickets.Find(x => x.token == c?.token);
            if (tk == null) throw new BackendException("Unknown run token");
            if (tk.used) throw new BackendException("Run already submitted");
            if (c.shotIndex == tk.lastShotIndex && c.damageScore == tk.lastDamage && c.killed == tk.lastKilled && c.shotsLeft == tk.lastShotsLeft)
            { c.receipt = Receipt(tk, c); return Task.FromResult(c); } // idempotent retry
            var err = CheckStep(tk, tk.lastShotIndex, tk.lastDamage, tk.lastKilled, c, _db.Titan.offlineShotDamageCap); // same per-shot cap online and offline
            if (err != null) throw new BackendException("Checkpoint rejected: " + err);
            tk.lastShotIndex = c.shotIndex; tk.lastDamage = c.damageScore; tk.lastShotsLeft = c.shotsLeft; tk.lastKilled = c.killed;
            c.receipt = Receipt(tk, c);
            Save();
            return Task.FromResult(c);
        }

        /// <summary>
        /// SEC-01 / BUG-04 / SG-11 / SG-22. Score source of truth = the server's own run state. From the journal the server only takes
        /// (a) checkpoints it signed (verified; a forged/edited one is ignored and flagged) and (b) an unsigned offline tail after its
        /// last state, accepted step by step only if plausible (offlineShotDamageCap per shot). The kill flag (and so the kill bonus,
        /// Titan clear XP and chest) survives queuing. The client never computes a signature.
        /// </summary>
        public Task<SubmitResponse> FinalizeRanked(RankedJournal j)
        {
            Online();
            var tk = _s.tickets.Find(x => x.token == j?.token);
            if (tk == null) return Task.FromResult(new SubmitResponse { status = SubmitStatus.RejectedInvalid, message = "Unknown run token" });
            if (tk.used) return Task.FromResult(new SubmitResponse { status = SubmitStatus.RejectedDuplicate, message = "Already submitted" });
            int idx = tk.lastShotIndex, dmg = tk.lastDamage, left = tk.lastShotsLeft; bool killed = tk.lastKilled;
            bool tampered = false, usedTail = false;
            var list = new List<RankedCheckpoint>(j.checkpoints ?? new List<RankedCheckpoint>());
            list.Sort((x, y) => x.shotIndex.CompareTo(y.shotIndex));
            foreach (var c in list)
            {
                if (c == null || c.token != tk.token) { tampered = true; continue; }
                if (!string.IsNullOrEmpty(c.receipt))
                {
                    if (!FixedTimeEquals(c.receipt, Receipt(tk, c))) tampered = true; // edited after signing
                    continue; // valid signed checkpoints are already in server state
                }
                if (c.shotIndex <= idx) { if (c.shotIndex < idx || c.damageScore != dmg || c.killed != killed) tampered = true; continue; }
                if (tampered) break; // never extend an edited journal
                if (CheckStep(tk, idx, dmg, killed, c, _db.Titan.offlineShotDamageCap) != null) { tampered = true; break; }
                idx = c.shotIndex; dmg = c.damageScore; left = c.shotsLeft; killed = c.killed; usedTail = true;
            }
            bool final = j.final || killed || left == 0;
            var resp = Commit(tk, dmg, left, killed, !final);
            if (tampered) resp.message += " (journal failed verification; server-held run state used)";
            else if (usedTail) resp.message += " (offline shots accepted after plausibility checks)";
            return Task.FromResult(resp);
        }

        SubmitResponse Commit(TicketRec tk, int damage, int shotsLeft, bool killed, bool partial)
        {
            var now = ServerNow; var t = _db.Titan;
            var st = Status(tk.dayKey);
            tk.used = true;
            var dayStart = DateTime.ParseExact(tk.dayKey, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
            var deadline = dayStart.AddDays(1).AddMinutes(t.rankedGraceMinutes);
            if (now > deadline)
            {
                st.status = "refunded_practice"; Save();
                return new SubmitResponse { status = SubmitStatus.RefundedAsPractice, message = "Submitted after the grace window; counted as practice" };
            }
            int score = Math.Min(TitanScore(damage, shotsLeft, killed, t), t.maxDamageScore + t.perShotLeftBonus * tk.shotBudget);
            st.submitted = true; st.score = score; st.status = partial ? "submitted_partial" : "submitted";
            AddSub(BoardType.DailyTitan, tk.dayKey, score, now);
            if (killed && !_s.titanClearDays.Contains(tk.dayKey))
            {
                _s.titanClearDays.Add(tk.dayKey);
                _s.seasonXp += _db.Season.xpPerTitanClear;
                foreach (var cr in t.completionRewards) Grant(cr);
                AddSub(BoardType.Season, SeasonKey(now), _s.seasonXp, now, replace: true);
            }
            Save();
            return new SubmitResponse { status = SubmitStatus.Accepted, finalScore = score, message = "Ranked score accepted" };
        }

        /// <summary>DP §3: damage dealt (0..10000) + 500 per shot left, bonus only on a kill.</summary>
        public static int TitanScore(int damageScore, int shotsLeft, bool killed, TitanTemplates t) => TitanScoring.Score(damageScore, shotsLeft, killed, t);

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
        // BUG-06: seasons roll over. The bundled season is the template; instance N starts at startUtc + N × length and gets
        // id "{id}" (N = 0) or "{id}_r{N}". XP, tiers, claims, premium and event bests reset; campaign is untouched.
        DateTime FirstSeasonStart => DateTime.Parse(_db.Season.startUtc, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal);
        int SeasonDays => _db.Season.weekDays * _db.Season.weeks.Length;
        int SeasonIndex(DateTime now) => (int)Math.Floor((now - FirstSeasonStart).TotalDays / SeasonDays);
        DateTime SeasonStartFor(DateTime now) => FirstSeasonStart.AddDays((double)SeasonIndex(now) * SeasonDays);
        string SeasonIdFor(int n) => n == 0 ? _db.Season.id : n < 0 ? _db.Season.id + "_pre" + (-n) : _db.Season.id + "_r" + n;
        string SeasonKey(DateTime now) => SeasonIdFor(SeasonIndex(now));
        string WeekKey(DateTime now) => SeasonKey(now) + "_w" + WeekIndex(now);
        int WeekIndex(DateTime now) => (int)Math.Floor((now - SeasonStartFor(now)).TotalDays / _db.Season.weekDays);
        DateTime SeasonEndFor(DateTime now) => SeasonStartFor(now).AddDays(SeasonDays);

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
                seasonId = SeasonKey(now), name = SeasonIndex(now) == 0 ? s.name : s.name.Replace("Season 1", "Season " + (SeasonIndex(now) + 1)), xp = _s.seasonXp, tier = TierForXp(s, _s.seasonXp), weekIndex = w,
                weekEventId = w >= 0 && w < s.weeks.Length ? s.weeks[w].id : "",
                weekEndsUnixUtc = Unix(SeasonStartFor(now).AddDays((w + 1) * s.weekDays)), seasonEndsUnixUtc = Unix(SeasonEndFor(now)),
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

        /// <summary>Store stub: the premium season track is cosmetic-only (VM-13). Per season; cleared on rollover.</summary>
#if CRASHVERSE_TESTS
        // Test-only stand-in for IServerEntitlements.GrantPremiumPass (server-only). Not present in any player build.
        public void GrantPremiumPassForTests() { _s.premium = true; Save(); }
#endif

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
        const string CampaignKey = "campaign_c1_alltime";
        string CurrentKey(BoardType b, DateTime now) => b == BoardType.DailyTitan ? UtcDay.Key(now) : b == BoardType.WeeklyEvent ? WeekKey(now) :
            b == BoardType.Campaign ? CampaignKey : SeasonKey(now);
        /// <summary>BUG-07: the real archived key of the previous period (yesterday / last week, across seasons / last season).</summary>
        string PreviousKey(BoardType b, DateTime now) => b == BoardType.DailyTitan ? UtcDay.Key(now.AddDays(-1)) :
            b == BoardType.WeeklyEvent ? WeekKey(now.AddDays(-_db.Season.weekDays)) :
            b == BoardType.Campaign ? CampaignKey : SeasonIdFor(SeasonIndex(now) - 1);

        Board GetBoard(BoardType type, string key, bool create = true)
        {
            var b = _s.boards.Find(x => x.type == (int)type && x.key == key);
            if (b != null || !create) return b;
            b = new Board { key = key, type = (int)type };
            var rng = new System.Random(StableSeed(key + type));
            int max = type == BoardType.DailyTitan ? 12000 : type == BoardType.WeeklyEvent ? 15 : type == BoardType.Campaign ? 40000 : 4500;
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
            else if (score > me.score) { me.score = score; me.ticks = now.Ticks; } // BUG-16: timestamp moves only when the score rises (ties = first to reach)
            else if (replace && score < me.score) me.score = score;               // replace boards may correct downwards; never refresh the timestamp
        }

        bool IsClosed(Board b)
        {
            var now = ServerNow;
            if (b.type == (int)BoardType.DailyTitan) return b.key != UtcDay.Key(now);
            if (b.type == (int)BoardType.WeeklyEvent) return b.key != WeekKey(now);
            if (b.type == (int)BoardType.Campaign) return false;
            return b.key != SeasonKey(now);
        }

        /// <summary>Sort: score desc, ties by earlier submission (DP §3).</summary>
        static List<Sub> Ranked(Board b, bool friendsOnly, string me)
        {
            var l = b.subs.FindAll(x => !friendsOnly || x.friend || x.playerId == me);
            l.Sort((x, y) => x.score != y.score ? y.score.CompareTo(x.score) : x.ticks.CompareTo(y.ticks));
            return l;
        }

        /// <summary>
        /// SC-13 / BUG-13 / SG-07. Validates a reported win against content and server-held progress (level exists, prerequisite
        /// cleared on the server, shots/ratio/combo within bounds), recomputes the score with CampaignScore, keeps the best
        /// per level and recomputes the board total from those bests. First validated clear grants the level's content reward.
        /// Residual (needs real server): the reported stats themselves come from the client; only their bounds are checked.
        /// </summary>
        public Task<CampaignSubmitResponse> SubmitCampaignResult(CampaignRunReport r)
        {
            Online();
            if (r != null && !string.IsNullOrEmpty(r.reportId) && _s.processedReports.Contains(r.reportId))
                return Task.FromResult(new CampaignSubmitResponse { accepted = true, message = "Already synced", levelBest = LevelBest(r.levelId), total = CampaignTotal() });
            var err = ValidateCampaign(r, out var lvl);
            if (err != null) return Task.FromResult(new CampaignSubmitResponse { accepted = false, message = "Rejected: " + err, total = CampaignTotal() });
            int score = Crashverse.Meta.CampaignScore.Compute(true, r.destroyedRatio, r.shotsLeft, r.bestCombo, r.mastery);
            var rec = _s.campaignLevels.Find(x => x.levelId == lvl.id);
            if (rec == null) { rec = new LevelBestRec { levelId = lvl.id }; _s.campaignLevels.Add(rec); }
            if (score > rec.best) rec.best = score;
            bool first = !_s.grantedFirstClears.Contains(lvl.id);
            if (first) { _s.grantedFirstClears.Add(lvl.id); if (lvl.reward != null) foreach (var rw in lvl.reward) Grant(rw); }
            if (!string.IsNullOrEmpty(r.reportId)) _s.processedReports.Add(r.reportId);
            int total = CampaignTotal();
            AddSub(BoardType.Campaign, CampaignKey, total, ServerNow, replace: true);
            Save();
            return Task.FromResult(new CampaignSubmitResponse { accepted = true, message = "Campaign result synced", levelBest = rec.best, total = total, firstClear = first });
        }

        string ValidateCampaign(CampaignRunReport r, out LevelEntry lvl)
        {
            lvl = null;
            if (r == null) return "empty report";
            lvl = Array.Find(_db.Chapter1.levels, l => l.id == r.levelId);
            if (lvl == null) return "unknown level";
            if (!r.won) return "only wins are recorded";
            if (!string.IsNullOrEmpty(lvl.requires) && !_s.grantedFirstClears.Contains(lvl.requires)) return "prerequisite level not cleared on server";
            if (r.shotsLeft < 0 || r.shotsLeft > lvl.shots - 1) return "shots left out of range";
            if (float.IsNaN(r.destroyedRatio) || r.destroyedRatio < 0f || r.destroyedRatio > 1f) return "ratio out of range";
            if (!lvl.IsBoss && !Crashverse.Meta.WinRule.Meets(r.destroyedRatio, lvl.winThreshold)) return "ratio below win threshold"; // BUG-17: exact, basis points
            int pieces = _db.GetLayout(lvl.layout)?.blocks?.Length ?? 0;
            if (r.bestCombo < 0 || r.bestCombo > pieces) return "combo out of range";
            return null;
        }

        int LevelBest(string id) => _s.campaignLevels.Find(x => x.levelId == id)?.best ?? 0;
        public int CampaignTotal() { int t = 0; foreach (var x in _s.campaignLevels) t += x.best; return t; }

        public int CampaignScoreCeiling()
        {
            int sum = 0;
            foreach (var l in _db.Chapter1.levels)
            {
                int pieces = _db.GetLayout(l.layout)?.blocks?.Length ?? 0;
                sum += 1000 + 500 * l.shots + 50 * pieces + 1000;
            }
            return sum;
        }

        public Task<LeaderboardPage> GetLeaderboard(BoardType board, BoardPeriod period, bool friendsOnly)
        {
            Online();
            var now = ServerNow;
            var key = period == BoardPeriod.Current ? CurrentKey(board, now) : PreviousKey(board, now);
            // BUG-07: a previous season that never existed for this player (before season 1) is an empty, closed archive, not bots.
            bool preHistory = board == BoardType.Season && period == BoardPeriod.Previous && SeasonIndex(now) - 1 < 0;
            var b = preHistory ? new Board { key = key, type = (int)board } : GetBoard(board, key);
            var me = _s.account.playerId;
            bool guest = !_s.account.linked;
            var all = Ranked(b, friendsOnly, me);
            var page = new LeaderboardPage { board = board, periodKey = key, closed = preHistory || IsClosed(b), fetchedUnixUtc = Unix(now) };
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
            // BUG-06/07: season rollover. Unclaimed reached tiers (free, and premium if owned) go to the inbox for 30 days.
            var curSeason = SeasonKey(now);
            if (string.IsNullOrEmpty(_s.seasonId)) _s.seasonId = curSeason;
            else if (_s.seasonId != curSeason)
            {
                var old = _s.seasonId;
                int tier = TierForXp(_db.Season, _s.seasonXp);
                long exp = Unix(now.AddDays(_db.Season.unclaimedInboxDays));
                for (int i = 1; i <= tier; i++)
                {
                    var tr = _db.Season.tiers[i - 1];
                    if (!_s.claimedFree.Contains(i) && tr.free != null)
                        _s.inbox.Add(new InboxItem { id = $"{old}_tier_{i}_free", source = $"Unclaimed season tier {i}", reward = tr.free, expiresUnixUtc = exp });
                    if (_s.premium && !_s.claimedPremium.Contains(i) && tr.premium != null)
                        _s.inbox.Add(new InboxItem { id = $"{old}_tier_{i}_premium", source = $"Unclaimed premium tier {i}", reward = tr.premium, expiresUnixUtc = exp });
                }
                _s.seasonEndProcessed = true; // BUG-15
                _s.seasonId = curSeason; _s.seasonXp = 0; _s.bonusChests = 0; _s.premium = false;
                _s.claimedFree.Clear(); _s.claimedPremium.Clear(); _s.eventBest.Clear();
                Save();
            }
        }
    }
}
#endif
