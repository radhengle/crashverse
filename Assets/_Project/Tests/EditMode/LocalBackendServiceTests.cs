#if CRASHVERSE_DEV || (UNITY_EDITOR && UNITY_INCLUDE_TESTS) // LocalBackendService is dev/editor-only (release guard B1)
using System;
using System.IO;
using Crashverse.Backend;
using Crashverse.Data;
using NUnit.Framework;

namespace Crashverse.Backend.Tests
{
    /// <summary>Basic flows of the dev/editor-only LocalBackendService.</summary>
    public class LocalBackendServiceTests
    {
        static ContentDatabase _db;
        string _dir;

        static DateTime U(int y, int m, int d) => new DateTime(y, m, d, 12, 0, 0, DateTimeKind.Utc);

        LocalBackendService New(string file = "db.json")
        {
            _db ??= ContentDatabase.LoadBundled();
            return new LocalBackendService(_db, new FakeClock(U(2030, 1, 1)), Path.Combine(_dir, file));
        }

        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "cv_editmode_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

        [Test]
        public void NewInstall_IsGuest_AndPersistsAcrossRestarts()
        {
            var a = New().GetAccount().Result;
            StringAssert.StartsWith("guest_", a.playerId);
            Assert.IsFalse(a.linked);
            Assert.AreEqual(a.playerId, New().GetAccount().Result.playerId); // same file → same guest
        }

        [Test]
        public void TestClock_FreezeAndAdvance()
        {
            var b = New();
            b.SetTestClock(U(2026, 10, 16));
            Assert.AreEqual(U(2026, 10, 16), b.ServerNow);
            b.AdvanceTestClock(TimeSpan.FromHours(3));
            Assert.AreEqual(U(2026, 10, 16).AddHours(3), b.ServerNow);
            b.ClearTestClock(); Assert.IsFalse(b.TestClockActive);
        }

        [Test]
        public void AgeGate_AdultAccepted_Under13Blocked_BadDobRejected()
        {
            var b = New(); b.SetTestClock(U(2026, 10, 16));
            var r = b.SubmitAgeGate(1990, 1, "US").Result;
            Assert.AreEqual(AgeBands.Adult, r.band); Assert.IsFalse(r.requiresParentalConsent);
            var ex = Assert.Throws<BackendException>(() => b.SubmitAgeGate(2020, 1, "US"));
            StringAssert.Contains("AGE_BLOCKED", ex.Message);
            Assert.Throws<BackendException>(() => b.SubmitAgeGate(2000, 13, "US"));
        }

        [Test]
        public void LinkAccount_RequiresToken_AndRecordsProvider()
        {
            var b = New(); b.SetTestClock(U(2026, 10, 16)); b.SubmitAgeGate(1990, 1, "US").Wait();
            Assert.Throws<BackendException>(() => b.LinkAccount(new LinkCredential { provider = AuthProvider.Google, token = "" }));
            var a = b.LinkAccount(new LinkCredential { provider = AuthProvider.Google, token = "tok" }).Result;
            Assert.IsTrue(a.linked); CollectionAssert.Contains(a.identities, "Google");
            Assert.Throws<BackendException>(() => b.LinkAccount(new LinkCredential { provider = AuthProvider.X, token = "tok" })); // X gated by server flag
        }

        [Test]
        public void EmailAuth_RegisterThenLogin_PasswordNeverPersisted()
        {
            var b = New(); b.SetTestClock(U(2026, 10, 16));
            Assert.Throws<BackendException>(() => b.AuthEmailRegister("a@b.co", "short"));
            var r = b.AuthEmailRegister("A@B.co", "longenough1").Result;
            Assert.IsNotEmpty(r.accessToken);
            Assert.IsNotNull(b.AuthEmailLogin("a@b.co", "anything8").Result.account);
            Assert.Throws<BackendException>(() => b.AuthEmailLogin("x@b.co", "anything8"));
            StringAssert.DoesNotContain("longenough1", File.ReadAllText(Path.Combine(_dir, "db.json")));
        }

        [Test]
        public void Offline_ThrowsForServerCalls()
        {
            var b = New(); b.SimulateOffline = true;
            Assert.Throws<BackendException>(() => b.GetAccount());
            Assert.Throws<BackendException>(() => b.GetWallet());
            b.SimulateOffline = false; Assert.IsNotNull(b.GetWallet().Result);
        }

        [Test]
        public void BeginAttempt_IssuesTokenBoundToLevel()
        {
            var b = New(); b.SetTestClock(U(2026, 10, 16));
            var t = b.BeginAttempt("c1_l01").Result;
            StringAssert.StartsWith("att_", t.token); Assert.AreEqual("c1_l01", t.levelId);
            Assert.Greater(t.expiresUnixUtc, t.issuedUnixUtc);
            Assert.AreNotEqual(t.token, b.BeginAttempt("c1_l01").Result.token);
        }
    }
}
#endif
