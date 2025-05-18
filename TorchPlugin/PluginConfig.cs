using System;
using Shared.Config;
using Torch;
using Torch.Views;

namespace TorchPlugin
{
    [Serializable]
    public class PluginConfig : ViewModel, IPluginConfig
    {
        private bool enabled = true;
        private bool detectCodeChanges = true;
        private bool enableHttpListener = true;
        // TODO: Implement your config fields and add the default values for Torch here.
        //       Be more conservative with changes and introduce new features as disabled
        //       at first, so admins can enable them first on their test deployments.
        //       Once the feature is stable set the default here to true to enable for
        //       newly created Torch deployments.

        [Display(Order = 1, GroupName = "General", Name = "Enable plugin", Description = "Enable the plugin")]
        public bool Enabled
        {
            get => enabled;
            set => SetValue(ref enabled, value);
        }

        [Display(Order = 2, GroupName = "General", Name = "Detect code changes", Description = "Disable the plugin if any changes to the game code are detected before patching")]
        public bool DetectCodeChanges
        {
            get => detectCodeChanges;
            set => SetValue(ref detectCodeChanges, value);
        }

        [Display(Order = 3, GroupName = "General", Name = "Enable HTTP Listener", Description = "Enable or disable the built-in HTTP listener (/ping endpoint)")]
        public bool EnableHttpListener
        {
            get => enableHttpListener;
            set => SetValue(ref enableHttpListener, value);
        }

        // TODO: Encapsulate them as properties and define their Display properties
    }
}