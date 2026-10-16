using UnityEngine;

namespace Crashverse.Import
{
    /// <summary>
    /// Plain data marker written by the import pipeline from FBX user properties (mass_kg, break_force_N).
    /// Authoring defaults only: Physics Developer owns the final values and how they are used. No logic here.
    /// </summary>
    public sealed class ImportedPhysicsHints : MonoBehaviour
    {
        [Tooltip("FBX user property mass_kg (kg). 0 = not authored.")]
        public float mass;
        [Tooltip("FBX user property break_force_N (N). 0 = not authored.")]
        public float breakForce;
    }
}
