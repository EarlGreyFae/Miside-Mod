using System;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiSideMod
{
    /// <summary>Per-frame hook for the mod. Also hosts the scene dump helper.</summary>
    public class ModBehaviour : MonoBehaviour
    {
        public ModBehaviour(IntPtr ptr) : base(ptr) { }

        private void Update()
        {
            if (Input.GetKeyDown(Plugin.DumpKey.Value))
                DumpScene();
        }

        private static void DumpScene()
        {
            var scene = SceneManager.GetActiveScene();
            var sb = new StringBuilder($"--- Scene '{scene.name}' ---\n");
            foreach (var root in scene.GetRootGameObjects())
                Walk(root.transform, 0, sb);
            Plugin.Log.LogInfo(sb.ToString());
        }

        private static void Walk(Transform t, int depth, StringBuilder sb)
        {
            sb.Append(' ', depth * 2).Append(t.name);
            var comps = t.gameObject.GetComponents<Component>();
            if (comps.Length > 1)
            {
                sb.Append("  [");
                for (int i = 0; i < comps.Length; i++)
                    if (comps[i] != null) sb.Append(i > 0 ? ", " : "").Append(comps[i].GetIl2CppType().Name);
                sb.Append(']');
            }
            sb.Append('\n');
            for (int i = 0; i < t.childCount; i++)
                Walk(t.GetChild(i), depth + 1, sb);
        }
    }
}
