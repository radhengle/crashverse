using System;
using System.Collections.Generic;
using System.IO;
using Crashverse.Audio;
using Crashverse.Feel;

// P0 no-sound: SFX router / pool / missing-clip / listener tests (gameplay/sfx-wiring).
static class SfxTests
{
    const string Json = @"{
 ""phys_source_priority"": { ""Cannon"": 32, ""Debris"": 192 },
 ""phys_hardness"": [""steel"",""stone"",""brick"",""glass"",""wood"",""terrain""],
 ""routes"": [
  { ""event"": ""ANIM_WRECKER_FIRE"", ""key"": {}, ""owner"": ""anim"", ""cooldown_ticks"": 0, ""notes"": """",
    ""clips"": [ { ""sfx_id"": ""sfx_cannon_fire"", ""variations"": [""SFX/Weapon/sfx_cannon_fire_01"",""SFX/Weapon/sfx_cannon_fire_02""], ""group"": ""SFX/Weapon"", ""priority"": 20 } ] },
  { ""event"": ""ANIM_WRECKER_FIDGET"", ""key"": {}, ""owner"": ""anim"", ""cooldown_ticks"": 0, ""clips"": [ { ""sfx_id"": ""sfx_wrecker_fidget"", ""variations"": [], ""group"": """", ""priority"": 150 } ] },
  { ""event"": ""ANIM_WRECKER_AIM_END"", ""key"": {}, ""owner"": ""anim"", ""notes"": ""stop aim loops"", ""clips"": [] },
  { ""event"": ""BREAK_WOOD"", ""key"": {}, ""owner"": ""visual"", ""clips"": [ { ""sfx_id"": ""sfx_x"", ""variations"": [""SFX/x""] } ] },
  { ""event"": ""PHYS_IMPACT"", ""key"": { ""material"": ""wood"", ""kind"": ""impact"", ""size"": ""S"", ""tier"": ""any"" }, ""owner"": ""phys"", ""cooldown_ticks"": 4,
    ""clips"": [ { ""sfx_id"": ""sfx_wood_impact_s"", ""variations"": [""SFX/Destruction/Wood/sfx_wood_impact_s_01""], ""group"": ""SFX/Destruction"", ""priority"": 128 } ] },
  { ""event"": ""PHYS_IMPACT"", ""key"": { ""material"": ""steel"", ""kind"": ""impact"", ""size"": ""S"", ""tier"": ""any"" }, ""owner"": ""phys"", ""cooldown_ticks"": 4,
    ""clips"": [ { ""sfx_id"": ""sfx_steel_impact_s"", ""variations"": [""SFX/Destruction/Steel/sfx_steel_impact_s_01""], ""group"": ""SFX/Destruction"", ""priority"": 128 } ] },
  { ""event"": ""PHYS_COLLAPSE_END"", ""key"": { ""material"": ""wood"", ""kind"": ""collapse"", ""count"": ""1-5"" }, ""owner"": ""phys"", ""clips"": [ { ""sfx_id"": ""c_small"", ""variations"": [""a""], ""priority"": 32 } ] },
  { ""event"": ""PHYS_COLLAPSE_END"", ""key"": { ""material"": ""wood"", ""kind"": ""collapse"", ""count"": ""21+"" }, ""owner"": ""phys"", ""clips"": [ { ""sfx_id"": ""c_big"", ""variations"": [""b""], ""priority"": 32 } ] },
  { ""event"": ""UI_COIN_BURST"", ""key"": {}, ""owner"": ""code"", ""clips"": [ { ""sfx_id"": ""sfx_reward_coin_burst"", ""variations"": [""SFX/Reward/sfx_reward_coin_burst""], ""group"": ""SFX/Rewards"", ""priority"": 90 } ] },
  { ""bogus"": true },
  ""not an object""
 ]}";

    class FakeImpact { public string kind = "Impact"; public string materialA = "Wood"; public string materialB = "Steel"; public string sizeClass = "S"; public string tier = "Heavy"; public string source = "Cannon"; }
    class FakeListener { public bool OnMainCamera; }
    class FakeCollapse { public string dominantMaterial = "Wood"; public int cascadeCount = 30; }

    public static void Register()
    {
        T.Add("sfx_routing_tolerant_parse", () =>
        {
            var warns = new List<string>();
            var t = SfxRoutingTable.Parse(Json, warns.Add);
            T.Eq(9, t.Count, "valid routes kept, bad rows skipped");
            T.True(warns.Exists(w => w.Contains("no event")), "warned for row without event");
            T.Eq(0, SfxRoutingTable.Parse(null, warns.Add).Count, "missing file → empty table");
            T.Eq(0, SfxRoutingTable.Parse("{not json", warns.Add).Count, "invalid json → empty, no throw");
            T.Eq(32, t.SourcePriority["Cannon"], "phys_source_priority");
        });
        T.Add("sfx_router_maps_events_to_clips", () =>
        {
            var r = new SfxRouter(SfxRoutingTable.Parse(Json));
            var p = r.Resolve("ANIM_WRECKER_FIRE", null, 0, out _);
            T.Eq(1, p.Count, "fire → 1 clip"); T.Eq("sfx_cannon_fire", p[0].Clip.SfxId); T.Eq("SFX/Weapon", p[0].Clip.Group); T.Eq(20, p[0].Priority);
            T.Eq(2, p[0].Clip.Variations.Count, "variations");
            T.Eq(0, r.Resolve("BREAK_WOOD", null, 0, out var vr).Count, "visual route skipped"); T.True(vr == null, "visual → no route");
            T.Eq(0, r.Resolve("NOT_ROUTED", null, 0, out _).Count, "unrouted → nothing");
            r.Resolve("ANIM_WRECKER_AIM_END", null, 0, out var stop); T.True(stop != null && stop.IsStop, "stop route");
            var c = r.Resolve("UI_COIN_BURST", null, 0, out var cr); T.Eq("code", cr.Owner); T.Eq("sfx_reward_coin_burst", c[0].Clip.SfxId);
        });
        T.Add("sfx_router_phys_payload_hardness_source_count", () =>
        {
            var r = new SfxRouter(SfxRoutingTable.Parse(Json));
            var p = r.Resolve("PHYS_IMPACT", new FakeImpact(), 0, out _);
            T.Eq("sfx_steel_impact_s", p[0].Clip.SfxId, "harder material wins (steel > wood)");
            T.Eq(32, p[0].Priority, "Cannon source priority");
            T.Eq("c_big", r.Resolve("PHYS_COLLAPSE_END", new FakeCollapse(), 0, out _)[0].Clip.SfxId, "count 30 → 21+");
            T.Eq("c_small", r.Resolve("PHYS_COLLAPSE_END", new Dictionary<string, string> { ["material"] = "wood", ["count"] = "3" }, 0, out _)[0].Clip.SfxId, "count 3 → 1-5");
            T.True(SfxRoutingTable.InRange("6-20", "6") && !SfxRoutingTable.InRange("6-20", "21") && SfxRoutingTable.InRange("21+", "99"), "ranges");
        });
        T.Add("sfx_router_cooldown_ticks", () =>
        {
            var r = new SfxRouter(SfxRoutingTable.Parse(Json)) { TickSeconds = 0.02 }; // 4 ticks = 80 ms
            var f = new Dictionary<string, string> { ["material"] = "wood", ["size"] = "S" };
            T.Eq(1, r.Resolve("PHYS_IMPACT", f, 1.000, out _).Count, "first plays");
            T.Eq(0, r.Resolve("PHYS_IMPACT", f, 1.050, out _).Count, "within 4 ticks blocked");
            T.Eq(1, r.Resolve("PHYS_IMPACT", f, 1.081, out _).Count, "after cooldown plays");
            r.Resolve("ANIM_WRECKER_FIRE", null, 1.0, out _);
            T.Eq(1, r.Resolve("ANIM_WRECKER_FIRE", null, 1.0, out _).Count, "0 cooldown never blocks");
        });
        T.Add("sfx_missing_clip_warns_once_per_route", () =>
        {
            var warns = new List<string>();
            var r = new SfxRouter(SfxRoutingTable.Parse(Json), warns.Add);
            T.Eq(0, r.Resolve("ANIM_WRECKER_FIDGET", null, 0, out _).Count, "missing clip skipped");
            r.Resolve("ANIM_WRECKER_FIDGET", null, 1, out _);
            T.Eq(1, warns.Count, "warned once"); T.True(warns[0].Contains("sfx_wrecker_fidget"), "names the clip");
            MissingClipLog.ResetForTests(); var sink = new List<string>(); MissingClipLog.Sink = sink.Add;
            T.True(MissingClipLog.Report("Music", "sunnyville_map_loop", "Music/sunnyville_map_loop"), "music warn");
            T.True(!MissingClipLog.Report("Music", "sunnyville_map_loop", "Music/sunnyville_map_loop"), "deduped");
            T.Eq(1, sink.Count); MissingClipLog.ResetForTests();
            var mp = File.ReadAllText(Path.Combine(Root, "Assets/_Project/Scripts/Audio/MusicPlayer.cs"));
            T.True(mp.Contains("MissingClipLog.Report(\"Music\""), "MusicPlayer no longer fails silently");
        });
        T.Add("sfx_pool_size_per_tier_and_reuse", () =>
        {
            T.Eq(12, SfxVoicePool.SizeFor(QualityTier.Low)); T.Eq(24, SfxVoicePool.SizeFor(QualityTier.Mid)); T.Eq(32, SfxVoicePool.SizeFor(QualityTier.High));
            var pool = new SfxVoicePool(2);
            var a = pool.Acquire("a", 100, 0); var b = pool.Acquire("b", 50, 1);
            T.Eq(2, pool.BusyCount);
            var c = pool.Acquire("c", 60, 2); T.True(c == a, "steals least important (largest priority number)");
            T.True(pool.Acquire("d", 200, 3) == null, "less important than everything playing → dropped");
            pool.Release(b); var e = pool.Acquire("e", 200, 4); T.True(e == b, "released voice reused");
            var ui = new SfxVoicePool(1); ui.Acquire("ui", 200, 0, 1f, true); T.True(ui.Acquire("x", 0, 1) == null, "UI voices never stolen");
            pool.Resize(32); T.Eq(32, pool.Capacity); pool.Resize(12); T.Eq(12, pool.Capacity);
        });
        T.Add("boss_tide_armor_hit_trigger", () =>
        {
            T.True(BossSfxRules.ArmorHit(1, true, false), "direct phase-1 armour hit plays");
            T.True(!BossSfxRules.ArmorHit(1, false, false), "debris/indirect damage silent");
            T.True(!BossSfxRules.ArmorHit(2, true, false), "phase-2 armour auto-break silent");
            T.True(!BossSfxRules.ArmorHit(1, true, true), "after defeat silent");
            var tw = File.ReadAllText(Path.Combine(Root, "Assets/_Project/Scripts/Boss/TidewardenController.cs"));
            T.True(tw.Contains("x.Damaged += OnArmorDamaged") && tw.Contains("SfxPlayer.PlayCode(\"BOSS_TIDE_ARMOR_HIT\")"), "armour Damaged hooked to PlayCode");
        });
        T.Add("audio_listener_guard_idempotent_and_collapses", () =>
        {
            var scene = new List<FakeListener>(); bool hasCam = true;
            FakeListener Ensure() => ListenerRule.Apply(scene, l => l.OnMainCamera, hasCam,
                () => { var l = new FakeListener { OnMainCamera = true }; scene.Add(l); return l; },
                () => { var l = new FakeListener(); scene.Add(l); return l; },
                l => scene.Remove(l));
            var first = Ensure(); T.Eq(1, scene.Count, "first Ensure adds one");
            for (int i = 0; i < 5; i++) T.True(ReferenceEquals(first, Ensure()), "repeated Ensure returns the same listener");
            T.Eq(1, scene.Count, "repeated Ensure leaves 1 listener");
            scene.Add(new FakeListener()); scene.Add(new FakeListener());          // a later scene/camera brings its own
            T.True(ReferenceEquals(first, Ensure()), "keeps the main-camera listener"); T.Eq(1, scene.Count, "late listeners collapse to 1");
            scene.Clear(); var late = new FakeListener(); scene.Add(late);         // camera unloaded, new scene has one (not main)
            T.True(ReferenceEquals(late, Ensure()), "single existing listener untouched"); T.Eq(1, scene.Count);
            scene.Clear(); hasCam = false; Ensure(); T.Eq(1, scene.Count, "no camera → standalone listener");
            var src = File.ReadAllText(Path.Combine(Root, "Assets/_Project/Scripts/Audio/SfxPlayer.cs"));
            T.True(src.Contains("SceneManager.sceneLoaded += "), "re-Ensure on scene load");
            int upd = src.IndexOf("void Update()"); T.True(upd < 0 || src.IndexOf("FindObjectsOfType", upd) < 0, "no per-frame FindObjectsOfType");
            T.True(src.Contains("if (Instance != null) return Instance;"), "SfxPlayer.Ensure idempotent");
        });
        T.Add("audio_single_listener", () =>
        {
            T.Eq(ListenerRule.Action.AddToMainCamera, ListenerRule.Decide(0, true));
            T.Eq(ListenerRule.Action.AddToNewObject, ListenerRule.Decide(0, false));
            T.Eq(ListenerRule.Action.None, ListenerRule.Decide(1, true));
            T.Eq(ListenerRule.Action.RemoveExtras, ListenerRule.Decide(2, true));
            var gb = File.ReadAllText(Path.Combine(Root, "Assets/_Project/Scripts/Core/GameBootstrap.cs"));
            T.True(gb.Contains("AudioListenerGuard.Ensure()"), "bootstrap ensures a listener even when the scene camera exists");
            T.True(!gb.Contains("AddComponent<AudioListener>"), "no unconditional AddComponent<AudioListener> (no duplicates)");
        });
        T.Add("sfx_routing_json_pr32_if_present", () =>
        {
            var f = Path.Combine(Root, "audio/sfx_routing.json");
            if (!File.Exists(f)) { Console.WriteLine("SKIP sfx_routing_json_pr32_if_present: audio/sfx_routing.json not on this branch (PR #32)"); return; }
            var warns = new List<string>(); var t = SfxRoutingTable.Parse(File.ReadAllText(f), warns.Add);
            T.True(t.Count >= 170, "full table loads (" + t.Count + ")"); T.Eq(0, warns.Count, "no parse warnings");
            var r = new SfxRouter(t); T.True(r.Resolve("ANIM_WRECKER_FIRE", null, 0, out _).Count > 0, "fire routes");
        });
    }

    static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
}
