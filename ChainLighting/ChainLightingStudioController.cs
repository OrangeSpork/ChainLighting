using BepInEx.Logging;
using ExtensibleSaveFormat;
using KKAPI.Studio.SaveLoad;
using Studio;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace ChainLighting
{
    public class ChainLightingStudioController : SceneCustomFunctionController
    {
        private ManualLogSource Log => ChainLighting.Instance.Log;

        protected override void OnSceneLoad(SceneOperationKind operation, KKAPI.Utilities.ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
        {
            if (operation == SceneOperationKind.Clear)
            {
                ChainLighting.Instance.LinkActive = ChainLighting.DefaultEnabled.Value;
                ChainLighting.Instance.ControlEmbeddedLights = ChainLighting.DefaultControlEmbeddedLights.Value;
            }
            else
            {
                if (operation == SceneOperationKind.Load)
                {
                    ChainLighting.Instance.SceneLoading = true;
                    ChainLighting.Instance.LinkActive = false;
                    ChainLighting.Instance.ControlEmbeddedLights = false;
                    PluginData pluginData = GetExtendedData();
                    if (pluginData != null && pluginData.data != null)
                    {
                        if (pluginData.data.TryGetValue("ChainLightingLinked", out object linkedObj))
                            ChainLighting.Instance.LinkActive = (bool)linkedObj;
                        else
                            ChainLighting.Instance.LinkActive = ChainLighting.DefaultEnabled.Value;
                        if (pluginData.data.TryGetValue("ChainLightingControlEmbedded", out object embeddedObj))
                            ChainLighting.Instance.ControlEmbeddedLights = (bool)embeddedObj;
                        else
                            ChainLighting.Instance.ControlEmbeddedLights = ChainLighting.DefaultControlEmbeddedLights.Value;
                    }
                    else
                    {
                        ChainLighting.Instance.LinkActive = ChainLighting.DefaultEnabled.Value;
                        ChainLighting.Instance.ControlEmbeddedLights = ChainLighting.DefaultControlEmbeddedLights.Value;
                    }
                    ChainLighting.Instance.SceneLoading = false;
                }                
            }
        }

        protected override void OnSceneSave()
        {
            PluginData pluginData = new PluginData();
            pluginData.data = new Dictionary<string, object>();
            pluginData.data["ChainLightingLinked"] = ChainLighting.Instance.LinkActive;
            pluginData.data["ChainLightingControlEmbedded"] = ChainLighting.Instance.ControlEmbeddedLights;
            SetExtendedData(pluginData);
        }
    }
}
