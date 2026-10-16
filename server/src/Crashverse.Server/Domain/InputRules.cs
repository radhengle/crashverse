using Crashverse.Backend;
namespace Crashverse.Server.Domain;

/// <summary>Agreed-interface input log checks (fixed point, 1/1000 units, ticks at PhysicsHz). Shared by campaign, events and Titan.</summary>
public static class InputRules
{
    public static readonly HashSet<string> Types = new() { "aim", "power", "powerup", "fire", "settle" };
    public const int MaxEventsPerShot = 64;      // per-shot cap
    public const int MaxYaw = 360_000, MaxPitch = 90_000, MaxPower = 1000, MaxPowerUpSlot = 3;

    /// <summary>Returns null when plausible. expectedShots = fire events required; minGapTicks = minimum ticks between a settle and the next fire.</summary>
    public static string Check(List<InputEvent> ev, int expectedShots, int minGapTicks)
    {
        if (ev == null || (expectedShots > 0 && ev.Count == 0)) return "inputs missing";
        if (ev.Count > MaxEventsPerShot * Math.Max(1, expectedShots)) return "too many input events";
        int fires = 0, sinceFire = 0, prevTick = -1, lastFire = int.MinValue / 2, lastSettle = int.MinValue / 2; bool awaitingSettle = false;
        foreach (var e in ev)
        {
            if (e == null || e.type == null || !Types.Contains(e.type)) return "unknown input type";
            if (e.tick < 0 || e.tick < prevTick) return "input ticks out of order";
            prevTick = e.tick;
            if (++sinceFire > MaxEventsPerShot) return "too many input events for one shot";
            switch (e.type)
            {
                case "aim": case "fire":
                    if (Math.Abs(e.x) > MaxYaw || Math.Abs(e.y) > MaxPitch) return "inputs out of range"; break;
                case "power": if (e.x < 0 || e.x > MaxPower || e.y != 0) return "inputs out of range"; break;
                case "powerup": if (e.x < 0 || e.x > MaxPowerUpSlot) return "inputs out of range"; break;
            }
            if (e.type == "fire")
            {
                if (awaitingSettle) return "missing settle";
                if (fires > 0 && e.tick - lastSettle < minGapTicks) return "shots too close together";
                if (fires > 0 && e.tick - lastFire < minGapTicks) return "shots too close together";
                fires++; sinceFire = 0; lastFire = e.tick; awaitingSettle = true;
            }
            else if (e.type == "settle")
            {
                if (!awaitingSettle || e.tick <= lastFire) return "settle without shot";
                awaitingSettle = false; lastSettle = e.tick;
            }
        }
        if (fires != expectedShots) return "input log does not match shots used";
        if (awaitingSettle) return "missing settle";
        return null;
    }

    /// <summary>Reference tick loop (Physics PR #6 ordering): for each tick t from 0 to the last input tick (+ extraTicks), apply
    /// that tick's events in list order, then step. An input at tick N therefore happens before the physics step for tick N.</summary>
    public static void Drive(List<InputEvent> ev, Action<InputEvent> apply, Action<int> step, int extraTicks = 0)
    {
        if (ev == null) return;
        int last = ev.Count == 0 ? -1 : ev.Max(e => e.tick), i = 0;
        var ordered = ev.Select((e, idx) => (e, idx)).OrderBy(p => p.e.tick).ThenBy(p => p.idx).Select(p => p.e).ToList(); // stable
        for (int t = 0; t <= last + extraTicks; t++)
        {
            while (i < ordered.Count && ordered[i].tick == t) apply(ordered[i++]);
            step(t);
        }
    }

    public static int Fires(List<InputEvent> ev) => ev?.Count(e => e?.type == "fire") ?? 0;
    public static string Hash(List<InputEvent> ev) => ev == null ? "-" : Infra.Hash.Sha256Hex(string.Join(";", ev.Select(e => $"{e?.tick}|{e?.type}|{e?.x}|{e?.y}")))[..16];
}
