using System;
using System.IO;
using System.Threading;
using ClientPlugin.Settings;
using ClientPlugin.Settings.Layouts;
using HarmonyLib;
using Sandbox.Graphics.GUI;
using Shared.Config;
using Shared.Logging;
using Shared.Patches;
using Shared.Plugin;
using VRage.FileSystem;
using VRage.Game;
using VRage.Plugins;
using ClientPlugin.GridList;
using System.Collections.Generic;
using Sandbox.Game.World;
using VRage.Utils;
using System.Threading.Tasks;

namespace ClientPlugin
{
    // ReSharper disable once UnusedType.Global
    public class Plugin : IPlugin, ICommonPlugin
    {
        public const string Name = "Gridmanager";
        public static Plugin Instance { get; private set; }
        private SettingsGenerator settingsGenerator;
        public long Tick { get; private set; }
        private static bool failed;
        private bool _initialized = false;

        public IPluginLogger Log => Logger;
        private static readonly IPluginLogger Logger = new PluginLogger(Name);

        public IPluginConfig Config => config?.Data;
        private PersistentConfig<PluginConfig> config;
        private static readonly string ConfigFileName = $"{Name}.cfg";
        private const ushort MSG_ID_GET_BLOCKS = 42424;
        private TaskCompletionSource<string> _blockListTcs;

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
                // TODO: Save state and close resources here, called when the game exists (not guaranteed!)
                // IMPORTANT: Do NOT call harmony.UnpatchAll() here! It may break other plugins.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dispose failed: {ex}");
            }
            Console.WriteLine("[Dispose] Instance set to null");
            Instance = null;
        }

        public void Update()
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
                    var player = Sandbox.Game.World.MySession.Static.LocalHumanPlayer;
                    var expanded = new Dictionary<long, bool>();
                    var controls = GenerateGridControls(player, expanded);

                    var parent = new MyGuiControlParent();
                    float y = 0f;
                    foreach (var c in controls)
                    {
                        c.Position = new VRageMath.Vector2(0f, y);
                        parent.Controls.Add(c);
                        y += c.Size.Y + 0.01f;
                    }
                    parent.Size = new VRageMath.Vector2(0.4f, Math.Max(0.4f, y));
                    var scroll = new MyGuiControlScrollablePanel(parent)
                    {
                        Position = new VRageMath.Vector2(0f, 0f),
                        Size = new VRageMath.Vector2(0.45f, 0.6f),
                        OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER
                    };
                    scroll.ScrollbarVEnabled = true;

                    var screen = new ClientPlugin.GridList.GridListScreen(
                        "Grid List",
                        () => new List<MyGuiControlBase> { scroll }
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

        private List<Sandbox.Graphics.GUI.MyGuiControlBase> GenerateGridControls(Sandbox.Game.World.MyPlayer player, Dictionary<long, bool> expanded)
        {
            Console.WriteLine("[GenerateGridControls] called");
            var controls = new List<Sandbox.Graphics.GUI.MyGuiControlBase>();
            if (player != null && player.Grids != null)
            {
                foreach (var gridId in player.Grids)
                {
                    string gridName = $"Grid: {gridId}";
                    var entity = Sandbox.ModAPI.MyAPIGateway.Entities.GetEntityById(gridId);
                    var cubeGrid = entity as VRage.Game.ModAPI.IMyCubeGrid;
                    if (cubeGrid != null && !string.IsNullOrEmpty(cubeGrid.DisplayName))
                        gridName = cubeGrid.DisplayName;
                    var btn = new Sandbox.Graphics.GUI.MyGuiControlButton(text: new System.Text.StringBuilder(gridName));
                    btn.UserData = gridId;
                    btn.ButtonClicked += async (b) => {
                        long id = (long)((Sandbox.Graphics.GUI.MyGuiControlButton)b).UserData;
                        Console.WriteLine($"[ButtonClicked] GridId: {id}, GridName: {gridName}");
                        var loadingScreen = new ClientPlugin.GridList.GridDetailScreen(
                            $"Grid Detail: {gridName}",
                            () => new List<Sandbox.Graphics.GUI.MyGuiControlBase> {
                                new Sandbox.Graphics.GUI.MyGuiControlLabel(text: "Loading block list from server...")
                            }
                        );
                        Sandbox.Graphics.GUI.MyGuiSandbox.AddScreen(loadingScreen);
                        try
                        {
                            Console.WriteLine($"[ButtonClicked] Awaiting block list from server for gridId={id}");
                            var blockList = await RequestBlockListFromServerAsync(id);
                            Console.WriteLine($"[ButtonClicked] blockList received: {(blockList == null ? "null" : string.Join(",", blockList))}");
                            var blocksText = blockList != null && blockList.Count > 0
                                ? string.Join("\n", blockList)
                                : "No blocks found.";
                            Console.WriteLine($"[ButtonClicked] blocksText: {blocksText}");
                            var detailScreen = new ClientPlugin.GridList.GridDetailScreen(
                                $"Grid Detail: {gridName}",
                                () => new List<Sandbox.Graphics.GUI.MyGuiControlBase> {
                                    new Sandbox.Graphics.GUI.MyGuiControlLabel(text: blocksText)
                                }
                            );
                            Console.WriteLine($"[ButtonClicked] Showing detailScreen for gridId={id}");
                            Sandbox.Graphics.GUI.MyGuiSandbox.AddScreen(detailScreen);
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
                Console.WriteLine("[GenerateGridControls] No grids found for player.");
                controls.Add(new Sandbox.Graphics.GUI.MyGuiControlLabel(text: "No grids found."));
            }
            return controls;
        }

        private async System.Threading.Tasks.Task<List<string>> RequestBlockListFromServerAsync(long gridId)
        {
            var steamId = Sandbox.ModAPI.MyAPIGateway.Multiplayer.MyId;
            var reqObj = new { grid_id = gridId, steam_id = steamId };
            var reqJson = Newtonsoft.Json.JsonConvert.SerializeObject(reqObj);
            var reqBytes = System.Text.Encoding.UTF8.GetBytes(reqJson);
            var result = new List<string>();
            _blockListTcs = new TaskCompletionSource<string>();

            Sandbox.ModAPI.MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(MSG_ID_GET_BLOCKS, OnGetBlocksResponse);
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
            try
            {
                var task = _blockListTcs.Task;
                if (await Task.WhenAny(task, Task.Delay(5000)) == task)
                    responseJson = task.Result;
                else
                    result.Add("Timeout waiting for server response.");
            }
            finally
            {
                Sandbox.ModAPI.MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(MSG_ID_GET_BLOCKS, OnGetBlocksResponse);
            }
            if (responseJson != null)
            {
                try
                {
                    var obj = Newtonsoft.Json.Linq.JObject.Parse(responseJson);
                    var blocks = obj["blocks"] as Newtonsoft.Json.Linq.JObject;
                    var mainOwner = obj["main_owner_steam_id"]?.ToString();
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