using Crashverse.Backend;
using System.Text.Json;
using Crashverse.Data;
using Crashverse.Server.Infra;

namespace Crashverse.Server.Domain;

/// <summary>Server-side content (loaded from the same JSON the client ships). The client never supplies config (H3).</summary>
public sealed class Content
{
    public ChapterData Chapter1; public TitanTemplates Titan; public SeasonData Season; public DailyRewardsData DailyRewards;
    public StoreCatalog Store; public List<StoreItem> AcceptedStore; public Dictionary<string, string> RequiresFlag = new(); public Dictionary<string, int> LayoutPieces = new();
    static readonly JsonSerializerOptions O = new() { IncludeFields = true, PropertyNameCaseInsensitive = false };

    public static Content Load(string root)
    {
        T R<T>(string f) => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(root, f)), O);
        var c = new Content
        {
            Chapter1 = R<ChapterData>("chapter1.json"), Titan = R<TitanTemplates>("titan_templates.json"), Season = R<SeasonData>("season_s1.json"),
            DailyRewards = R<DailyRewardsData>("daily_rewards.json"), Store = R<StoreCatalog>("store_catalog.json")
        };
        // requiresFlag (client PR #10 catalog) is read from the raw JSON so it applies whether or not StoreItem has the field yet.
        if (System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(root, "store_catalog.json")))?["items"] is System.Text.Json.Nodes.JsonArray items)
            foreach (var it in items)
                if (it?["sku"]?.GetValue<string>() is string s && it?["requiresFlag"] is System.Text.Json.Nodes.JsonValue rf && rf.TryGetValue<string>(out var f) && f.Length > 0) c.RequiresFlag[s] = f;
        Crashverse.Meta.StoreValidator.Validate(c.Store, out c.AcceptedStore); // server re-runs the cosmetics-only rule (review §5.7)
        var ld = Path.Combine(root, "Layouts");
        if (Directory.Exists(ld))
            foreach (var f in Directory.GetFiles(ld, "*.json"))
            {
                var l = JsonSerializer.Deserialize<LayoutData>(File.ReadAllText(f), O);
                if (l?.id != null) c.LayoutPieces[l.id] = l.blocks?.Length ?? 0;
            }
        return c;
    }

    public LevelEntry Level(string id) => Array.Find(Chapter1.levels, l => l.id == id);
    public LevelEntry EventLevel(string id) => Season.weeks.SelectMany(w => w.levels ?? Array.Empty<LevelEntry>()).FirstOrDefault(l => l.id == id);
    public int Pieces(LevelEntry l) => l?.layout != null && LayoutPieces.TryGetValue(l.layout, out var n) ? n : 0;
    public static string LevelVersion(LevelEntry l) => Hash.Sha256Hex(JsonSerializer.Serialize(l, O))[..12];
    public StoreItem Sku(string sku) => AcceptedStore.FirstOrDefault(i => i.sku == sku);
}

/// <summary>Shared dependencies for all services.</summary>
public sealed class Ctx
{
    public IDocStore Db; public IClock Clock; public ServerOptions Opt; public Content Content; public TokenService Tokens; public ReceiptService Receipts;
    public INonceCache Nonces; public IRateLimiter Limits; public IIntegrityVerifier Integrity; public IGoogleTokenVerifier Google;
    public IFacebookTokenVerifier Facebook; public IXAuthClient X; public IEmailSender Email; public IParentalConsentMailer ConsentMailer; public ConsentAgeTable ConsentAges; public EconomyConfig Economy; public ICaptchaVerifier Captcha; public ISecretProvider Secrets; public IPlayDeveloperApi Play;
    public IPushAuthVerifier PushAuth; public IReplayWorker Replay; public ISampler Sampler;

    public DateTime Now => Clock.UtcNow;
    /// <summary>Store SKU as sold right now: catalog-accepted AND its feature flag on. Gem packs (catalog requiresFlag "enableGemPacks",
    /// or ServerOptions.GemPackSkus) resolve only with ENABLE_GEM_PACKS; an unknown requiresFlag fails closed.</summary>
    public StoreItem Sku(string sku)
    {
        var i = Content?.Sku(sku); if (i == null) return null;
        string flag = Content.RequiresFlag.TryGetValue(sku, out var f) ? f : Array.IndexOf(ServerOptions.GemPackSkus, sku) >= 0 ? "enableGemPacks" : null;
        if (flag == null) return i;
        return flag == "enableGemPacks" && Opt.EnableGemPacks ? i : null;
    }

    public long NowMs => new DateTimeOffset(DateTime.SpecifyKind(Clock.UtcNow, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
    public static long Unix(DateTime d) => new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)).ToUnixTimeSeconds();
    public static string Iso(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
    public static long UnixMs(DateTime d) => new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    static readonly HashSet<string> SystemActors = new() { "system", "play", "replay", "admin", "parent", "web", "lifecycle" };
    /// <summary>S3/D6: audit rows never hold a raw player id. The pseudonym is HMAC(deletion pepper, playerId), so investigators can still
    /// find a live player's rows, and once the account is purged nothing links them back.</summary>
    public string Pseudo(string pid) => pid == null ? null : "p_" + Hash.HmacHex(Opt.DeletionPepper, pid)[..32];
    public Task Audit(string actor, string action, string playerId, string detail) =>
        Db.TryCreate("audit", $"{NowMs:D15}:{Hash.NewId()}", new AuditEntry { id = Hash.NewId(), ts = NowMs, actor = actor == null || SystemActors.Contains(actor) ? actor : Pseudo(actor),
            action = action, playerId = Pseudo(playerId), detail = detail });
}

/// <summary>Server time (SG-15/18/20/22). Only the server clock is used.</summary>
public sealed class Calendar
{
    readonly Ctx _x; public Calendar(Ctx x) { _x = x; }
    SeasonData S => _x.Content.Season;
    DateTime FirstSeasonStart => DateTime.Parse(S.startUtc, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal);
    int SeasonDays => S.weekDays * S.weeks.Length;
    public int SeasonIndex(DateTime now) => (int)Math.Floor((now - FirstSeasonStart).TotalDays / SeasonDays);
    public DateTime SeasonStartFor(DateTime now) => FirstSeasonStart.AddDays((double)SeasonIndex(now) * SeasonDays);
    public string SeasonIdFor(int n) => n == 0 ? S.id : n < 0 ? S.id + "_pre" + (-n) : S.id + "_r" + n;
    public string SeasonKey(DateTime now) => SeasonIdFor(SeasonIndex(now));
    public int WeekIndex(DateTime now) => (int)Math.Floor((now - SeasonStartFor(now)).TotalDays / S.weekDays);
    public string WeekKey(DateTime now) => SeasonKey(now) + "_w" + WeekIndex(now);
    public DateTime SeasonEndFor(DateTime now) => SeasonStartFor(now).AddDays(SeasonDays);
    public static DateTime DayStart(string dayKey) => DateTime.ParseExact(dayKey, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
    /// <summary>SG-10/SG-22: accepted up to and including 00:15:00.000 UTC of the next day.</summary>
    public DateTime RankedDeadline(string dayKey) => DayStart(dayKey).AddDays(1).AddMinutes(_x.Content.Titan.rankedGraceMinutes);

    public string CurrentKey(Crashverse.Backend.BoardType b, DateTime now) => b switch
    {
        Crashverse.Backend.BoardType.DailyTitan => "titan:" + UtcDay.Key(now),
        Crashverse.Backend.BoardType.WeeklyEvent => "week:" + WeekKey(now),
        Crashverse.Backend.BoardType.Campaign => "campaign:c1_alltime",
        _ => "season:" + SeasonKey(now)
    };
    public string PreviousKey(Crashverse.Backend.BoardType b, DateTime now) => b switch
    {
        Crashverse.Backend.BoardType.DailyTitan => "titan:" + UtcDay.Key(now.AddDays(-1)),
        Crashverse.Backend.BoardType.WeeklyEvent => "week:" + WeekKey(now.AddDays(-S.weekDays)),
        Crashverse.Backend.BoardType.Campaign => "campaign:c1_alltime",
        _ => "season:" + SeasonIdFor(SeasonIndex(now) - 1)
    };
    public bool IsClosed(string boardKey, DateTime now)
    {
        var t = boardKey.Split(':')[0];
        return t switch { "campaign" => false, _ => boardKey != CurrentKey(t switch { "titan" => Crashverse.Backend.BoardType.DailyTitan, "week" => Crashverse.Backend.BoardType.WeeklyEvent, _ => Crashverse.Backend.BoardType.Season }, now) };
    }
}
