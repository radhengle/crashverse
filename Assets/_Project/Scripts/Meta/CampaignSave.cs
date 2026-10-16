using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Crashverse.Meta
{
    /// <summary>
    /// QA-UI-006: the ONLY local unencrypted save. Holds campaign progress (stars, clears, mastery, loadouts,
    /// last node). Wallet, rewards, ranked Titan, season and leaderboard live behind IBackendService.
    /// </summary>
    [Serializable] public class LevelProgress { public string id; public int bestStars; public bool cleared; public bool mastery; }
    [Serializable] public class SavedLoadout { public string levelId; public string core; public string module; public string booster; }

    [Serializable]
    public class CampaignSaveData
    {
        public int version = 1;
        public List<LevelProgress> levels = new List<LevelProgress>();
        public List<SavedLoadout> loadouts = new List<SavedLoadout>();
        public SavedLoadout lastLoadout = new SavedLoadout();
        public string lastPlayedLevel = "c1_l01";
    }

    public class CampaignSave
    {
        public CampaignSaveData Data { get; private set; } = new CampaignSaveData();
        readonly string _path;

        public CampaignSave(string path = null)
        {
            _path = path ?? Path.Combine(Application.persistentDataPath, "campaign_save.json");
            Load();
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_path)) Data = JsonUtility.FromJson<CampaignSaveData>(File.ReadAllText(_path)) ?? new CampaignSaveData();
            }
            catch (Exception e) { Debug.Log("[Save] corrupt save, resetting: " + e.Message); Data = new CampaignSaveData(); }
            if (Data.levels == null) Data.levels = new List<LevelProgress>();
            if (Data.loadouts == null) Data.loadouts = new List<SavedLoadout>();
            if (Data.lastLoadout == null) Data.lastLoadout = new SavedLoadout();
        }

        public void Save()
        {
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(Data));
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(tmp, _path);
        }

        public LevelProgress Get(string id)
        {
            foreach (var l in Data.levels) if (l.id == id) return l;
            return null;
        }

        public int BestStars(string id) => Get(id)?.bestStars ?? 0;
        public bool IsCleared(string id) => Get(id)?.cleared ?? false;

        /// <summary>Records a result. Lower stars never overwrite a higher best. Returns true on first clear.</summary>
        public bool RecordResult(string id, bool won, int stars, bool masteryDone)
        {
            var p = Get(id);
            if (p == null) { p = new LevelProgress { id = id }; Data.levels.Add(p); }
            bool firstClear = won && !p.cleared;
            if (won) p.cleared = true;
            if (stars > p.bestStars) p.bestStars = stars;
            if (masteryDone) p.mastery = true;
            Data.lastPlayedLevel = id;
            Save();
            return firstClear;
        }

        /// <summary>SG-09: per-level loadout; first visit uses the last loadout used anywhere.</summary>
        public SavedLoadout GetLoadout(string levelId)
        {
            foreach (var l in Data.loadouts) if (l.levelId == levelId) return l;
            var last = Data.lastLoadout;
            return new SavedLoadout { levelId = levelId, core = last.core, module = last.module, booster = last.booster };
        }

        public void SetLoadout(SavedLoadout lo)
        {
            Data.loadouts.RemoveAll(x => x.levelId == lo.levelId);
            Data.loadouts.Add(lo);
            Data.lastLoadout = new SavedLoadout { levelId = "", core = lo.core, module = lo.module, booster = lo.booster };
            Save();
        }
    }
}
