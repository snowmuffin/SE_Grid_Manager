using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ClientPlugin.GridList;
using ClientPlugin.Settings;
using ClientPlugin.Settings.Layouts;
using HarmonyLib;
using Sandbox.Definitions;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using Shared.Config;
using Shared.Logging;
using Shared.Patches;
using Shared.Plugin;
using VRage.FileSystem;
using VRage.Game;
using VRage.Plugins;
using VRage.Utils;

namespace ClientPlugin
{
    // ReSharper disable once UnusedType.Global
    public class Plugin : IPlugin, ICommonPlugin
    {
        public const string Name = "Gridmanager";
        public static Plugin Instance { get; private set; }
        private static readonly IPluginLogger Logger = new PluginLogger(Name);
        private static readonly string ConfigFileName = $"{Name}.cfg";
        private const ushort MSG_ID_GET_BLOCKS = 42424;
        private const ushort MSG_ID_GET_GRIDS = 42425;

        private static bool failed;

        private SettingsGenerator settingsGenerator;
        private PersistentConfig<PluginConfig> config;
        private TaskCompletionSource<string> _blockListTcs;
        private bool _isBlockListHandlerRegistered = false;
        private bool _initialized = false;
        private TaskCompletionSource<string> _gridListTcs;


        public long Tick { get; private set; }
        public IPluginLogger Log => Logger;
        public IPluginConfig Config => config?.Data;

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Init(object gameInstance)
        {
            AllocConsole();
            var stdOut = Console.OpenStandardOutput();
            var writer = new StreamWriter(stdOut) { AutoFlush = true };
            Console.SetOut(writer);
            Console.WriteLine("=== GridManager Plugin Console Initialized ===");
#if DEBUG
            // Allow the debugger some time to connect once the plugin assembly is loaded
            Thread.Sleep(100);
#endif
            Instance = this;
            Instance.settingsGenerator = new SettingsGenerator();

            Console.WriteLine("Loading");
            Console.WriteLine("[Init] Loading");

            var configPath = Path.Combine(MyFileSystem.UserDataPath, ConfigFileName);
            config = PersistentConfig<PluginConfig>.Load(Log, configPath);

            var gameVersion = MyFinalBuildConstants.APP_VERSION_STRING.ToString();
            Common.SetPlugin(this, gameVersion, MyFileSystem.UserDataPath);


            if (!PatchHelpers.HarmonyPatchAll(Log, new Harmony(Name)))
            {
                failed = true;
                return;
            }

            Console.WriteLine("Successfully loaded");
        }

        public void Dispose()
        {
            try
            {
                Console.WriteLine("[Dispose] Called");

                if (_isBlockListHandlerRegistered)
                {
                    Sandbox.ModAPI.MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(MSG_ID_GET_GRIDS, OnGetBlocksResponse);
                    _isBlockListHandlerRegistered = false;
                }
                if (_isGridListHandlerRegistered)
                {
                    Sandbox.ModAPI.MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(MSG_ID_GET_GRIDS, OnGetGridsResponse);
                    _isGridListHandlerRegistered = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dispose failed: {ex}");
            }
            Console.WriteLine("[Dispose] Instance set to null");
            Instance = null;
        }

        public async void Update()
        {
            if (failed)
            {
                Console.WriteLine("[Update] failed flag is set, skipping update.");
                return;
            }
            try
            {
                if (Sandbox.ModAPI.MyAPIGateway.Input != null &&
                    Sandbox.ModAPI.MyAPIGateway.Input.IsKeyPress(VRage.Input.MyKeys.LeftControl) &&
                    Sandbox.ModAPI.MyAPIGateway.Input.IsNewKeyPressed(VRage.Input.MyKeys.G))
                {
                    Console.WriteLine("[Update] Ctrl+G pressed, opening grid list UI.");
                    var controls = await GenerateGridControlsAsync();
                    var parent = new MyGuiControlParent();
                    float y = 0f;

                    foreach (var c in controls)
                    {
                        list.Controls.Add(c);
                    }
                    var labelCount = new MyGuiControlLabel(text: $"Total items: {controls.Count}") { Position = new VRageMath.Vector2(-0.3f, -0.35f) };
                    var screen = new GridListScreen(
                        "Grid List",
                        () => new List<MyGuiControlBase> {
                            labelCount, list
                        }
                    );
                    MyGuiSandbox.AddScreen(screen);
                }
                CustomUpdate();
                Tick++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Update] {ex}");
                Sandbox.ModAPI.MyAPIGateway.Utilities.ShowMessage("GridManager", $"Update failed: {ex.Message}");
                failed = true;
            }
        }

        private async Task<List<MyGuiControlBase>> GenerateGridControlsAsync()
        {
            Console.WriteLine("[GenerateGridControlsAsync] called");
            var controls = new List<MyGuiControlBase>();
            var gridList = await RequestGridListFromServerAsync();
            if (gridList != null && gridList.Count > 0)
            {
                foreach (var gridInfo in gridList)
                {
                    string gridName = gridInfo;
                    long gridId = 0;

                    int idx = gridInfo.LastIndexOf("(ID: ");
                    if (idx >= 0)
                    {
                        int endIdx = gridInfo.LastIndexOf(")");
                        if (endIdx > idx)
                        {
                            var idStr = gridInfo.Substring(idx + 5, endIdx - (idx + 5));
                            long.TryParse(idStr.Trim(), out gridId);
                            gridName = gridInfo.Substring(0, idx).Trim();
                        }
                    }
                    var btn = new MyGuiControlButton(text: new System.Text.StringBuilder(gridName));
                    btn.UserData = gridId;
                    btn.ButtonClicked += async (b) =>
                    {
                        long id = (long)((MyGuiControlButton)b).UserData;
                        Console.WriteLine($"[ButtonClicked] GridId: {id}, GridName: {gridName}");
                        try
                        {
                            Console.WriteLine($"[ButtonClicked] Awaiting block list from server for gridId={id}");
                            var blockList = await RequestBlockListFromServerAsync(id);
                            Console.WriteLine($"[ButtonClicked] blockList received: {(blockList == null ? "null" : string.Join(",", blockList))}");
                            var blocksText = blockList != null && blockList.Count > 0
                                ? string.Join("\n", blockList)
                                : "No blocks found.";
                            // MyGuiControlList로 상세 목록 구현
                            var detailList = new MyGuiControlList(
                                position: new VRageMath.Vector2(0.5f, 0.5f),
                                size: new VRageMath.Vector2(0.35f, 0.6f),
                                backgroundColor: null,
                                toolTip: null,
                                visualStyle: 0
                            );
                            if (blockList != null && blockList.Count > 0)
                            {
                                foreach (var block in blockList)
                                {
                                    // block: "BlockName: SteamId" 형식 가정
                                    var label = new MyGuiControlLabel(text: block);
                                    var parts = block.Split(':');
                                    string blockName = parts[0].Trim();
                                    string blockSteamId = parts.Length > 1 ? parts[1].Trim() : "";
                                    var actionBtn = new MyGuiControlButton(text: new System.Text.StringBuilder("Action"));
                                    actionBtn.UserData = new Tuple<long, string>(id, blockName); // blockSteamId -> blockName
                                    actionBtn.ButtonClicked += (btn2) =>
                                    {
                                        var tuple = (Tuple<long, string>)((MyGuiControlButton)btn2).UserData;
                                        SendBlockActionMessageAsync(tuple.Item1, tuple.Item2); // tuple.Item2는 blockName
                                    };
                                    label.Position = new VRageMath.Vector2(-0.13f, 0f);
                                    actionBtn.Position = new VRageMath.Vector2(0.12f, 0f);
                                    detailList.Controls.Add(label);
                                    detailList.Controls.Add(actionBtn);
                                }
                            }
                            else
                            {
                                detailList.Controls.Add(new MyGuiControlLabel(text: "No blocks found."));
                            }
                            var detailScreen = new GridDetailScreen(
                                $"Grid Detail: {gridName}",
                                (Func<List<MyGuiControlBase>>)(() => new List<MyGuiControlBase> {
                                    detailList
                                })
                            );
                            Console.WriteLine($"[ButtonClicked] Showing detailScreen for gridId={id}");
                            MyGuiSandbox.AddScreen(detailScreen);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ButtonClicked] Exception: {ex}");
                        }
                    };
                    controls.Add(btn);
                }
            }
            else
            {
                Console.WriteLine("[GenerateGridControlsAsync] No grids found for player.");
                controls.Add(new MyGuiControlLabel(text: "No grids found."));
            }
            return controls;
        }
        public string GetDisplayNameFromDefinitionIdString(string defIdString)
        {
            if (string.IsNullOrWhiteSpace(defIdString)) return "Invalid Block Definition";
            string[] parts = null;
            if (defIdString.Contains("/"))
                parts = defIdString.Split('/');
            else if (defIdString.Contains(" "))
                parts = defIdString.Split(' ');
            else if (defIdString.Contains("_"))
                parts = defIdString.Split('_');
            else
                parts = new[] { defIdString };

            if (parts.Length == 2)
            {
                try
                {
                    var type = VRage.ObjectBuilders.MyObjectBuilderType.Parse(parts[0]);
                    var defId = new MyDefinitionId(type, parts[1]);
                    var def = MyDefinitionManager.Static.GetDefinition(defId);
                    if (def == null)
                    {
                        Log.Info($"[GetDisplayNameFromDefinitionIdString] Definition not found for: {defIdString}");
                        return $"Unknown ({defIdString})";
                    }
                    if (string.IsNullOrWhiteSpace(def.DisplayNameText))
                        return defId.ToString();
                    return def.DisplayNameText;
                }
                catch (Exception ex)
                {
                    Log.Error($"[GetDisplayNameFromDefinitionIdString] Exception for '{defIdString}': {ex.Message}");
                }
            }
            else
            {
                Log.Info($"[GetDisplayNameFromDefinitionIdString] Unexpected format: {defIdString}");
            }
            return $"Unknown Block ({defIdString})";
        }
        private async Task<List<string>> RequestGridListFromServerAsync()
        {
            var steamId = Sandbox.ModAPI.MyAPIGateway.Multiplayer.MyId;
            var reqObj = new { steam_id = steamId };
            var reqJson = Newtonsoft.Json.JsonConvert.SerializeObject(reqObj);
            var reqBytes = System.Text.Encoding.UTF8.GetBytes(reqJson);
            var result = new List<string>();
            _gridListTcs = new TaskCompletionSource<string>();

            if (!_isGridListHandlerRegistered)
            {
                Sandbox.ModAPI.MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(MSG_ID_GET_GRIDS, OnGetGridsResponse);
                _isGridListHandlerRegistered = true;
            }
            try
            {
                Console.WriteLine($"[Client] Sending grid list request: steamId={steamId}, reqJson={reqJson}");
                bool sent = Sandbox.ModAPI.MyAPIGateway.Multiplayer.SendMessageToServer(MSG_ID_GET_GRIDS, reqBytes);
                Console.WriteLine($"[Client] SendMessageToServer returned: {sent}");
                if (!sent)
                {
                    Console.WriteLine("[Client] SendMessageToServer failed to send message!");
                    result.Add("SendMessageToServer failed to send message!");
                    return result;
                }
            }
            catch (Exception ex)
            {
                var msg = $"Failed to send grid list request: {ex.Message}";
                Console.WriteLine(msg);
                result.Add(msg);
                return result;
            }

            string responseJson = null;
            var task = _gridListTcs.Task;
            if (await Task.WhenAny(task, Task.Delay(5000)) == task)
                responseJson = task.Result;
            else
                result.Add("Timeout waiting for server response.");

            if (responseJson != null)
            {
                try
                {
                    var obj = Newtonsoft.Json.Linq.JObject.Parse(responseJson);
                    var grids = obj["grids"] as Newtonsoft.Json.Linq.JArray;
                    if (grids != null)
                    {
                        foreach (var grid in grids)
                        {
                            var name = grid["name"]?.ToString() ?? "Unnamed Grid";
                            var entityId = grid["entity_id"]?.ToString() ?? "?";
                            result.Add($"{name} (ID: {entityId})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing server response: {ex.Message}");
                    result.Add($"Error parsing server response: {ex.Message}");
                }
            }
            return result;
        }
        private async Task<List<string>> RequestBlockListFromServerAsync(long gridId)
        {
            var steamId = Sandbox.ModAPI.MyAPIGateway.Multiplayer.MyId;
            var reqObj = new { grid_id = gridId, steam_id = steamId };
            var reqJson = Newtonsoft.Json.JsonConvert.SerializeObject(reqObj);
            var reqBytes = System.Text.Encoding.UTF8.GetBytes(reqJson);
            var result = new List<string>();
            _blockListTcs = new TaskCompletionSource<string>();

            if (!_isBlockListHandlerRegistered)
            {
                Sandbox.ModAPI.MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(MSG_ID_GET_BLOCKS, OnGetBlocksResponse);
                _isBlockListHandlerRegistered = true;
            }
            try
            {
                Console.WriteLine($"[Client] Preparing to send block list request:");
                Console.WriteLine($"  gridId: {gridId}");
                Console.WriteLine($"  steamId: {steamId}");
                Console.WriteLine($"  reqJson: {reqJson}");
                Console.WriteLine($"  reqBytes.Length: {reqBytes.Length}");
                Sandbox.ModAPI.MyAPIGateway.Utilities.ShowMessage("GridManager", $"[Send] gridId={gridId}, steamId={steamId}, bytes={reqBytes.Length}");
                bool sent = Sandbox.ModAPI.MyAPIGateway.Multiplayer.SendMessageToServer(MSG_ID_GET_BLOCKS, reqBytes);
                Console.WriteLine($"[Client] SendMessageToServer returned: {sent}");
                Sandbox.ModAPI.MyAPIGateway.Utilities.ShowMessage("GridManager", $"SendMessageToServer returned: {sent}");
                if (!sent)
                {
                    Console.WriteLine("[Client] SendMessageToServer failed to send message!");
                    Sandbox.ModAPI.MyAPIGateway.Utilities.ShowMessage("GridManager", "SendMessageToServer failed to send message!");
                }
            }
            catch (Exception ex)
            {
                var msg = $"Failed to send mod message: {ex.Message}";
                Console.WriteLine(msg);
                Sandbox.ModAPI.MyAPIGateway.Utilities.ShowMessage("GridManager", msg);
                result.Add(msg);
                return result;
            }

            string responseJson = null;
            var task = _blockListTcs.Task;
            if (await Task.WhenAny(task, Task.Delay(5000)) == task)
                responseJson = task.Result;
            else
                result.Add("Timeout waiting for server response.");

            if (responseJson != null)
            {
                try
                {
                    var obj = Newtonsoft.Json.Linq.JObject.Parse(responseJson);
                    var blocks = obj["blocks"] as Newtonsoft.Json.Linq.JObject;
                    var mainOwner = obj["main_owner_name"]?.ToString();
                    if (blocks != null)
                    {
                        foreach (var prop in blocks.Properties())
                        {
                            result.Add($"{prop.Name}: {prop.Value}");
                        }
                    }
                    if (!string.IsNullOrEmpty(mainOwner))
                    {
                        result.Insert(0, $"Main Owner SteamId: {mainOwner}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing server response: {ex.Message}");
                    result.Add($"Error parsing server response: {ex.Message}");
                }
            }
            return result;
        }
        private void OnGetGridsResponse(ushort handlerId, byte[] data, ulong sender, bool fromServer)
        {
            try
            {
                var json = System.Text.Encoding.UTF8.GetString(data);
                Console.WriteLine($"[OnGetGridsResponse] Received data from server. Length: {data?.Length}");
                _gridListTcs?.TrySetResult(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to process mod message: {ex.Message}");
                Sandbox.ModAPI.MyAPIGateway.Utilities.ShowMessage("GridManager", $"Failed to process mod message: {ex.Message}");
            }
        }
        private void OnGetBlocksResponse(ushort handlerId, byte[] data, ulong sender, bool fromServer)
        {
            try
            {
                var json = System.Text.Encoding.UTF8.GetString(data);
                Console.WriteLine($"[OnGetBlocksResponse] Received data from server. Length: {data?.Length}");
                _blockListTcs?.TrySetResult(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to process mod message: {ex.Message}");
                Sandbox.ModAPI.MyAPIGateway.Utilities.ShowMessage("GridManager", $"Failed to process mod message: {ex.Message}");
            }
        }

        private async void SendBlockActionMessageAsync(long gridId, string blockName)
        {
            var steamId = Sandbox.ModAPI.MyAPIGateway.Multiplayer.MyId;
            var reqObj = new { grid_id = gridId, block_name = blockName, steam_id = steamId };
            var reqJson = Newtonsoft.Json.JsonConvert.SerializeObject(reqObj);
            var reqBytes = System.Text.Encoding.UTF8.GetBytes(reqJson);
            if (!_isBlockListHandlerRegistered)
            {
                Sandbox.ModAPI.MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(MSG_ID_BLOCK_ACTION, OnBlockActionResponse);
                _isBlockListHandlerRegistered = true;
            }
            try
            {
                Console.WriteLine($"[Client] Sending block action message: gridId={gridId}, blockName={blockName}, steamId={steamId}");
                bool sent = Sandbox.ModAPI.MyAPIGateway.Multiplayer.SendMessageToServer(MSG_ID_BLOCK_ACTION, reqBytes);
                Console.WriteLine($"[Client] SendMessageToServer returned: {sent}");
                if (!sent)
                {
                    Console.WriteLine("[Client] SendMessageToServer failed to send block action message!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send block action message: {ex.Message}");
            }
        }
        private void OnBlockActionResponse(ushort handlerId, byte[] data, ulong sender, bool fromServer)
        {
            try
            {
                var json = System.Text.Encoding.UTF8.GetString(data);
                Console.WriteLine($"[OnBlockActionResponse] Received data from server. Length: {data?.Length}");
                Sandbox.ModAPI.MyAPIGateway.Utilities.ShowMessage("GridManager", $"Block action response: {json}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to process block action response: {ex.Message}");
                Sandbox.ModAPI.MyAPIGateway.Utilities.ShowMessage("GridManager", $"Failed to process block action response: {ex.Message}");
            }
        }

        private void CustomUpdate()
        {
            PatchHelpers.PatchUpdates();
        }

        public void OpenConfigDialog()
        {
            Instance.settingsGenerator.SetLayout<Simple>();
            MyGuiSandbox.AddScreen(Instance.settingsGenerator.Dialog);
        }
    }
}