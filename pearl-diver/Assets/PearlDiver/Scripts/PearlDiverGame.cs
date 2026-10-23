using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PearlDiver
{
    /// <summary>
    /// Pearl Diver prototype ("grey-box"): one script that builds the scene from
    /// simple shapes and runs the whole core loop:
    /// hold to dive -> collect pearls -> surface for air -> dive ends ->
    /// collect pearls -> buy upgrades -> dive again.
    /// </summary>
    public class PearlDiverGame : MonoBehaviour
    {
        enum State { Ready, Diving, Results }
        enum Kind { Pearl, Jelly, Shark }

        class Entity
        {
            public Transform tr;
            public Kind kind;
            public int value;
            public bool rare;
            public float baseY;
            public float phase;
        }

        class Bubble
        {
            public Transform tr;
            public float life;
        }

        struct Popup
        {
            public Vector3 world;
            public string text;
            public float life;
            public Color color;
        }

        // Zones from the game design document
        static readonly float[] ZoneStart = { 0f, 10f, 25f, 45f };
        static readonly string[] ZoneName = { "Shallow Lagoon", "Coral Garden", "Sunken Dhow", "Deep Blue" };
        static readonly Color[] ZoneColor =
        {
            new Color(0.13f, 0.62f, 0.72f),
            new Color(0.07f, 0.45f, 0.66f),
            new Color(0.04f, 0.27f, 0.50f),
            new Color(0.02f, 0.08f, 0.20f),
        };
        static readonly Color SkyColor = new Color(0.66f, 0.88f, 1f);
        static readonly Color Gold = new Color(1f, 0.79f, 0.29f);

        const float DiverX = -1.2f;
        const float SurfaceY = -0.3f;

        Tuning tune;
        Camera cam;
        Transform diver, finA, finB, boat;
        readonly List<Entity> entities = new List<Entity>();
        readonly List<Bubble> bubbles = new List<Bubble>();
        readonly List<Popup> popups = new List<Popup>();
        readonly List<Transform> decor = new List<Transform>();
        readonly List<Transform> dust = new List<Transform>();
        Material pearlWhite, pearlPink, pearlGold, pearlBlack, jellyMat, tentacleMat, sharkMat, eyeMat, bubbleMat;

        State state = State.Ready;
        float y = SurfaceY, vy, oxygen, t, camY;
        float pearlTimer, jellyTimer, sharkTimer, safeTimer, bubbleTimer, flash, lastPickTime = -10f;
        int divePearls, combo, bestCombo, lastZone = -1;
        float deepest;
        bool blackSpawned, continueUsed, banked;
        bool sharkWarning;
        float sharkWarnTimer, sharkWarnY;
        string endReason = "";
        string toast = "";
        float toastTimer;

        // Saved progress
        int totalPearls, tankLevel, finsLevel;
        float bestDepth;

        // GUI
        GUIStyle label, labelBig, labelSmall, labelCenter, button;
        float guiScale = 1f;

        float Depth => -y;
        float MaxOxygen => tune.baseOxygen + tankLevel * tune.oxygenPerTankLevel;
        float FinsMul => 1f + finsLevel * tune.finsSpeedPerLevel;

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            tune = gameObject.AddComponent<Tuning>();
            Load();
            SetupCameraAndLight();
            BuildMaterials();
            BuildWorld();
            BuildDiver();
            ResetDive();
            camY = SurfaceY - 1f;
        }

        void SetupCameraAndLight()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.orthographic = false;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.transform.rotation = Quaternion.identity;

            bool hasLight = false;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) hasLight = true;
            if (!hasLight)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.6f, 0.7f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
        }

        void BuildMaterials()
        {
            pearlWhite = Shapes.Mat(Color.white, 0.45f, 0.9f);
            pearlPink = Shapes.Mat(new Color(1f, 0.62f, 0.82f), 0.45f, 0.9f);
            pearlGold = Shapes.Mat(Gold, 0.8f, 0.9f);
            pearlBlack = Shapes.Mat(new Color(0.12f, 0.08f, 0.2f), 0f, 0.95f);
            pearlBlack.EnableKeyword("_EMISSION");
            if (pearlBlack.HasProperty("_EmissionColor")) pearlBlack.SetColor("_EmissionColor", new Color(0.35f, 0.1f, 0.55f));
            jellyMat = Shapes.Mat(new Color(1f, 0.5f, 0.78f), 0.5f);
            tentacleMat = Shapes.Mat(new Color(1f, 0.72f, 0.88f), 0.3f);
            sharkMat = Shapes.Mat(new Color(0.45f, 0.52f, 0.6f));
            eyeMat = Shapes.Mat(new Color(0.05f, 0.05f, 0.08f));
            bubbleMat = Shapes.Mat(new Color(0.9f, 0.98f, 1f), 0.6f, 0.9f);
        }

        void BuildWorld()
        {
            var world = Shapes.Group("World");
            Shapes.Make(PrimitiveType.Cube, world, new Vector3(0f, 20f, 12f), new Vector3(200f, 40f, 1f), Shapes.Mat(SkyColor, 0.8f));
            Shapes.Make(PrimitiveType.Cube, world, new Vector3(0f, 0f, 1f), new Vector3(200f, 0.14f, 8f), Shapes.Mat(new Color(0.9f, 0.99f, 1f), 0.3f));
            Shapes.Make(PrimitiveType.Cube, world, new Vector3(0f, -tune.seabedDepth - 0.5f, 1f), new Vector3(200f, 1f, 10f), Shapes.Mat(new Color(0.86f, 0.76f, 0.54f)));

            // The dhow: home base at the surface
            boat = Shapes.Group("Dhow", world);
            boat.position = new Vector3(-2.6f, 0.3f, 0.5f);
            var wood = Shapes.Mat(new Color(0.54f, 0.35f, 0.2f));
            Shapes.Make(PrimitiveType.Cube, boat, Vector3.zero, new Vector3(2.4f, 0.55f, 1f), wood);
            Shapes.Make(PrimitiveType.Cube, boat, new Vector3(1.15f, 0.2f, 0f), new Vector3(0.6f, 0.5f, 0.9f), wood, new Vector3(0f, 0f, 30f));
            Shapes.Make(PrimitiveType.Cylinder, boat, new Vector3(0f, 1.2f, 0f), new Vector3(0.08f, 0.95f, 0.08f), Shapes.Mat(new Color(0.36f, 0.23f, 0.12f)));
            Shapes.Make(PrimitiveType.Cube, boat, new Vector3(0.35f, 1.3f, -0.1f), new Vector3(1.1f, 1.3f, 0.04f), Shapes.Mat(new Color(0.98f, 0.96f, 0.9f)), new Vector3(0f, 0f, -12f));

            // Rocks and sea grass on the seabed
            var rock = Shapes.Mat(new Color(0.44f, 0.5f, 0.56f));
            var weed = Shapes.Mat(new Color(0.18f, 0.62f, 0.36f));
            for (int i = 0; i < 14; i++)
            {
                float x = -10f + i * 1.5f;
                Transform d = (i % 2 == 0)
                    ? Shapes.Make(PrimitiveType.Sphere, world, new Vector3(x, -tune.seabedDepth + 0.1f, Random.Range(-0.5f, 1.5f)), new Vector3(Random.Range(0.6f, 1.2f), 0.5f, 0.8f), rock)
                    : Shapes.Make(PrimitiveType.Cylinder, world, new Vector3(x, -tune.seabedDepth + 0.8f, Random.Range(-0.5f, 1.5f)), new Vector3(0.1f, Random.Range(0.6f, 1.1f), 0.1f), weed);
                decor.Add(d);
            }

            // Floating specks that show movement in open water
            var speck = Shapes.Mat(new Color(0.8f, 0.95f, 1f), 0.4f);
            for (int i = 0; i < 70; i++)
            {
                var s = Shapes.Make(PrimitiveType.Sphere, world,
                    new Vector3(Random.Range(-9f, 9f), Random.Range(-10f, 0f), Random.Range(-3f, 4f)),
                    Vector3.one * Random.Range(0.03f, 0.07f), speck);
                dust.Add(s);
            }
        }

        void BuildDiver()
        {
            diver = Shapes.Group("Diver");
            var suit = Shapes.Mat(new Color(1f, 0.54f, 0.24f));
            Shapes.Make(PrimitiveType.Capsule, diver, Vector3.zero, new Vector3(0.5f, 0.55f, 0.5f), suit, new Vector3(0f, 0f, 90f));
            Shapes.Make(PrimitiveType.Sphere, diver, new Vector3(0.72f, 0f, 0f), Vector3.one * 0.56f, Shapes.Mat(new Color(0.94f, 0.77f, 0.61f)));
            Shapes.Make(PrimitiveType.Cube, diver, new Vector3(0.97f, 0.05f, 0f), new Vector3(0.12f, 0.22f, 0.42f), Shapes.Mat(new Color(0.44f, 0.9f, 1f), 0.4f, 0.95f));
            Shapes.Make(PrimitiveType.Cylinder, diver, new Vector3(0.05f, 0.32f, 0f), new Vector3(0.24f, 0.36f, 0.24f), Shapes.Mat(new Color(0.79f, 0.83f, 0.86f), 0f, 0.7f), new Vector3(0f, 0f, 90f));
            var finMat = Shapes.Mat(new Color(1f, 0.82f, 0.24f));
            finA = Shapes.Make(PrimitiveType.Cube, diver, new Vector3(-0.85f, 0.06f, 0.12f), new Vector3(0.55f, 0.05f, 0.22f), finMat);
            finB = Shapes.Make(PrimitiveType.Cube, diver, new Vector3(-0.85f, -0.06f, -0.12f), new Vector3(0.55f, 0.05f, 0.22f), finMat);
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            t += dt;
            HandleTestingHelpers();
            bool held = Held();

            switch (state)
            {
                case State.Ready:
                    y = SurfaceY + Mathf.Sin(t * 2f) * 0.05f;
                    vy = 0f;
                    if (held) state = State.Diving;
                    break;
                case State.Diving:
                    UpdateDive(dt, held);
                    break;
            }

            UpdateEffects(dt, held);
            UpdateCamera(dt);
        }

        void UpdateDive(float dt, bool held)
        {
            float maxV = tune.maxVerticalSpeed * FinsMul;
            vy += (held ? -tune.diveForce * FinsMul : tune.floatForce) * dt;
            vy *= Mathf.Max(0f, 1f - tune.waterDrag * dt);
            vy = Mathf.Clamp(vy, -maxV, maxV);
            y += vy * dt;
            float floor = -tune.seabedDepth + 0.5f;
            if (y < floor) { y = floor; if (vy < 0f) vy = 0f; }
            if (y > SurfaceY) { y = SurfaceY; if (vy > 0f) vy = 0f; }
            deepest = Mathf.Max(deepest, Depth);

            // Oxygen
            bool underwater = y < SurfaceY - 0.3f;
            if (underwater)
                oxygen -= Mathf.Lerp(tune.drainNearSurface, tune.drainAtSeabed, Depth / tune.seabedDepth) * dt;
            else
                oxygen = Mathf.Min(MaxOxygen, oxygen + tune.refillPerSecond * dt);
            if (oxygen <= 0f) { oxygen = 0f; EndDive("Out of air!"); return; }

            int zone = ZoneIndex(Depth);
            if (zone != lastZone)
            {
                if (zone > lastZone && lastZone >= 0) ShowToast(ZoneName[zone] + "  +" + Mathf.RoundToInt(zone * tune.depthBonusPerZone * 100f) + "% pearls");
                lastZone = zone;
            }
            float scroll = tune.scrollSpeed * (1f + tune.scrollSpeedPerZone * zone);

            // Spawning
            pearlTimer -= dt;
            jellyTimer -= dt;
            if (pearlTimer <= 0f) { SpawnPearl(); pearlTimer = Random.Range(tune.pearlInterval.x, tune.pearlInterval.y); }
            if (jellyTimer <= 0f) { SpawnJelly(); jellyTimer = Random.Range(tune.jellyInterval.x, tune.jellyInterval.y); }
            if (Depth >= tune.sharkMinDepth - 5f && !sharkWarning)
            {
                sharkTimer -= dt;
                if (sharkTimer <= 0f)
                {
                    sharkWarning = true;
                    sharkWarnTimer = tune.sharkWarningTime;
                    sharkWarnY = Mathf.Clamp(y + Random.Range(-1.5f, 1.5f), floor, -tune.sharkMinDepth);
                    sharkTimer = Random.Range(tune.sharkInterval.x, tune.sharkInterval.y);
                }
            }
            if (sharkWarning)
            {
                sharkWarnTimer -= dt;
                if (sharkWarnTimer <= 0f) { sharkWarning = false; SpawnShark(sharkWarnY); }
            }

            // Move things and check what the diver touches
            safeTimer -= dt;
            float hx = DiverX + 0.45f;
            for (int i = entities.Count - 1; i >= 0; i--)
            {
                var e = entities[i];
                var p = e.tr.position;
                float speed = e.kind == Kind.Shark ? tune.sharkSpeed : e.kind == Kind.Jelly ? scroll * 0.85f : scroll;
                p.x -= speed * dt;
                if (e.kind == Kind.Jelly)
                {
                    p.y = e.baseY + Mathf.Sin(t * 2f + e.phase) * 0.45f;
                    float s = 1f + Mathf.Sin(t * 5f + e.phase) * 0.08f;
                    e.tr.GetChild(0).localScale = new Vector3(0.9f * s, 0.55f / s, 0.9f * s);
                }
                else if (e.kind == Kind.Pearl)
                {
                    e.tr.Rotate(0f, 90f * dt, 0f);
                }
                e.tr.position = p;

                float dx = p.x - hx, dy = p.y - y;
                if (e.kind == Kind.Pearl && dx * dx + dy * dy < tune.collectRadius * tune.collectRadius)
                {
                    Collect(e);
                    RemoveAt(i);
                    continue;
                }
                if (e.kind == Kind.Jelly && safeTimer <= 0f && dx * dx + (dy - 0.1f) * (dy - 0.1f) < 0.64f)
                {
                    Sting();
                }
                if (e.kind == Kind.Shark && Mathf.Abs(dx) < 1.3f && Mathf.Abs(dy) < 0.55f)
                {
                    EndDive("A shark came too close!");
                    return;
                }
                if (p.x < -10f) RemoveAt(i);
            }

            foreach (var d in decor)
            {
                var p = d.position;
                p.x -= scroll * 0.9f * dt;
                if (p.x < -11f) p.x += 21f;
                d.position = p;
            }
        }

        void UpdateEffects(float dt, bool held)
        {
            diver.position = new Vector3(DiverX, y, 0f);
            float tilt = Mathf.Clamp(vy * 7f, -tune.maxTiltDegrees, tune.maxTiltDegrees);
            diver.rotation = Quaternion.Euler(0f, 0f, tilt);
            float flap = Mathf.Sin(t * (held ? 16f : 8f)) * 25f;
            finA.localEulerAngles = new Vector3(0f, flap, 0f);
            finB.localEulerAngles = new Vector3(0f, -flap, 0f);
            bool visible = safeTimer <= 0f || ((int)(t * 12f)) % 2 == 0;
            if (diver.gameObject.activeSelf != visible) diver.gameObject.SetActive(visible);
            boat.position = new Vector3(-2.6f, 0.3f + Mathf.Sin(t * 1.6f) * 0.06f, 0.5f);
            boat.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.3f) * 3f);

            // Bubbles from the mask
            bool underwater = y < SurfaceY - 0.3f;
            bubbleTimer -= dt;
            if (state == State.Diving && underwater && bubbleTimer <= 0f)
            {
                var b = Shapes.Make(PrimitiveType.Sphere, null, new Vector3(DiverX + 0.95f, y + 0.2f, -0.2f), Vector3.one * 0.12f, bubbleMat);
                bubbles.Add(new Bubble { tr = b });
                bubbleTimer = Random.Range(0.18f, 0.35f);
            }
            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                var b = bubbles[i];
                b.life += dt;
                var p = b.tr.position;
                p.y += 1.6f * dt;
                p.x -= (state == State.Diving ? tune.scrollSpeed * 0.4f : 0f) * dt;
                p.x += Mathf.Sin(t * 6f + i) * 0.004f;
                b.tr.position = p;
                b.tr.localScale = Vector3.one * Mathf.Lerp(0.12f, 0.04f, b.life / 2f);
                if (b.life > 2f || p.y > -0.1f)
                {
                    Destroy(b.tr.gameObject);
                    bubbles.RemoveAt(i);
                }
            }

            // Specks drift past and wrap around the camera
            float drift = state == State.Diving ? tune.scrollSpeed * 0.5f : 0.1f;
            foreach (var s in dust)
            {
                var p = s.position;
                p.x -= drift * dt;
                if (p.x < -9f) p.x += 18f;
                if (p.y > camY + 8f) p.y -= 16f;
                if (p.y < camY - 8f) p.y += 16f;
                s.position = p;
                bool show = p.y < -0.3f;
                if (s.gameObject.activeSelf != show) s.gameObject.SetActive(show);
            }

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var pp = popups[i];
                pp.life -= dt;
                pp.world.y += 0.9f * dt;
                if (pp.life <= 0f) popups.RemoveAt(i); else popups[i] = pp;
            }
            flash = Mathf.Max(0f, flash - dt * 1.5f);
            toastTimer -= dt;
        }

        void UpdateCamera(float dt)
        {
            float target = Mathf.Clamp(y - 1f, -tune.seabedDepth + 5f, SurfaceY - 1f);
            camY = Mathf.Lerp(camY, target, 1f - Mathf.Exp(-5f * dt));
            cam.transform.position = new Vector3(0f, camY, -14f);

            float d = Mathf.Clamp01(-camY / tune.seabedDepth);
            Color water = WaterColorAt(-camY);
            cam.backgroundColor = water;
            RenderSettings.fogColor = water;
            RenderSettings.fogStartDistance = Mathf.Lerp(30f, 6f, d);
            RenderSettings.fogEndDistance = Mathf.Lerp(70f, 26f, d);
        }

        // ------------------------------------------------------------------ gameplay

        void SpawnPearl()
        {
            float floor = -tune.seabedDepth + 0.6f;
            float py = Mathf.Clamp(y + Random.Range(-5.5f, 5.5f), floor, -1f);
            float depth = -py;
            int zone = ZoneIndex(depth);

            int baseValue = 1;
            Material mat = pearlWhite;
            bool rare = false;
            float roll = Random.value;
            if (depth > 45f && !blackSpawned && roll < 0.02f) { baseValue = 100; mat = pearlBlack; rare = true; blackSpawned = true; }
            else if (depth > 25f && roll < 0.08f) { baseValue = 25; mat = pearlGold; rare = true; }
            else if (depth > 10f && roll < 0.35f) { baseValue = 5; mat = pearlPink; }

            var tr = Shapes.Group("Pearl");
            tr.position = new Vector3(7f, py, 0f);
            Shapes.Make(PrimitiveType.Sphere, tr, Vector3.zero, Vector3.one * (rare ? 0.5f : 0.42f), mat);
            int value = Mathf.Max(1, Mathf.RoundToInt(baseValue * (1f + tune.depthBonusPerZone * zone)));
            entities.Add(new Entity { tr = tr, kind = Kind.Pearl, value = value, rare = rare });
        }

        void SpawnJelly()
        {
            float floor = -tune.seabedDepth + 1.2f;
            float jy = Mathf.Clamp(y + Random.Range(-4.5f, 4.5f), floor, -1.5f);
            var tr = Shapes.Group("Jellyfish");
            tr.position = new Vector3(7.5f, jy, 0f);
            Shapes.Make(PrimitiveType.Sphere, tr, Vector3.zero, new Vector3(0.9f, 0.55f, 0.9f), jellyMat);
            for (int i = 0; i < 4; i++)
                Shapes.Make(PrimitiveType.Cylinder, tr, new Vector3(-0.27f + i * 0.18f, -0.5f, 0f), new Vector3(0.05f, 0.4f, 0.05f), tentacleMat);
            entities.Add(new Entity { tr = tr, kind = Kind.Jelly, baseY = jy, phase = Random.Range(0f, 6f) });
        }

        void SpawnShark(float sy)
        {
            var tr = Shapes.Group("Shark");
            tr.position = new Vector3(10f, sy, 0f);
            Shapes.Make(PrimitiveType.Capsule, tr, Vector3.zero, new Vector3(0.8f, 1.7f, 0.6f), sharkMat, new Vector3(0f, 0f, 90f));
            Shapes.Make(PrimitiveType.Cube, tr, new Vector3(0.1f, 0.55f, 0f), new Vector3(0.35f, 0.55f, 0.08f), sharkMat, new Vector3(0f, 0f, -25f));
            Shapes.Make(PrimitiveType.Cube, tr, new Vector3(1.75f, 0.1f, 0f), new Vector3(0.15f, 0.8f, 0.1f), sharkMat, new Vector3(0f, 0f, 15f));
            Shapes.Make(PrimitiveType.Sphere, tr, new Vector3(-1.2f, 0.12f, -0.3f), Vector3.one * 0.1f, eyeMat);
            entities.Add(new Entity { tr = tr, kind = Kind.Shark });
        }

        void Collect(Entity e)
        {
            combo = (t - lastPickTime <= tune.comboWindow) ? Mathf.Min(combo + 1, tune.maxCombo) : 1;
            lastPickTime = t;
            bestCombo = Mathf.Max(bestCombo, combo);
            int gain = e.value * combo;
            divePearls += gain;
            string text = "+" + gain + (combo > 1 ? "  x" + combo : "");
            popups.Add(new Popup { world = e.tr.position, text = text, life = 0.9f, color = e.rare ? Gold : Color.white });
            if (e.value >= 100) ShowToast("BLACK PEARL!");
            else if (e.rare) ShowToast("Golden pearl!");
        }

        void Sting()
        {
            oxygen = Mathf.Max(1f, oxygen - tune.jellyStingOxygen);
            safeTimer = tune.stingSafeTime;
            flash = 0.35f;
            popups.Add(new Popup { world = new Vector3(DiverX, y + 0.6f, 0f), text = "Ouch!  -" + Mathf.RoundToInt(tune.jellyStingOxygen) + " air", life = 1f, color = new Color(1f, 0.45f, 0.45f) });
        }

        void EndDive(string reason)
        {
            state = State.Results;
            endReason = reason;
            banked = false;
            sharkWarning = false;
            bestDepth = Mathf.Max(bestDepth, deepest);
        }

        void ContinueWithAd()
        {
            // Placeholder: the real game shows a reward video here.
            continueUsed = true;
            oxygen = MaxOxygen * 0.5f;
            safeTimer = tune.stingSafeTime;
            state = State.Diving;
        }

        void Bank(int multiplier)
        {
            totalPearls += divePearls * multiplier;
            banked = true;
            Save();
        }

        void ResetDive()
        {
            foreach (var e in entities) Destroy(e.tr.gameObject);
            entities.Clear();
            foreach (var b in bubbles) Destroy(b.tr.gameObject);
            bubbles.Clear();
            popups.Clear();
            y = SurfaceY; vy = 0f;
            oxygen = MaxOxygen;
            divePearls = 0; combo = 0; bestCombo = 0; deepest = 0f; lastZone = -1;
            blackSpawned = false; continueUsed = false; sharkWarning = false;
            pearlTimer = 0.3f; jellyTimer = 1.5f; sharkTimer = 2f; safeTimer = 0f;
            state = State.Ready;
        }

        void RemoveAt(int i)
        {
            Destroy(entities[i].tr.gameObject);
            entities.RemoveAt(i);
        }

        int UpgradeCost(int level) => Mathf.RoundToInt(tune.upgradeBaseCost * Mathf.Pow(tune.upgradeCostGrowth, level));

        static int ZoneIndex(float depth)
        {
            int z = 0;
            for (int i = 0; i < ZoneStart.Length; i++) if (depth >= ZoneStart[i]) z = i;
            return z;
        }

        Color WaterColorAt(float depth)
        {
            if (depth <= ZoneStart[0]) return ZoneColor[0];
            for (int i = 0; i < ZoneStart.Length - 1; i++)
                if (depth < ZoneStart[i + 1])
                    return Color.Lerp(ZoneColor[i], ZoneColor[i + 1], (depth - ZoneStart[i]) / (ZoneStart[i + 1] - ZoneStart[i]));
            return ZoneColor[ZoneColor.Length - 1];
        }

        void ShowToast(string text)
        {
            toast = text;
            toastTimer = 2f;
        }

        static bool Held()
        {
#if ENABLE_INPUT_SYSTEM
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.isPressed) return true;
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.spaceKey.isPressed;
#else
            return Input.GetMouseButton(0) || Input.touchCount > 0 || Input.GetKey(KeyCode.Space);
#endif
        }

        // ------------------------------------------------------------------ save

        void Load()
        {
            totalPearls = PlayerPrefs.GetInt("pd_total", 0);
            tankLevel = PlayerPrefs.GetInt("pd_tank", 0);
            finsLevel = PlayerPrefs.GetInt("pd_fins", 0);
            bestDepth = PlayerPrefs.GetFloat("pd_best_depth", 0f);
        }

        void Save()
        {
            PlayerPrefs.SetInt("pd_total", totalPearls);
            PlayerPrefs.SetInt("pd_tank", tankLevel);
            PlayerPrefs.SetInt("pd_fins", finsLevel);
            PlayerPrefs.SetFloat("pd_best_depth", bestDepth);
            PlayerPrefs.Save();
        }

        void HandleTestingHelpers()
        {
            if (tune.resetSave)
            {
                tune.resetSave = false;
                totalPearls = 0; tankLevel = 0; finsLevel = 0; bestDepth = 0f;
                Save();
                ResetDive();
                ShowToast("Progress reset");
            }
            if (tune.add1000Pearls)
            {
                tune.add1000Pearls = false;
                totalPearls += 1000;
                Save();
                ShowToast("+1,000 pearls for testing");
            }
        }

        // ------------------------------------------------------------------ screen (prototype UI)

        void EnsureStyles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold };
            label.normal.textColor = Color.white;
            labelBig = new GUIStyle(label) { fontSize = 48, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            labelSmall = new GUIStyle(label) { fontSize = 26, fontStyle = FontStyle.Normal };
            labelCenter = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            button = new GUIStyle(GUI.skin.button) { fontSize = 30, fontStyle = FontStyle.Bold, wordWrap = true };
        }

        void Box(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void OnGUI()
        {
            EnsureStyles();
            guiScale = Screen.height / 1280f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(guiScale, guiScale, 1f));
            float W = Screen.width / guiScale;
            const float H = 1280f;
            var panel = new Color(0.03f, 0.08f, 0.16f, 0.6f);

            if (flash > 0f) Box(new Rect(0, 0, W, H), new Color(1f, 0.2f, 0.2f, flash));

            // Pearls this dive + combo
            Box(new Rect(20, 20, 250, 60), panel);
            GUI.Label(new Rect(36, 28, 240, 50), "Pearls  " + divePearls, label);
            if (state == State.Diving && combo > 1 && t - lastPickTime <= tune.comboWindow)
            {
                var c = new GUIStyle(label);
                c.normal.textColor = Gold;
                GUI.Label(new Rect(284, 28, 120, 50), "x" + combo, c);
            }

            // Oxygen bar
            float frac = Mathf.Clamp01(oxygen / MaxOxygen);
            GUI.Label(new Rect(W - 330, 18, 300, 40), "Oxygen", labelSmall);
            Box(new Rect(W - 330, 56, 300, 26), new Color(1f, 1f, 1f, 0.25f));
            Box(new Rect(W - 330, 56, 300 * frac, 26), frac < 0.3f ? new Color(1f, 0.35f, 0.35f) : new Color(0.36f, 0.88f, 1f));

            // Depth and zone
            GUI.Label(new Rect(W - 330, 92, 300, 44), Mathf.RoundToInt(Depth) + " m", label);
            GUI.Label(new Rect(W - 330, 134, 320, 36), ZoneName[ZoneIndex(Depth)], labelSmall);

            // Floating "+5" texts
            foreach (var p in popups)
            {
                Vector3 sp = cam.WorldToScreenPoint(p.world);
                if (sp.z < 0f) continue;
                var st = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
                st.normal.textColor = new Color(p.color.r, p.color.g, p.color.b, Mathf.Clamp01(p.life * 2f));
                GUI.Label(new Rect(sp.x / guiScale - 150, (Screen.height - sp.y) / guiScale - 25, 300, 50), p.text, st);
            }

            // Shark warning on the right edge
            if (sharkWarning && Mathf.Repeat(t * 6f, 1f) < 0.6f)
            {
                Vector3 sp = cam.WorldToScreenPoint(new Vector3(6f, sharkWarnY, 0f));
                var warn = new GUIStyle(labelBig);
                warn.normal.textColor = new Color(1f, 0.3f, 0.3f);
                GUI.Label(new Rect(W - 110, (Screen.height - sp.y) / guiScale - 50, 90, 100), "!", warn);
            }

            if (toastTimer > 0f)
            {
                Box(new Rect(W / 2 - 260, 200, 520, 70), new Color(1f, 0.79f, 0.29f, 0.95f));
                var ts = new GUIStyle(labelCenter);
                ts.normal.textColor = new Color(0.16f, 0.1f, 0f);
                GUI.Label(new Rect(W / 2 - 260, 200, 520, 70), toast, ts);
            }

            if (state == State.Ready)
            {
                GUI.Label(new Rect(0, H - 300, W, 70), "Hold anywhere to dive", labelBig);
                GUI.Label(new Rect(0, H - 220, W, 50), "Let go to float up. Surface to refill oxygen.", labelCenter);
                GUI.Label(new Rect(0, H - 160, W, 50), "Saved pearls: " + totalPearls + "    Best depth: " + Mathf.RoundToInt(bestDepth) + " m", labelCenter);
            }

            if (state == State.Results) DrawResults(W);
        }

        void DrawResults(float W)
        {
            float pw = Mathf.Min(640f, W - 40f);
            float x = W / 2 - pw / 2;
            float top = 260f;
            Box(new Rect(x, top, pw, 760), new Color(0.03f, 0.08f, 0.16f, 0.88f));

            GUI.Label(new Rect(x, top + 20, pw, 70), endReason, labelBig);
            GUI.Label(new Rect(x, top + 100, pw, 44), "Pearls this dive: " + divePearls, labelCenter);
            GUI.Label(new Rect(x, top + 144, pw, 44), "Deepest: " + Mathf.RoundToInt(deepest) + " m    Best combo: x" + Mathf.Max(1, bestCombo), labelCenter);

            float bx = x + 30, bw = pw - 60, by = top + 220;
            if (!banked)
            {
                if (!continueUsed && endReason == "Out of air!")
                {
                    if (GUI.Button(new Rect(bx, by, bw, 90), "Watch ad: keep diving with 50% air\n(placeholder, no real ad)", button)) ContinueWithAd();
                    by += 110;
                }
                if (GUI.Button(new Rect(bx, by, bw, 90), "Watch ad: double to " + divePearls * 2 + " pearls\n(placeholder, no real ad)", button)) Bank(2);
                by += 110;
                if (GUI.Button(new Rect(bx, by, bw, 80), "Collect " + divePearls + " pearls", button)) Bank(1);
                return;
            }

            GUI.Label(new Rect(x, by - 10, pw, 44), "Saved pearls: " + totalPearls, labelCenter);
            by += 50;

            // Upgrade: oxygen tank
            int tankCost = UpgradeCost(tankLevel);
            bool tankMax = tankLevel >= tune.maxUpgradeLevel;
            GUI.enabled = !tankMax && totalPearls >= tankCost;
            string tankText = tankMax ? "Oxygen tank: MAX" : "Oxygen tank  Lv " + (tankLevel + 1) + " -> " + (tankLevel + 2) + "\n+" + Mathf.RoundToInt(tune.oxygenPerTankLevel) + " air   (" + tankCost + " pearls)";
            if (GUI.Button(new Rect(bx, by, bw, 100), tankText, button))
            {
                totalPearls -= tankCost; tankLevel++; Save();
            }
            by += 120;

            // Upgrade: fins
            int finsCost = UpgradeCost(finsLevel);
            bool finsMax = finsLevel >= tune.maxUpgradeLevel;
            GUI.enabled = !finsMax && totalPearls >= finsCost;
            string finsText = finsMax ? "Fins: MAX" : "Fins  Lv " + (finsLevel + 1) + " -> " + (finsLevel + 2) + "\n+" + Mathf.RoundToInt(tune.finsSpeedPerLevel * 100f) + "% speed   (" + finsCost + " pearls)";
            if (GUI.Button(new Rect(bx, by, bw, 100), finsText, button))
            {
                totalPearls -= finsCost; finsLevel++; Save();
            }
            by += 130;

            GUI.enabled = true;
            if (GUI.Button(new Rect(bx, by, bw, 90), "Dive again", button)) ResetDive();
        }
    }
}
