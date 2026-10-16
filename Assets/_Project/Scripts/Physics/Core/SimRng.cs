namespace Crashverse.Sim
{
    /// <summary>
    /// Deterministic RNG for the sim (xorshift64*), seeded from AttemptToken.seed (long). Pure C#, no UnityEngine, integer core:
    /// same seed gives the same sequence on every device and on the server. Never use UnityEngine.Random in sim code.
    /// </summary>
    public sealed class SimRng
    {
        const ulong ZeroSeedReplacement = 0x9E3779B97F4A7C15UL;
        ulong _state;

        public SimRng(long seed) { Reset(seed); }

        public void Reset(long seed)
        {
            // splitmix64 scramble so nearby seeds diverge; xorshift state must be non-zero.
            ulong z = unchecked((ulong)seed + 0x9E3779B97F4A7C15UL);
            z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
            z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
            z ^= z >> 31;
            _state = z == 0 ? ZeroSeedReplacement : z;
        }

        public ulong NextULong()
        {
            ulong x = _state;
            x ^= x >> 12; x ^= x << 25; x ^= x >> 27;
            _state = x;
            return unchecked(x * 0x2545F4914F6CDD1DUL);
        }

        public uint NextUInt() => (uint)(NextULong() >> 32);

        /// <summary>[0, maxExclusive), integer only. maxExclusive must be > 0.</summary>
        public int NextInt(int maxExclusive) => maxExclusive <= 0 ? 0 : (int)((NextULong() >> 33) % (ulong)maxExclusive);

        /// <summary>[minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive) => maxExclusive <= minInclusive ? minInclusive : minInclusive + NextInt(maxExclusive - minInclusive);

        /// <summary>[0,1) from 24 random bits (exactly representable in float).</summary>
        public float NextFloat01() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>[-1,1).</summary>
        public float NextSigned() => NextFloat01() * 2f - 1f;

        /// <summary>Independent child stream (e.g. debris vs spawn) so adding draws in one system can't shift another.</summary>
        public SimRng Fork(long salt) => new SimRng(unchecked((long)NextULong() ^ salt));
    }
}
