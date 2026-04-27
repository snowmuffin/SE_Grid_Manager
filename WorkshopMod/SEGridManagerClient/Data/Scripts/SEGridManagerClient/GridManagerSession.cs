using System;
using System.Text;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Utils;

namespace SEGridManagerClient
{
    /// <summary>
    /// Client-side session entry point. Registers secure message handlers for server replies
    /// and exposes static helpers to send requests to the Torch Gridmanager plugin.
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public sealed class GridManagerSession : MySessionComponentBase
    {
        /// <summary>Raised for each non-empty server reply (may arrive off the main game thread; UI should marshal).</summary>
        public static event Action<string, string> ServerReply;

        private static bool s_handlersRegistered;

        /// <summary>Register secure handlers if Init ran before Multiplayer was ready; safe to call from the input path each frame.</summary>
        public static void TryRegisterIfNeeded()
        {
            if (s_handlersRegistered)
            {
                return;
            }

            if (MyAPIGateway.Utilities == null)
            {
                return;
            }

            if (MyAPIGateway.Utilities.IsDedicated)
            {
                return;
            }

            if (!ShouldRegisterClientHandlers())
            {
                return;
            }

            if (MyAPIGateway.Multiplayer == null)
            {
                return;
            }

            try
            {
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(GridManagerProtocol.MsgGetBlocks, OnGetBlocksMessage);
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(GridManagerProtocol.MsgGetGrids, OnGetGridsMessage);
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(GridManagerProtocol.MsgBlockDelete, OnBlockDeleteMessage);
                s_handlersRegistered = true;
                MyLog.Default.WriteLine("[SEGridManagerClient] TryRegisterIfNeeded: secure message handlers registered.");
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine($"[SEGridManagerClient] TryRegisterIfNeeded: {ex}");
            }
        }

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            base.Init(sessionComponent);

            if (MyAPIGateway.Utilities.IsDedicated)
            {
                return;
            }

            // Avoid duplicate handler registration on the same process as the Torch host.
            // Remote clients: MultiplayerActive && !IsServer.
            if (!ShouldRegisterClientHandlers())
            {
                MyLog.Default.WriteLine("[SEGridManagerClient] Skipping handler registration (host / dedicated / policy).");
                return;
            }

            try
            {
                if (s_handlersRegistered)
                {
                    return;
                }
                if (MyAPIGateway.Multiplayer == null)
                {
                    return;
                }
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(GridManagerProtocol.MsgGetBlocks, OnGetBlocksMessage);
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(GridManagerProtocol.MsgGetGrids, OnGetGridsMessage);
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(GridManagerProtocol.MsgBlockDelete, OnBlockDeleteMessage);
                s_handlersRegistered = true;
                MyLog.Default.WriteLine("[SEGridManagerClient] Secure message handlers registered.");
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine($"[SEGridManagerClient] Failed to register handlers: {ex}");
            }
        }

        protected override void UnloadData()
        {
            if (!s_handlersRegistered)
            {
                base.UnloadData();
                return;
            }

            try
            {
                if (MyAPIGateway.Multiplayer != null)
                {
                    MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(GridManagerProtocol.MsgGetBlocks, OnGetBlocksMessage);
                    MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(GridManagerProtocol.MsgGetGrids, OnGetGridsMessage);
                    MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(GridManagerProtocol.MsgBlockDelete, OnBlockDeleteMessage);
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine($"[SEGridManagerClient] Unregister handlers: {ex}");
            }

            s_handlersRegistered = false;
            base.UnloadData();
        }

        private static bool ShouldRegisterClientHandlers()
        {
            if (!MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                // Single-player: local game acts as server; plugin may not be present — still allow UI experiments.
                return true;
            }

            // Remote connected client
            return !MyAPIGateway.Multiplayer.IsServer;
        }

        /// <summary>True when the client may send mod messages to a remote game server (not listen-server host).</summary>
        public static bool CanSendRequests()
        {
            if (MyAPIGateway.Multiplayer == null)
            {
                return true;
            }

            if (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                return false;
            }

            return true;
        }

        private static void EmitServerReplyOnGameThread(string kind, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (MyAPIGateway.Utilities != null)
            {
                var copy = text;
                var copyKind = kind;
                MyAPIGateway.Utilities.InvokeOnGameThread(() => ServerReply?.Invoke(copyKind, copy));
            }
            else
            {
                ServerReply?.Invoke(kind, text);
            }
        }

        private static void OnGetBlocksMessage(ushort handlerId, byte[] data, ulong sender, bool fromServer)
        {
            if (data == null || data.Length == 0)
            {
                return;
            }

            try
            {
                var text = Encoding.UTF8.GetString(data);
                MyLog.Default.WriteLine($"[SEGridManagerClient] get-blocks reply ({data.Length} B) fromServer={fromServer}: {text}");
                EmitServerReplyOnGameThread("get-blocks", text);
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine($"[SEGridManagerClient] get-blocks reply parse: {ex}");
            }
        }

        private static void OnGetGridsMessage(ushort handlerId, byte[] data, ulong sender, bool fromServer)
        {
            if (data == null || data.Length == 0)
            {
                return;
            }

            try
            {
                var text = Encoding.UTF8.GetString(data);
                MyLog.Default.WriteLine($"[SEGridManagerClient] get-grids reply ({data.Length} B) fromServer={fromServer}: {text}");
                EmitServerReplyOnGameThread("get-grids", text);
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine($"[SEGridManagerClient] get-grids reply parse: {ex}");
            }
        }

        private static void OnBlockDeleteMessage(ushort handlerId, byte[] data, ulong sender, bool fromServer)
        {
            if (data == null || data.Length == 0)
            {
                return;
            }

            try
            {
                var text = Encoding.UTF8.GetString(data);
                MyLog.Default.WriteLine($"[SEGridManagerClient] block-delete reply ({data.Length} B) fromServer={fromServer}: {text}");
                EmitServerReplyOnGameThread("block-delete", text);
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine($"[SEGridManagerClient] block-delete reply parse: {ex}");
            }
        }

        /// <summary>Request grid list for a Steam user (must match server JSON contract).</summary>
        public static void RequestGetGrids(ulong steamId)
        {
            if (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                MyLog.Default.WriteLine("[SEGridManagerClient] RequestGetGrids ignored on multiplayer server.");
                return;
            }

            var json = "{\"steam_id\":" + steamId + "}";
            var bytes = Encoding.UTF8.GetBytes(json);
            MyAPIGateway.Multiplayer.SendMessageToServer(GridManagerProtocol.MsgGetGrids, bytes, reliable: true);
        }

        /// <summary>Request block summary for a grid (must match server JSON contract).</summary>
        public static void RequestGetBlocks(long gridId, ulong steamId)
        {
            if (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                MyLog.Default.WriteLine("[SEGridManagerClient] RequestGetBlocks ignored on multiplayer server.");
                return;
            }

            var json = "{\"grid_id\":" + gridId + ",\"steam_id\":" + steamId + "}";
            var bytes = Encoding.UTF8.GetBytes(json);
            MyAPIGateway.Multiplayer.SendMessageToServer(GridManagerProtocol.MsgGetBlocks, bytes, reliable: true);
        }

        /// <summary>Request block delete (must match server JSON contract).</summary>
        public static void RequestBlockDelete(long gridId, string blockName, ulong steamId)
        {
            if (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                MyLog.Default.WriteLine("[SEGridManagerClient] RequestBlockDelete ignored on multiplayer server.");
                return;
            }

            var safeName = (blockName ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
            var json = "{\"grid_id\":" + gridId + ",\"block_name\":\"" + safeName + "\",\"steam_id\":" + steamId + "}";
            var bytes = Encoding.UTF8.GetBytes(json);
            MyAPIGateway.Multiplayer.SendMessageToServer(GridManagerProtocol.MsgBlockDelete, bytes, reliable: true);
        }
    }
}
