namespace Crashverse.Meta
{
    /// <summary>Player-facing copy for server purchase errors. Never shows raw codes; unknown codes get a generic line.</summary>
    public static class PurchaseErrors
    {
        public static string Message(string code)
        {
            switch (code)
            {
                case "GEM_PACKS_DISABLED": return "Gem packs aren't available. Earn gems by playing!";
                case "AGE_RESTRICTED": return "Purchases aren't available on this account.";
                default: return "Purchase failed. Please try again later.";
            }
        }
    }
}
