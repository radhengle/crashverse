using System;
using System.Collections.Generic;
using System.Reflection;

namespace Crashverse.VFX
{
    /// <summary>
    /// Thin adapter from the physics ImpactEvent payload (PR #18 Crashverse.Sim.ImpactEvent, raised as AnimEventBus "PHYS_*", wired by
    /// PR #20) to VfxImpact. Until #18 merges, the type doesn't exist on main, so this reads the payload by field name (cached per type)
    /// and the VFX PR has no compile dependency on the physics branches. Once #18 is on main, define CRASHVERSE_SIM_IMPACT (or delete
    /// the reflection branch) to use the direct, allocation-free cast below. Field names follow #18: kind, tier, impulse, materialTypeA/B,
    /// dominantMaterial, position, pieceId, damageStep, source.
    /// </summary>
    public static class ImpactEventAdapter
    {
        public const string Impact = "PHYS_IMPACT", Crack = "PHYS_CRACK", Break = "PHYS_BREAK", JointSnap = "PHYS_JOINT_SNAP",
                            CollapseStart = "PHYS_COLLAPSE_START", CollapseEnd = "PHYS_COLLAPSE_END";
        public static bool IsPhysEvent(string name) => name != null && name.StartsWith("PHYS_", StringComparison.Ordinal);

        public static bool TryRead(object payload, out VfxImpact e)
        {
            e = default;
            if (payload == null) return false;
#if CRASHVERSE_SIM_IMPACT
            if (payload is Crashverse.Sim.ImpactEvent ie)
            {
                e.Kind = (int)ie.kind; e.Tier = (int)ie.tier; e.Source = (int)ie.source; e.PieceId = ie.pieceId; e.DamageStep = ie.damageStep;
                e.MaterialA = DamageRules.FromSimMaterial((int)ie.materialTypeA); e.MaterialB = DamageRules.FromSimMaterial((int)ie.materialTypeB);
                e.Dominant = DamageRules.FromSimMaterial((int)ie.dominantMaterial); e.Impulse = ie.impulse;
                e.X = ie.position.x; e.Y = ie.position.y; e.Z = ie.position.z; return true;
            }
            return false;
#else
            var t = payload.GetType();
            if (t.Name != "ImpactEvent") return false;
            var f = Fields(t);
            e.Kind = I(f, "kind", payload); e.Tier = I(f, "tier", payload); e.Source = I(f, "source", payload);
            e.PieceId = f.ContainsKey("pieceId") ? I(f, "pieceId", payload) : -1; e.DamageStep = I(f, "damageStep", payload);
            e.MaterialA = DamageRules.FromSimMaterial(I(f, "materialTypeA", payload, 4));
            e.MaterialB = DamageRules.FromSimMaterial(I(f, "materialTypeB", payload, 4));
            e.Dominant = DamageRules.FromSimMaterial(I(f, "dominantMaterial", payload, 4));
            e.Impulse = f.TryGetValue("impulse", out var fi) ? Convert.ToSingle(fi.GetValue(payload)) : 0f;
            if (f.TryGetValue("position", out var pf))
            {
                var p = pf.GetValue(payload); var pt = p.GetType();
                e.X = F(pt, "x", p); e.Y = F(pt, "y", p); e.Z = F(pt, "z", p);
            }
            return true;
#endif
        }

        static readonly Dictionary<Type, Dictionary<string, FieldInfo>> _cache = new Dictionary<Type, Dictionary<string, FieldInfo>>();
        static Dictionary<string, FieldInfo> Fields(Type t)
        {
            if (_cache.TryGetValue(t, out var d)) return d;
            d = new Dictionary<string, FieldInfo>();
            foreach (var fi in t.GetFields(BindingFlags.Public | BindingFlags.Instance)) d[fi.Name] = fi;
            _cache[t] = d; return d;
        }
        static int I(Dictionary<string, FieldInfo> f, string n, object o, int dflt = 0) => f.TryGetValue(n, out var fi) ? Convert.ToInt32(fi.GetValue(o)) : dflt;
        static float F(Type t, string n, object o) { var fi = t.GetField(n); return fi != null ? Convert.ToSingle(fi.GetValue(o)) : 0f; }
    }
}
