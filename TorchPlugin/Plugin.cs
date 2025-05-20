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
using Newtonsoft.Json.Linq;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
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
        public const string PluginName = "SE_Grid_Manager";
        public const string PluginVersion = "1.0.0";
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

        private const ushort MSG_ID_GET_BLOCKS = 42424;

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
                    Log.Info($"[HTTP] ListenerPrefix: {ListenerPrefix}");
                    httpListener = new System.Net.HttpListener();
                    httpListener.Prefixes.Add(ListenerPrefix);
                    httpListener.Start();
                    httpListener.BeginGetContext(OnHttpRequest, null);
                    Log.Info($"HTTP Listener started at {ListenerPrefix}");
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to start HTTP Listener at {ListenerPrefix}: {ex.Message}");
                    Log.Error($"[HTTP] Config.HttpPort: {Config?.HttpPort}, EnableHttpListener: {Config?.EnableHttpListener}");
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
            var identities = MySession.Static.Players.GetAllIdentities();
            long foundIdentityId = 0;
            string foundDisplayName = null;
            foreach (var identity in identities)
            {
                if (identity == null) continue;
                ulong id = MyAPIGateway.Players.TryGetSteamId(identity.IdentityId);
                if (id == steamId)
                {
                    foundIdentityId = identity.IdentityId;
                    foundDisplayName = identity.DisplayName;
                    break;
                }
            }
            if (foundIdentityId == 0)
            {
                Log.Info($"[PlayerList] No identity found for SteamId: {steamId}");
                return null;
            }
            Log.Info($"[PlayerList] Found identity: {foundDisplayName}, SteamId: {steamId}, IdentityId: {foundIdentityId}");
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            var player = players.FirstOrDefault(p => p.IdentityId == foundIdentityId);
            if (player == null)
            {
                Log.Info($"[PlayerList] No IMyPlayer found for IdentityId: {foundIdentityId}");
            }
            else
            {
                Log.Info($"[PlayerList] Found IMyPlayer: {player.DisplayName}, SteamUserId: {player.SteamUserId}, IdentityId: {player.IdentityId}");
            }
            return player;
        }

        public async Task<List<IMyCubeGrid>> GetGridsWithPlayerBlocksAsync(long identityId)
        {
            return await Task.Run(() => {
                var result = new List<IMyCubeGrid>();
                if (identityId == 0)
                    return result;

                var entities = new HashSet<IMyEntity>();
                MyAPIGateway.Entities.GetEntities(entities, e => e is IMyCubeGrid);
                foreach (var entity in entities)
                {
                    var cubeGrid = entity as IMyCubeGrid;
                    if (cubeGrid == null) continue;
                    var blocks = new List<IMySlimBlock>();
                    cubeGrid.GetBlocks(blocks);
                    if (blocks.Any(b => b.OwnerId == identityId))
                    {
                        result.Add(cubeGrid);
                    }
                }
                Log.Info($"[GridList] Found {result.Count} grids for IdentityId: {identityId}");
                foreach (var grid in result)
                {
                    Log.Info($"[Grid] Name: {grid.DisplayName}, EntityId: {grid.EntityId}");
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
                        Log.Info($"[HTTP] /Update-Grid body: {body}");
                        ulong steamId = 0;
                        bool valid = false;
                        try
                        {
                            // JSON 파싱 시도
                            if (body.Trim().StartsWith("{"))
                            {
                                var obj = Newtonsoft.Json.Linq.JObject.Parse(body);
                                JToken idToken = null;
                                if (obj.TryGetValue("steamId", out idToken) || obj.TryGetValue("steam_id", out idToken))
                                {
                                    valid = ulong.TryParse(idToken.ToString(), out steamId);
                                }
                            }
                            else
                            {
                                valid = ulong.TryParse(body, out steamId);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[HTTP] JSON parse error: {ex.Message}");
                        }
                        Log.Info($"[HTTP] Parsed steamId: {steamId}, valid: {valid}");
                        var identityId = valid ? MyAPIGateway.Players.TryGetIdentityId(steamId) : 0;
                        Log.Info($"[HTTP] IdentityId found: {identityId}");
                        string result = (valid && identityId != 0) ? "success" : "fail";
                        var buffer = Encoding.UTF8.GetBytes(result);
                        response.ContentLength64 = buffer.Length;
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                        response.OutputStream.Close();
                        // 비동기로 그리드 조회 및 전송
                        if (valid && identityId != 0)
                        {
                            Task.Run(async () =>
                            {
                                var grids = await GetGridsWithPlayerBlocksAsync(identityId);
                                await NotifyGridsAsync(steamId, grids);
                            });
                        }
                    }
                }
                else if (request.Url.AbsolutePath == "/get-blocks" && request.HttpMethod == "POST")
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        var body = reader.ReadToEnd();
                        Log.Info($"[HTTP] /get-blocks body: {body}");
                        long gridId = 0;
                        ulong steamId = 0;
                        try
                        {
                            var obj = JObject.Parse(body);
                            JToken gridToken = null, steamToken = null;
                            if (obj.TryGetValue("grid_id", out gridToken))
                                long.TryParse(gridToken.ToString(), out gridId);
                            if (obj.TryGetValue("steam_id", out steamToken))
                                ulong.TryParse(steamToken.ToString(), out steamId);
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[HTTP] /get-blocks JSON parse error: {ex.Message}");
                        }
                        Log.Info($"[HTTP] Parsed gridId: {gridId}, steamId: {steamId}");
                        var entity = MyAPIGateway.Entities.GetEntityById(gridId) as IMyCubeGrid;
                        var resultDict = new Dictionary<string, int>();
                        ulong mainOwnerSteamId = 0;
                        if (entity != null)
                        {
                            // 주 소유자
                            var bigOwner = entity.BigOwners.FirstOrDefault();
                            mainOwnerSteamId = MyAPIGateway.Players.TryGetSteamId(bigOwner);
                            // 블럭 집계
                            var blocks = new List<IMySlimBlock>();
                            entity.GetBlocks(blocks);
                            foreach (var block in blocks)
                            {
                                if (block.OwnerId == 0) continue;
                                var blockSteamId = MyAPIGateway.Players.TryGetSteamId(block.OwnerId);
                                if (blockSteamId == steamId)
                                {
                                    var name = block.FatBlock?.DisplayNameText ?? block.BlockDefinition.ToString();
                                    if (!resultDict.ContainsKey(name))
                                        resultDict[name] = 0;
                                    resultDict[name]++;
                                }
                            }
                        }
                        var responseObj = new
                        {
                            main_owner_steam_id = mainOwnerSteamId,
                            blocks = resultDict
                        };
                        var json = JsonConvert.SerializeObject(responseObj);
                        var buffer = Encoding.UTF8.GetBytes(json);
                        response.ContentType = "application/json";
                        response.ContentLength64 = buffer.Length;
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                        response.OutputStream.Close();
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

        private void OnGetBlocksMessage(ushort handlerId, byte[] data, ulong sender, bool fromServer)
        {
            Log.Info($"[ModAPI] OnGetBlocksMessage received: handlerId={handlerId}, sender={sender}, fromServer={fromServer}, dataLength={data?.Length}");
            try
            {
                // 클라이언트가 보낸 요청 파싱
                var json = Encoding.UTF8.GetString(data);
                var obj = JObject.Parse(json);
                long gridId = obj["grid_id"]?.ToObject<long>() ?? 0;
                ulong steamId = obj["steam_id"]?.ToObject<ulong>() ?? 0;
                Log.Info($"[ModAPI] Received get-blocks: gridId={gridId}, steamId={steamId}, sender={sender}");

                var entity = MyAPIGateway.Entities.GetEntityById(gridId) as IMyCubeGrid;
                var resultDict = new Dictionary<string, int>();
                ulong mainOwnerSteamId = 0;
                if (entity != null)
                {
                    var bigOwner = entity.BigOwners.FirstOrDefault();
                    mainOwnerSteamId = MyAPIGateway.Players.TryGetSteamId(bigOwner);
                    var blocks = new List<IMySlimBlock>();
                    entity.GetBlocks(blocks);
                    foreach (var block in blocks)
                    {
                        if (block.OwnerId == 0) continue;
                        var blockSteamId = MyAPIGateway.Players.TryGetSteamId(block.OwnerId);
                        if (blockSteamId == steamId)
                        {
                            var name = block.FatBlock?.DisplayNameText ?? block.BlockDefinition.ToString();
                            if (!resultDict.ContainsKey(name))
                                resultDict[name] = 0;
                            resultDict[name]++;
                        }
                    }
                }
                var responseObj = new
                {
                    main_owner_steam_id = mainOwnerSteamId,
                    blocks = resultDict
                };
                var responseJson = JsonConvert.SerializeObject(responseObj);
                var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                // 클라이언트에게 응답 전송 (반드시 sender로!)
                Sandbox.ModAPI.MyAPIGateway.Multiplayer.SendMessageTo(MSG_ID_GET_BLOCKS, responseBytes, sender);
            }
            catch (Exception ex)
            {
                Log.Error($"[ModAPI] get-blocks handler error: {ex.Message}");
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
                    var notifyUrl = (Config?.WebHostAddress ?? "http://localhost") + ":" + (Config?.HttpPort ?? 8080) + "/notify-grids";
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
                    Sandbox.ModAPI.MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(MSG_ID_GET_BLOCKS, OnGetBlocksMessage);
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