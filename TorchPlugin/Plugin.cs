#define USE_HARMONY

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using HarmonyLib;
using Newtonsoft.Json;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.ModAPI;
using Shared.Config;
using Shared.Logging;
using Shared.Patches;
using Shared.Plugin;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Session;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace TorchPlugin
{
    public sealed class Plugin : TorchPluginBase, IWpfPlugin
    {
        public const string PluginName = "Se_web";
        public static Plugin Instance { get; private set; }
        public long Tick { get; private set; }

        private static readonly IPluginLogger Logger = new PluginLogger("Se_web");
        public IPluginLogger Log => Logger;

        public PluginConfig Config => _config?.Data;
        private static readonly string ConfigFileName = $"{PluginName}.cfg";

        public UserControl GetControl() => _control ?? (_control = new ConfigView(this));
        private Persistent<PluginConfig> _config;
        private ConfigView _control;
        private TorchSessionManager sessionManager;
        private bool initialized;
        private bool failed;




        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            SetupConfig();

            sessionManager = torch.Managers.GetManager<TorchSessionManager>();
            sessionManager.SessionStateChanged += SessionStateChanged;

            initialized = true;
        }

        private void SetupConfig()
        {
            var configFile = Path.Combine(StoragePath,ConfigFileName);

            try
            {
                _config = Persistent<PluginConfig>.Load(configFile);
            }
            catch (Exception e)
            {
                Log.Info(e.Message);
            }

            if (_config?.Data == null)
            {
                Log.Info("Create Default Config, because none was found!");

                _config = new Persistent<PluginConfig>(configFile, new PluginConfig());
                Save();
            }
        }

        private ulong GetSteamIdFromEntity(IMyEntity entity)
        {
            switch (entity)
            {
                case MyCharacter character when character.ControllerInfo != null:
                    var players = new List<IMyPlayer>();
                    MyAPIGateway.Players.GetPlayers(players);
                    return players.FirstOrDefault(p => p.Character == character)?.SteamUserId ?? 0;

                case IMyCubeGrid grid:
                    long ownerId = grid.BigOwners.FirstOrDefault();
                    return MyAPIGateway.Players.TryGetSteamId(ownerId);

                case IMyCubeBlock block:
                    return MyAPIGateway.Players.TryGetSteamId(block.OwnerId);

                case IMyGunBaseUser gunBaseUser:
                    return MyAPIGateway.Players.TryGetSteamId(gunBaseUser.OwnerId);

                default:
                    return 0;
            }
        }


        public void Save()
        {
            try
            {
                _config.Save();
                Log.Info("Configuration Saved.");
            }
            catch (IOException e)
            {
                Log.Info(e, "Configuration failed to save");
            }
        }
        private void SessionStateChanged(ITorchSession session, TorchSessionState newstate)
        {
            switch (newstate)
            {
                case TorchSessionState.Loading:
                    Log.Debug("Loading");
                    break;

                case TorchSessionState.Loaded:

                    break;

                case TorchSessionState.Unloading:
                    Log.Debug("Unloading");

                    break;

                case TorchSessionState.Unloaded:
                    Log.Debug("Unloaded");
                    break;
            }
        }

        public override void Dispose()
        {
            if (initialized)
            {
                Log.Debug("Disposing");

                sessionManager.SessionStateChanged -= SessionStateChanged;
                sessionManager = null;

                Log.Debug("Disposed");
            }

            Instance = null;

            base.Dispose();
        }

        public override void Update()
        {
            if (failed)
            {
                return;
            }

            try
            {
                CustomUpdate();
                Tick++;
            }
            catch (Exception e)
            {
                Log.Critical(e, "Update failed");
                failed = true;
            }
        }

        private void CustomUpdate()
        {
            PatchHelpers.PatchUpdates();
        }
    }
}