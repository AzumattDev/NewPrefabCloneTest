using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using JetBrains.Annotations;
using NewPrefabCloneTest;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace ItemManager;

[PublicAPI]
public enum WorkTable
{
    Disabled,
    Inventory,
    [InternalName("F1_Stove")] Stove,
    [InternalName("A1_Research Table")] ResearchTable,
    [InternalName("B2_Avil")] Anvil,
    [InternalName("C1_Gun Workshop")] GunWorkshop,
    [InternalName("C2_Ammo Workshop")] AmmoWorkShop,
    [InternalName("C3_Armor Workshop")] ArmorWorkShop,
    Custom,
}

public class InternalName : Attribute
{
    public readonly string internalName;
    public InternalName(string internalName) => this.internalName = internalName;
}

[PublicAPI]
public class RequiredResourceList
{
    public readonly List<Requirement> Requirements = new();
    public bool Free = false; // If Requirements empty and Free is true, then it costs nothing. If Requirements empty and Free is false, then it won't be craftable.

    public void Add(string itemName, int amount, int quality = 0) => Requirements.Add(new Requirement { itemName = itemName, amount = amount, quality = quality });
    public void Add(string itemName, ConfigEntry<int> amountConfig, int quality = 0) => Requirements.Add(new Requirement { itemName = itemName, amountConfig = amountConfig, quality = quality });
}

[PublicAPI]
public class CraftingStationList
{
    public readonly List<WorkTableConfig> Stations = new();

    public void Add(WorkTable table, int level) => Stations.Add(new WorkTableConfig { Table = table, level = level });
    public void Add(string customTable, int level) => Stations.Add(new WorkTableConfig { Table = WorkTable.Custom, level = level, custom = customTable });
}

[PublicAPI]
public class CustomItemRequirement
{
    public readonly RequiredResourceList RequiredItems = new();
    public readonly RequiredResourceList RequiredUpgradeItems = new();
    public readonly CraftingStationList Crafting = new();
    public int CraftAmount = 1;
    public bool RequireOnlyOneIngredient = false;
    public float QualityResultAmountMultiplier = 1;
    public ConfigEntryBase? CustomItemRequirementIsActive = null;
    public int Level = 1;
    public bool Rare = false;
    public int Amount = 1;
    public int Stack = 1;
    public bool CanBeSold = true;
    public float Condition = 100f;
    public float MaxCondition = 100f;
    public EquipmentSlotType EquipmentSlotType = EquipmentSlotType.Misc;
    public string Icon = "I_Gasoline";
}

public struct Requirement
{
    public string itemName;
    public int amount;
    public ConfigEntry<int>? amountConfig;

    [Description("Set to a non-zero value to apply the requirement only for a specific quality")]
    public int quality;
}

public struct WorkTableConfig
{
    public WorkTable Table;
    public int level;
    public string? custom;
}

public struct MockItem
{
    public string? name;
    public string? description;
    public string? mockFrom;
}

[Flags]
public enum Configurability
{
    Disabled = 0,
    CustomItemRequirement = 1,
    Stats = 2,
    Drop = 4,
    Trader = 8,
    Full = CustomItemRequirement | Drop | Stats | Trader,
}

public enum Toggle
{
    On = 1,
    Off = 0,
}

[PublicAPI]
public class CustomItem
{
    private class ItemConfig
    {
        public ConfigEntry<string>? craft;
        public ConfigEntry<string>? upgrade;
        public ConfigEntry<WorkTable> table = null!;
        public ConfigEntry<int> tableLevel = null!;
        public ConfigEntry<string> customTable = null!;
        public ConfigEntry<int>? maximumTableLevel;
        public ConfigEntry<float> qualityResultAmountMultiplier = null!;
    }

    private static readonly List<CustomItem> registeredItems = new();
    private static readonly Dictionary<Item, CustomItem> ItemMap = new();
    private static Dictionary<CustomItem, Dictionary<string, List<CraftingItemRequirement>>> activeCustomItemRequirements = new();
    private static Dictionary<CustomItem, Dictionary<string, ItemConfig>> itemCraftConfigs = new();
    private static Dictionary<CustomItem, ConfigEntry<string>> ItemConfigs = new();
    private readonly Dictionary<ConfigEntryBase, Action> statsConfigs = new();

    public static Configurability DefaultConfigurability = Configurability.Full;
    public Configurability? Configurable = null;
    private Configurability configurability => Configurable ?? DefaultConfigurability;
    private Configurability configurationVisible = Configurability.Full;

    public readonly GameObject Prefab = null!;

    private MockItem? mockItem;

    [Description("Specifies the resources needed to craft the item.\nUse .Add to add resources with their internal ID and an amount.\nUse one .Add for each resource type the item should need.")]
    public RequiredResourceList RequiredItems => this[""].RequiredItems;


    [Description("Specifies the resources needed to upgrade the item.\nUse .Add to add resources with their internal ID and an amount. This amount will be multipled by the item quality level.\nUse one .Add for each resource type the upgrade should need.")]
    public RequiredResourceList RequiredUpgradeItems => this[""].RequiredUpgradeItems;

    [Description("Specifies the crafting station needed to craft the item.\nUse .Add to add a crafting station, using the CraftingTable enum and a minimum level for the crafting station.\nUse one .Add for each crafting station.")]
    public CraftingStationList Crafting => this[""].Crafting;

    [Description("Specifies a config entry which toggles whether a recipe is active.")]
    public ConfigEntryBase? CustomItemRequirementIsActive
    {
        get => this[""].CustomItemRequirementIsActive;
        set => this[""].CustomItemRequirementIsActive = value;
    }

    [Description("Specifies the number of items that should be given to the player with a single craft of the item.\nDefaults to 1.")]
    public int Level
    {
        get => this[""].Level;
        set => this[""].Level = value;
    }

    [Description("Specifies the rarity of the item.\nDefaults to false.")]
    public bool Rare
    {
        get => this[""].Rare;
        set => this[""].Rare = value;
    }

    [Description("Specifies the number of items that should be given to the player with a single craft of the item.\nDefaults to 1.")]
    public int CraftAmount
    {
        get => this[""].CraftAmount;
        set => this[""].CraftAmount = value;
    }

    [Description("Specifies the stack size of the item.\nDefaults to 1.")]
    public int Stack
    {
        get => this[""].Stack;
        set => this[""].Stack = value;
    }

    [Description("Specifies if the item can be sold to a trader.\nDefaults to true.")]
    public bool CanBeSold
    {
        get => this[""].CanBeSold;
        set => this[""].CanBeSold = value;
    }

    [Description("Specifies the condition of the item.\nDefaults to 100.")]
    public float Condition
    {
        get => this[""].Condition;
        set => this[""].Condition = value;
    }

    [Description("Specifies the maximum condition of the item.\nDefaults to 100.")]
    public float MaxCondition
    {
        get => this[""].MaxCondition;
        set => this[""].MaxCondition = value;
    }

    [Description("Specifies the equipment slot type of the item.\nDefaults to Misc.")]
    public EquipmentSlotType EquipmentSlotType
    {
        get => this[""].EquipmentSlotType;
        set => this[""].EquipmentSlotType = value;
    }

    [Description("Specifies the icon of the item.\nDefaults to I_Gasoline.")]
    public string Icon
    {
        get => this[""].Icon;
        set => this[""].Icon = value;
    }

    public Dictionary<string, CustomItemRequirement> CustomItemRequirements = new();

    public CustomItemRequirement this[string name]
    {
        get
        {
            if (CustomItemRequirements.TryGetValue(name, out CustomItemRequirement recipe))
            {
                return recipe;
            }

            return CustomItemRequirements[name] = new CustomItemRequirement();
        }
    }

    public CustomItem(string assetBundleFileName, string prefabName) : this(PrefabManager.GetAssetBundleFromResources(assetBundleFileName), prefabName)
    {
    }

    public CustomItem(AssetBundle bundle, string prefabName) : this(PrefabManager.RegisterPrefab(bundle, prefabName))
    {
    }

    public CustomItem(string prefabName, string description, string mockFrom) : this(PrefabManager.RegisterPrefab(prefabName, description, mockFrom))
    {
        mockItem = new MockItem() { name = prefabName, description = description, mockFrom = mockFrom };
        registeredItems.Add(this);
        ItemMap[Prefab.GetComponent<Item>()] = this;
    }

    public CustomItem(GameObject prefab, bool skipRegistering = false)
    {
        if (!skipRegistering)
        {
            PrefabManager.RegisterPrefab(prefab);
        }

        Prefab = prefab;
        registeredItems.Add(this);
        ItemMap[Prefab.GetComponent<Item>()] = this;
    }

    private CustomItem(Dictionary<string, MockItem> assetBundleFileName)
    {
    }

    public void ToggleConfigurationVisibility(Configurability visible)
    {
        void Toggle(ConfigEntryBase cfg, Configurability check)
        {
            foreach (object? tag in cfg.Description.Tags)
            {
                if (tag is ConfigurationManagerAttributes attrs)
                {
                    attrs.Browsable = (visible & check) != 0 && (attrs.browsability is null || attrs.browsability());
                }
            }
        }

        void ToggleObj(object obj, Configurability check)
        {
            foreach (FieldInfo field in obj.GetType().GetFields())
            {
                if (field.GetValue(obj) is ConfigEntryBase cfg)
                {
                    Toggle(cfg, check);
                }
            }
        }

        configurationVisible = visible;
        if (ItemConfigs.TryGetValue(this, out ConfigEntry<string> dropCfg))
        {
            Toggle(dropCfg, Configurability.Drop);
        }

        if (itemCraftConfigs.TryGetValue(this, out Dictionary<string, ItemConfig> craftCfgs))
        {
            foreach (ItemConfig craftCfg in craftCfgs.Values)
            {
                ToggleObj(craftCfg, Configurability.CustomItemRequirement);
            }
        }

        foreach (KeyValuePair<ConfigEntryBase, Action> cfg in statsConfigs)
        {
            Toggle(cfg.Key, Configurability.Stats);
            if ((visible & Configurability.Stats) != 0)
            {
                cfg.Value();
            }
        }
    }


    private class ConfigurationManagerAttributes
    {
        [UsedImplicitly] public int? Order;
        [UsedImplicitly] public bool? Browsable;
        [UsedImplicitly] public string? Category;
        [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
        public Func<bool>? browsability;
    }

    private static string getInternalName<T>(T value) where T : struct => ((InternalName)typeof(T).GetMember(value.ToString())[0].GetCustomAttributes(typeof(InternalName)).First()).internalName;

    private static BaseUnityPlugin? _plugin;

    private static BaseUnityPlugin plugin
    {
        get
        {
            if (_plugin is null)
            {
                IEnumerable<TypeInfo> types;
                try
                {
                    types = Assembly.GetExecutingAssembly().DefinedTypes.ToList();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).Select(t => t.GetTypeInfo());
                }

                _plugin = (BaseUnityPlugin)BepInEx.Bootstrap.Chainloader.ManagerObject.GetComponent(types.First(t => t.IsClass && typeof(BaseUnityPlugin).IsAssignableFrom(t)));
            }

            return _plugin;
        }
    }

    private void registerRecipes(RM instance, GameObject container)
    {
        foreach (CustomItem? customItem in registeredItems)
        {
            activeCustomItemRequirements[customItem] = new Dictionary<string, List<CraftingItemRequirement>>();
            itemCraftConfigs.TryGetValue(this, out Dictionary<string, ItemConfig> cfgs);
            if (!string.IsNullOrWhiteSpace(customItem.mockItem?.mockFrom))
            {
                Item item = null;
                foreach (Transform allItemsItem in instance.allItems.items.Where(allItemsItem => allItemsItem.name == customItem.mockItem?.mockFrom))
                {
                    item = allItemsItem.GetComponent<Item>();
                }

                if (item == null) return;
                
                Debug.Log("Creating mock from " + customItem.mockItem?.mockFrom);

                Transform newPrefab = Object.Instantiate(item.transform, container.transform, true);
                if (!newPrefab) return;
                Item newItemComponent = newPrefab.GetComponent<Item>();
                if (!newItemComponent) return;
                newPrefab.name = customItem.mockItem?.name ?? "MockItem";
                newItemComponent.name = customItem.mockItem?.name ?? "MockItem";

                // Set the values on the newItemComponent from the config on customItem
                newItemComponent.level = customItem.CustomItemRequirements[customItem.mockItem?.name].Level;
                newItemComponent.rare = customItem.CustomItemRequirements[customItem.mockItem?.name].Rare;
                newItemComponent.craftAmount = customItem.CustomItemRequirements[customItem.mockItem?.name].CraftAmount;
                newItemComponent.stackAmount = customItem.CustomItemRequirements[customItem.mockItem?.name].Stack;
                newItemComponent.canBeSold = customItem.CustomItemRequirements[customItem.mockItem?.name].CanBeSold;
                newItemComponent.condition = customItem.CustomItemRequirements[customItem.mockItem?.name].Condition;
                newItemComponent.MaxCondition = customItem.CustomItemRequirements[customItem.mockItem?.name].MaxCondition;
                newItemComponent.equipmentSlotType = customItem.CustomItemRequirements[customItem.mockItem?.name].EquipmentSlotType;
                LoadSpriteFromURL(customItem.CustomItemRequirements[customItem.mockItem?.name].Icon, (sprite) =>
                {
                    if (sprite != null)
                    {
                        newItemComponent.icon = sprite.texture;
                    }
                });
                
                // Print the values to the console
                Debug.Log($"Created mock item with name: {newItemComponent.name} and level: {newItemComponent.level} and rare: {newItemComponent.rare} and craftAmount: {newItemComponent.craftAmount} and stackAmount: {newItemComponent.stackAmount} and canBeSold: {newItemComponent.canBeSold} and condition: {newItemComponent.condition} and MaxCondition: {newItemComponent.MaxCondition} and equipmentSlotType: {newItemComponent.equipmentSlotType} and icon: {newItemComponent.icon} {customItem.Icon}");

                // Find the max ItemID and set the new one
                int maxID = instance.allItems.items.Max(i => i.GetComponent<Item>()?.ItemID ?? 0);
                newItemComponent.ItemID = maxID + 1;
                newItemComponent.itemIndex = newItemComponent.ItemID;
                newItemComponent.localizedItemName = LocalizationRuntimeManager.CreateLocalizedString(LocalizationSettings.StringDatabase.DefaultTable, $"{newPrefab.name}_{newItemComponent.ItemID.ToString()}", customItem.mockItem?.name);
                newItemComponent.localizedItemName.FallbackState = FallbackBehavior.UseFallback;
                newItemComponent.localizedItemDescription = LocalizationRuntimeManager.CreateLocalizedString(LocalizationSettings.StringDatabase.DefaultTable, $"{newPrefab.name}_{newItemComponent.ItemID.ToString()}_description", customItem.mockItem?.description);
                newItemComponent.localizedItemDescription.FallbackState = FallbackBehavior.UseFallback;
                newItemComponent.GetCurrentLocaleAndRefresh();
                instance.allItems.AddItemDifferentName(newPrefab);
                if (!instance.ItemDictionary.ContainsKey(newItemComponent.ItemID))
                {
                    instance.ItemDictionary.Add(newItemComponent.ItemID, newItemComponent);
                    newItemComponent.itemIndex = newItemComponent.ItemID;
                }

                Craftable craftable = newPrefab.GetComponent<Craftable>();
                if (craftable)
                {
                    // Custom code here to change the values for the new prefab
                    craftable.itemRequirements = customItem.RequiredItems.Requirements.Select(r => new CraftingItemRequirement { item = instance.allItems.items.FirstOrDefault(x => x.name == r.itemName), amount = r.amount }).ToArray();

                    instance._allCraftables.Add(craftable);
                    craftable.NeedBlueprint = false;
                }

                if (newPrefab && newPrefab.TryGetComponent(out Blueprint component3))
                {
                    if (component3 && component3.UnlockItems.Length != 0)
                    {
                        foreach (Item unlockItem in component3.UnlockItems)
                        {
                            Craftable component4;
                            if (unlockItem.TryGetComponent(out component4))
                                component4.NeedBlueprint = true;
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

                newItemComponent.localizedItemName.RefreshString();
                newItemComponent.localizedItemDescription.RefreshString();
                NewPrefabCloneTestPlugin.NewPrefabCloneTestLogger.LogDebug($"{customItem.mockItem?.name} created! with ID: {newItemComponent.ItemID} " +
                                                                           $"and name: {newItemComponent.name} " +
                                                                           $"and display name: {newItemComponent.GetDisplayName()} and description {newItemComponent.GetDisplayDescription()} and localized name: {LocalizationRuntimeManager.GetString(newItemComponent.localizedItemName)} and localized description: {LocalizationRuntimeManager.GetString(newItemComponent.localizedItemDescription)}");
            }
        }
    }

    internal static IEnumerator LoadSpriteFromURL(string? imageURL, Action<Sprite> callback)
    {
        if (imageURL == null)
        {
            callback(null!);
            yield break;
        }

        UnityWebRequest www = UnityWebRequestTexture.GetTexture(imageURL);
        yield return www.SendWebRequest();

        if (www.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError("Error while fetching image: " + www.error);
            callback(null!);
        }
        else
        {
            Texture2D texture = DownloadHandlerTexture.GetContent(www);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            callback(sprite);
        }
    }


    [HarmonyPriority(Priority.VeryHigh)]
    public static void Patch_RMLoadResources(RM __instance)
    {
        GameObject container;
        if (GameObject.Find("SunkenlandItemManagerContainer") == null)
        {
            container = new GameObject("SunkenlandItemManagerContainer");
            Object.DontDestroyOnLoad(container);
        }
        else
        {
            container = GameObject.Find("SunkenlandItemManagerContainer");
        }
        
        // Print all transforms in __instance.allItems.items
        foreach (Transform allItemsItem in __instance.allItems.items)
        {
            NewPrefabCloneTestPlugin.NewPrefabCloneTestLogger.LogError($"allItemsItem: {allItemsItem}");
        }

        foreach (CustomItem customItem in registeredItems)
        {
            customItem.registerRecipes(__instance, container);
        }
    }
}

[PublicAPI]
public static class PrefabManager
{
    static PrefabManager()
    {
        Harmony harmony = new("org.bepinex.helpers.ItemManager");
        harmony.Patch(AccessTools.DeclaredMethod(typeof(RM), nameof(RM.LoadResources)), postfix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(CustomItem), nameof(CustomItem.Patch_RMLoadResources))));
    }

    private struct BundleId
    {
        [UsedImplicitly] public string assetBundleFileName;
    }

    private static readonly Dictionary<BundleId, AssetBundle> bundleCache = new();

    internal static AssetBundle GetAssetBundleFromResources(string assetBundleFileName)
    {
        BundleId id = new() { assetBundleFileName = assetBundleFileName };
        if (!bundleCache.TryGetValue(id, out AssetBundle assetBundle))
        {
            var execAssembly = Assembly.GetExecutingAssembly();
            var resourceName = execAssembly.GetManifestResourceNames().Single(str => str.EndsWith(assetBundleFileName));
            using (var stream = execAssembly.GetManifestResourceStream(resourceName))
            {
                assetBundle = AssetBundle.LoadFromStream(stream);
            }
        }

        if (assetBundle == null)
        {
            throw new Exception($"Failed to load asset bundle {assetBundleFileName}, please make sure you have set this as an embedded resource in your project and that the name matches the filename.");
        }

        return assetBundle;
    }

    private static readonly List<GameObject> prefabs = new();
    private static readonly Dictionary<string, MockItem> mockPrefabs = new();

    public static GameObject RegisterPrefab(string assetBundleFileName, string prefabName) => RegisterPrefab(GetAssetBundleFromResources(assetBundleFileName), prefabName);

    public static GameObject RegisterPrefab(AssetBundle assets, string prefabName) => RegisterPrefab(assets.LoadAsset<GameObject>(prefabName));

    public static Dictionary<string, MockItem> RegisterPrefab(string prefabName, string description, string mockFrom)
    {
        mockPrefabs.Add(prefabName, new MockItem() { name = prefabName, description = description, mockFrom = mockFrom });
        return new Dictionary<string, MockItem>() { { prefabName, new MockItem() { name = prefabName, description = description, mockFrom = mockFrom } } };
    }

    public static GameObject RegisterPrefab(GameObject prefab)
    {
        prefabs.Add(prefab);
        return prefab;
    }
}

public class LocalizationRuntimeManager : MonoBehaviour
{
    private static Dictionary<string, StringTable> runtimeTables = new Dictionary<string, StringTable>();

    public static LocalizedString CreateLocalizedString(string tableName, string key, string value)
    {
        LocalizationSettings.StringDatabase.GetTable(LocalizationSettings.StringDatabase.DefaultTable).AddEntry(key, value);

        NewPrefabCloneTestPlugin.NewPrefabCloneTestLogger.LogError($"Added entry with key: {key} and value: {value} to table: {tableName}");

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