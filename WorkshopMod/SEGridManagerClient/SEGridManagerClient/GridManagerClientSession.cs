using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Input;
using VRage.ModAPI;

namespace SEGridManagerClient
{
    /// <summary>Registers network handlers and drives MyGui grid list + detail screens.</summary>
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class GridManagerClientSession : MySessionComponentBase
    {
        private readonly List<GridManagerParse.GridRow> _workGrids = new List<GridManagerParse.GridRow>();
        private readonly List<GridManagerParse.GridRow> _lastGrids = new List<GridManagerParse.GridRow>();
        private long _pendingGetBlocksForGridId;
        private string _pendingGetBlocksGridName = string.Empty;
        private long _currentDetailGridId;
        private string _currentDetailGridName = string.Empty;
        private string _lastMainOwner = string.Empty;
        private Dictionary<string, int> _lastBlockSnapshot = new Dictionary<string, int>(StringComparer.Ordinal);

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            base.Init(sessionComponent);
            if (MyAPIGateway.Utilities.IsDedicated)
            {
                return;
            }

            if (!ShouldRunClient())
            {
                return;
            }

            GridManagerNet.ServerReply += OnServerReply;
            GridManagerNet.RegisterHandlers();
        }

        protected override void UnloadData()
        {
            if (ShouldRunClient())
            {
                try
                {
                    GridManagerNet.ServerReply -= OnServerReply;
                }
                catch
                {
                }

                GridManagerNet.UnregisterHandlers();
            }

            base.UnloadData();
        }

        private static bool ShouldRunClient()
        {
            if (MyAPIGateway.Multiplayer == null)
            {
                return true;
            }

            if (MyAPIGateway.Multiplayer.MultiplayerActive && MyAPIGateway.Multiplayer.IsServer)
            {
                return false;
            }

            return true;
        }

        public override void HandleInput()
        {
            if (MyAPIGateway.Utilities.IsDedicated)
            {
                return;
            }

            if (MyAPIGateway.Session == null || MyAPIGateway.Session.Player == null)
            {
                return;
            }

            if (ShouldRunClient())
            {
                GridManagerNet.TryRegisterIfNeeded();
            }

            if (!ShouldRunClient())
            {
                return;
            }

            if (!MyAPIGateway.Gui.IsCursorVisible && !MyAPIGateway.Gui.ChatEntryVisible)
            {
                var i = MyAPIGateway.Input;
                if (i == null)
                {
                    return;
                }

                if ((i.IsAnyCtrlKeyPressed() && i.IsAnyShiftKeyPressed() && i.IsNewKeyPressed(MyKeys.G))
                    || (i.IsAnyAltKeyPressed() && i.IsAnyShiftKeyPressed() && !i.IsAnyCtrlKeyPressed() && i.IsNewKeyPressed(MyKeys.G)))
                {
                    OpenOrRefreshGrids();
                }
            }
        }

        private void OpenOrRefreshGrids()
        {
            if (!GridManagerNet.CanSendRequests())
            {
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] not available on listen-server host", 4000);
                return;
            }

            var p = MyAPIGateway.Session?.Player;
            if (p == null)
            {
                return;
            }

            MyAPIGateway.Utilities.ShowNotification("[SEGrid] loading grids…", 1500);
            _pendingGetBlocksForGridId = 0;
            GridManagerNet.RequestGetGrids(p.SteamUserId);
        }

        private void OnServerReply(string kind, string text)
        {
            var p = MyAPIGateway.Session?.Player;
            if (p == null)
            {
                return;
            }

            if (string.Equals(kind, "get-grids", StringComparison.OrdinalIgnoreCase))
            {
                if (GridDetailScreen.Active != null)
                {
                    GridDetailScreen.Active.CloseScreen();
                }

                _workGrids.Clear();
                if (!GridManagerParse.TryParseGetGrids(text, _workGrids))
                {
                    MyAPIGateway.Utilities.ShowNotification("[SEGrid] could not parse grid list", 5000);
                    return;
                }

                _lastGrids.Clear();
                _lastGrids.AddRange(_workGrids);
                GridListScreen.OpenOrReplace(
                    onDetail: id => OpenGridDetail(id, p.SteamUserId),
                    onClose: () => { },
                    onRefresh: () => OpenOrRefreshGrids(),
                    _lastGrids);
                return;
            }

            if (string.Equals(kind, "get-blocks", StringComparison.OrdinalIgnoreCase))
            {
                if (!GridManagerParse.TryParseGetBlocks(text, out var owner, out var blocks))
                {
                    MyAPIGateway.Utilities.ShowNotification("[SEGrid] could not parse block list", 5000);
                    return;
                }

                var gid = _pendingGetBlocksForGridId;
                var gname = _pendingGetBlocksGridName;
                _pendingGetBlocksForGridId = 0;
                if (gid == 0L)
                {
                    return;
                }

                _currentDetailGridId = gid;
                _currentDetailGridName = gname;
                _lastMainOwner = owner ?? string.Empty;
                _lastBlockSnapshot = blocks != null
                    ? new Dictionary<string, int>(blocks, StringComparer.Ordinal)
                    : new Dictionary<string, int>(StringComparer.Ordinal);

                GridDetailScreen.Open(
                    gid,
                    gname,
                    _lastMainOwner,
                    _lastBlockSnapshot,
                    blockDef => GridManagerNet.RequestBlockDelete(gid, blockDef, p.SteamUserId),
                    onBack: () => ShowListAgain());
                return;
            }

            if (string.Equals(kind, "block-delete", StringComparison.OrdinalIgnoreCase))
            {
                if (GridManagerParse.TryParseBlockDeleteOk(text, out var ok))
                {
                    MyAPIGateway.Utilities.ShowNotification(
                        ok ? "[SEGrid] block delete: ok" : "[SEGrid] block delete: failed",
                        4000);
                    if (ok && _currentDetailGridId != 0L)
                    {
                        _pendingGetBlocksForGridId = _currentDetailGridId;
                        _pendingGetBlocksGridName = _currentDetailGridName;
                        GridManagerNet.RequestGetBlocks(_currentDetailGridId, p.SteamUserId);
                    }
                }

                return;
            }
        }

        private void OpenGridDetail(long entityId, ulong steamId)
        {
            if (!GridManagerNet.CanSendRequests())
            {
                return;
            }

            _pendingGetBlocksForGridId = entityId;
            _pendingGetBlocksGridName = string.Empty;
            foreach (var g in _lastGrids)
            {
                if (g.EntityId == entityId)
                {
                    _pendingGetBlocksGridName = g.Name ?? string.Empty;
                    break;
                }
            }

            if (string.IsNullOrEmpty(_pendingGetBlocksGridName))
            {
                _pendingGetBlocksGridName = "grid " + entityId;
            }

            MyAPIGateway.Utilities.ShowNotification("[SEGrid] loading blocks…", 1500);
            GridManagerNet.RequestGetBlocks(entityId, steamId);
        }

        private void ShowListAgain()
        {
            if (GridListScreen.Active != null)
            {
                GridListScreen.Active.CloseScreen();
            }

            if (_lastGrids.Count == 0)
            {
                return;
            }

            var p = MyAPIGateway.Session?.Player;
            if (p == null)
            {
                return;
            }

            GridListScreen.OpenOrReplace(
                onDetail: id => OpenGridDetail(id, p.SteamUserId),
                onClose: () => { },
                onRefresh: () => OpenOrRefreshGrids(),
                _lastGrids);
        }
    }
}
