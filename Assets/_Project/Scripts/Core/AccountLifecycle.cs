using System;
using System.Threading.Tasks;
using Crashverse.Backend;
using Crashverse.Meta;
using Crashverse.UI;

namespace Crashverse.Core
{
    /// <summary>Account-state hooks run whenever the backend is reachable (launch/resume): §E3, §A5, §A6, §C2.</summary>
    public static class AccountLifecycle
    {
        /// <summary>Provider re-auth hook (A3/F1): returns a fresh provider token or email-OTP token. Wired to provider SDKs later.
        /// TODO(Dev): Google/Apple/X SDK re-auth and email OTP UI. Dev builds return a fake token.</summary>
        public static Func<AuthProvider, Task<string>> ReauthProvider = p =>
#if CRASHVERSE_DEV
            Task.FromResult("dev-reauth-" + p);
#else
            Task.FromResult<string>(null);
#endif

        public static async Task OnConnected()
        {
            var b = AppServices.Backend; var r = AppServices.Router;
            try
            {
                var band = AgeGateScreen.Current;
                if (band != AgeGate.Band.Unknown) await b.SetAgeGate(AgeGate.ToServer(band), AgeGateScreen.Country()); // E3
                if (AccountDeletion.Pending)
                {
                    var res = await AccountDeletion.Flush(b); // A6: queued offline request goes out now
                    if (res == AccountDeletion.Result.Scheduled) { GameBootstrap.RestartFirstRun(); r?.Toast("Your account deletion request was sent.", 4f); return; }
                    if (res == AccountDeletion.Result.ReauthRequired) r?.Toast("Sign in again to finish deleting your account.", 4f);
                }
                var acc = AppServices.Account = await b.GetAccount();
                if (acc.pendingDeletionUnixUtc > 0) // A5: signing in during grace offers cancel
                    r?.Push(new ConfirmModal("Account scheduled for deletion",
                        $"Your account will be permanently deleted on {DateTimeOffset.FromUnixTimeSeconds(acc.pendingDeletionUnixUtc):d MMM yyyy} (UTC). Cancel the deletion to keep it.",
                        "Cancel deletion", async () => { AppServices.Account = await b.CancelAccountDeletion(); AccountDeletion.Cancel(); r.Toast("Deletion cancelled."); }));
                else if (acc.deletionWarningAtUnixUtc > 0) // C2
                    r?.Push(new ConfirmModal("Inactive guest account",
                        $"Guest accounts are deleted after 365 days without play. This account will be deleted on {DateTimeOffset.FromUnixTimeSeconds(acc.guestPurgeAtUnixUtc):d MMM yyyy} (UTC).",
                        "Keep my account", async () => { AppServices.Account = await b.KeepAccountActive(); }));
            }
            catch (BackendException) { /* offline: try again on next connection */ }
        }
    }
}
