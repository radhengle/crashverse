namespace Crashverse.Weapons
{
    /// <summary>
    /// Wrecker v2 greybox numbers from /workspace/art/m1b/CHANGES.md (cannon on wheels, no turret). Units = metres,
    /// root at ground centre, barrel points along +X at yaw 0. Unit-tested so the builder can't silently regress to v1.
    /// </summary>
    public static class WreckerSpec
    {
        public const int Version = 2;
        public const float EnvelopeL = 4.0f, EnvelopeW = 2.0f, EnvelopeH = 2.4f;
        // Carriage (wooden wedge) ~2.8 x 1.6, 0.55..1.35 high
        public const float CarriageL = 2.8f, CarriageW = 1.6f, CarriageBottom = 0.55f, CarriageTop = 1.35f;
        // Wheels
        public const float FrontWheelDiameter = 1.2f, FrontAxleX = 0.15f, FrontAxleY = 0.6f;
        public const float RearRollerDiameter = 0.7f, RearAxleX = -1.35f, RearAxleY = 0.35f;
        public const float WheelWidth = 0.3f, WheelZ = 0.85f;
        // Core orb at barrel pivot
        public const float PivotX = 0f, PivotY = 1.5f, PivotZ = 0f, CoreDiameter = 0.9f;
        // Barrel: tapered 0.84 -> 0.68, 2.3 long, from -0.7 to +1.6 off the pivot
        public const float BarrelRearDiameter = 0.84f, BarrelFrontDiameter = 0.68f, BarrelLength = 2.3f;
        public const float BarrelStart = -0.7f, BarrelEnd = 1.6f;
        // Module pod 0.64 x 0.44 x 0.4 on barrel top: 0.65 along, 0.62 up
        public const float ModuleL = 0.64f, ModuleH = 0.44f, ModuleW = 0.4f, ModuleAlong = 0.65f, ModuleUp = 0.62f;
        // Booster ring 0.9 diameter, depth 0.3, at 1.75 along
        public const float BoosterDiameter = 0.9f, BoosterDepth = 0.3f, BoosterAlong = 1.75f;
        // Projectile spawn = booster centre + 0.2 forward
        public const float SpawnAlong = BoosterAlong + 0.2f;
        // Aim limits (unchanged)
        public const float MinElevation = 5f, MaxElevation = 70f, MaxYaw = 60f, RestElevation = 25f;
        // Colours
        public const string Core = "22D3EE", Module = "8B5CF6", Booster = "FF7A1A";
        public const string BarrelNavy = "3E4C6E", CarriageWood = "D9965A", Gold = "E8B04B", Outline = "101828", Tyre = "1B1B1F";
        public const float WreckerX = -20f;
    }
}
