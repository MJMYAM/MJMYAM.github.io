using UnityEngine;

namespace PearlDiver
{
    /// <summary>
    /// Starts the prototype automatically when you press Play in any scene,
    /// so there is nothing to set up by hand in the Unity editor.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartGame()
        {
            if (Object.FindAnyObjectByType<PearlDiverGame>() != null) return;
            new GameObject("Pearl Diver").AddComponent<PearlDiverGame>();
        }
    }
}
