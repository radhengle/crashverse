using System.IO;
using Crashverse.Backend;
using UnityEngine;

namespace Crashverse.Core
{
    /// <summary>
    /// SG-11 / SG-13 / SEC-01: a ranked run is journaled after every settled shot (and at start). Each entry is reported to the
    /// server, which returns it with a server HMAC receipt. The client never signs anything: on relaunch the journal is sent
    /// VERBATIM via FinalizeRanked, and the server trusts only its own run state, its own receipts and a plausibility-checked
    /// offline tail. Editing this file cannot raise a score above what the server has already seen.
    /// </summary>
    public static class TitanRunJournal
    {
        static string PathFor => System.IO.Path.Combine(Application.persistentDataPath, "titan_ranked_journal.json");

        public static void Write(RankedJournal j) { File.WriteAllText(PathFor, JsonUtility.ToJson(j)); }
        public static void Clear() { if (File.Exists(PathFor)) File.Delete(PathFor); }
        public static RankedJournal Read()
        {
            try { return File.Exists(PathFor) ? JsonUtility.FromJson<RankedJournal>(File.ReadAllText(PathFor)) : null; }
            catch { return null; }
        }

        /// <summary>
        /// Launch/resume: finalizes an orphaned ranked run. `final` is kept exactly as recorded, and so is the kill flag (BUG-04),
        /// so a kill that was queued offline keeps its bonus. Stays queued while offline.
        /// </summary>
        public static async System.Threading.Tasks.Task<SubmitResponse> FlushPending(IBackendService backend)
        {
            var j = Read();
            if (j == null) return null;
            try
            {
                var resp = await backend.FinalizeRanked(j); // no client-side mutation or signing (SEC-01)
                Clear();
                return resp;
            }
            catch (BackendException) { return new SubmitResponse { status = SubmitStatus.QueuedOffline, message = "Ranked result queued; will retry when online" }; }
        }
    }
}
