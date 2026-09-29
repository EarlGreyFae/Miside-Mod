using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
            if (Input.GetKeyDown(Plugin.TypesKey.Value))
                DumpTypes();
        }

        /// <summary>Lists fields/properties/methods of the minigame classes to a text file.</summary>
        private static void DumpTypes()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (asm == null) { Plugin.Log.LogWarning("Assembly-CSharp not found"); return; }

            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }

            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var sb = new StringBuilder();
            foreach (var t in types.Where(t => t.Name.Contains("Tamagotchi") || t.Name.Contains("Chibi"))
                                   .OrderBy(t => t.Name))
            {
                sb.Append("\n== ").Append(t.FullName).Append(" : ").Append(t.BaseType?.Name).Append('\n');
                foreach (var f in t.GetFields(all))
                    sb.Append("  field  ").Append(f.FieldType.Name).Append(' ').Append(f.Name).Append('\n');
                foreach (var p in t.GetProperties(all))
                    sb.Append("  prop   ").Append(p.PropertyType.Name).Append(' ').Append(p.Name).Append('\n');
                foreach (var m in t.GetMethods(all).Where(m => !m.IsSpecialName))
                    sb.Append("  method ").Append(m.ReturnType.Name).Append(' ').Append(m.Name).Append('(')
                      .Append(string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name + " " + x.Name)))
                      .Append(")\n");
            }
            var path = Path.Combine(BepInEx.Paths.BepInExRootPath, "MiSideMod_types.txt");
            File.WriteAllText(path, sb.ToString());
            Plugin.Log.LogInfo("Wrote " + path);
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
