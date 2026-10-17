namespace Crashverse.Server.Tests;

/// <summary>Backend Tester HIGH (PR #1 @b8abe02): PurgeAccount must purge by ownership only, never by substring.
/// Each scenario runs on the in-memory store and on the Firestore emulator.</summary>
public class PurgeOwnershipTests
{
    const string Project = "demo-crashverse";

    internal static async Task FriendsDocOfOtherPlayerKept(IDocStore shared)
    {
        await using var h = await Harness.Start(shared: shared);
        var a = await h.Guest(); var b = await h.Guest(); var c = await h.Guest();
        await h.X.Db.Put("friends", a.pid, new FriendDoc { playerId = a.pid, added = new() { b.pid, c.pid } });
        await h.X.Db.Put("friends", b.pid, new FriendDoc { playerId = b.pid, added = new() { a.pid } });
        var counts = await new DataLifecycleService(h.X).PurgeAccount(b.pid);
        var fa = await h.X.Db.Get<FriendDoc>("friends", a.pid);
        Assert.NotNull(fa);                                   // A's doc survives
        Assert.Equal(new[] { c.pid }, fa.added);             // B dropped, C kept
        Assert.Null(await h.X.Db.Get<FriendDoc>("friends", b.pid)); // B's own doc purged
        Assert.NotNull(await h.X.Db.Get<AccountDoc>("accounts", a.pid));
        Assert.NotNull(await h.X.Db.Get<AccountDoc>("accounts", c.pid));
        Assert.Null(await h.X.Db.Get<AccountDoc>("accounts", b.pid));
        Assert.Equal(1, counts.GetValueOrDefault("friends_unlinked"));
        Assert.Empty(await new DataLifecycleService(h.X).PurgeAccount(b.pid)); // idempotent
    }

    internal static async Task AuditRedactionByPidOnly(IDocStore shared)
    {
        await using var h = await Harness.Start(shared: shared);
        var a = await h.Guest(); var b = await h.Guest();
        var tag = Guid.NewGuid().ToString("N");
        await h.X.Audit("support", "note_" + tag, null, "ticket about " + b.pid);
        await h.X.Audit("support", "name_" + tag, null, "Guest Wrecker reported by " + a.pid); // shared display name, A's row
        await new DataLifecycleService(h.X).PurgeAccount(b.pid);
        var rows = (await h.X.Db.List<AuditEntry>("audit")).Where(e => e.action?.EndsWith(tag) == true).ToList();
        Assert.Equal("[redacted: account deleted]", rows.Single(e => e.action == "note_" + tag).detail);
        Assert.Equal("Guest Wrecker reported by " + a.pid, rows.Single(e => e.action == "name_" + tag).detail);
    }

    internal static async Task KeepRunsTop100IgnoresShadowAndHidden(IDocStore shared)
    {
        await using var h = await Harness.Start(shared: shared);
        const string B = "campaign:c1_alltime";
        var b = await h.Guest(); var g = await h.Guest();
        var acc = await h.X.Db.Get<AccountDoc>("accounts", b.pid); acc.isGuest = false; await h.X.Db.Put("accounts", b.pid, acc);
        int top = 2_000_000_000;
        for (int i = 0; i < 100; i++) // 100 shadow entries above B: they must not take top-100 slots
        {
            var sp = "shadow_" + Guid.NewGuid().ToString("N");
            await h.X.Db.Put("boards", B + "|" + sp, new BoardEntryDoc { boardKey = B, playerId = sp, score = top - i, shadow = true, hasValidated = true });
        }
        await h.X.Db.Put("boards", B + "|" + g.pid, new BoardEntryDoc { boardKey = B, playerId = g.pid, score = top - 200, guestAtSubmit = true, hasValidated = true }); // hidden guest
        await h.X.Db.Put("boards", B + "|" + b.pid, new BoardEntryDoc { boardKey = B, playerId = b.pid, score = top - 300, hasValidated = true });
        var runId = "run_" + Guid.NewGuid().ToString("N");
        await h.X.Db.Put("runs", runId, new RunDoc { runId = runId, playerId = b.pid, boardKeys = new() { B }, boardKey = B });
        await new DataLifecycleService(h.X).PurgeAccount(b.pid);
        var run = await h.X.Db.Get<RunDoc>("runs", runId);
        Assert.NotNull(run);                         // kept for anti-cheat: B is #1 among public entries
        Assert.StartsWith("tomb_", run.playerId);
        Assert.Null(await h.X.Db.Get<BoardEntryDoc>("boards", B + "|" + b.pid));
        Assert.NotNull(await h.X.Db.Get<BoardEntryDoc>("boards", B + "|" + g.pid)); // other player's entry untouched
    }

    [Fact] public Task Mem_DeletingB_KeepsAsFriendsDoc_DropsB() => FriendsDocOfOtherPlayerKept(null);
    [Fact] public Task Mem_AuditRedaction_ByPidOnly_NotDisplayName() => AuditRedactionByPidOnly(null);
    [Fact] public Task Mem_KeepRunsTop100_ExcludesShadowAndHidden() => KeepRunsTop100IgnoresShadowAndHidden(null);

    [Trait("Category", "Emulator")][EmulatorFact] public Task Emu_DeletingB_KeepsAsFriendsDoc_DropsB() => FriendsDocOfOtherPlayerKept(new FirestoreDocStore(Project));
    [Trait("Category", "Emulator")][EmulatorFact] public Task Emu_AuditRedaction_ByPidOnly_NotDisplayName() => AuditRedactionByPidOnly(new FirestoreDocStore(Project));
    [Trait("Category", "Emulator")][EmulatorFact] public Task Emu_KeepRunsTop100_ExcludesShadowAndHidden() => KeepRunsTop100IgnoresShadowAndHidden(new FirestoreDocStore(Project));

    [Theory]
    [InlineData("friends", "B", "{}", true)]
    [InlineData("campaignBest", "B:c1_l01", "{}", true)]
    [InlineData("campaignBest", "BB:c1_l01", "{}", false)]
    [InlineData("boards", "campaign:c1_alltime|B", "{}", true)]
    [InlineData("friends", "A", "{\"playerId\":\"A\",\"added\":[\"B\"]}", false)]
    [InlineData("purchases", "tok", "{\"playerId\":\"B\"}", true)]
    [InlineData("purchases", "tok", "{\"playerId\":\"xBx\"}", false)]
    [InlineData("sessions", "fam", "{\"ownerId\":\"B\"}", true)]
    public void Owns_IsExact(string col, string id, string json, bool owned) => Assert.Equal(owned, DataLifecycleService.Owns(col, id, json, "B"));
}
