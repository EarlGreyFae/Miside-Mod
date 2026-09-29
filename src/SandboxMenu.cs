using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MiSideMod
{
    /// <summary>
    /// In-game sandbox panel (F2), driven by the keyboard because the game hides/locks the mouse cursor:
    /// Up/Down select, Enter runs, Left/Right switch tabs. Mouse clicks also work if the cursor is free.
    /// Everything here reuses content the game already ships.
    /// </summary>
    internal static class SandboxMenu
    {
        internal static bool Open;
        internal static bool InfiniteEnergy;
        internal static bool FreeMinigames;

        private const int MaxEnergy = 30; // energy is re-rolled to 15-29 each cycle; 30 is a safe "full"
        private const float RowH = 26f;
        private static readonly string[] Tabs = { "Cheats", "Minigames", "Chibi", "Dialogues", "Quests", "Mod chats" };

        private class Item
        {
            public Func<string> Label;
            public Action Run;
        }

        private static readonly List<Item> Items = new List<Item>();
        private static int _tab, _sel;
        private static Vector2 _scroll;
        private static float _nextTick;
        private static bool _prevCursorVisible;
        private static CursorLockMode _prevLock;

        internal static void Toggle()
        {
            Open = !Open;
            if (Open)
            {
                _prevCursorVisible = Cursor.visible;
                _prevLock = Cursor.lockState;
                Refresh();
            }
            else
            {
                Cursor.visible = _prevCursorVisible;
                Cursor.lockState = _prevLock;
            }
        }

        private static string Label(Component c)
        {
            var p = c.transform.parent;
            return p != null ? $"{c.name}   ({p.name})" : c.name;
        }

        private static void Add(string label, Action run) => Items.Add(new Item { Label = () => label, Run = run });
        private static void Add(Func<string> label, Action run) => Items.Add(new Item { Label = label, Run = run });
        private static string OnOff(bool b) => b ? "ON" : "OFF";

        private static void Refresh()
        {
            Items.Clear();
            _sel = 0;
            _scroll = Vector2.zero;
            var main = UnityEngine.Object.FindObjectOfType<Tamagotchi_Main>();
            switch (_tab)
            {
                case 0:
                    Add("Coins +100", () => main.MoneyAdd(100));
                    Add("Coins +1000", () => main.MoneyAdd(1000));
                    Add("Refill energy", () => main.energy = MaxEnergy);
                    Add(() => $"Infinite energy: {OnOff(InfiniteEnergy)}", () => InfiniteEnergy = !InfiniteEnergy);
                    Add(() => $"Minigames cost no energy: {OnOff(FreeMinigames)}", () => FreeMinigames = !FreeMinigames);
                    Add(() => $"Stay in the phone world (block Day 37 jump): {OnOff(Plugin.BlockJump.Value)}", () =>
                    {
                        Plugin.BlockJump.Value = !Plugin.BlockJump.Value;
                        ModBehaviour.SetTransitionBlocked(Plugin.BlockJump.Value, false);
                    });
                    Add("Unlock every shop item", () =>
                    {
                        int n = 0;
                        foreach (var b in Resources.FindObjectsOfTypeAll<Tamagotchi_BuyCase>().Where(b => b.gameObject.scene.IsValid()))
                            if (b.close) { b.close = false; n++; }
                        Plugin.Log.LogInfo($"Sandbox: unlocked {n} shop item(s)");
                    });
                    Add("Stop current minigame", () => main.MiniGameStop());
                    Add("New cycle (re-roll energy, hunger, mood)", () => main.NewDay());
                    break;
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
                            Add(() => $"{q.name}  [{(q.activeSelf ? "active" : "off")}]", () => q.SetActive(true));
                        }
                    break;
            }
            if (_tab == 5)
            {
                Add("Play a random chat  (also F3)", () => { Toggle(); ModDialogue.PlayRandom(); });
                foreach (var f in ModDialogue.ScriptFiles())
                {
                    var path = f;
                    Add(System.IO.Path.GetFileNameWithoutExtension(path), () => { Toggle(); ModDialogue.Play(path); });
                }
            }
            if (_tab != 0 && _tab != 5) Items.Sort((a, b) => string.CompareOrdinal(a.Label(), b.Label()));
        }

        private static void Run(Item it)
        {
            try { it.Run(); }
            catch (Exception e) { Plugin.Log.LogWarning($"Sandbox action '{it.Label()}' failed: {e.Message}"); }
        }

        /// <summary>Called every frame from Update: keyboard control plus the always-on toggles.</summary>
        internal static void Tick()
        {
            if (Open && !ModDialogue.Active)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;

                if (Input.GetKeyDown(KeyCode.DownArrow)) _sel++;
                if (Input.GetKeyDown(KeyCode.UpArrow)) _sel--;
                if (Input.GetKeyDown(KeyCode.PageDown)) _sel += 8;
                if (Input.GetKeyDown(KeyCode.PageUp)) _sel -= 8;
                _sel = Mathf.Clamp(_sel, 0, Mathf.Max(0, Items.Count - 1));

                int tab = _tab;
                if (Input.GetKeyDown(KeyCode.RightArrow)) tab = (_tab + 1) % Tabs.Length;
                if (Input.GetKeyDown(KeyCode.LeftArrow)) tab = (_tab + Tabs.Length - 1) % Tabs.Length;
                if (tab != _tab) { _tab = tab; Refresh(); }

                if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && Items.Count > 0)
                    Run(Items[_sel]);
            }

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
            try
            {
                var win = new Rect(20, 20, 520, Mathf.Min(Screen.height - 40, 640));
                GUI.Box(win, "MiSide Sandbox   (F2 close)");
                GUI.Label(new Rect(win.x + 8, win.y + 22, win.width - 16, 22), "Up/Down select   Enter run   Left/Right tabs");

                for (int i = 0; i < Tabs.Length; i++)
                    if (GUI.Button(new Rect(win.x + 8 + i * 100, win.y + 46, 96, 24), (i == _tab ? "[ " + Tabs[i] + " ]" : Tabs[i])))
                    {
                        _tab = i;
                        Refresh();
                    }

                float top = win.y + 76;
                if (_tab == 5)
                {
                    GUI.Label(new Rect(win.x + 8, top, win.width - 16, 22), @"Scripts: BepInEx\config\MiSideMod\dialogues");
                    top += 24;
                }
                if (_tab == 0)
                {
                    var main = UnityEngine.Object.FindObjectOfType<Tamagotchi_Main>();
                    if (main != null)
                    {
                        GUI.Label(new Rect(win.x + 8, top, win.width - 16, 22), $"Coins: {main.money}    Energy: {main.energy}");
                        top += 24;
                    }
                }

                var view = new Rect(win.x + 8, top, win.width - 16, win.yMax - top - 8);
                if (Items.Count == 0)
                {
                    GUI.Label(new Rect(view.x, view.y, view.width, 24), "Nothing found. Load the Tamagotchi scene, then reopen (F2).");
                    return;
                }

                // keep the selected row in view
                if (_sel * RowH < _scroll.y) _scroll.y = _sel * RowH;
                if ((_sel + 1) * RowH > _scroll.y + view.height) _scroll.y = (_sel + 1) * RowH - view.height;

                var content = new Rect(0, 0, view.width - 20, Items.Count * RowH);
                _scroll = GUI.BeginScrollView(view, _scroll, content);
                for (int i = 0; i < Items.Count; i++)
                {
                    var r = new Rect(0, i * RowH, content.width, RowH - 2);
                    if (GUI.Button(r, (i == _sel ? ">  " : "    ") + Items[i].Label()))
                    {
                        _sel = i;
                        Run(Items[i]);
                    }
                }
                GUI.EndScrollView();
            }
            catch (Exception e) { Plugin.Log.LogWarning("Sandbox draw failed: " + e.Message); }
        }
    }
}
