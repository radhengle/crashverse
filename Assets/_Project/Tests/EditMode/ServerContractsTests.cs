using System.Collections.Generic;
using Crashverse.Backend;
using Crashverse.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Crashverse.Backend.Tests
{
    /// <summary>Wire contracts shared with the server (server/src links ServerContracts.cs and BackendModels.cs).</summary>
    public class ServerContractsTests
    {
        [TestCase("13to15", true)]
        [TestCase("16to17", true)]
        [TestCase("adult", false)]
        [TestCase("under13", false)]
        [TestCase("13to17", false)] // legacy band is not one of the four
        [TestCase(null, false)]
        [TestCase("", false)]
        public void AgeBands_IsMinor(string band, bool minor) => Assert.AreEqual(minor, AgeBands.IsMinor(band));

        [Test]
        public void AgeBands_AreTheFourServerStrings()
        {
            CollectionAssert.AreEqual(new[] { "under13", "13to15", "16to17", "adult" },
                new[] { AgeBands.Under13, AgeBands.Teen13to15, AgeBands.Teen16to17, AgeBands.Adult });
        }

        [Test]
        public void AuthProvider_SerializesAsName_NotInt()
        {
            Assert.AreEqual("\"Google\"", JsonConvert.SerializeObject(AuthProvider.Google));
            Assert.AreEqual("\"Facebook\"", JsonConvert.SerializeObject(AuthProvider.Facebook));
            Assert.AreEqual(AuthProvider.Email, JsonConvert.DeserializeObject<AuthProvider>("\"Email\""));
        }

        [Test]
        public void LinkCredential_WireShape()
        {
            var j = JObject.Parse(JsonConvert.SerializeObject(new LinkCredential { provider = AuthProvider.Google, token = "id-token", nonce = "n1" }));
            Assert.AreEqual("Google", (string)j["provider"]);
            Assert.AreEqual(JTokenType.String, j["provider"].Type);
            Assert.AreEqual("id-token", (string)j["token"]);
            Assert.AreEqual("n1", (string)j["nonce"]);
            foreach (var k in new[] { "codeVerifier", "redirectUri" }) Assert.IsTrue(j.ContainsKey(k), k);
            foreach (var k in new[] { "idToken", "accessToken", "authCode", "password" }) Assert.IsFalse(j.ContainsKey(k), k);
            var back = JsonConvert.DeserializeObject<LinkCredential>("{\"provider\":\"Facebook\",\"token\":\"t\",\"codeVerifier\":\"v\",\"redirectUri\":\"r\"}");
            Assert.AreEqual(AuthProvider.Facebook, back.provider); Assert.AreEqual("v", back.codeVerifier); Assert.AreEqual("r", back.redirectUri); Assert.IsNull(back.nonce);
        }

        [Test]
        public void ScoreSubmission_FixedPointInputs_RoundTrip()
        {
            var rec = new InputRecorder();
            rec.Record(3, "fire", 1.2345f, -0.5f);
            var e = rec.ToArray()[0];
            Assert.AreEqual(1235, e.x); Assert.AreEqual(-500, e.y); Assert.AreEqual(3, e.tick);
            var sub = new ScoreSubmission { attemptToken = "att", inputs = rec.ToArray(), clientScoreBp = 9000, buildHash = "h" };
            var j = JObject.Parse(JsonConvert.SerializeObject(sub));
            CollectionAssert.AreEquivalent(new[] { "attemptToken", "inputs", "clientScoreBp", "buildHash" }, ((IDictionary<string, JToken>)j).Keys);
            Assert.AreEqual(JTokenType.Integer, j["inputs"][0]["x"].Type);
            var back = JsonConvert.DeserializeObject<ScoreSubmission>(j.ToString());
            Assert.AreEqual(1235, back.inputs[0].x); Assert.AreEqual("fire", back.inputs[0].type);
        }

        [Test]
        public void WrappedLists_RoundTrip_JsonUtility_And_Newtonsoft()
        {
            // JsonUtility cannot serialise a top-level list, so list payloads travel inside wrapper objects.
            var w = new Wallet { coins = 5, shards = 2 }; w.cosmetics.Add("paint_red"); w.powerUps.Add("splitstrike");
            var wu = JsonUtility.FromJson<Wallet>(JsonUtility.ToJson(w));
            CollectionAssert.AreEqual(w.cosmetics, wu.cosmetics); CollectionAssert.AreEqual(w.powerUps, wu.powerUps); Assert.AreEqual(5, wu.coins);
            var wn = JsonConvert.DeserializeObject<Wallet>(JsonConvert.SerializeObject(w));
            CollectionAssert.AreEqual(w.cosmetics, wn.cosmetics);

            var page = new LeaderboardPage { periodKey = "2026-10-16", closed = true };
            page.top.Add(new LeaderboardEntry { rank = 1, playerId = "p1", score = 900 });
            page.top.Add(new LeaderboardEntry { rank = 2, playerId = "p2", score = 800 });
            var pj = JObject.Parse(JsonConvert.SerializeObject(page));
            Assert.AreEqual(JTokenType.Array, pj["top"].Type);
            var pb = JsonUtility.FromJson<LeaderboardPage>(JsonUtility.ToJson(page));
            Assert.AreEqual(2, pb.top.Count); Assert.AreEqual("p2", pb.top[1].playerId);

            var acc = new AccountState(); acc.identities.Add("Google");
            Assert.AreEqual("Google", JsonConvert.DeserializeObject<AccountState>(JsonConvert.SerializeObject(acc)).identities[0]);
        }

        [Test]
        public void TitanScoring_ClampsDamage_AndKillBonus()
        {
            var t = new TitanTemplates { maxDamageScore = 1000, perShotLeftBonus = 50 };
            Assert.AreEqual(1000, TitanScoring.Score(5000, 2, false, t));
            Assert.AreEqual(1100, TitanScoring.Score(5000, 2, true, t));
            Assert.AreEqual(0, TitanScoring.Score(-5, -1, true, t));
        }
    }
}
