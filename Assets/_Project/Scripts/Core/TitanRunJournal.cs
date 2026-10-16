using System.IO;
using Crashverse.Backend;
using UnityEngine;

namespace Crashverse.Core
{
    /// <summary>
    /// SG-11 / SG-13: a ranked run's progress is written after every settled shot (and on pause). If the app is killed,
    /// the next launch submits the partial result. Not editable for gain: the backend validates the signature/bounds.
    /// </summary>
    public static class TitanRunJournal
    {
        static string PathFor => System.IO.Path.Combine(Application.persistentDataPath, "titan_ranked_journal.json");

        public static void Write(RankedResult r) { File.WriteAllText(PathFor, JsonUtility.ToJson(r)); }
        public static void Clear() { if (File.Exists(PathFor)) File.Delete(PathFor); }
        public static RankedResult Read()
        {
            try { return File.Exists(PathFor) ? JsonUtility.FromJson<RankedResult>(File.ReadAllText(PathFor)) : null; }
            catch { return null; }
        }

        /// <summary>Called at launch/resume: submits any orphaned ranked run as partial; keeps it queued while offline.</summary>
        public static async System.Threading.Tasks.Task<SubmitResponse> FlushPending(IBackendService backend)
        {
            var r = Read();
            if (r == null) return null;
            r.partial = true; r.killed = false;
            r.signature = LocalBackendService.Sign(r);
            try
            {
                var resp = await backend.SubmitRanked(r);
                Clear();
                return resp;
            }
            catch (BackendException) { return new SubmitResponse { status = SubmitStatus.QueuedOffline, message = "Ranked result queued; will retry when online" }; }
        }
    }
}
