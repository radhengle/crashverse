using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Crashverse.Backend;

namespace Crashverse.Core
{
    /// <summary>
    /// In-app deletion client (Deletion_AgeGate_Requirements §A, §C1). Offline requests are queued and shown as pending (A6),
    /// never faked. On server confirmation: sign out, wipe every CRASHVERSE file + prefs (A4), restart in first-run state.
    /// </summary>
    public static class AccountDeletion
    {
        public const string PendingKey = "cv_delete_pending";
        public static readonly string[] LocalFiles = { "campaign_save.json", "campaign_save.json.tmp", "titan_ranked_journal.json", "campaign_sync_queue.json", "local_server_db.json" };
        public static bool Pending => PlayerPrefs.GetInt(PendingKey, 0) == 1;

        public enum Result { Scheduled, PendingOffline, ReauthRequired, Failed }

        /// <summary>Re-auth token from POST /auth/reauth: memory only (never persisted), valid 5 minutes.</summary>
        static string _reauthToken;

        /// <summary>Send (or queue) the request. Linked accounts pass the reauthToken from /auth/reauth (A3); guests pass null.</summary>
        public static async Task<Result> Request(IBackendService backend, string reauthToken)
        {
            _reauthToken = reauthToken;
            PlayerPrefs.SetInt(PendingKey, 1); PlayerPrefs.Save();
            return await Flush(backend);
        }

        /// <summary>Called on launch/resume (A6). Returns Scheduled only after the server confirmed.</summary>
        public static async Task<Result> Flush(IBackendService backend)
        {
            if (!Pending) return Result.Failed;
            try
            {
                var r = await backend.DeleteAccount(_reauthToken);
                if (r.status != "scheduled") return Result.Failed;
                _reauthToken = null;
                WipeLocal();
                return Result.Scheduled;
            }
            catch (BackendException e) when (e.Code == "REAUTH_REQUIRED") { return Result.ReauthRequired; }
            catch (BackendException e) when (e.IsOffline) { return Result.PendingOffline; }
            catch (BackendException) { return Result.Failed; }
        }

        public static void Cancel() { PlayerPrefs.DeleteKey(PendingKey); PlayerPrefs.Save(); }

        /// <summary>A4: no CRASHVERSE files remain in persistentDataPath; all prefs (incl. age band) cleared → first-run state.
        /// Session tokens are memory-only (nothing in Keystore today); when a Keystore token store is added it must be cleared here.</summary>
        public static void WipeLocal()
        {
            var dir = Application.persistentDataPath;
            foreach (var f in LocalFiles) { var p = Path.Combine(dir, f); if (File.Exists(p)) File.Delete(p); }
            if (Directory.Exists(dir)) foreach (var p in Directory.GetFiles(dir, "*.json")) File.Delete(p); // any other CRASHVERSE json
            PlayerPrefs.DeleteAll(); PlayerPrefs.Save();
        }
    }
}
