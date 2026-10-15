using System.Collections.Generic;
using Crashverse.Destruction;
using UnityEngine;

namespace Crashverse.Level
{
    /// <summary>
    /// Procedural greybox for m1_clockwork_tower (Contract §7) in Sunnyville material colours (arena_brief).
    /// Brick plinth, wood wings, 3 steel ring-joints, wood/brick crown.
    /// Tower placed ~30u from Wrecker at x=-20 → tower centre near x=10.
    /// </summary>
    public class LevelBuilder : MonoBehaviour
    {
        public Transform structureRoot;

        MaterialDefinition _wood;
        MaterialDefinition _brick;
        MaterialDefinition _steel;
        readonly List<DestructibleBlock> _blocks = new List<DestructibleBlock>();

        public IReadOnlyList<DestructibleBlock> Blocks => _blocks;
        public MaterialDefinition Wood => _wood;
        public MaterialDefinition Brick => _brick;
        public MaterialDefinition Steel => _steel;

        public void BuildClockworkTower(Vector3 basePosition)
        {
            Clear();
            EnsureMaterials();
            if (structureRoot == null)
            {
                var root = new GameObject("Structure_ClockworkTower");
                structureRoot = root.transform;
                structureRoot.SetParent(transform);
            }
            structureRoot.position = basePosition;

            // --- Brick plinth (heavy base) ---
            var plinth = MakeBlock("Plinth", new Vector3(0f, 0.6f, 0f), new Vector3(4.5f, 1.2f, 4.5f), _brick, objective: true);
            // Ground anchor — plinth sits on invisible static ground via support null = grounded
            // Mark plinth as grounded by giving it a kinematic floor sibling connection
            var ground = MakeStaticGround(basePosition);

            // --- Wood scaffold wings (teaching weak points) ---
            var wingL = MakeBlock("Wing_L", new Vector3(-3.2f, 2.2f, 0f), new Vector3(1.2f, 2.8f, 0.5f), _wood, true);
            var wingR = MakeBlock("Wing_R", new Vector3(3.2f, 2.2f, 0f), new Vector3(1.2f, 2.8f, 0.5f), _wood, true);
            wingL.ConnectJoint(plinth, _wood.jointBreakForce);
            wingR.ConnectJoint(plinth, _wood.jointBreakForce);
            // Cross braces
            var braceL = MakeBlock("Brace_L", new Vector3(-2.2f, 1.8f, 0.8f), new Vector3(1.6f, 0.3f, 0.3f), _wood, true);
            var braceR = MakeBlock("Brace_R", new Vector3(2.2f, 1.8f, 0.8f), new Vector3(1.6f, 0.3f, 0.3f), _wood, true);
            braceL.ConnectJoint(wingL); braceL.ConnectJoint(plinth);
            braceR.ConnectJoint(wingR); braceR.ConnectJoint(plinth);

            // --- Tower shaft segments (brick walls) + 3 steel ring-joints at 1/3, 1/2, 2/3 ---
            // Heights: plinth top ~1.2; shaft rises to ~10
            float[] jointYs = { 3.5f, 5.5f, 7.5f }; // ~1/3, 1/2, 2/3 of tower body
            DestructibleBlock below = plinth;
            DestructibleBlock[] joints = new DestructibleBlock[3];

            for (int i = 0; i < 3; i++)
            {
                float y0 = i == 0 ? 1.4f : jointYs[i - 1] + 0.35f;
                float y1 = jointYs[i] - 0.35f;
                float midY = (y0 + y1) * 0.5f;
                float h = Mathf.Max(0.6f, y1 - y0);

                // Four brick wall panels (hollow shaft)
                var wallF = MakeBlock($"Wall_F_{i}", new Vector3(0f, midY, 1.1f), new Vector3(2.2f, h, 0.35f), _brick, true);
                var wallB = MakeBlock($"Wall_B_{i}", new Vector3(0f, midY, -1.1f), new Vector3(2.2f, h, 0.35f), _brick, true);
                var wallL = MakeBlock($"Wall_L_{i}", new Vector3(-1.1f, midY, 0f), new Vector3(0.35f, h, 2.2f), _brick, true);
                var wallR = MakeBlock($"Wall_R_{i}", new Vector3(1.1f, midY, 0f), new Vector3(0.35f, h, 2.2f), _brick, true);

                wallF.AddSupport(below); wallB.AddSupport(below);
                wallL.AddSupport(below); wallR.AddSupport(below);
                wallF.ConnectJoint(below); wallB.ConnectJoint(below);
                wallL.ConnectJoint(below); wallR.ConnectJoint(below);

                // Steel ring-joint (critical) — visually thicker / gold-tint weak point marker via scale
                var joint = MakeBlock($"SteelRing_{i}", new Vector3(0f, jointYs[i], 0f), new Vector3(2.6f, 0.45f, 2.6f), _steel, true, steelJoint: true);
                // Gold weak-point outline: slight emissive via brighter lerp
                TintWeakPoint(joint);
                joint.AddSupport(wallF); joint.AddSupport(wallB);
                joint.AddSupport(wallL); joint.AddSupport(wallR);
                joint.ConnectJoint(wallF); joint.ConnectJoint(wallB);
                joint.ConnectJoint(wallL); joint.ConnectJoint(wallR);
                joints[i] = joint;
                below = joint;
            }

            // --- Crown: wood + brick clock house (high objective mass) ---
            var crownFloor = MakeBlock("CrownFloor", new Vector3(0f, 8.3f, 0f), new Vector3(3.2f, 0.35f, 3.2f), _brick, true);
            crownFloor.AddSupport(below);
            crownFloor.ConnectJoint(below);

            var crownWood = MakeBlock("CrownWood", new Vector3(0f, 9.2f, 0f), new Vector3(2.8f, 1.4f, 2.8f), _wood, true);
            crownWood.AddSupport(crownFloor);
            crownWood.ConnectJoint(crownFloor);

            var crownBrick = MakeBlock("CrownBrick", new Vector3(0f, 10.3f, 0f), new Vector3(2.4f, 0.8f, 2.4f), _brick, true);
            crownBrick.AddSupport(crownWood);
            crownBrick.ConnectJoint(crownWood);

            // Roof (navy, non-structural look but still objective mass as brick-family for readability)
            var roof = MakeBlock("CrownRoof", new Vector3(0f, 11.1f, 0f), new Vector3(3.0f, 0.7f, 3.0f), _brick, true);
            roof.AddSupport(crownBrick);
            roof.ConnectJoint(crownBrick);
            // Paint roof navy per art brief
            SetColor(roof, Hex("2F4A6B"));

            // Decorative NonObjective barrel near base (does not count)
            var barrel = MakeBlock("ExplosiveBarrel_Decor", new Vector3(5.5f, 0.6f, 2f), new Vector3(0.8f, 1.2f, 0.8f), _wood, objective: false);
            SetColor(barrel, Hex("E5484D"));
            // Sit on ground kinematic
            var barrelRb = barrel.GetComponent<Rigidbody>();
            barrelRb.isKinematic = true;

            // Plinth grounded
            plinth.GetComponent<Rigidbody>().isKinematic = true; // base stays until heavily damaged
            // Allow plinth to become dynamic when broken via HP
            // Keep kinematic until Break() releases it — already handled

            Debug.Log($"[LevelBuilder] Clockwork Tower built: {_blocks.Count} blocks at {basePosition}");
        }

        public void Clear()
        {
            foreach (var b in _blocks)
            {
                if (b != null) Destroy(b.gameObject);
            }
            _blocks.Clear();
            if (structureRoot != null)
            {
                for (int i = structureRoot.childCount - 1; i >= 0; i--)
                    Destroy(structureRoot.GetChild(i).gameObject);
            }
            DebrisPool.Instance?.ClearAll();
        }

        void EnsureMaterials()
        {
            if (_wood == null) _wood = MaterialDefinition.CreateRuntime(MaterialKind.Wood);
            if (_brick == null) _brick = MaterialDefinition.CreateRuntime(MaterialKind.Brick);
            if (_steel == null) _steel = MaterialDefinition.CreateRuntime(MaterialKind.Steel);
        }

        DestructibleBlock MakeBlock(string name, Vector3 localPos, Vector3 scale, MaterialDefinition mat, bool objective, bool steelJoint = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(structureRoot, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var rb = go.AddComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.sleepThreshold = 0.08f;
            rb.isKinematic = true;
            var block = go.AddComponent<DestructibleBlock>();
            block.Configure(mat, objective, steelJoint);
            _blocks.Add(block);
            return block;
        }

        GameObject MakeStaticGround(Vector3 towerBase)
        {
            // Large ground already created by arena; this is a local anchor marker
            return null;
        }

        static void TintWeakPoint(DestructibleBlock joint)
        {
            var rend = joint.GetComponent<Renderer>();
            if (rend == null) return;
            // Mix steel with gold outline feel
            Color steel = Hex("6B7684");
            Color gold = Hex("E8B04B");
            var c = Color.Lerp(steel, gold, 0.35f);
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            rend.SetPropertyBlock(block);
        }

        static void SetColor(DestructibleBlock b, Color c)
        {
            var rend = b.GetComponent<Renderer>();
            if (rend == null) return;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            rend.SetPropertyBlock(block);
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
