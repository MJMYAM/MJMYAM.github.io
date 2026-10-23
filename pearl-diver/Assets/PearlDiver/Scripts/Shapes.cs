using UnityEngine;
using UnityEngine.Rendering;

namespace PearlDiver
{
    /// <summary>
    /// Builds simple coloured 3D shapes. The prototype uses these instead of
    /// real 3D models; they get replaced by proper art in production.
    /// Works with both the Universal (URP) and the Built-in render pipeline.
    /// </summary>
    public static class Shapes
    {
        static Shader shader;
        static bool urp;

        static void Init()
        {
            if (shader != null) return;
            urp = GraphicsSettings.currentRenderPipeline != null;
            shader = urp ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");
        }

        public static Material Mat(Color color, float glow = 0f, float smoothness = 0.3f)
        {
            Init();
            var m = new Material(shader);
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (glow > 0f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * glow);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        public static Transform Make(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale,
                                     Material mat, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>()); // the game checks distances itself
            var tr = go.transform;
            tr.SetParent(parent, false);
            tr.localPosition = localPos;
            tr.localScale = scale;
            tr.localEulerAngles = euler;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return tr;
        }

        public static Transform Group(string name, Transform parent = null)
        {
            var tr = new GameObject(name).transform;
            if (parent != null) tr.SetParent(parent, false);
            return tr;
        }
    }
}
