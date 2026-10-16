using System.Collections.Generic;
using NUnit.Framework;

namespace Crashverse.Sim.Tests
{
    public class SimRngTests
    {
        [Test] public void SameSeed_SameSequence()
        {
            var a = new SimRng(123456789012345L); var b = new SimRng(123456789012345L);
            for (int i = 0; i < 1000; i++) Assert.AreEqual(a.NextULong(), b.NextULong());
        }

        [Test] public void DifferentSeeds_Diverge()
        {
            var a = new SimRng(1); var b = new SimRng(2);
            Assert.AreNotEqual(a.NextULong(), b.NextULong());
        }

        [Test] public void ZeroSeed_IsUsable()
        {
            var r = new SimRng(0); ulong x = r.NextULong(), y = r.NextULong();
            Assert.AreNotEqual(0UL, x); Assert.AreNotEqual(x, y);
        }

        [Test] public void Reset_Replays()
        {
            var r = new SimRng(42); var first = new List<ulong>();
            for (int i = 0; i < 16; i++) first.Add(r.NextULong());
            r.Reset(42);
            for (int i = 0; i < 16; i++) Assert.AreEqual(first[i], r.NextULong());
        }

        [Test] public void Ranges_InBounds()
        {
            var r = new SimRng(7);
            for (int i = 0; i < 10000; i++)
            {
                float f = r.NextFloat01(); Assert.That(f >= 0f && f < 1f);
                int n = r.Range(-3, 4); Assert.That(n >= -3 && n < 4);
            }
        }

        [Test] public void Fork_IsDeterministic()
        {
            var a = new SimRng(9).Fork(1); var b = new SimRng(9).Fork(1);
            Assert.AreEqual(a.NextULong(), b.NextULong());
        }
    }

    public class TickInputQueueTests
    {
        [Test] public void Drains_InTickOrder_StableWithinTick()
        {
            var q = new TickInputQueue<string>();
            q.Enqueue(5, "c"); q.Enqueue(2, "a"); q.Enqueue(5, "d"); q.Enqueue(3, "b");
            var got = new List<string>();
            q.Drain(4, (t, s) => got.Add(s));
            CollectionAssert.AreEqual(new[] { "a", "b" }, got);
            q.Drain(5, (t, s) => got.Add(s));
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, got);
        }

        [Test] public void Rejects_InputsForConsumedTicks()
        {
            var q = new TickInputQueue<int>();
            q.Drain(10, (t, v) => { });
            Assert.IsFalse(q.Enqueue(10, 1));
            Assert.IsFalse(q.Enqueue(3, 1));
            Assert.IsTrue(q.Enqueue(11, 1));
        }

        [Test] public void Clear_ResetsConsumedTick()
        {
            var q = new TickInputQueue<int>(); q.Drain(10, (t, v) => { }); q.Clear();
            Assert.IsTrue(q.Enqueue(0, 1));
        }

        [Test] public void SecondsToTicks_RoundsUp()
        {
            Assert.AreEqual(125, SimTime.SecondsToTicks(2.5f));
            Assert.AreEqual(50, SimTime.SecondsToTicks(1.0f));
            Assert.AreEqual(1, SimTime.SecondsToTicks(0.001f));
            Assert.AreEqual(0, SimTime.SecondsToTicks(0f));
        }
    }
}

namespace Crashverse.Sim.Tests
{
    public class DebrisRulesTests
    {
        [Test] public void RetryReseed_GivesSameSequence()
        {
            const long runSeed = 987654321012L;
            var fresh = new SimRng(DebrisRules.RngSeed(runSeed));
            var first = new ulong[64];
            for (int i = 0; i < first.Length; i++) first[i] = fresh.NextULong();
            // Retry: same run seed, reseeded regardless of how many draws or ticks happened before.
            var retry = new SimRng(DebrisRules.RngSeed(runSeed));
            for (int i = 0; i < first.Length; i++) Assert.AreEqual(first[i], retry.NextULong());
        }

        [Test] public void DebrisStream_DiffersFromMainStream()
        {
            Assert.AreNotEqual(new SimRng(5).NextULong(), new SimRng(DebrisRules.RngSeed(5)).NextULong());
        }

        [Test] public void RankedCap_SameAcrossTiers()
        {
            foreach (int tierCap in new[] { 25, 60, 90 })
                Assert.AreEqual(DebrisRules.RankedSimCap, DebrisRules.SimCap(true, tierCap));
            Assert.AreEqual(60, DebrisRules.RankedSimCap);
        }

        [Test] public void NonRanked_UsesTierCap()
        {
            Assert.AreEqual(25, DebrisRules.SimCap(false, 25));
            Assert.AreEqual(90, DebrisRules.SimCap(false, 90));
        }
    }
}
