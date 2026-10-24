using Crashverse.Core;
using System.Collections.Generic;
using Crashverse.Data;
using Crashverse.Destruction;
using UnityEngine;

namespace Crashverse.Level
{
    /// <summary>Builds any structure from layout JSON (no per-level code, DP §1 AC1).</summary>
    public class LevelBuilder : MonoBehaviour
    {
        public Transform structureRoot;
        readonly List<DestructibleBlock> _blocks = new List<DestructibleBlock>();
        readonly Dictionary<string, DestructibleBlock> _byId = new Dictionary<string, DestructibleBlock>();
        public IReadOnlyList<DestructibleBlock> Blocks => _blocks;
        public DestructibleBlock Find(string id) => id != null && _byId.TryGetValue(id, out var b) ? b : null;

        public void Build(LayoutData layout, Vector3 basePosition, RuleModifiers mod)
        {
            Clear();
            if (structureRoot == null)
            {
                structureRoot = new GameObject("Structure").transform;
                structureRoot.SetParent(transform);
            }
            structureRoot.position = basePosition;
            var mats = new Dictionary<string, MaterialDefinition>();
            foreach (var bd in layout.blocks)
            {
                if (!mats.TryGetValue(bd.mat, out var mat))
                {
                    var kind = MaterialDefinition.Parse(bd.mat) ?? MaterialKind.Wood;
                    mats[bd.mat] = mat = MaterialDefinition.CreateRuntime(kind, mod);
                }
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); RenderMaterials.Apply(go);
                go.name = bd.id;
                go.transform.SetParent(structureRoot, false);
                go.transform.localPosition = V(bd.p);
                go.transform.localScale = V(bd.s);
                var rb = go.AddComponent<Rigidbody>();
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                var block = go.AddComponent<DestructibleBlock>();
                block.Configure(bd, mat);
                Crashverse.Gameplay.PieceFeedbackWiring.Attach(block); // VFX #31 DamageFeedback, pieceId = StableId.Of(blockId)
                if (bd.spinDps > 0f && bd.spinAxis != null && bd.spinAxis.Length == 3)
                {
                    var mp = go.AddComponent<MovingPart>();
                    mp.axis = V(bd.spinAxis);
                    mp.pivotWorld = structureRoot.position + V(bd.pivot);
                    mp.degreesPerSecond = bd.spinDps;
                }
                _blocks.Add(block);
                _byId[bd.id] = block;
            }
            foreach (var bd in layout.blocks)
            {
                var b = _byId[bd.id];
                if (bd.sup != null) foreach (var s in bd.sup) b.AddSupport(Find(s), withJoint: true);
            }
        }

        public void Clear()
        {
            foreach (var b in _blocks) if (b != null) Destroy(b.gameObject);
            _blocks.Clear(); _byId.Clear();
            if (DebrisPool.Instance != null) DebrisPool.Instance.ClearAll();
        }

        static Vector3 V(float[] a) => a != null && a.Length == 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;
    }
}
