using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MiSideMod
{
    /// <summary>
    /// Plays mod-written dialogue scripts (BepInEx/config/MiSideMod/dialogues/*.txt) in a text box.
    /// Format: see docs/dialogue-format.md. Enter/Space advances, Escape cancels.
    /// </summary>
    internal static class ModDialogue
    {
        internal static bool Active;

        private enum Kind { Say, Anim, Coins }

        private class Step
        {
            public Kind K;
            public string Speaker = "", Text = "", Arg = "";
            public int N;
        }

        private static List<Step> _steps = new List<Step>();
        private static int _i;
        private static string _speaker = "", _full = "";
        private static float _shown;
        private static int _startFrame;
        private static GUIStyle _nameStyle, _textStyle, _hintStyle;
        private static readonly System.Random Rng = new System.Random();

        internal static string Folder => Path.Combine(BepInEx.Paths.ConfigPath, "MiSideMod", "dialogues");

        private static readonly Dictionary<string, string> Samples = new Dictionary<string, string>
        {
            ["still_here.txt"] =
                "# Plays well after Day 37, when you stay in the phone world.\n" +
                "@anim Hello\n" +
                "Mita: Oh! You're still here.\n" +
                "Mita: I thought the ending would... I don't know. Take you somewhere.\n" +
                "@anim Idle\n" +
                "Mita: But you're right here, [player], and honestly? I like that a lot.\n" +
                "Mita: So! No more waiting. What do you want to do today?\n",
            ["snack_time.txt"] =
                "@anim Hungry\n" +
                "Mita: [player]... my stomach is making that noise again.\n" +
                "Mita: Do you think we could do a snack run? Nothing fancy.\n" +
                "@anim Jump\n" +
                "Mita: Salad! No, soup! No... both!\n" +
                "Mita: You pick. I trust your taste.\n",
            ["game_night.txt"] =
                "@anim Jump\n" +
                "Mita: Game night! I've been saving my high score all week.\n" +
                "Mita: Here, some pocket money for the arcade.\n" +
                "@coins 50\n" +
                "Mita: Don't spend it all on snacks. ...Okay, spend some of it on snacks.\n",
        };

        internal static void EnsureSamples()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                if (Directory.EnumerateFiles(Folder, "*.txt").Any()) return;
                foreach (var kv in Samples)
                    File.WriteAllText(Path.Combine(Folder, kv.Key), kv.Value, new UTF8Encoding(false));
            }
            catch (Exception e) { Plugin.Log.LogWarning("Could not prepare dialogue folder: " + e.Message); }
        }

        internal static List<string> ScriptFiles()
        {
            EnsureSamples();
            try { return Directory.EnumerateFiles(Folder, "*.txt").OrderBy(x => x).ToList(); }
            catch { return new List<string>(); }
        }

        internal static void PlayRandom()
        {
            var files = ScriptFiles();
            if (files.Count > 0) Play(files[Rng.Next(files.Count)]);
        }

        internal static void Play(string path)
        {
            if (Active) return;
            try
            {
                _steps = Parse(File.ReadAllLines(path, Encoding.UTF8));
                _i = 0;
                _startFrame = Time.frameCount;
                Active = true;
                Advance();
                Plugin.Log.LogInfo($"Playing dialogue script '{Path.GetFileName(path)}' ({_steps.Count} steps)");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"Could not play '{path}': {e.Message}"); Active = false; }
        }

        private static List<Step> Parse(string[] lines)
        {
            var steps = new List<Step>();
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//")) continue;

                if (line.StartsWith("@"))
                {
                    var parts = line.Substring(1).Split(new[] { ' ' }, 2);
                    var cmd = parts[0].ToLowerInvariant();
                    var arg = parts.Length > 1 ? parts[1].Trim() : "";
                    if (cmd == "anim") steps.Add(new Step { K = Kind.Anim, Arg = arg });
                    else if (cmd == "coins" && int.TryParse(arg, out var n)) steps.Add(new Step { K = Kind.Coins, N = n });
                    else Plugin.Log.LogWarning("Unknown dialogue directive: " + line);
                    continue;
                }

                var step = new Step { K = Kind.Say, Text = line };
                int c = line.IndexOf(':');
                if (c > 0 && c <= 20 && !line.Substring(0, c).Contains(' '))
                {
                    var sp = line.Substring(0, c);
                    step.Speaker = char.ToUpperInvariant(sp[0]) + sp.Substring(1);
                    step.Text = line.Substring(c + 1).Trim();
                }
                steps.Add(step);
            }
            return steps;
        }

        private static void Advance()
        {
            while (true)
            {
                if (_i >= _steps.Count) { Active = false; return; }
                var s = _steps[_i++];
                switch (s.K)
                {
                    case Kind.Anim: DoAnim(s.Arg); break;
                    case Kind.Coins: DoCoins(s.N); break;
                    case Kind.Say:
                        _speaker = s.Speaker;
                        _full = s.Text.Replace("[player]", Plugin.PlayerName.Value);
                        _shown = 0;
                        return;
                }
            }
        }

        private static void DoAnim(string name)
        {
            try
            {
                var chibi = UnityEngine.Object.FindObjectOfType<Mob_ChibiMita>();
                var anim = Resources.FindObjectsOfTypeAll<Mob_ChibiMita_Animation>()
                    .FirstOrDefault(a => a.gameObject.scene.IsValid() &&
                                         a.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
                if (chibi == null || anim == null) { Plugin.Log.LogWarning($"@anim '{name}': no matching animation in this scene"); return; }
                chibi.AnimationPlay(anim);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"@anim '{name}' failed: {e.Message}"); }
        }

        private static void DoCoins(int n)
        {
            try { UnityEngine.Object.FindObjectOfType<Tamagotchi_Main>()?.MoneyAdd(n); }
            catch (Exception e) { Plugin.Log.LogWarning($"@coins failed: {e.Message}"); }
        }

        internal static void Tick()
        {
            if (!Active || Time.frameCount == _startFrame) return;
            _shown = Mathf.Min(_full.Length, _shown + Time.unscaledDeltaTime * 45f);

            if (Input.GetKeyDown(KeyCode.Escape)) { Active = false; return; }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                if (_shown < _full.Length) _shown = _full.Length;
                else Advance();
            }
        }

        internal static void Draw()
        {
            if (!Active) return;
            try
            {
                if (_nameStyle == null)
                {
                    _nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
                    _textStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true };
                    _hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.LowerRight };
                }
                var r = new Rect(Screen.width * 0.15f, Screen.height - 210, Screen.width * 0.7f, 180);
                GUI.Box(r, "");
                if (_speaker.Length > 0) GUI.Label(new Rect(r.x + 18, r.y + 8, r.width - 36, 34), _speaker, _nameStyle);
                GUI.Label(new Rect(r.x + 18, r.y + 46, r.width - 36, r.height - 60), _full.Substring(0, (int)_shown), _textStyle);
                if (_shown >= _full.Length)
                    GUI.Label(new Rect(r.x, r.y, r.width - 14, r.height - 8), "Enter", _hintStyle);
            }
            catch (Exception e) { Plugin.Log.LogWarning("Dialogue draw failed: " + e.Message); }
        }
    }
}
