using Crashverse.Sim;

namespace Crashverse.Destruction
{
    /// <summary>
    /// Per-tick debris damage: collisions reported during a physics step are collected and applied at the start of the next
    /// SimRunner tick, sorted by (target blockId, source key). Without a running SimRunner (menus, editor tools) damage applies at once.
    /// </summary>
    public static class DebrisDamageQueue
    {
        public static DebrisDamageConfig Config = DebrisDamageConfig.Default;
        static readonly DebrisDamageAccumulator<DestructibleBlock> _acc = new DebrisDamageAccumulator<DestructibleBlock>();
        static SimRunner _hooked;

        public static void Enqueue(DestructibleBlock target, string sourceKey, int milliHp)
        {
            var sim = SimRunner.Instance;
            if (sim == null || !sim.Running) { Apply(target, milliHp); return; }
            if (_hooked != sim) { if (_hooked != null) _hooked.ContactsFlush -= Flush; sim.ContactsFlush += Flush; _hooked = sim; _acc.Clear(); }
            _acc.Add(target.blockId, sourceKey, milliHp, target);
        }

        static void Flush(int tick) => _acc.Flush(Apply);

        static void Apply(DestructibleBlock target, int milliHp)
        {
            if (target == null) return;
            using (ImpactSourceScope.Push(ImpactSourceKind.Debris)) target.ApplyDamage(milliHp / 1000f, false, splash: true);
        }

        public static void ResetForTests() { _acc.Clear(); if (_hooked != null) _hooked.ContactsFlush -= Flush; _hooked = null; }
    }
}
