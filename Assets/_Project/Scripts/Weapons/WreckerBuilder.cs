using Crashverse.Destruction;
using UnityEngine;
using S = Crashverse.Weapons.WreckerSpec;

namespace Crashverse.Weapons
{
    /// <summary>Procedural Wrecker v2: wooden carriage, 2 big front wheels + 2 rear rollers, navy barrel, slot parts, gold bands.</summary>
    public class WreckerBuilder : MonoBehaviour
    {
        public Transform yawPivot, pitchPivot, projectileSpawn, coreOrb, modulePod, boosterRing;

        public void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);

            float cH = S.CarriageTop - S.CarriageBottom;
            Prim("Carriage", PrimitiveType.Cube, transform, new Vector3(-0.35f, S.CarriageBottom + cH * 0.5f, 0f),
                 new Vector3(S.CarriageL, cH, S.CarriageW), S.CarriageWood, keepCollider: true);
            foreach (float z in new[] { -S.WheelZ, S.WheelZ })
            {
                Wheel("FrontWheel", new Vector3(S.FrontAxleX, S.FrontAxleY, z), S.FrontWheelDiameter);
                Wheel("RearRoller", new Vector3(S.RearAxleX, S.RearAxleY, z), S.RearRollerDiameter);
            }

            yawPivot = new GameObject("YawPivot").transform;
            yawPivot.SetParent(transform, false);
            yawPivot.localPosition = new Vector3(S.PivotX, S.PivotY, S.PivotZ);
            pitchPivot = new GameObject("PitchPivot").transform;
            pitchPivot.SetParent(yawPivot, false);
            pitchPivot.localRotation = Quaternion.Euler(0f, 0f, S.RestElevation);

            coreOrb = Prim("Core", PrimitiveType.Sphere, pitchPivot, Vector3.zero, Vector3.one * S.CoreDiameter, S.Core).transform;

            // Tapered barrel approximated by two segments (rear 0.84, front 0.68), along +X
            float mid = (S.BarrelStart + S.BarrelEnd) * 0.5f;
            Barrel("BarrelRear", S.BarrelStart, mid, S.BarrelRearDiameter, S.BarrelNavy);
            Barrel("BarrelFront", mid, S.BarrelEnd, S.BarrelFrontDiameter, S.BarrelNavy);
            Barrel("GoldBandMid", mid - 0.06f, mid + 0.06f, S.BarrelRearDiameter + 0.04f, S.Gold);
            Barrel("GoldBandTip", S.BarrelEnd - 0.14f, S.BarrelEnd - 0.02f, S.BarrelFrontDiameter + 0.04f, S.Gold);

            modulePod = Prim("Module", PrimitiveType.Cube, pitchPivot, new Vector3(S.ModuleAlong, S.ModuleUp, 0f),
                             new Vector3(S.ModuleL, S.ModuleH, S.ModuleW), S.Module).transform;
            boosterRing = Barrel("Booster", S.BoosterAlong - S.BoosterDepth * 0.5f, S.BoosterAlong + S.BoosterDepth * 0.5f, S.BoosterDiameter, S.Booster).transform;

            projectileSpawn = new GameObject("ProjectileSpawn").transform;
            projectileSpawn.SetParent(pitchPivot, false);
            projectileSpawn.localPosition = new Vector3(S.SpawnAlong, 0f, 0f);
        }

        void Wheel(string name, Vector3 pos, float d)
        {
            var w = Prim(name, PrimitiveType.Cylinder, transform, pos, new Vector3(d, S.WheelWidth * 0.5f, d), S.Tyre);
            w.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // axle along Z
            var hub = Prim(name + "Hub", PrimitiveType.Cylinder, transform, pos, new Vector3(d * 0.4f, S.WheelWidth * 0.55f, d * 0.4f), S.Gold);
            hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        GameObject Barrel(string name, float from, float to, float diameter, string hex)
        {
            float len = to - from;
            var g = Prim(name, PrimitiveType.Cylinder, pitchPivot, new Vector3((from + to) * 0.5f, 0f, 0f), new Vector3(diameter, len * 0.5f, diameter), hex);
            g.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // cylinder Y axis -> barrel X axis
            return g;
        }

        static GameObject Prim(string name, PrimitiveType t, Transform parent, Vector3 lp, Vector3 scale, string hex, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = lp;
            go.transform.localScale = scale;
            if (!keepCollider) { var c = go.GetComponent<Collider>(); if (c != null) Destroy(c); }
            var r = go.GetComponent<Renderer>();
            var b = new MaterialPropertyBlock();
            var col = MaterialDefinition.Hex(hex);
            b.SetColor("_BaseColor", col); b.SetColor("_Color", col);
            b.SetFloat("_Smoothness", hex == S.BarrelNavy || hex == S.Gold ? 0.7f : 0.5f);
            r.SetPropertyBlock(b);
            return go;
        }
    }
}
