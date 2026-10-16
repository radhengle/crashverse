namespace Crashverse.Sim
{
    /// <summary>Engine-free material availability rules.</summary>
    public static class MaterialRules
    {
        /// <summary>Neon District is chapter 3 (Content Roadmap). Game designer ruling: glass is Neon and later only.</summary>
        public const int NeonChapter = 3;
        /// <summary>First chapter a material key may appear in. Unknown keys: 1.</summary>
        public static int MinChapter(string materialKey) => materialKey == "glass" ? NeonChapter : 1;
        public static bool AllowedInChapter(string materialKey, int chapter) => chapter >= MinChapter(materialKey);
    }
}
