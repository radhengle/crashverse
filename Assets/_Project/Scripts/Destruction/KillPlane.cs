using UnityEngine;

namespace Crashverse.Destruction
{
    /// <summary>
    /// Art v2: below the floating island. Objective pieces that fall off count as destroyed (DestructionTracker);
    /// debris and projectiles are recycled. Implemented as a height check (cheap, no trigger setup needed).
    /// </summary>
    public class KillPlane : MonoBehaviour
    {
        public float killY = -6f;
        public static KillPlane Instance { get; private set; }
        DestructibleBlock[] _blocks = new DestructibleBlock[0];

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Bind(DestructibleBlock[] blocks) { _blocks = blocks ?? new DestructibleBlock[0]; }

        void FixedUpdate()
        {
            for (int i = 0; i < _blocks.Length; i++)
            {
                var b = _blocks[i];
                if (b == null || b.IsBroken || b.IsOutOfBounds) continue;
                if (b.transform.position.y < killY) b.MarkOutOfBounds();
            }
        }
    }

    /// <summary>Kinematic rotating part (L4 windmill sails). Time Fracture scales speed globally.</summary>
    public class MovingPart : MonoBehaviour
    {
        public Vector3 axis = Vector3.right;
        public Vector3 pivotWorld;
        public float degreesPerSecond = 30f;
        public static float GlobalSpeedScale = 1f;

        void FixedUpdate()
        {
            float a = degreesPerSecond * GlobalSpeedScale * Time.fixedDeltaTime;
            transform.RotateAround(pivotWorld, axis, a);
        }
    }
}
