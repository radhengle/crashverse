using Crashverse.Destruction;
using Crashverse.Level;
using Crashverse.Perf;
using Crashverse.UI;
using Crashverse.Weapons;
using UnityEngine;

namespace Crashverse.Core
{
    /// <summary>
    /// Minimal bootstrap — builds arena, Wrecker, Clockwork Tower, HUD, session.
    /// No hand-authored scene binary required beyond an empty Bootstrap scene with this component.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        void Awake()
        {
            Application.targetFrameRate = 60;
            QualityController.ApplyMid();

            var arenaGo = new GameObject("Arena");
            var arena = arenaGo.AddComponent<ArenaBuilder>();
            arena.Build();

            // Debris pool
            var debrisGo = new GameObject("DebrisPool");
            debrisGo.AddComponent<DebrisPool>();

            // Wrecker at x=-20
            var wreckerGo = new GameObject("Wrecker");
            wreckerGo.transform.position = new Vector3(-20f, 0f, 0f);
            var wrecker = wreckerGo.AddComponent<WreckerBuilder>();
            wrecker.Build();

            var aim = wreckerGo.AddComponent<AimController>();
            aim.Bind(wrecker.yawPivot, wrecker.pitchPivot, wrecker.projectileSpawn);

            var fire = wreckerGo.AddComponent<FireController>();
            fire.aim = aim;

            // Level / tower ~30u away → x≈10
            var levelGo = new GameObject("Level");
            var builder = levelGo.AddComponent<LevelBuilder>();
            var tracker = levelGo.AddComponent<DestructionTracker>();
            var integrity = levelGo.AddComponent<StructuralIntegrity>();

            var sessionGo = new GameObject("GameSession");
            var session = sessionGo.AddComponent<GameSession>();
            session.tracker = tracker;
            session.fire = fire;
            session.builder = builder;
            session.integrity = integrity;

            var cfg = LevelConfig.CreateM1ClockworkTower();
            Vector3 towerBase = new Vector3(10f, 0f, 0f); // 30u from wrecker at -20
            session.Begin(cfg, towerBase);

            // Camera orbit pivot = midpoint
            var cam = Camera.main;
            if (cam != null)
            {
                var orbit = cam.gameObject.AddComponent<OrbitCamera>();
                orbit.target = builder.structureRoot != null ? builder.structureRoot : levelGo.transform;
                orbit.pivotOffset = new Vector3(0f, 5f, 0f);
                orbit.distance = 24f;
            }

            // HUD
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<HudController>();
            hud.Bind(session, fire);

            var qualityGo = new GameObject("Quality");
            qualityGo.AddComponent<QualityController>();
        }
    }
}
