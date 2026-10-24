using System;
using System.Linq;
using C = Crashverse.EditorTools.Import.ImportConventions;

static class Program
{
    static int fails, runs;
    static void Eq<T>(string label, T got, T want)
    {
        runs++;
        bool ok = Equals(got, want);
        if (!ok) fails++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {label}: got '{got}' want '{want}'");
    }

    static int Main()
    {
        // BLOCKER regression (Physics review of #28): fragment hulls keep the full fragment name.
        Eq("UCX_Wall_frag_03", C.ColliderTarget("UCX_Wall_frag_03"), "Wall_frag_03");
        Eq("UCX_Kit_Brick_Wall_frag_17", C.ColliderTarget("UCX_Kit_Brick_Wall_frag_17"), "Kit_Brick_Wall_frag_17");
        Eq("UCX_Carriage_01", C.ColliderTarget("UCX_Carriage_01"), "Carriage");
        Eq("UCX_Barrel", C.ColliderTarget("UCX_Barrel"), "Barrel");
        Eq("UCX_Island_Sunnyville_Mesh_01", C.ColliderTarget("UCX_Island_Sunnyville_Mesh_01"), "Island_Sunnyville_Mesh");
        Eq("Wheel_FL_COL_02", C.ColliderTarget("Wheel_FL_COL_02"), "Wheel_FL");
        Eq("Wall_frag_03 not a hull", C.ColliderTarget("Wall_frag_03"), (string)null);
        Eq("IsFragment Wall_frag_03", C.IsFragment("Wall_frag_03"), true);
        Eq("IsFragment Wall_frag_03_LOD1", C.IsFragment("Wall_frag_03_LOD1"), true);
        Eq("IsJoint JNT_A__B", C.IsJoint("JNT_Wall_frag_01__Wall_frag_02"), true);
        Eq("Socket_Muzzle", C.SocketName("Socket_Muzzle"), "Muzzle");
        Eq("LOD level", C.LodLevel("Wrecker_v2_LOD2", out _), 2);
        Eq("Kit path ok", C.KitPathOk("Assets/_Project/Models/BuildingKit/Kit_Brick/Kit_Brick_Wall.fbx"), true);
        Eq("Kit path bad", C.KitPathOk("Assets/_Project/Models/Kit_Brick/Kit_Brick_Wall.fbx"), false);
        Eq("Budget hero", C.TriBudget(C.AssetClass.Hero, "Wrecker_v2"), 12000);
        Eq("Budget island", C.TriBudget(C.AssetClass.Environment, "Island_Sunnyville"), 10000);
        Eq("Budget kit max", C.TriBudget(C.AssetClass.Kit, "Kit_Wood_Wall"), 1500);
        Eq("Budget kit min", C.TriMin(C.AssetClass.KitFractured), 500);
        Eq("JNT not fragment", C.IsFragment("JNT_Wall_frag_01__Wall_frag_02"), false);
        Eq("ANCHOR not fragment", C.IsFragment("ANCHOR_Wall_frag_01"), false);
        Eq("UCX not fragment", C.IsFragment("UCX_Wall_frag_01"), false);
        Eq("Kit texture max", C.PolicyFor("T_Brick_ORM", "Assets/_Project/Models/BuildingKit/Kit_Brick/textures/T_Brick_ORM.png").androidMax, 512);
        Eq("Hero texture max", C.PolicyFor("T_Wrecker_A", "Assets/_Project/Models/Wrecker_v2/textures/T_Wrecker_A.png").androidMax, 1024);
        Eq("Fragment limit", C.FragmentTriLimit, 400);
        Eq("Fractured exempt", C.RequiresLodChain(C.AssetClass.KitFractured), false);
        Eq("Intact kit needs LODs", C.RequiresLodChain(C.AssetClass.Kit), true);
        // Hero buildings (PM via 3D Modeler): T_Bld_* / Bld_* folders / hero kit folders cap at 1024 on Android; kit stays 512, _Inner 256.
        Eq("T_Bld_X_A Android", C.PolicyFor("T_Bld_X_A", "Assets/_Project/Models/Buildings/Bld_X/textures/T_Bld_X_A.png").androidMax, 1024);
        Eq("T_Bld_X_N in BuildingKit", C.PolicyFor("T_Bld_X_N", "Assets/_Project/Models/BuildingKit/Kit_X/textures/T_Bld_X_N.png").androidMax, 1024);
        Eq("T_Bld_X_N ASTC unchanged", C.PolicyFor("T_Bld_X_N", "Assets/_Project/Models/BuildingKit/Kit_X/textures/T_Bld_X_N.png").astc, "5x5");
        Eq("Hero kit folder T_House_ORM", C.PolicyFor("T_House_ORM", "Assets/_Project/Models/BuildingKit/Kit_House/textures/T_House_ORM.png").androidMax, 1024);
        Eq("Hero kit inner stays 256", C.PolicyFor("T_House_Inner_A", "Assets/_Project/Models/BuildingKit/Kit_House/textures/T_House_Inner_A.png").androidMax, 256);
        Eq("T_Bld inner stays 256", C.PolicyFor("T_Bld_X_Inner_A", "Assets/_Project/Models/Buildings/Bld_X/textures/T_Bld_X_Inner_A.png").androidMax, 256);
        Eq("Kit still 512", C.PolicyFor("T_Brick_A", "Assets/_Project/Models/BuildingKit/Kit_Brick/textures/T_Brick_A.png").androidMax, 512);
        Eq("Kit file named like hero stays 512 (folder rule)", C.PolicyFor("T_House_A", "Assets/_Project/Models/BuildingKit/Kit_Brick/textures/T_House_A.png").androidMax, 512);
        Eq("Hero _E still <=512", C.PolicyFor("T_Bld_X_E", "Assets/_Project/Models/Buildings/Bld_X/textures/T_Bld_X_E.png").androidMax, 512);
        Eq("KitFolder", C.KitFolder("Assets/_Project/Models/BuildingKit/Kit_House/Kit_House_Roof.fbx"), "House");
        Eq("Hero module", C.TryHeroModule("Assets/_Project/Models/BuildingKit/Kit_House/Kit_House_GroundDoor_Fractured.fbx", out var hk, out var hm, out var hf) && hk == "House" && hm == "GroundDoor" && hf, true);
        Eq("Hero module LOD file", C.TryHeroModule("Assets/_Project/Models/BuildingKit/Kit_Keep/Kit_Keep_Wall_LOD1.fbx", out _, out var hm2, out var hf2) && hm2 == "Wall" && !hf2, true);
        Eq("Not a hero kit", C.TryHeroModule("Assets/_Project/Models/BuildingKit/Kit_Brick/Kit_Brick_Wall.fbx", out _, out _, out _), false);
        var st = C.ParseHeroStats("{\"building\":\"House\",\"texel_px_per_m\":77.1,\"atlas\":2048,\"modules\":{\"GroundDoor\":{\"LOD0\":640,\"aabb\":[[-2.0,-1.05,0.0],[2.0,0.2,2.8]],\"frac\":\"Stone\",\"note\":\"arched \\\"door\\\"\",\"mass\":\"686.8-1774.3\"},\"Floor\":{\"frac\":\"Wood\"},\"Odd\":{\"LOD0\":1}}}");
        Eq("JSON building", st.building, "House");
        Eq("JSON GroundDoor", st.ClassFor("GroundDoor"), "Stone");
        Eq("JSON Floor", st.ClassFor("Floor"), "Wood");
        Eq("JSON entry w/o frac", st.HasModule("Odd") && st.ClassFor("Odd") == null, true);
        Eq("JSON missing entry", st.ClassFor("Chimney"), (string)null);
        bool threw = false; try { C.ParseHeroStats("{\"building\":\"X\"}"); } catch (FormatException) { threw = true; }
        Eq("JSON without modules throws", threw, true);
        Eq("Compare modules", string.Join("|", C.CompareHeroModules("House", new[] { "A", "B" }, new[] { "B", "C" })),
           "module A is in models/HeroBuildings/Kit_House_stats.json but there is no Kit_House_A.fbx / Kit_House_A_Fractured.fbx|Kit_House_C has no entry in models/HeroBuildings/Kit_House_stats.json");
        foreach (var f in System.IO.Directory.Exists("../../../models/HeroBuildings") ? System.IO.Directory.GetFiles("../../../models/HeroBuildings", "Kit_*_stats.json") : new string[0])
        { var rs = C.ParseHeroStats(System.IO.File.ReadAllText(f)); Eq("real " + System.IO.Path.GetFileName(f) + " parses with a class per module", rs.modules.Count > 0 && rs.modules.Values.All(v => v != null), true); }
        Console.WriteLine($"{runs - fails}/{runs} passed");
        return fails == 0 ? 0 : 1;
    }
}
