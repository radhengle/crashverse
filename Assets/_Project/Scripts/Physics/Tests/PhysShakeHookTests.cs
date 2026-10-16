using NUnit.Framework;

namespace Crashverse.Sim.Tests
{
    public class PhysShakeHookTests
    {
        [TearDown] public void Clear() => PhysShakeHook.Sink = null;

        [Test] public void NoSink_IsNoOp() { PhysShakeHook.Sink = null; Assert.IsFalse(PhysShakeHook.Submit("PHYS_BREAK", new ImpactEvent())); }

        [Test] public void Sink_GetsNameMaterialBTier()
        {
            string n = null; int m = -1, t = -1;
            PhysShakeHook.Sink = (a, b, c) => { n = a; m = b; t = c; return true; };
            var e = new ImpactEvent { materialTypeA = MaterialType.Wood, materialTypeB = MaterialType.Steel, tier = ImpactTier.Heavy };
            Assert.IsTrue(PhysShakeHook.Submit("PHYS_IMPACT", e));
            Assert.AreEqual("PHYS_IMPACT", n); Assert.AreEqual((int)MaterialType.Steel, m); Assert.AreEqual((int)ImpactTier.Heavy, t);
        }
    }
}
