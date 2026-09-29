using System;
using UnityEngine;

namespace MiSideMod
{
    /// <summary>
    /// Free play: the state after Day 37 when the jump into Mita's world is blocked.
    /// Switches off every story quest (so nothing is left "waiting"), restores the normal
    /// interface, and has Mita start a mod chat now and then.
    /// </summary>
    internal static class FreePlay
    {
        internal static bool Active;

        private static float _pendingSince = -1f;
        private static float _nextChat;
        private static readonly System.Random Rng = new System.Random();

        /// <summary>Called when a game dialogue starts; the final quest's second dialogue is the one that would end the game.</summary>
        internal static void OnGameDialogue(Tamagotchi_Dialogue_Mob mob)
        {
            if (Active || !Plugin.BlockJump.Value || mob == null) return;
            var parent = mob.transform.parent;
            if (mob.name == "Dialogue 2" && parent != null && parent.name.StartsWith("Quest 10"))
            {
                _pendingSince = Time.time;
                Plugin.Log.LogInfo("Final dialogue started with the jump blocked; free play will begin when it ends");
            }
        }

        private static bool GameDialogueRunning()
        {
            var d = UnityEngine.Object.FindObjectOfType<Tamagotchi_Dialogue>();
            return d != null && d.dialogueRun != null;
        }

        internal static void Enter()
        {
            var main = UnityEngine.Object.FindObjectOfType<Tamagotchi_Main>();
            if (main == null) { Plugin.Log.LogWarning("Free play: Tamagotchi_Main not found"); return; }

            if (Plugin.BlockJump.Value == false)
            {
                Plugin.BlockJump.Value = true;
                ModBehaviour.SetTransitionBlocked(true, false);
            }

            int off = 0;
            var quests = GameObject.Find("Quests");
            if (quests != null)
                for (int i = 0; i < quests.transform.childCount; i++)
                {
                    var q = quests.transform.GetChild(i).gameObject;
                    if (q.activeSelf) { q.SetActive(false); off++; }
                }

            Try(() => main.ShowInterface(true), "ShowInterface");
            Try(() => main.ShowButtonShop(true), "ShowButtonShop");
            Try(() => main.ShowButtonGames(true), "ShowButtonGames");
            Try(() => main.CanMoveRoom(true), "CanMoveRoom");

            Active = true;
            _pendingSince = -1f;
            ScheduleChat();
            Plugin.Log.LogInfo($"Free play started ({off} quest object(s) switched off)");
        }

        private static void Try(Action a, string what)
        {
            try { a(); } catch (Exception e) { Plugin.Log.LogWarning($"Free play: {what} failed: {e.Message}"); }
        }

        private static void ScheduleChat()
        {
            float mins = Mathf.Max(0.5f, Plugin.ChatMinutes.Value);
            _nextChat = Time.time + mins * 60f * (0.75f + (float)Rng.NextDouble() * 0.5f);
        }

        internal static void Tick()
        {
            try
            {
                // Auto-enter once the blocked final dialogue has finished (at least 5 s, at most 90 s after it began).
                if (_pendingSince >= 0f)
                {
                    float t = Time.time - _pendingSince;
                    if ((t > 5f && !GameDialogueRunning()) || t > 90f) Enter();
                }

                if (!Active || Plugin.ChatMinutes.Value <= 0f || Time.time < _nextChat) return;
                var main = UnityEngine.Object.FindObjectOfType<Tamagotchi_Main>();
                bool busy = ModDialogue.Active || SandboxMenu.Open || GameDialogueRunning()
                            || (main != null && main.playMinigame);
                if (busy) { _nextChat = Time.time + 15f; return; }
                ModDialogue.PlayRandom();
                ScheduleChat();
            }
            catch (Exception e) { Plugin.WarnOnce("Free play tick failed: " + e.Message); }
        }
    }
}
