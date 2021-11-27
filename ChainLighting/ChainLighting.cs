using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using KKAPI;
using KKAPI.Studio.UI;
using Studio;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChainLighting
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInProcess("StudioNEOV2")]
    [BepInProcess("CharaStudio")]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    [BepInDependency(ExtensibleSaveFormat.ExtendedSave.GUID)]
    public class ChainLighting : BaseUnityPlugin
    {
        public const string GUID = "orange.spork.chainlighting";
        public const string PluginName = "ChainLighting";
        public const string Version = "1.0.1";

        public static ChainLighting Instance { get; private set; }

        internal BepInEx.Logging.ManualLogSource Log => Logger;

        public static ConfigEntry<bool> DefaultEnabled { get; set; }
        public static ConfigEntry<bool> DefaultControlEmbeddedLights { get; set; }

        private bool _linkActive = false;
        public bool LinkActive { 
            get { return _linkActive; }
            set {
                if (_linkActive == value)
                    return;
#if DEBUG
                Log.LogInfo($"Link Active Set to {value}");
#endif
                _linkActive = value;
                if (linkActiveToggle != null)
                    linkActiveToggle.SetValue(_linkActive, false);
                if (_linkActive)
                    UpdateAllLightingStates();
                else
                    ReactivateNonOCILights();
            }
        }

        private bool _controlEmbeddedLights = false;
        public bool ControlEmbeddedLights
        {
            get { return _controlEmbeddedLights; }
            set
            {
                if (_controlEmbeddedLights == value)
                    return;
#if DEBUG
                Log.LogInfo($"Control Embedded Lights Set to {value}");
#endif
                _controlEmbeddedLights = value;
                if (controlNonLightsToggle != null)
                    controlNonLightsToggle.SetValue(_controlEmbeddedLights, false);
                if (_controlEmbeddedLights)
                    UpdateAllLightingStates();
                else if (LinkActive)
                    ReactivateNonOCILights();

            }
        }

        public bool SceneLoading { get; set; } = false;

        public ChainLighting()
        {
            if (Instance != null)
            {
                throw new InvalidOperationException("Singleton Only.");
            }

            Instance = this;

            DefaultEnabled = Config.Bind("Options", "Enabled by Default", false);
            DefaultControlEmbeddedLights = Config.Bind("Options", "Control Embedded Lights (Lights part of non-light Studio Items)", false);

            var harmony = new Harmony(GUID);
            harmony.Patch(typeof(AddObjectFolder).GetMethod(nameof(AddObjectFolder.Load), new Type[] { typeof(OIFolderInfo), typeof(ObjectCtrlInfo), typeof(TreeNodeObject), typeof(bool), typeof(int)}), null, new HarmonyMethod(typeof(ChainLighting).GetMethod(nameof(ChainLighting.RegisterOnVisibleDelegate), AccessTools.all)));
            harmony.Patch(typeof(AddObjectLight).GetMethod(nameof(AddObjectLight.Load), new Type[] { typeof(OILightInfo), typeof(ObjectCtrlInfo), typeof(TreeNodeObject), typeof(bool), typeof(int) }), null, new HarmonyMethod(typeof(ChainLighting).GetMethod(nameof(ChainLighting.RegisterOnVisibleDelegate), AccessTools.all)));


           // harmony.Patch(typeof(OCICamera).GetMethod(nameof(OCICamera.OnVisible)), null, new HarmonyMethod(typeof(ChainLighting).GetMethod(nameof(ChainLighting.UpdateLightingState), AccessTools.all)));
            harmony.Patch(typeof(OCIChar).GetMethod(nameof(OCIChar.OnVisible)), null, new HarmonyMethod(typeof(ChainLighting).GetMethod(nameof(ChainLighting.UpdateLightingState), AccessTools.all)));
            harmony.Patch(typeof(OCIFolder).GetMethod(nameof(OCIFolder.OnVisible)), null, new HarmonyMethod(typeof(ChainLighting).GetMethod(nameof(ChainLighting.UpdateLightingState), AccessTools.all)));
            harmony.Patch(typeof(OCIItem).GetMethod(nameof(OCIItem.OnVisible)), null, new HarmonyMethod(typeof(ChainLighting).GetMethod(nameof(ChainLighting.UpdateLightingState), AccessTools.all)));
            harmony.Patch(typeof(OCILight).GetMethod(nameof(OCILight.OnVisible)), null, new HarmonyMethod(typeof(ChainLighting).GetMethod(nameof(ChainLighting.UpdateLightingState), AccessTools.all)));

            LinkActive = DefaultEnabled.Value;
            ControlEmbeddedLights = DefaultControlEmbeddedLights.Value;

#if DEBUG
            Log.LogInfo($"Chain Lighting Plugin ON");
#endif

        }

        public static void RegisterOnVisibleDelegate(ObjectCtrlInfo __result)
        {
            try
            {
                __result.treeNodeObject.onVisible = (TreeNodeObject.OnVisibleFunc)Delegate.Combine(__result.treeNodeObject.onVisible, new TreeNodeObject.OnVisibleFunc(__result.OnVisible));
            }
            catch (Exception registerDelegate)
            {
                Instance.Log.LogError($"{registerDelegate.Message}\n{registerDelegate.StackTrace}");
            }
        }

        public void UpdateAllLightingStates()
        {
            if (!KKAPI.Studio.StudioAPI.StudioLoaded)
                return;
            try
            {
                foreach (ObjectCtrlInfo oci in Studio.Studio.Instance.dicObjectCtrl.Values)
                {
                    if (oci.treeNodeObject.parent == null)
                        oci.treeNodeObject.ResetVisible();
                }
            }
            catch (Exception errLightUpdate) { Log.LogDebug($"Error updating ALL lighting states: {errLightUpdate.Message}\n{errLightUpdate.StackTrace}"); }
        }

        private static void UpdateLightingState(ObjectCtrlInfo __instance, bool _visible)
        {
            if (!Instance.LinkActive || __instance?.guideObject?.transformTarget == null)
                return;

            try
            {
#if DEBUG
                ChainLighting.Instance.Log.LogInfo($"Set Visible {__instance.GetType()} {__instance?.guideObject?.transformTarget?.name}");
#endif

                // Look for OCILight Children
                if (__instance.GetType() == typeof(OCILight))
                {
                    // Disable OCILight's this way so they read correctly on the interface
                    OCILight ociLight = (OCILight)__instance;                    
                    if (Instance.SceneLoading)
                    {
                        ociLight.SetEnable( (!_visible || ociLight.light.enabled) && _visible );
#if DEBUG
                        ChainLighting.Instance.Log.LogInfo($"Setting OCILight: {ociLight.treeNodeObject.textName} to {ociLight.light.enabled} GO: {ociLight.objectLight.name}");
#endif
                    }
                    else
                    {
                        ociLight.SetEnable(_visible);
#if DEBUG
                        ChainLighting.Instance.Log.LogInfo($"Setting OCILight: {ociLight.treeNodeObject.textName} to {ociLight.light.enabled} GO: {ociLight.objectLight.name}");
#endif
                    }
                }

                if (Instance.ControlEmbeddedLights)
                {
                    List<Transform> allStudioObjects = Singleton<Studio.Studio>.Instance.dicObjectCtrl.Values.ToList().Select<ObjectCtrlInfo, Transform>(oci => oci.guideObject.transformTarget).ToList();
                    foreach (Transform child in __instance.guideObject.transformTarget)
                    {
                        RecurseForNonOCILights(child, allStudioObjects, _visible, __instance.treeNodeObject.textName);
                    }                    
                }
            }
            catch (Exception errLightUpdate) { Instance.Log.LogDebug($"Error updating ALL lighting states: {errLightUpdate.Message}\n{errLightUpdate.StackTrace}"); }
        }

        private static void ReactivateNonOCILights()
        {
            List<Transform> allStudioObjects = Singleton<Studio.Studio>.Instance.dicObjectCtrl.Values.ToList().Select<ObjectCtrlInfo, Transform>(oci => oci.guideObject.transformTarget).ToList();
            foreach (ObjectCtrlInfo oci in Studio.Studio.Instance.dicObjectCtrl.Values)
            {
                foreach (Transform child in oci.guideObject.transformTarget)
                {
                    RecurseForNonOCILights(child, allStudioObjects, true, oci.treeNodeObject.textName);
                }
            }
        }

        private static void RecurseForNonOCILights(Transform item, List<Transform> allStudioObjects, bool _visible, string myName)
        {
            if (allStudioObjects.Contains(item))
                return;

            Light light = item.GetComponent<Light>();
            if (light != null)
            {
                if (Instance.SceneLoading)
                {
                    light.enabled = (!_visible || light.enabled) && _visible;
#if DEBUG
                    ChainLighting.Instance.Log.LogInfo($"Setting Light: {light.gameObject.name} to {_visible} Child of: {myName}");
#endif
                }
                else
                {
                    light.enabled = _visible;
#if DEBUG
                    ChainLighting.Instance.Log.LogInfo($"Setting Light: {light.gameObject.name} to {_visible} Child of: {myName}");
#endif
                }
            }

            foreach (Transform child in item)
            {
                RecurseForNonOCILights(child, allStudioObjects, _visible, myName);
            }
        }

        private void Awake()
        {
            KKAPI.Studio.StudioAPI.StudioLoadedChanged += StudioAPI_StudioLoadedChanged;
            KKAPI.Studio.SaveLoad.StudioSaveLoadApi.RegisterExtraBehaviour<ChainLightingStudioController>(GUID);
        }

        private void StudioAPI_StudioLoadedChanged(object sender, EventArgs e)
        {
            if (linkActiveToggle != null)
                return;

            // UI Stuff Here
            var menu = new SceneEffectsCategory("Chain Lighting - Link Lights Enabled State to Workspace Visibility");
            linkActiveToggle = menu.AddToggleSet("Link Active", (la) => { LinkActive = la;  }, LinkActive);
            controlNonLightsToggle = menu.AddToggleSet("Control Embedded Lights", (cnl) => { ControlEmbeddedLights = cnl; }, ControlEmbeddedLights);
        }

        private SceneEffectsToggleSet linkActiveToggle;
        private SceneEffectsToggleSet controlNonLightsToggle;
    }
}
