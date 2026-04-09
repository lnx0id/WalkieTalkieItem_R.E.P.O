using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using REPOLib.Objects.Sdk;
using System;
using System.IO;
using UnityEngine;

namespace Radio;

[BepInPlugin("Lnx0id.Radio", "Radio", "1.0.0")]
public class Radio : BaseUnityPlugin
{
    internal static Radio Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;
    internal Harmony? Harmony { get; set; }
    internal WalkieTalkieLn? scriptInstance { get; private set; }

    private void Awake()
    {
        Instance = this;
        
        // Prevent the plugin from being deleted
        this.gameObject.transform.parent = null;
        this.gameObject.hideFlags = HideFlags.HideAndDontSave;

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
        Harmony ??= new Harmony(Info.Metadata.GUID);
        Harmony.PatchAll();
    }

    internal void Unpatch()
    {
        Harmony?.UnpatchSelf();
    }
}