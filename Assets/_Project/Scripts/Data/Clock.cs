using System;

namespace Crashverse.Data
{
    public interface IClock { DateTime UtcNow { get; } }
    /// <summary>Device clock. Display/diagnostics only: never used for resets, ranked or rewards (SG-20 / BUG-03).</summary>
    public sealed class SystemClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }

    /// <summary>A clock that must be synced from an authoritative source before use.</summary>
    public interface ISyncableClock : IClock { bool Synced { get; } System.Threading.Tasks.Task<bool> SyncAsync(); }

    /// <summary>
    /// BUG-03: authoritative UTC from a network source (HTTP Date header of the backend host in this stub), then advanced with a
    /// monotonic timer (Stopwatch), so changing the device clock has no effect. Until the first successful sync UtcNow throws
    /// and the backend reports "server time unavailable" (live-ops disabled, SG-20). A real backend returns its own time.
    /// </summary>
    public sealed class NetworkServerClock : ISyncableClock
    {
        readonly Func<System.Threading.Tasks.Task<DateTime?>> _fetch;
        readonly Func<double> _monoSeconds;
        DateTime _anchorUtc; double _anchorMono; bool _synced;
        static readonly System.Diagnostics.Stopwatch Sw = System.Diagnostics.Stopwatch.StartNew();

        public NetworkServerClock(Func<System.Threading.Tasks.Task<DateTime?>> fetch, Func<double> monoSeconds = null)
        { _fetch = fetch; _monoSeconds = monoSeconds ?? (() => Sw.Elapsed.TotalSeconds); }

        public bool Synced => _synced;
        public DateTime UtcNow
        {
            get
            {
                if (!_synced) throw new InvalidOperationException("server time not synced");
                return _anchorUtc.AddSeconds(_monoSeconds() - _anchorMono);
            }
        }

        public async System.Threading.Tasks.Task<bool> SyncAsync()
        {
            DateTime? t = null;
            try { t = await _fetch(); } catch (Exception) { t = null; }
            if (t == null) return _synced; // keep the previous anchor (monotonic) if we had one
            _anchorUtc = DateTime.SpecifyKind(t.Value, DateTimeKind.Utc); _anchorMono = _monoSeconds(); _synced = true;
            return true;
        }

        /// <summary>Default fetcher: HTTP Date header (1 s resolution) from a host we trust.</summary>
        public static Func<System.Threading.Tasks.Task<DateTime?>> HttpDate(string url, int timeoutMs = 4000) => async () =>
        {
            using (var c = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) })
            using (var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, url))
            using (var resp = await c.SendAsync(req))
                return resp.Headers.Date?.UtcDateTime;
        };
    }
    public sealed class FakeClock : IClock { public DateTime Now; public DateTime UtcNow => Now; public FakeClock(DateTime n) { Now = n; } }

    public static class UtcDay
    {
        /// <summary>Day key rolls over at 00:00 UTC (05:30 IST).</summary>
        public static string Key(DateTime utc) => utc.ToUniversalTime().ToString("yyyy-MM-dd");
        public static int Index(DateTime utc) => (int)(utc.ToUniversalTime().Date - new DateTime(1970, 1, 1)).TotalDays;
        public static DateTime NextReset(DateTime utc) => utc.ToUniversalTime().Date.AddDays(1);
    }
}
