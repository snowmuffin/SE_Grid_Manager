using System;
using System.Text;
using Sandbox.ModAPI;

namespace SEGridManagerClient
{
    internal static class GridManagerProtocol
    {
        public const ushort MsgGetBlocks = 42424;
        public const ushort MsgGetGrids = 42425;
        public const ushort MsgBlockDelete = 42426;
    }

    /// <summary>Secure message handlers; marshals replies onto the game thread for MyGui.</summary>
    internal static class GridManagerNet
    {
        public static event Action<string, string> ServerReply;

        private static bool _registered;

        /// <summary>Call from input path if <see cref="Init"/> ran before Multiplayer was available.</summary>
        public static void TryRegisterIfNeeded()
        {
            RegisterHandlers();
        }

        public static void RegisterHandlers()
        {
            if (_registered)
            {
                return;
            }

            if (MyAPIGateway.Multiplayer == null)
            {
                return;
            }

            try
            {
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(GridManagerProtocol.MsgGetBlocks, OnGetBlocks);
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(GridManagerProtocol.MsgGetGrids, OnGetGrids);
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(GridManagerProtocol.MsgBlockDelete, OnBlockDelete);
                _registered = true;
            }
            catch
            {
            }
        }

        public static void UnregisterHandlers()
        {
            if (!_registered || MyAPIGateway.Multiplayer == null)
            {
                return;
            }

            try
            {
                MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(GridManagerProtocol.MsgGetBlocks, OnGetBlocks);
                MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(GridManagerProtocol.MsgGetGrids, OnGetGrids);
                MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(GridManagerProtocol.MsgBlockDelete, OnBlockDelete);
            }
            catch
            {
            }

            _registered = false;
        }

        private static void Emit(string kind, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var copyKind = kind;
            var copyText = text;
            if (MyAPIGateway.Utilities != null)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(() => ServerReply?.Invoke(copyKind, copyText));
            }
            else
            {
                ServerReply?.Invoke(copyKind, copyText);
            }
        }

        private static void OnGetBlocks(ushort id, byte[] data, ulong sender, bool fromServer)
        {
            if (data == null || data.Length == 0)
            {
                return;
            }

            try
            {
                Emit("get-blocks", Encoding.UTF8.GetString(data));
            }
            catch
            {
            }
        }

        private static void OnGetGrids(ushort id, byte[] data, ulong sender, bool fromServer)
        {
            if (data == null || data.Length == 0)
            {
                return;
            }

            try
            {
                Emit("get-grids", Encoding.UTF8.GetString(data));
            }
            catch
            {
            }
        }

        private static void OnBlockDelete(ushort id, byte[] data, ulong sender, bool fromServer)
        {
            if (data == null || data.Length == 0)
            {
                return;
            }

            try
            {
                Emit("block-delete", Encoding.UTF8.GetString(data));
            }
            catch
            {
            }
        }

        public static void RequestGetGrids(ulong steamId)
        {
            if (MyAPIGateway.Multiplayer == null)
            {
                return;
            }

            if (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                return;
            }

            var json = "{\"steam_id\":" + steamId + "}";
            MyAPIGateway.Multiplayer.SendMessageToServer(GridManagerProtocol.MsgGetGrids, Encoding.UTF8.GetBytes(json), true);
        }

        public static void RequestGetBlocks(long gridId, ulong steamId)
        {
            if (MyAPIGateway.Multiplayer == null)
            {
                return;
            }

            if (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                return;
            }

            var json = "{\"grid_id\":" + gridId + ",\"steam_id\":" + steamId + "}";
            MyAPIGateway.Multiplayer.SendMessageToServer(GridManagerProtocol.MsgGetBlocks, Encoding.UTF8.GetBytes(json), true);
        }

        public static void RequestBlockDelete(long gridId, string blockName, ulong steamId)
        {
            if (MyAPIGateway.Multiplayer == null)
            {
                return;
            }

            if (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                return;
            }

            var safe = (blockName ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
            var json = "{\"grid_id\":" + gridId + ",\"block_name\":\"" + safe + "\",\"steam_id\":" + steamId + "}";
            MyAPIGateway.Multiplayer.SendMessageToServer(GridManagerProtocol.MsgBlockDelete, Encoding.UTF8.GetBytes(json), true);
        }

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
    }
}
