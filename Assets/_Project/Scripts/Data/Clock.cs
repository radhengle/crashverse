using System;

namespace Crashverse.Data
{
    public interface IClock { DateTime UtcNow { get; } }
    public sealed class SystemClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
    public sealed class FakeClock : IClock { public DateTime Now; public DateTime UtcNow => Now; public FakeClock(DateTime n) { Now = n; } }

    public static class UtcDay
    {
        /// <summary>Day key rolls over at 00:00 UTC (05:30 IST).</summary>
        public static string Key(DateTime utc) => utc.ToUniversalTime().ToString("yyyy-MM-dd");
        public static int Index(DateTime utc) => (int)(utc.ToUniversalTime().Date - new DateTime(1970, 1, 1)).TotalDays;
        public static DateTime NextReset(DateTime utc) => utc.ToUniversalTime().Date.AddDays(1);
    }
}
