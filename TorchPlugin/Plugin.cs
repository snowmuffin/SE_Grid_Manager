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

        private System.Net.HttpListener httpListener;
        private string ListenerPrefix => $"http://localhost:{Config?.HttpPort ?? 8080}/";

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            SetupConfig();

            sessionManager = torch.Managers.GetManager<TorchSessionManager>();
            sessionManager.SessionStateChanged += SessionStateChanged;

            initialized = true;

            // Start HTTP listener if enabled
            if (Config != null && Config.EnableHttpListener)
            {
                try
                {
                    httpListener = new System.Net.HttpListener();
                    httpListener.Prefixes.Add(ListenerPrefix);
                    httpListener.Start();
                    httpListener.BeginGetContext(OnHttpRequest, null);
                    Log.Info($"HTTP Listener started at {ListenerPrefix}");
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to start HTTP Listener: {ex.Message}");
                }
            }
        }

        private void SetupConfig()
        {
            var configFile = Path.Combine(StoragePath, ConfigFileName);

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

        public IMyPlayer GetPlayerBySteamId(ulong steamId)
        {
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            return players.FirstOrDefault(p => p.SteamUserId == steamId);
        }

        public async Task<List<IMyCubeGrid>> GetGridsWithPlayerBlocksAsync(IMyPlayer player)
        {
            return await Task.Run(() => {
                var result = new List<IMyCubeGrid>();
                if (player == null)
                    return result;

                var entities = new HashSet<IMyEntity>();
                MyAPIGateway.Entities.GetEntities(entities, e => e is IMyCubeGrid);
                foreach (var entity in entities)
                {
                    var cubeGrid = entity as IMyCubeGrid;
                    if (cubeGrid == null) continue;
                    var blocks = new List<IMySlimBlock>();
                    cubeGrid.GetBlocks(blocks);
                    if (blocks.Any(b => b.OwnerId == player.IdentityId))
                    {
                        result.Add(cubeGrid);
                    }
                }
                return result;
            });
        }

        private void OnHttpRequest(IAsyncResult ar)
        {
            if (httpListener == null || !httpListener.IsListening)
                return;
            System.Net.HttpListenerContext context = null;
            try
            {
                context = httpListener.EndGetContext(ar);
                httpListener.BeginGetContext(OnHttpRequest, null);
                var request = context.Request;
                var response = context.Response;

                if (request.Url.AbsolutePath == "/ping")
                {
                    var buffer = System.Text.Encoding.UTF8.GetBytes("pong");
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                    response.OutputStream.Close();
                }
                else if (request.Url.AbsolutePath == "/Update-Grid" && request.HttpMethod == "POST")
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        var body = reader.ReadToEnd();
                        ulong steamId;
                        bool valid = ulong.TryParse(body, out steamId);
                        var player = valid ? GetPlayerBySteamId(steamId) : null;
                        string result = (valid && player != null) ? "success" : "fail";
                        var buffer = Encoding.UTF8.GetBytes(result);
                        response.ContentLength64 = buffer.Length;
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                        response.OutputStream.Close();
                        // 비동기로 그리드 조회 및 전송
                        if (valid && player != null)
                        {
                            Task.Run(async () =>
                            {
                                var grids = await GetGridsWithPlayerBlocksAsync(player);
                                await NotifyGridsAsync(steamId, grids);
                            });
                        }
                    }
                }
                else
                {
                    response.StatusCode = 404;
                    response.OutputStream.Close();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"HTTP Listener error: {ex.Message}");
                try { context?.Response.OutputStream.Close(); } catch { }
            }
        }

        private async Task NotifyGridsAsync(ulong steamId, List<IMyCubeGrid> grids)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var gridInfos = grids.Select(g => new {
                        name = g.DisplayName,
                        entity_id = g.EntityId
                    }).ToList();
                    var payload = new
                    {
                        steam_id = steamId,
                        grids = gridInfos,
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var notifyUrl = (Config?.NotifyHostAddress ?? "http://localhost") + ":" + (Config?.HttpPort ?? 8080) + "/notify-grids";
                    await client.PostAsync(notifyUrl, content);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"NotifyGridsAsync error: {ex.Message}");
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

                // Stop HTTP listener
                if (httpListener != null)
                {
                    try
                    {
                        httpListener.Stop();
                        httpListener.Close();
                        Log.Info("HTTP Listener stopped");
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Error stopping HTTP Listener: {ex.Message}");
                    }
                    httpListener = null;
                }

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