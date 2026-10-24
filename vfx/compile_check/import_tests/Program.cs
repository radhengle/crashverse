using System;
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
        Console.WriteLine($"{runs - fails}/{runs} passed");
        return fails == 0 ? 0 : 1;
    }
}
