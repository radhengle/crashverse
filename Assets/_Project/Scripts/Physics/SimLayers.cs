using UnityEngine;

namespace Crashverse.Sim
{
    /// <summary>
    /// Fixed collision layers for the sim (ProjectSettings/TagManager.asset: 8 Structure, 9 Debris, 10 Projectile).
    /// Ranked runs re-apply the debris rows of the matrix in code so nothing at runtime can make them differ between devices.
    /// </summary>
    public static class SimLayers
    {
        public const int Default = 0, Structure = 8, Debris = 9, Projectile = 10;

        /// <summary>Debris collides with pieces (Default/Structure) and with other debris, never with projectiles.</summary>
        public static void ApplyRankedMatrix()
        {
            Physics.IgnoreLayerCollision(Debris, Default, false);
            Physics.IgnoreLayerCollision(Debris, Structure, false);
            Physics.IgnoreLayerCollision(Debris, Debris, false);
            Physics.IgnoreLayerCollision(Debris, Projectile, true);
        }
    }
}
