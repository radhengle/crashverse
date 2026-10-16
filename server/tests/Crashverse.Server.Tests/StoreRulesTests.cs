namespace Crashverse.Server.Tests;

/// <summary>Store rules found while testing #1 with client PRs #10/#19 on top.</summary>
public class StoreRulesTests
{
    static async Task Gems(Harness h, Session s, int gems) { var w = await new WalletService(h.X).Get(s.pid); w.gems = gems; await h.X.Db.Put("wallets", s.pid, w); }

    [Fact] // HIGH: the season pass is never sold through /store/buy, whatever its price type (#10 prices it at 450 gems)
    public async Task Buy_RejectsSeasonPass_EvenWhenPricedInGems()
    {
        await using var h = await Harness.Start();
        var pass = h.X.Content.AcceptedStore.Single(i => i.type == "season_pass");
        pass.priceType = "gems"; pass.price = 450; // as in client PR #10
        var s = await h.Guest(); await Gems(h, s, 1000);
        var r = await h.Post("/v1/store/buy", new { sku = pass.sku }, s);
        Assert.Equal("USE_SEASON_UNLOCK", r.Code);
        var w = await h.Wallet(s);
        Assert.Equal(1000, w.gems); Assert.DoesNotContain(pass.sku, w.cosmetics);
        Assert.Null(await h.X.Db.Get<EntitlementDoc>("entitlements", s.pid + ":season_premium:" + pass.sku)); // nothing granted
    }

    [Fact] // MEDIUM: gem packs don't resolve while ENABLE_GEM_PACKS is off
    public async Task GemPacks_DoNotResolve_WhenFlagOff()
    {
        await using var off = await Harness.Start(o => o.EnableGemPacks = false);
        Assert.Null(off.X.Sku("gems_small"));
        Assert.NotNull(off.X.Content.Sku("gems_small")); // still in the catalog
        Assert.NotNull(off.X.Sku("deco_lighthouse"));
        await using var on = await Harness.Start(o => o.EnableGemPacks = true);
        Assert.NotNull(on.X.Sku("gems_small"));
    }

    [Fact] // MEDIUM: the catalog's requiresFlag is honoured on the server; unknown flags fail closed
    public async Task CatalogRequiresFlag_IsHonoured_UnknownFlagFailsClosed()
    {
        await using var h = await Harness.Start(o => o.EnableGemPacks = false);
        h.X.Content.RequiresFlag["deco_lighthouse"] = "enableGemPacks";
        Assert.Null(h.X.Sku("deco_lighthouse"));
        h.X.Opt.EnableGemPacks = true; Assert.NotNull(h.X.Sku("deco_lighthouse"));
        h.X.Content.RequiresFlag["deco_lighthouse"] = "someFutureFlag";
        Assert.Null(h.X.Sku("deco_lighthouse"));
    }
}
