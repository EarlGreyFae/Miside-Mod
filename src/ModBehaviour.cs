using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
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

            if (Input.GetKeyDown(KeyCode.F12))
                ProbeDays();

            // Test keys: F10 = +100 coins, F11 = Tamagotchi_Main.NewDay (re-rolls energy, not the story day)
            if (Input.GetKeyDown(KeyCode.F10) || Input.GetKeyDown(KeyCode.F11))
            {
                var main = UnityEngine.Object.FindObjectOfType<Tamagotchi_Main>();
                if (main == null) { Plugin.Log.LogWarning("Tamagotchi_Main not found in this scene"); return; }
                if (Input.GetKeyDown(KeyCode.F10)) main.MoneyAdd(100);
                else main.NewDay();
            }
        }

        private static string PathOf(Transform t)
        {
            var s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        private static string Listeners(UnityEvent e)
        {
            if (e == null) return "-";
            var sb = new StringBuilder();
            try
            {
                for (int i = 0; i < e.GetPersistentEventCount(); i++)
                {
                    var tgt = e.GetPersistentTarget(i);
                    sb.Append(i > 0 ? "; " : "").Append(tgt != null ? tgt.name : "null")
                      .Append('.').Append(e.GetPersistentMethodName(i));
                }
            }
            catch (Exception ex) { sb.Append("<error ").Append(ex.Message).Append('>'); }
            return sb.Length == 0 ? "(none)" : sb.ToString();
        }

        /// <summary>Read-only snapshot: scenes, Scene_Load components, active quest, money/energy.</summary>
        private static void ProbeDays()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Plugin.Log.LogInfo($"Loaded scene {i}: {SceneManager.GetSceneAt(i).name}");

            foreach (var l in Resources.FindObjectsOfTypeAll<Scene_Load>())
                Plugin.Log.LogInfo($"  Scene_Load {PathOf(l.transform)} active={l.gameObject.activeInHierarchy} " +
                                   $"load='{l.nameSceneLoad}' unload='{l.nameSceneUnload}' continue='{l.nameSceneContinue}' loading={l.loading}");

            var main = UnityEngine.Object.FindObjectOfType<Tamagotchi_Main>();
            if (main != null)
                Plugin.Log.LogInfo($"  Tamagotchi_Main money={main.money} energy={main.energy} room={main.indexRoomShow}");

            var loc1 = UnityEngine.Object.FindObjectOfType<Location1Main>();
            if (loc1 != null)
                Plugin.Log.LogInfo($"  Location1Main canBuyTelevision={loc1.canBuyTelevision} buyTV->{Listeners(loc1.eventBuyTelevision)}");

            foreach (var b in Resources.FindObjectsOfTypeAll<Tamagotchi_BuyCase>())
                Plugin.Log.LogInfo($"  Shop '{PathOf(b.transform)}' price={b.money} closed={b.close} buy->{Listeners(b.eventBuy)}");

            var quests = GameObject.Find("Quests");
            if (quests != null)
                for (int i = 0; i < quests.transform.childCount; i++)
                {
                    var c = quests.transform.GetChild(i);
                    Plugin.Log.LogInfo($"  Quest '{c.name}' active={c.gameObject.activeSelf}");
                }
        }

        private static bool Wanted(string n)
        {
            string[] keys = { "Tamagotchi", "Chibi", "RealRoom", "Location1", "Global", "Save", "Time_",
                              "Events_Data", "Scene_Load", "Day", "Quest", "Progress", "Story" };
            return keys.Any(k => n.Contains(k));
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
            foreach (var t in types.Where(t => Wanted(t.Name))
                                   .OrderBy(t => t.Name))
            {
                sb.Append("\n== ").Append(t.FullName).Append(" : ").Append(t.BaseType?.Name).Append('\n');
                foreach (var f in t.GetFields(all).Where(f => !f.Name.StartsWith("NativeFieldInfoPtr")
                                                            && !f.Name.StartsWith("NativeMethodInfoPtr")))
                    sb.Append("  field  ").Append(f.FieldType.Name).Append(' ').Append(f.Name).Append('\n');
                foreach (var p in t.GetProperties(all))
                {
                    sb.Append("  prop   ").Append(p.PropertyType.Name).Append(' ').Append(p.Name);
                    // Show current values of static properties (globals such as the day counter)
                    if (p.GetGetMethod(true)?.IsStatic == true && p.GetIndexParameters().Length == 0)
                    {
                        try { sb.Append(" = ").Append(p.GetValue(null)); } catch { sb.Append(" = <error>"); }
                    }
                    sb.Append('\n');
                }
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
