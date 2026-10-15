using System;
using Crashverse.Level;

static class StarFormulaTest
{
    public static int Main()
    {
        var cfg = LevelConfig.CreateM1ClockworkTower();
        Assert(cfg.levelId == "m1_clockwork_tower", "level id");
        Assert(cfg.moveBudget == 5, "moves");
        Assert(Math.Abs(cfg.winThreshold - 0.85f) < 0.001f, "threshold");
        Assert(cfg.ComputeStars(false, 5) == 0, "fail=0");
        Assert(cfg.ComputeStars(true, 0) == 1, "0->1");
        Assert(cfg.ComputeStars(true, 1) == 1, "1->1");
        Assert(cfg.ComputeStars(true, 2) == 2, "2->2");
        Assert(cfg.ComputeStars(true, 3) == 2, "3->2");
        Assert(cfg.ComputeStars(true, 4) == 3, "4->3");
        Assert(cfg.ComputeStars(true, 5) == 3, "5->3");
        Console.WriteLine("StarFormulaTest: PASS");
        return 0;
    }
    static void Assert(bool c, string m) { if (!c) throw new Exception("FAIL " + m); }
}
