using System;
using System.Threading.Tasks;
using Crashverse.Backend;
using Crashverse.Data;
using Crashverse.Meta;
using Crashverse.UI;

namespace Crashverse.Core
{
    /// <summary>Composition root. Swap Backend for a real server implementation of IBackendService here.</summary>
    public static class AppServices
    {
        public static ContentDatabase Content;
        public static CampaignSave Save;
        public static IBackendService Backend;
        public static LocalBackendService LocalBackend => Backend as LocalBackendService; // QA panel only
        public static UIRouter Router;
        public static UITheme Theme;
        public static System.Collections.Generic.List<StoreItem> StoreItems = new System.Collections.Generic.List<StoreItem>();
        public static System.Collections.Generic.List<string> StoreErrors = new System.Collections.Generic.List<string>();

        static TimeSpan _serverOffset; static bool _synced;
        /// <summary>SG-20: server time = device + offset measured at launch/resume. Time-gated UI disables when not synced.</summary>
        public static bool TimeSynced => _synced;
        public static DateTime ServerNow => DateTime.UtcNow + _serverOffset;

        public static async Task SyncServerTime()
        {
            try { var s = await Backend.GetServerTimeUtc(); _serverOffset = s - DateTime.UtcNow; _synced = true; }
            catch (BackendException) { _synced = false; }
        }

        /// <summary>Display helper: local wall-clock for a UTC instant, labelled with the device zone (e.g. IST).</summary>
        public static string LocalLabel(DateTime utc) => utc.ToLocalTime().ToString("HH:mm") + " " + ZoneAbbrev();
        static string ZoneAbbrev()
        {
            var off = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
            return off == new TimeSpan(5, 30, 0) ? "IST" : "UTC" + (off >= TimeSpan.Zero ? "+" : "-") + off.ToString(@"hh\:mm");
        }
    }
}
