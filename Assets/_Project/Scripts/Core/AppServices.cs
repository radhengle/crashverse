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
        /// <summary>Auth session token: memory only, never persisted (passwords are never stored at all).</summary>
        public static string SessionToken;
        public static AccountState Account; // last fetched server account (age band, consent, deletion state)
        public static ContentDatabase Content;
        public static CampaignSave Save;
        /// <summary>integration/v2 local progression: guest profile, wallet, chests, Daily Boss, achievement counters (offline).</summary>
        public static PlayerProgress Progress;
        public static IBackendService Backend;
#if CRASHVERSE_DEV
        public static LocalBackendService LocalBackend => Backend as LocalBackendService; // QA panel only
#endif
        public static UIRouter Router;
        public static UITheme Theme;
        public static System.Collections.Generic.List<StoreItem> StoreItems = new System.Collections.Generic.List<StoreItem>();
        public static System.Collections.Generic.List<string> StoreErrors = new System.Collections.Generic.List<string>();

        static DateTime _serverAnchor; static double _monoAnchor; static bool _synced;
        static readonly System.Diagnostics.Stopwatch Mono = System.Diagnostics.Stopwatch.StartNew();
        /// <summary>SG-20 / BUG-03: server time at the last sync advanced by a monotonic timer. The device clock is never read.</summary>
        public static bool TimeSynced => _synced;
        public static DateTime ServerNow => _serverAnchor.AddSeconds(Mono.Elapsed.TotalSeconds - _monoAnchor);

        public static async Task SyncServerTime()
        {
            try { var s = await Backend.GetServerTimeUtc(); _serverAnchor = s; _monoAnchor = Mono.Elapsed.TotalSeconds; _synced = true; }
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
