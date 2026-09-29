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

        private void OnGUI()
        {
            SandboxMenu.Draw();
            ModDialogue.Draw();
        }

        private void Update()
        {
            if (Input.GetKeyDown(Plugin.DumpKey.Value))
                DumpScene();
            if (Input.GetKeyDown(Plugin.TypesKey.Value))
                DumpTypes();

            if (Input.GetKeyDown(KeyCode.F12))
                ProbeDays();

            if (Input.GetKeyDown(KeyCode.F2))
                SandboxMenu.Toggle();
            SandboxMenu.Tick();
            ModDialogue.Tick();
            FreePlay.Tick();
            if (Input.GetKeyDown(KeyCode.F3) && !ModDialogue.Active)
                ModDialogue.PlayRandom();

            // F5 = debug: switch on the final quest (Day 37) to test the ending / the transition block
            if (Input.GetKeyDown(KeyCode.F5))
            {
                var quests = GameObject.Find("Quests");
                Transform q10 = null;
                if (quests != null)
                    for (int i = 0; i < quests.transform.childCount; i++)
                        if (quests.transform.GetChild(i).name.StartsWith("Quest 10")) q10 = quests.transform.GetChild(i);
                if (q10 == null) Plugin.Log.LogWarning("Quest 10 not found");
                else { q10.gameObject.SetActive(true); Plugin.Log.LogInfo("Activated " + q10.name); }
            }

            // F6 toggles the block on the Day 37 jump into Mita's world (saved per session in the config)
            if (Input.GetKeyDown(KeyCode.F6))
            {
                Plugin.BlockJump.Value = !Plugin.BlockJump.Value;
                SetTransitionBlocked(Plugin.BlockJump.Value, false);
            }
            // If the config says to block the jump, keep retrying until the Tamagotchi scene has loaded
            // and the event exists (the scene is not there yet while the title/menu is showing).
            if (Plugin.BlockJump.Value && !_blockApplied && Time.time > _nextBlockTry)
            {
                _nextBlockTry = Time.time + 3f;
                _blockApplied = SetTransitionBlocked(true, true) > 0;
            }

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

        private static readonly string[] LoadMethods = { "GoScene", "StartLoad", "GoSceneAfterPause", "SaveGame" };

        private static void ReportEvent(string owner, string prop, UnityEvent ev)
        {
            var l = Listeners(ev);
            if (LoadMethods.Any(m => l.Contains("." + m)))
                Plugin.Log.LogInfo($"  >>> LOAD TRIGGER: {owner}.{prop} -> {l}");
        }

        /// <summary>Looks at one object's UnityEvent properties, and one level into arrays of game objects.</summary>
        private static void ScanEvents(string owner, object obj, Action<string, string, UnityEvent> visit, bool intoArrays = true)
        {
            if (obj == null) return;
            foreach (var p in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    if (p.GetIndexParameters().Length > 0) continue;
                    if (p.PropertyType == typeof(UnityEvent))
                        visit(owner, p.Name, (UnityEvent)p.GetValue(obj));
                    else if (intoArrays && p.PropertyType.Name.StartsWith("Il2CppReferenceArray"))
                    {
                        int i = 0;
                        if (p.GetValue(obj) is System.Collections.IEnumerable en)
                            foreach (var el in en) ScanEvents($"{owner}.{p.Name}[{i++}]", el, visit, false);
                    }
                }
                catch { /* ignore properties that cannot be read */ }
            }
        }

        private static void ScanAll<T>(Action<string, string, UnityEvent> visit) where T : Component
        {
            foreach (var c in Resources.FindObjectsOfTypeAll<T>())
                ScanEvents(PathOf(c.transform), c, visit);
        }

        private static void VisitAllEvents(Action<string, string, UnityEvent> visit)
        {
            ScanAll<Events_Data>(visit);
            ScanAll<Time_Events>(visit);
            ScanAll<Tamagotchi_BuyCase>(visit);
            ScanAll<Tamagotchi_MiniGame>(visit);
            ScanAll<Tamagotchi_Dialogue_Mob>(visit);
            ScanAll<Location1Main>(visit);
            ScanAll<Scene_Load>(visit);
        }

        /// <summary>
        /// The pull into Mita's world is a saved event on the last quest (Day 37 "Wait" dialogue):
        /// TamagotchiHouse.SetActive, GameStop, ..., World.GoScene. Switching every call in that event
        /// off (a runtime Unity feature, no hooking) keeps the player in the minigame.
        /// </summary>
        internal static int SetTransitionBlocked(bool block, bool quiet)
        {
            int found = 0;
            VisitAllEvents((owner, prop, ev) =>
            {
                try
                {
                    int n = ev.GetPersistentEventCount();
                    bool jump = false;
                    for (int i = 0; i < n; i++)
                        if (ev.GetPersistentMethodName(i) == "GoScene") { jump = true; break; }
                    if (!jump) return;
                    found++;
                    for (int i = 0; i < n; i++)
                        ev.SetPersistentListenerState(i, block ? UnityEventCallState.Off : UnityEventCallState.RuntimeOnly);
                    Plugin.Log.LogInfo($"  {(block ? "Disabled" : "Restored")} {n} calls on {owner}.{prop}");
                }
                catch (Exception e) { Plugin.Log.LogWarning($"  transition toggle failed on {owner}: {e.Message}"); }
            });
            if (!quiet || found > 0)
                Plugin.Log.LogInfo($"Realm transition {(block ? "BLOCKED" : "restored")} ({found} event(s) found)");
            return found;
        }

        private bool _blockApplied;
        private float _nextBlockTry;

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
            {
                var names = new StringBuilder();
                try
                {
                    foreach (var t in b.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                        if (!string.IsNullOrWhiteSpace(t.text)) names.Append('[').Append(t.text.Replace("\n", " ")).Append(']');
                }
                catch { }
                Plugin.Log.LogInfo($"  Shop '{PathOf(b.transform)}' price={b.money} closed={b.close} texts={names} buy->{Listeners(b.eventBuy)}");
            }

            Plugin.Log.LogInfo("  Scanning events for scene-load triggers...");
            VisitAllEvents(ReportEvent);
            Plugin.Log.LogInfo("  Scan done.");

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
