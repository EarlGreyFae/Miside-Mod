using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MiSideMod
{
    /// <summary>
    /// In-game sandbox panel (F2). Everything here reuses content the game already ships:
    /// its minigames, Chibi Mita animations, dialogues and quests, plus a few economy cheats.
    /// </summary>
    internal static class SandboxMenu
    {
        internal static bool Open;
        internal static bool InfiniteEnergy;
        internal static bool FreeMinigames;

        private const int MaxEnergy = 30; // energy is re-rolled to 15-29 each cycle; 30 is a safe "full"
        private static readonly string[] Tabs = { "Cheats", "Minigames", "Chibi", "Dialogues", "Quests" };
        private static readonly List<KeyValuePair<string, Action>> Items = new List<KeyValuePair<string, Action>>();
        private static int _tab;
        private static Vector2 _scroll;
        private static float _nextTick;

        internal static void Toggle()
        {
            Open = !Open;
            if (Open) Refresh();
        }

        private static string Label(Component c)
        {
            var p = c.transform.parent;
            return p != null ? $"{c.name}   ({p.name})" : c.name;
        }

        private static void Add(string label, Action a) => Items.Add(new KeyValuePair<string, Action>(label, a));

        private static void Refresh()
        {
            Items.Clear();
            var main = UnityEngine.Object.FindObjectOfType<Tamagotchi_Main>();
            switch (_tab)
            {
                case 1:
                    foreach (var g in Resources.FindObjectsOfTypeAll<Tamagotchi_MiniGame>().Where(x => x.gameObject.scene.IsValid()))
                    {
                        var game = g;
                        Add(game.name, () => main.MiniGamePlay(game));
                    }
                    break;
                case 2:
                    var chibi = UnityEngine.Object.FindObjectOfType<Mob_ChibiMita>();
                    foreach (var a in Resources.FindObjectsOfTypeAll<Mob_ChibiMita_Animation>().Where(x => x.gameObject.scene.IsValid()))
                    {
                        var anim = a;
                        Add(Label(anim), () => chibi.AnimationPlay(anim));
                    }
                    break;
                case 3:
                    foreach (var d in Resources.FindObjectsOfTypeAll<Tamagotchi_Dialogue_Mob>().Where(x => x.gameObject.scene.IsValid()))
                    {
                        var dlg = d;
                        Add(Label(dlg), () => dlg.StartDialogue());
                    }
                    break;
                case 4:
                    var quests = GameObject.Find("Quests");
                    if (quests != null)
                        for (int i = 0; i < quests.transform.childCount; i++)
                        {
                            var q = quests.transform.GetChild(i).gameObject;
                            Add($"{q.name}  [{(q.activeSelf ? "active" : "off")}]", () => q.SetActive(true));
                        }
                    break;
            }
            Items.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        }

        /// <summary>Called about once a second from Update for the always-on toggles.</summary>
        internal static void Tick()
        {
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + 1f;
            try
            {
                if (InfiniteEnergy)
                {
                    var main = UnityEngine.Object.FindObjectOfType<Tamagotchi_Main>();
                    if (main != null && main.energy < MaxEnergy) main.energy = MaxEnergy;
                }
                if (FreeMinigames)
                    foreach (var c in Resources.FindObjectsOfTypeAll<Tamagotchi_MiniGameCase>())
                        if (c.energy != 0) c.energy = 0;
            }
            catch (Exception e) { Plugin.Log.LogWarning("Sandbox tick failed: " + e.Message); }
        }

        internal static void Draw()
        {
            if (!Open) return;
            var win = new Rect(20, 20, 480, Mathf.Min(Screen.height - 40, 640));
            GUI.Box(win, "MiSide Sandbox   (F2 to close)");

            for (int i = 0; i < Tabs.Length; i++)
                if (GUI.Button(new Rect(win.x + 8 + i * 92, win.y + 26, 88, 24), Tabs[i]))
                {
                    _tab = i;
                    _scroll = Vector2.zero;
                    Refresh();
                }

            float top = win.y + 58;
            try
            {
                if (_tab == 0) DrawCheats(win.x + 8, top, win.width - 16);
                else DrawList(new Rect(win.x + 8, top, win.width - 16, win.yMax - top - 8));
            }
            catch (Exception e) { Plugin.Log.LogWarning("Sandbox draw failed: " + e.Message); }
        }

        private static void DrawCheats(float x, float y, float w)
        {
            var main = UnityEngine.Object.FindObjectOfType<Tamagotchi_Main>();
            if (main == null) { GUI.Label(new Rect(x, y, w, 24), "Tamagotchi_Main not found (load the Tamagotchi scene)."); return; }

            GUI.Label(new Rect(x, y, w, 24), $"Coins: {main.money}    Energy: {main.energy}");
            y += 30;
            if (GUI.Button(new Rect(x, y, w / 2 - 2, 26), "+100 coins")) main.MoneyAdd(100);
            if (GUI.Button(new Rect(x + w / 2 + 2, y, w / 2 - 2, 26), "+1000 coins")) main.MoneyAdd(1000);
            y += 32;
            if (GUI.Button(new Rect(x, y, w, 26), "Refill energy")) main.energy = MaxEnergy;
            y += 32;
            InfiniteEnergy = GUI.Toggle(new Rect(x, y, w, 24), InfiniteEnergy, "Infinite energy");
            y += 28;
            FreeMinigames = GUI.Toggle(new Rect(x, y, w, 24), FreeMinigames, "Minigames cost no energy");
            y += 28;
            bool block = GUI.Toggle(new Rect(x, y, w, 24), Plugin.BlockJump.Value, "Stay in the phone world (block the Day 37 jump)");
            if (block != Plugin.BlockJump.Value)
            {
                Plugin.BlockJump.Value = block;
                ModBehaviour.SetTransitionBlocked(block);
            }
            y += 34;
            if (GUI.Button(new Rect(x, y, w, 26), "Unlock every shop item"))
            {
                int n = 0;
                foreach (var b in Resources.FindObjectsOfTypeAll<Tamagotchi_BuyCase>().Where(b => b.gameObject.scene.IsValid()))
                    if (b.close) { b.close = false; n++; }
                Plugin.Log.LogInfo($"Sandbox: unlocked {n} shop item(s)");
            }
            y += 32;
            if (GUI.Button(new Rect(x, y, w, 26), "Stop current minigame")) main.MiniGameStop();
            y += 32;
            if (GUI.Button(new Rect(x, y, w, 26), "New cycle (re-roll energy, hunger, mood)")) main.NewDay();
        }

        private static void DrawList(Rect view)
        {
            if (Items.Count == 0)
            {
                GUI.Label(new Rect(view.x, view.y, view.width, 24), "Nothing found. Load the Tamagotchi scene, then reopen.");
                return;
            }
            var content = new Rect(0, 0, view.width - 20, Items.Count * 26);
            _scroll = GUI.BeginScrollView(view, _scroll, content);
            for (int i = 0; i < Items.Count; i++)
                if (GUI.Button(new Rect(0, i * 26, content.width, 24), Items[i].Key))
                {
                    try { Items[i].Value(); }
                    catch (Exception e) { Plugin.Log.LogWarning($"Sandbox action '{Items[i].Key}' failed: {e.Message}"); }
                }
            GUI.EndScrollView();
        }
    }
}
