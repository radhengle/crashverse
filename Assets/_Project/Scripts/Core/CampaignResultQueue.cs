using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Crashverse.Backend;
using UnityEngine;

namespace Crashverse.Core
{
    /// <summary>
    /// BUG-14: campaign wins are queued on disk first, then synced in order. Offline wins stay queued and are retried at launch,
    /// on resume and after the next win. The file is client-editable, so it carries raw run stats only. The server re-validates
    /// every entry (BUG-13), and reportId makes retries idempotent.
    /// </summary>
    public static class CampaignResultQueue
    {
        [System.Serializable] class QueueFile { public List<CampaignRunReport> items = new List<CampaignRunReport>(); }
        static string PathFor => System.IO.Path.Combine(Application.persistentDataPath, "campaign_sync_queue.json");

        static QueueFile Load()
        {
            try { return File.Exists(PathFor) ? (JsonUtility.FromJson<QueueFile>(File.ReadAllText(PathFor)) ?? new QueueFile()) : new QueueFile(); }
            catch { return new QueueFile(); }
        }
        static void Store(QueueFile q) => File.WriteAllText(PathFor, JsonUtility.ToJson(q));

        public static int Count => Load().items.Count;
        public static void Clear() { if (File.Exists(PathFor)) File.Delete(PathFor); }

        public static void Enqueue(CampaignRunReport r)
        {
            if (string.IsNullOrEmpty(r.reportId)) r.reportId = System.Guid.NewGuid().ToString("N");
            var q = Load(); q.items.Add(r); Store(q);
        }

        /// <summary>Syncs queued reports oldest first. Stops at the first offline error and keeps the rest. Returns the last response, or null.</summary>
        public static async Task<CampaignSubmitResponse> Flush(IBackendService backend)
        {
            var q = Load(); CampaignSubmitResponse last = null;
            while (q.items.Count > 0)
            {
                try { last = await backend.SubmitCampaignResult(q.items[0]); }
                catch (BackendException) { break; }
                q.items.RemoveAt(0); Store(q); // accepted or rejected: either way it's settled by the server
            }
            return last;
        }
    }
}
