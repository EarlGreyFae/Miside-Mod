using HarmonyLib;

namespace MiSideMod
{
    /// <summary>Log-only hooks on the Tamagotchi game flow. Used to learn when things fire.</summary>
    [HarmonyPatch]
    internal static class Patches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(Tamagotchi_Main), nameof(Tamagotchi_Main.NewDay))]
        private static void NewDay(Tamagotchi_Main __instance)
            => Plugin.Log.LogInfo($"NewDay: money={__instance.money} energy={__instance.energy}");

        [HarmonyPostfix, HarmonyPatch(typeof(Tamagotchi_Main), nameof(Tamagotchi_Main.MoneyAdd))]
        private static void MoneyAdd(int x)
            => Plugin.Log.LogInfo($"MoneyAdd: {x}");

        [HarmonyPostfix, HarmonyPatch(typeof(Tamagotchi_Main), nameof(Tamagotchi_Main.MiniGamePlay))]
        private static void MiniGamePlay(Tamagotchi_MiniGame _game)
            => Plugin.Log.LogInfo($"MiniGamePlay: {(_game != null ? _game.name : "null")}");

        [HarmonyPostfix, HarmonyPatch(typeof(Tamagotchi_Main), nameof(Tamagotchi_Main.MiniGameStop))]
        private static void MiniGameStop()
            => Plugin.Log.LogInfo("MiniGameStop");

        [HarmonyPostfix, HarmonyPatch(typeof(Tamagotchi_Dialogue), nameof(Tamagotchi_Dialogue.StartDialogue))]
        private static void StartDialogue(Tamagotchi_Dialogue_Mob _dialogueRun)
            => Plugin.Log.LogInfo($"StartDialogue: {(_dialogueRun != null ? _dialogueRun.name + " file=" + _dialogueRun.dialogueFile : "null")}");
    }
}
