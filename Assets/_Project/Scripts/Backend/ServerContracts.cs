using System;
using System.Collections.Generic;
using Crashverse.Data;

namespace Crashverse.Backend
{
    /// <summary>Shared (client + server) Titan scoring formula. Lives outside LocalBackendService so release builds keep it.</summary>
    public static class TitanScoring
    {
        public static int Score(int damageScore, int shotsLeft, bool killed, TitanTemplates t)
        {
            int d = Math.Max(0, Math.Min(t.maxDamageScore, damageScore));
            return d + (killed ? t.perShotLeftBonus * Math.Max(0, shotsLeft) : 0);
        }
    }

    public enum AuthProvider { Google, Apple, Email, X }

    /// <summary>Opaque provider credential (OAuth id token, or an email session token from AuthEmailLogin). Never a password.</summary>
    [Serializable] public class ProviderCredential { public AuthProvider provider; public string token; }

    /// <summary>Server session for email auth. The client keeps only this token in memory; passwords are never persisted.</summary>
    [Serializable] public class AuthSession { public string playerId; public string email; public string sessionToken; public long expiresUnixUtc; }

    /// <summary>Server-provided feature flags (Security Review v1: X login is gated here, never by a client constant).</summary>
    [Serializable] public class FeatureFlags { public bool xLoginEnabled; public bool emailAuthEnabled = true; public bool accountDeletionEnabled = true; }

    /// <summary>Per-attempt token (Security Review v1 §4): issued before a run, bound to level + seed, single use.</summary>
    [Serializable] public class AttemptToken { public string token; public string levelId; public int seed; public long issuedUnixUtc; public long expiresUnixUtc; }

    /// <summary>Input log for server-side replay/validation: aim + fire + power-up events with sim timestamps.</summary>
    [Serializable] public class InputEvent { public float t; public string kind; public float yaw, elevation; public string powerUp; }
    [Serializable] public class InputLog { public string attemptToken; public string levelId; public List<InputEvent> events = new List<InputEvent>(); public string clientVersion; }
    [Serializable] public class InputLogResponse { public bool accepted; public string message; }

    [Serializable] public class DeleteAccountResponse { public bool accepted; public string message; public long scheduledUnixUtc; }

    /// <summary>
    /// SERVER-ONLY. Purchases / premium pass entitlements are granted by the server after store receipt validation.
    /// No client implementation exists or may exist; the client only reads SeasonState.premium. (Security Review v1 §2.)
    /// </summary>
    public interface IServerEntitlements
    {
        void GrantPremiumPass(string playerId, string seasonId, string verifiedReceiptId);
        void GrantPurchase(string playerId, string sku, string verifiedReceiptId);
    }
}
