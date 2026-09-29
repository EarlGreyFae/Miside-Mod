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

        public override void Load()
        {
            Log = base.Log;
            DumpKey = Config.Bind("Debug", "DumpSceneKey", UnityEngine.KeyCode.F8,
                "Press to write the active scene's object hierarchy to the BepInEx log.");

            TypesKey = Config.Bind("Debug", "DumpTypesKey", UnityEngine.KeyCode.F9,
                "Press to write Tamagotchi/Chibi class members to BepInEx/MiSideMod_types.txt.");

            ClassInjector.RegisterTypeInIl2Cpp<ModBehaviour>();
            AddComponent<ModBehaviour>();

            new Harmony(Guid).PatchAll();
            Log.LogInfo($"{Name} {Version} loaded");
        }
    }
}
