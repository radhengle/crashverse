using System.IO;
using UnityEngine;
using Crashverse.Meta;

namespace Crashverse.Core
{
    /// <summary>B6: after the server confirms DeleteAccount, wipe every local trace (campaign save, queues, journals, prefs except the age band).</summary>
    public static class AccountDeletion
    {
        public static void WipeLocal()
        {
            TitanRunJournal.Clear();
            CampaignResultQueue.Clear();
            var p = CampaignSave.DefaultPath; if (File.Exists(p)) File.Delete(p);
            AppServices.Save = new CampaignSave();
            int band = PlayerPrefs.GetInt(UI.AgeGateScreen.PrefKey + AgeGate.Version, 0);
            PlayerPrefs.DeleteAll();
            if (band != 0) PlayerPrefs.SetInt(UI.AgeGateScreen.PrefKey + AgeGate.Version, band); // TODO(Security Admin): keep or re-ask after deletion?
            PlayerPrefs.Save();
        }
    }
}
