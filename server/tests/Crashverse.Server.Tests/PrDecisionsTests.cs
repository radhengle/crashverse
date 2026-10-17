using System.Net;
using Crashverse.Server.Domain;
using Crashverse.Server.Infra;

namespace Crashverse.Server.Tests;

/// <summary>PM decisions on PR #1: age bands + consent table, dev-only client run summary, fail-closed public ranked board (B3).</summary>
public class PrDecisionsTests
{
    // ---------------- 1) bands + consent table ----------------
    [Theory]
    [InlineData(2008, 9, "US", "adult", false)]    // 18
    [InlineData(2008, 10, "US", "16to17", false)]  // turns 18 this month → still 17
    [InlineData(2010, 9, "US", "16to17", false)]   // 16
    [InlineData(2010, 10, "US", "13to15", false)]  // turns 16 this month → 15
    [InlineData(2008, 10, "IN", "16to17", true)]   // 17 in India (table: 18)
    [InlineData(2008, 9, "IN", "adult", false)]
    [InlineData(2011, 1, "NL", "13to15", true)]
    [InlineData(2011, 1, "ZZ", "13to15", true)]    // unknown market → 18 while the table is not legally reviewed
    public async Task Bands_Under13_13to15_16to17_Adult(int y, int m, string c, string band, bool consent)
    {
        await using var h = await Harness.Start();
        var r = await h.Post("/v1/agegate", new { birthYear = y, birthMonth = m, country = c, deviceInstallId = "d" }, null);
        Assert.Equal(band, r["band"].GetString()); Assert.Equal(consent, r["requiresParentalConsent"].GetBoolean());
    }

    [Fact]
    public void ConsentTable_IsTheConfigFile_MarkedNeedsLegalReview()
    {
        var path = ConsentAgeTable.FindPath();
        Assert.EndsWith(Path.Combine("server", "config", "consent_ages.json"), path);
        var t = ConsentAgeTable.Load(path);
        Assert.Equal("NEEDS LEGAL REVIEW", t.Status);
        Assert.False(t.LegalReviewed);
        Assert.Equal(16, t.For("de")); Assert.Equal(18, t.For("IN")); Assert.Equal(18, t.For("ZZ")); // unreviewed → most restrictive default
    }

    [Fact]
    public async Task ConsentTable_DrivesTheAnswer_AndBadFilesFailStartup()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var good = Path.Combine(dir, "c.json");
        File.WriteAllText(good, "{\"_status\":\"NEEDS LEGAL REVIEW\",\"default\":13,\"markets\":{\"US\":16}}");
        await using (var h = await Harness.Start(o => o.ConsentAgesPath = good))
            Assert.True((await h.Post("/v1/agegate", new { birthYear = 2011, birthMonth = 1, country = "US", deviceInstallId = "d" }, null))["requiresParentalConsent"].GetBoolean());
        var bad = Path.Combine(dir, "bad.json");
        File.WriteAllText(bad, "{\"default\":13,\"markets\":{\"US\":9}}");
        await Assert.ThrowsAsync<InvalidDataException>(() => Harness.Start(o => o.ConsentAgesPath = bad));
        await Assert.ThrowsAsync<FileNotFoundException>(() => Harness.Start(o => o.ConsentAgesPath = Path.Combine(dir, "missing.json")));
    }

    // ---------------- 2) guest deletion stays session-only, linked needs re-auth ----------------
    [Fact]
    public async Task GuestSessionDeletes_LinkedNeedsFreshReauth()
    {
        await using var h = await Harness.Start();
        Assert.Equal("scheduled", (await h.Post("/v1/account/delete", new { }, await h.Guest()))["status"].GetString());
        var l = await h.Linked();
        Assert.Equal("REAUTH_REQUIRED", (await h.Post("/v1/account/delete", new { }, l)).Code);
    }

    // ---------------- 3a) ALLOW_CLIENT_RUN_SUMMARY ----------------
    [Theory]
    [InlineData("Development", null, true)]
    [InlineData("development", "true", true)]
    [InlineData("Development", "false", false)]
    [InlineData("Production", "true", false)]
    [InlineData("Staging", "true", false)]
    [InlineData(null, "true", false)]               // no environment = Production
    public void ClientRunSummary_IsDevelopmentOnly(string env, string flag, bool expected)
    {
        var o = new ServerOptions();
        ServerOptions.ApplyEnvFlags(o, k => k switch { "ASPNETCORE_ENVIRONMENT" => env, "ALLOW_CLIENT_RUN_SUMMARY" => flag, _ => null });
        Assert.Equal(expected, o.AllowClientRunSummary);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("TRUE ", false)]
    [InlineData("true", true)]
    public void RankedPublicBoard_DefaultsOff(string flag, bool expected)
    {
        var o = new ServerOptions(); Assert.False(o.RankedPublicBoardEnabled);
        ServerOptions.ApplyEnvFlags(o, k => k == "RANKED_PUBLIC_BOARD_ENABLED" ? flag : null);
        Assert.Equal(expected, o.RankedPublicBoardEnabled);
    }

    [Fact]
    public async Task WithoutClientSummary_ClaimsAreIgnored_NothingChangesUntilServerReplay()
    {
        await using var h = await Harness.Start(o => o.AllowClientRunSummary = false);
        var s = await h.Linked(); // linked: guests are hidden on boards
        var coins = (await h.Wallet(s)).coins;
        var rep = Harness.Win("c1_l01", await h.Attempt(s, "c1_l01"), 1, 4, ratio: 1.0f, combo: 99); // inflated claims
        var r = await h.Post("/v1/score", rep, s);
        Assert.True(r["accepted"].GetBoolean()); Assert.Equal("pending", r["status"].GetString());
        Assert.Equal(0, r["serverScoreBp"].GetInt32()); Assert.Equal("awaiting_replay", r["validation"].GetString());
        Assert.Equal(coins, (await h.Wallet(s)).coins);                                   // no provisional reward
        Assert.Empty(await h.X.Db.List<LevelBestDoc>("campaignBest"));                    // no best
        Assert.Empty(await h.X.Db.List<BoardEntryDoc>("boards"));                         // no board entry
        var run = (await h.X.Db.List<RunDoc>("runs")).Single();
        Assert.False(run.campaign.won); Assert.Equal(0f, run.campaign.destroyedRatio);    // claims not stored
        Assert.Equal("queued", run.replayState);

        h.Replay.Fn = v => new ReplayResult { won = true, destroyedRatio = 0.85f, shotsLeft = 1, bestCombo = 2 };
        await h.Job("/internal/jobs/replay");
        var best = (await h.X.Db.List<LevelBestDoc>("campaignBest")).Single();
        Assert.Equal(Crashverse.Meta.CampaignScore.Compute(true, 0.85f, 1, 2, false), best.best); Assert.True(best.validated);
        var page = await h.Get("/v1/leaderboards/3", s);
        Assert.Equal(best.best, page["me"].GetProperty("score").GetInt32());
    }

    [Fact]
    public async Task WithoutClientSummary_ReplayLoss_RecordsNothing_AndBadInputsStillRejected()
    {
        await using var h = await Harness.Start(o => o.AllowClientRunSummary = false);
        var s = await h.Guest();
        var rep = Harness.Win("c1_l01", await h.Attempt(s, "c1_l01"), 1, 4);
        await h.Post("/v1/score", rep, s);
        h.Replay.Fn = v => new ReplayResult { won = false, destroyedRatio = 0.2f, shotsLeft = 0 };
        await h.Job("/internal/jobs/replay");
        Assert.Empty(await h.X.Db.List<LevelBestDoc>("campaignBest"));
        var bad = Harness.Win("c1_l01", await h.Attempt(s, "c1_l01"), 1, 4); bad.inputs.RemoveAt(3); // drop a settle
        Assert.Equal("rejected", (await h.Post("/v1/score", bad, s))["status"].GetString());
        var locked = Harness.Win("c1_l03", await h.Attempt(s, "c1_l03"), 1, 4);
        Assert.Contains("prerequisite", (await h.Post("/v1/score", locked, s))["message"].GetString());
    }

    // ---------------- 3b) public ranked board fails closed (B3) ----------------
    static async Task<(Session viewer, Session player)> TitanEntry(Harness h)
    {
        var viewer = await h.Linked(); var p = await h.Linked();
        h.Replay.Fn = r => new ReplayResult { damageScore = r.clientScore };
        await h.RankedRun(p, 1, 1000);
        return (viewer, p);
    }

    [Fact]
    public async Task RankedBoard_OffByDefault_ShowsOnlyMe()
    {
        await using var h = await Harness.Start(o => o.RankedPublicBoardEnabled = false);
        var (viewer, p) = await TitanEntry(h);
        await h.Job("/internal/jobs/replay");
        var page = await h.Get("/v1/leaderboards/0", viewer);
        Assert.False(page["publicBoard"].GetBoolean()); Assert.Equal("disabled", page["publicBoardReason"].GetString());
        Assert.Equal(0, page["top"].GetArrayLength());
        var mine = await h.Get("/v1/leaderboards/0", p);
        Assert.Equal(0, mine["top"].GetArrayLength()); Assert.Equal(p.pid, mine["me"].GetProperty("playerId").GetString());
        Assert.True((await h.Get("/v1/leaderboards/3", viewer))["publicBoard"].GetBoolean()); // other boards unaffected
    }

    [Fact]
    public async Task RankedBoard_NeedsAConfiguredReplayWorker()
    {
        await using var h = await Harness.Start();
        var (viewer, _) = await TitanEntry(h);
        await h.Job("/internal/jobs/replay");
        h.Replay.IsConfigured = false;
        var page = await h.Get("/v1/leaderboards/0", viewer);
        Assert.Equal("replay_worker_not_configured", page["publicBoardReason"].GetString()); Assert.Equal(0, page["top"].GetArrayLength());
        Assert.False(new StubReplayWorker() is IReplayWorker w && w.Configured); // the dev stub never counts as configured
    }

    [Fact]
    public async Task RankedBoard_NeedsEveryTopNEntryReplayed()
    {
        await using var h = await Harness.Start();
        var (viewer, p) = await TitanEntry(h);
        var q = await h.Linked(); await h.RankedRun(q, 1, 500);
        var page = await h.Get("/v1/leaderboards/0", viewer);       // nothing replayed yet
        Assert.Equal("top_n_not_fully_replayed", page["publicBoardReason"].GetString()); Assert.Equal(0, page["top"].GetArrayLength());
        h.Replay.Fn = r => r.clientScore == 1000 ? new ReplayResult { damageScore = 1000 } : null; // only one of two replayed
        await h.Job("/internal/jobs/replay");
        Assert.Equal("top_n_not_fully_replayed", (await h.Get("/v1/leaderboards/0", viewer))["publicBoardReason"].GetString());
        h.Replay.Fn = r => new ReplayResult { damageScore = r.clientScore };
        await h.Job("/internal/jobs/replay");
        page = await h.Get("/v1/leaderboards/0", viewer);
        Assert.True(page["publicBoard"].GetBoolean()); Assert.Equal(2, page["top"].GetArrayLength());
    }
}
