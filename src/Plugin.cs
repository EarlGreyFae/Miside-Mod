using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;

namespace MiSideMod
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        public const string Guid = "com.earlgreyfae.miside.minigame";
        public const string Name = "MiSide Minigame Expansion";
        public const string Version = "0.1.0";

        internal static new ManualLogSource Log;
        internal static ConfigEntry<UnityEngine.KeyCode> DumpKey;
        internal static ConfigEntry<UnityEngine.KeyCode> TypesKey;
        internal static ConfigEntry<bool> BlockJump;
        private static readonly System.Collections.Generic.HashSet<string> Warned = new System.Collections.Generic.HashSet<string>();

        /// <summary>Logs a warning only the first time this exact message appears (per-frame code can repeat).</summary>
        internal static void WarnOnce(string message)
        {
            if (Warned.Add(message)) Log.LogWarning(message);
        }

        internal static ConfigEntry<string> PlayerName;
        internal static ConfigEntry<float> ChatMinutes;

        public override void Load()
        {
            Log = base.Log;
            DumpKey = Config.Bind("Debug", "DumpSceneKey", UnityEngine.KeyCode.F8,
                "Press to write the active scene's object hierarchy to the BepInEx log.");

            TypesKey = Config.Bind("Debug", "DumpTypesKey", UnityEngine.KeyCode.F9,
                "Press to write Tamagotchi/Chibi class members to BepInEx/MiSideMod_types.txt.");

            BlockJump = Config.Bind("Gameplay", "BlockRealmTransition", false,
                "If true, the Day 37 jump into Mita's world is switched off so you stay in the minigame. F6 toggles it in game.");

            PlayerName = Config.Bind("Dialogue", "PlayerName", "friend",
                "Name that replaces [player] in mod dialogue scripts.");
            ChatMinutes = Config.Bind("FreePlay", "ChatEveryMinutes", 4f,
                "In free play, Mita starts a random mod chat roughly this often. 0 turns it off.");
            ModDialogue.EnsureSamples();

            ClassInjector.RegisterTypeInIl2Cpp<ModBehaviour>();
            AddComponent<ModBehaviour>();

            new Harmony(Guid).PatchAll();
            Log.LogInfo($"{Name} {Version} loaded");
        }
    }
}
