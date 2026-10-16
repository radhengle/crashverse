using System.Collections.Generic;
using Crashverse.Data;

namespace Crashverse.Meta
{
    public enum Slot { Core = 0, Module = 1, Booster = 2 }

    /// <summary>DP §2 / SG-09: one tile per slot, slot type must match, forced tiles pre-filled and locked.</summary>
    public class Loadout
    {
        public readonly string[] Tiles = new string[3];
        public static readonly string[] SlotNames = { "Core", "Module", "Booster" };

        public static Slot? ParseSlot(string s)
        {
            for (int i = 0; i < 3; i++) if (SlotNames[i] == s) return (Slot)i;
            return null;
        }

        public string this[Slot s] { get => Tiles[(int)s]; set => Tiles[(int)s] = value; }

        public static bool IsSlotLocked(LevelEntry level, Slot s)
        {
            if (level.lockedSlots != null) foreach (var l in level.lockedSlots) if (l == SlotNames[(int)s]) return true;
            return ForcedTile(level, s) != null;
        }

        public static string ForcedTile(LevelEntry level, Slot s)
        {
            if (level.forced == null) return null;
            foreach (var f in level.forced) if (f.slot == SlotNames[(int)s]) return f.powerup;
            return null;
        }

        /// <summary>Tries to place a tile. Returns an error string or null on success.</summary>
        public string TryEquip(Slot s, PowerUpData p, LevelEntry level, ContentDatabase db, CampaignSave save)
        {
            if (IsSlotLocked(level, s)) return "This slot is locked for this level";
            if (p == null) { this[s] = null; return null; }
            if (ParseSlot(p.slot) != s) return $"{p.name} only fits the {p.slot} slot";
            if (!UnlockRules.IsPowerUpUnlocked(p, db, save)) return p.unlockText;
            this[s] = p.id;
            return null;
        }

        /// <summary>Builds the effective loadout for a level: saved choice, then forced/locked rules, then unlock check.</summary>
        public static Loadout Resolve(LevelEntry level, SavedLoadout saved, ContentDatabase db, CampaignSave save)
        {
            var lo = new Loadout();
            var wanted = new[] { saved?.core, saved?.module, saved?.booster };
            for (int i = 0; i < 3; i++)
            {
                var s = (Slot)i;
                var forced = ForcedTile(level, s);
                if (forced != null) { lo[s] = forced; continue; }
                if (IsSlotLocked(level, s)) continue;
                var p = db.GetPowerUp(wanted[i]);
                if (p != null && ParseSlot(p.slot) == s && UnlockRules.IsPowerUpUnlocked(p, db, save)) lo[s] = p.id;
            }
            return lo;
        }

        public SavedLoadout ToSaved(string levelId) =>
            new SavedLoadout { levelId = levelId, core = Tiles[0], module = Tiles[1], booster = Tiles[2] };

        public IEnumerable<string> Equipped() { foreach (var t in Tiles) if (!string.IsNullOrEmpty(t)) yield return t; }
    }
}
