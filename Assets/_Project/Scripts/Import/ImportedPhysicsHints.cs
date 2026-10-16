using UnityEngine;

namespace Crashverse.Import
{
    /// <summary>
    /// Plain data marker written by the import pipeline from FBX user properties. Float SI authoring defaults only:
    /// Physics Developer converts them to integer units, maps them onto MaterialDefinition and owns their use. No logic here.
    /// </summary>
    public sealed class ImportedPhysicsHints : MonoBehaviour
    {
        [Tooltip("mass_kg (kg). Steel uses an effective density of 800 kg/m3. 0 = not authored.")]
        public float mass;
        [Tooltip("break_force_N (N). 0 = not authored.")]
        public float breakForce;
        [Tooltip("break_torque_Nm (N·m). 0 = not authored.")]
        public float breakTorque;
        [Tooltip("anchor_to: name of the object/anchor this piece is anchored to. Empty = not authored.")]
        public string anchorTo;
    }
}
