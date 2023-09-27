using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ItemManager;
using JetBrains.Annotations;

namespace NewPrefabCloneTest
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class NewPrefabCloneTestPlugin : BaseUnityPlugin
    {
        internal const string ModName = "NewPrefabCloneTest";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "Azumatt";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource NewPrefabCloneTestLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        public static NewPrefabCloneTestPlugin Instance { get; private set; } = null!;


        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            Instance = this;
            CustomItem testingItem = new CustomItem("Faction Coin", "A coin that, when used, changes your faction randomly", "I_Gasoline");
            testingItem.Crafting.Add(WorkTable.Stove, 1);
            testingItem.RequiredItems.Add("I_Gasoline", 10);
            testingItem.RequiredItems.Add("A2B_Iron Axe", 1);
            testingItem.Category = CraftingCategory.materials;
            testingItem.Level = 1;
            testingItem.Rare = false;
            testingItem.CraftAmount = 1;
            testingItem.Craftable = true;
            testingItem.Stack = 10;
            testingItem.CanBeSold = false;
            testingItem.Condition = 100f;
            testingItem.MaxCondition = 100f;
            testingItem.EquipmentSlotType = EquipmentSlotType.Armor;
            testingItem.Icon = "https://gcdn.thunderstore.io/live/repository/icons/Azumatt-FactionAssigner-1.0.0.png.128x128_q95.png";

            CustomItem testingItem2 = new CustomItem("My Iron Axe", "Azu's Cool Shit", "A2B_Iron Axe");
            testingItem2.Crafting.Add(WorkTable.None, 1);
            testingItem2.RequiredItems.Add("A2B_Iron Axe", 1);
            testingItem2.Level = 1;
            testingItem2.Rare = false;
            testingItem2.CraftAmount = 1;
            testingItem2.Craftable = true;
            testingItem2.Stack = 10;
            testingItem2.CanBeSold = false;
            testingItem2.Condition = 100f;
            testingItem2.MaxCondition = 100f;
            testingItem2.EquipmentSlotType = EquipmentSlotType.None;
            testingItem2.Icon = "https://cdn-icons-png.flaticon.com/512/6436/6436195.png";
            
            CustomItem testingItem4 = new CustomItem("Shotgun for the Homies", "Azu's Cool Shit", "A4_Double Barrel Shotgun");
            testingItem4.Crafting.Add(WorkTable.Stove, 1);
            testingItem4.RequiredItems.Add("I_Gasoline", 1);
            testingItem4.Level = 3;
            testingItem4.Rare = false;  
            testingItem4.CraftAmount = 1;
            testingItem4.Stack = 10;
            testingItem4.CanBeSold = false;
            testingItem4.Condition = 100f;
            testingItem4.MaxCondition = 100f;
            testingItem4.EquipmentSlotType = EquipmentSlotType.None;
            testingItem4.Icon = "https://gcdn.thunderstore.io/live/repository/icons/Azumatt-FactionAssigner-1.0.0.png.128x128_q95.png";

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                NewPrefabCloneTestLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                NewPrefabCloneTestLogger.LogError($"There was an issue loading your {ConfigFileName}");
                NewPrefabCloneTestLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order;
            [UsedImplicitly] public bool? Browsable;
            [UsedImplicitly] public string? Category;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
        }

        class AcceptableShortcuts : AcceptableValueBase
        {
            public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
            {
            }

            public override object Clamp(object value) => value;
            public override bool IsValid(object value) => true;

            public override string ToDescriptionString() =>
                "# Acceptable values: " + string.Join(", ", UnityInput.Current.SupportedKeyCodes);
        }

        #endregion
    }

    /*[HarmonyPatch(typeof(RM), nameof(RM.LoadResources))]
    static class RMLoadResourcesPatch
    {
        static void Postfix(RM __instance)
        {
            NewPrefabCloneTestPlugin.NewPrefabCloneTestLogger.LogWarning("RM.LoadResources Postfix called");
            foreach (var allItemsItem in __instance.allItems.items)
            {
                if (allItemsItem.GetComponent<Item>())
                    NewPrefabCloneTestPlugin.NewPrefabCloneTestLogger.LogWarning($"Found Item {allItemsItem.name}");
            }
        }
    }*/
}