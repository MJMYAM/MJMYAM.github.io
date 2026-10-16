using UnityEngine;

namespace PearlDiver
{
    /// <summary>
    /// Every number that controls how the game feels. While the game is playing,
    /// select the "Pearl Diver" object in the Hierarchy and change these values
    /// in the Inspector to feel the difference immediately.
    /// Note: changes made in Play mode are lost when you press Stop.
    /// </summary>
    public class Tuning : MonoBehaviour
    {
        [Header("Diver movement")]
        public float diveForce = 10f;
        public float floatForce = 5.5f;
        public float maxVerticalSpeed = 4.5f;
        public float waterDrag = 1.6f;
        public float maxTiltDegrees = 30f;

        [Header("World")]
        public float scrollSpeed = 2.0f;
        [Tooltip("Extra scroll speed per zone, 0.05 = +5%")]
        public float scrollSpeedPerZone = 0.05f;
        public float seabedDepth = 70f;

        [Header("Oxygen")]
        public float baseOxygen = 100f;
        public float oxygenPerTankLevel = 15f;
        public float drainNearSurface = 7f;
        public float drainAtSeabed = 10f;
        public float refillPerSecond = 45f;
        public float jellyStingOxygen = 20f;
        public float stingSafeTime = 1.2f;

        [Header("Spawning (seconds between spawns)")]
        public Vector2 pearlInterval = new Vector2(0.6f, 1.1f);
        public Vector2 jellyInterval = new Vector2(1.6f, 2.6f);
        public Vector2 sharkInterval = new Vector2(6f, 10f);
        public float sharkMinDepth = 45f;
        public float sharkWarningTime = 1f;
        public float sharkSpeed = 7f;

        [Header("Pearls")]
        public float collectRadius = 0.6f;
        public float comboWindow = 1.5f;
        public int maxCombo = 3;
        [Tooltip("Extra pearl value per zone, 0.25 = +25%")]
        public float depthBonusPerZone = 0.25f;

        [Header("Upgrades")]
        public int upgradeBaseCost = 50;
        public float upgradeCostGrowth = 1.6f;
        [Tooltip("Extra dive speed per fins level, 0.04 = +4%")]
        public float finsSpeedPerLevel = 0.04f;
        public int maxUpgradeLevel = 10;

        [Header("Testing helpers")]
        [Tooltip("Tick to wipe all saved progress")]
        public bool resetSave;
        [Tooltip("Tick to add 1,000 pearls for testing upgrades")]
        public bool add1000Pearls;
    }
}
