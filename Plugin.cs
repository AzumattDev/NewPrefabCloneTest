using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

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


        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            exampleConfig = Config.Bind("1 - General", "Example Config", Toggle.On, "If on, do something here.");


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

        private static ConfigEntry<Toggle> exampleConfig = null!;

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

    [HarmonyPatch(typeof(RM), nameof(RM.LoadResources))]
    static class RMAwakePatch
    {
        static void Postfix(RM __instance)
        {
            // Create new gameobject and parent it to the RM
            GameObject container = new GameObject("NewPrefabCloneTestContainer");
            // DontDestroyOnLoad
            Object.DontDestroyOnLoad(container);

            // Get the first item in allItems.items with the component Item
            Item item = null;
            foreach (Transform allItemsItem in __instance.allItems.items)
            {
                if (allItemsItem.name.ToLower().Contains("gasoline"))
                {
                    item = allItemsItem.GetComponent<Item>();
                }
            }

            if (item == null) return;

            NewPrefabCloneTestPlugin.NewPrefabCloneTestLogger.LogError($"item name: {item.ItemID} itemname {item.name} display name{item._displayName} ID: {item.ItemID}");

            // Clone the item
            Transform newPrefab = Object.Instantiate(item.transform, container.transform, true);
            if (!newPrefab) return;

            Item newItemComponent = newPrefab.GetComponent<Item>();
            if (!newItemComponent) return;

            newPrefab.name = "NewPrefabCloneTest";
            newItemComponent.name = "NewPrefabCloneTest";
            // Find the max ItemID and set the new one
            int maxID = __instance.allItems.items.Max(i => i.GetComponent<Item>()?.ItemID ?? 0);
            newItemComponent.ItemID = maxID + 1;
            newItemComponent.itemIndex = newItemComponent.ItemID;
            newItemComponent.localizedItemName = LocalizationRuntimeManager.CreateLocalizedString(LocalizationSettings.StringDatabase.DefaultTable, $"{newPrefab.name}_{newItemComponent.ItemID.ToString()}", "NewPrefabCloneTest");
            newItemComponent.localizedItemName.FallbackState = FallbackBehavior.UseFallback;
            newItemComponent.localizedItemDescription = LocalizationRuntimeManager.CreateLocalizedString(LocalizationSettings.StringDatabase.DefaultTable, $"{newPrefab.name}_{newItemComponent.ItemID.ToString()}_description", "Test Description");
            newItemComponent.localizedItemDescription.FallbackState = FallbackBehavior.UseFallback;
            newItemComponent.GetCurrentLocaleAndRefresh();

            // Add the new prefab to the list of prefabs
            __instance.allItems.AddItemDifferentName(newPrefab);

            // Add to dictionary
            if (!__instance.ItemDictionary.ContainsKey(newItemComponent.ItemID))
            {
                __instance.ItemDictionary.Add(newItemComponent.ItemID, newItemComponent);
                newItemComponent.itemIndex = newItemComponent.ItemID;
            }

            Craftable craftable = newPrefab.GetComponent<Craftable>();
            if (craftable)
            {
                __instance._allCraftables.Add(craftable);
                craftable.NeedBlueprint = false;
            }

            Blueprint component3;
            if (newPrefab && newPrefab.TryGetComponent(out component3))
            {
                if (component3 && component3.UnlockItems.Length != 0)
                {
                    foreach (Item unlockItem in component3.UnlockItems)
                    {
                        Craftable component4;
                        if (unlockItem.TryGetComponent(out component4))
                            component4.NeedBlueprint = true;
                        else
                            __instance.LogError("物品：" + unlockItem.name + " 没有Craftable组件，需要修复！");
                    }
                }

                if (component3 && component3.UnlockBuildings.Length != 0)
                {
                    foreach (BuildingPiece unlockBuilding in component3.UnlockBuildings)
                    {
                        if (!(unlockBuilding == null))
                            unlockBuilding.GetComponent<Craftable>().NeedBlueprint = true;
                    }
                }
            }

            newItemComponent.localizedItemName.RefreshString(); // Attempt to refresh both strings just before using them.
            newItemComponent.localizedItemDescription.RefreshString(); // Attempt to refresh both strings just before using them.
            NewPrefabCloneTestPlugin.NewPrefabCloneTestLogger.LogError($"NewPrefabCloneTest created! with ID: {newItemComponent.ItemID} " +
                                                                       $"and name: {newItemComponent.name} " +
                                                                       $"and display name: {newItemComponent.GetDisplayName()} and description {newItemComponent.GetDisplayDescription()} and localized name: {LocalizationRuntimeManager.GetString(newItemComponent.localizedItemName)} and localized description: {LocalizationRuntimeManager.GetString(newItemComponent.localizedItemDescription)}");

            // Print all items and their names/descriptions from the allItems list
            foreach (Transform allItemsItem in __instance.allItems.items)
            {
                Item itemComponent = allItemsItem.GetComponent<Item>();
                if (itemComponent && item.ItemID == newItemComponent.ItemID)
                {
                    NewPrefabCloneTestPlugin.NewPrefabCloneTestLogger.LogError($"item ID: {newItemComponent.ItemID} item name {newItemComponent.name} display name internal{newItemComponent._displayName}");
                }
            }
        }
    }


    public class LocalizationRuntimeManager : MonoBehaviour
    {
        private static Dictionary<string, StringTable> runtimeTables = new Dictionary<string, StringTable>();

        public static LocalizedString CreateLocalizedString(string tableName, string key, string value)
        {
            // If not in the default table, then check runtimeTables and add/update there
            if (!runtimeTables.ContainsKey(tableName))
            {
                var table = ScriptableObject.CreateInstance<StringTable>();
                var sharedData = ScriptableObject.CreateInstance<SharedTableData>();
                table.SharedData = sharedData;
                runtimeTables[tableName] = table;
            }

            // Add the entry to the table in runtimeTables
            //runtimeTables[tableName].AddEntry(key, value);
            LocalizationSettings.StringDatabase.GetTable(LocalizationSettings.StringDatabase.DefaultTable).AddEntry(key, value);
            // Logging added entry
            NewPrefabCloneTestPlugin.NewPrefabCloneTestLogger.LogError($"Added entry with key: {key} and value: {value} to table: {tableName}");

            // Create a LocalizedString pointing to that entry
            LocalizedString localizedString = new LocalizedString
            {
                TableReference = LocalizationSettings.StringDatabase.DefaultTable,
                TableEntryReference = key,
                LocaleOverride = LocalizationSettings.SelectedLocale
            };

            return localizedString;
        }

        public static string GetString(LocalizedString localizedString)
        {
            if (runtimeTables.TryGetValue(localizedString.TableReference, out var table))
            {
                var entry = table.GetEntry((string)localizedString.TableEntryReference);
                if (entry != null)
                {
                    return entry.LocalizedValue;
                }
                else
                {
                    NewPrefabCloneTestPlugin.NewPrefabCloneTestLogger.LogError($"No entry found with key: {localizedString.TableEntryReference} in table: {localizedString.TableReference}");
                }
            }
            else
            {
                NewPrefabCloneTestPlugin.NewPrefabCloneTestLogger.LogError($"Table not found: {localizedString.TableReference}");
            }


            return null;
        }
    }
}