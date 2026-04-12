using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using KeybindLib;
using KeybindLib.Classes;
using REPOLib.Objects.Sdk;
using System;
using System.IO;
using UnityEngine;


namespace Radio;

[BepInDependency("bulletbot.keybindlib", BepInDependency.DependencyFlags.HardDependency)]
[BepInPlugin("Lnx0id.Radio", "Radio", "1.1.1")]
public class Radio : BaseUnityPlugin
{
    internal static Radio Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;
    internal Harmony? _Harmony { get; set; }
    internal WalkieTalkieLn? scriptInstance { get; private set; }

    private void Awake()
    {
        Instance = this;
        
        // Prevent the plugin from being deleted
        this.gameObject.transform.parent = null;
        this.gameObject.hideFlags = HideFlags.HideAndDontSave;

        BindConfig.switchWalkieChannel = Keybinds.Bind("SwitchChannel", "<Keyboard>/v");
        Logger.LogWarning($"bind is {BindConfig.switchWalkieChannel.inputKey} set by default");

        scriptInstance = this.gameObject.AddComponent<WalkieTalkieLn>();

        Patch();

        string pluginFolderPath = Path.GetDirectoryName(Info.Location);
        string bundlePath = Path.Combine(pluginFolderPath, "WalkieTalkieItemBundle");
        REPOLib.BundleLoader.LoadBundle(bundlePath, assetBundle =>
        {
            var item = assetBundle.LoadAsset<ItemContent>("WalkieTalkieItemRepoLibItem");
            REPOLib.Modules.Items.RegisterItem(item);
        });
        Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
    }

    internal void Patch()
    {
        _Harmony ??= new Harmony(Info.Metadata.GUID);
        _Harmony.PatchAll();
    }

    internal void Unpatch()
    {
        _Harmony?.UnpatchSelf();
    }
}

internal static class BindConfig {
    public static Keybind switchWalkieChannel;
}
