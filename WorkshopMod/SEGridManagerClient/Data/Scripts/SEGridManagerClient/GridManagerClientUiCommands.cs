using Sandbox.ModAPI;

namespace SEGridManagerClient
{
    /// <summary>Shared request helpers for mission/chat and Rich HUD terminal.</summary>
    internal static class GridManagerClientUiCommands
    {
        public static void OpenOrRefreshGrids()
        {
            if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated)
            {
                return;
            }

            if (!GridManagerSession.CanSendRequests())
            {
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] not available on listen-server host", 4000);
                ShowListenHostBlockedPanel("grid list (get-grids)");
                return;
            }

            var p = MyAPIGateway.Session != null ? MyAPIGateway.Session.Player : null;
            if (p == null)
            {
                return;
            }

            MyAPIGateway.Utilities.ShowNotification("[SEGrid] loading grids…", 1500);
            GridManagerClientUiState.PendingGetBlocksForGridId = 0L;
            GridManagerClientUiState.PendingGetBlocksGridName = string.Empty;
            GridManagerSession.RequestGetGrids(p.SteamUserId);
            GridManagerRichHudSession.TryOpenTerminalToGridsPage();
        }

        public static void TryGetBlocksFromChat()
        {
            if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated)
            {
                return;
            }

            if (!GridManagerSession.CanSendRequests())
            {
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] not available on listen-server host", 5000);
                ShowListenHostBlockedPanel("block list (get-blocks)");
                return;
            }

            if (GridManagerUiSession.TargetGridId == 0L)
            {
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] set target grid: /gmg grid <id> or the terminal field", 6000);
                return;
            }

            var p = MyAPIGateway.Session != null ? MyAPIGateway.Session.Player : null;
            if (p == null)
            {
                return;
            }

            long targetId = GridManagerUiSession.TargetGridId;
            GridManagerClientUiState.PendingGetBlocksForGridId = targetId;
            GridManagerClientUiState.PendingGetBlocksGridName = string.Empty;
            for (int i = 0; i < GridManagerClientUiState.LastGrids.Count; i++)
            {
                if (GridManagerClientUiState.LastGrids[i].EntityId == targetId)
                {
                    GridManagerClientUiState.PendingGetBlocksGridName = GridManagerClientUiState.LastGrids[i].Name ?? string.Empty;
                    break;
                }
            }

            if (string.IsNullOrEmpty(GridManagerClientUiState.PendingGetBlocksGridName))
            {
                GridManagerClientUiState.PendingGetBlocksGridName = "grid " + targetId;
            }

            MyAPIGateway.Utilities.ShowNotification("[SEGrid] get blocks sent…", 2000);
            GridManagerSession.RequestGetBlocks(targetId, p.SteamUserId);
            GridManagerRichHudSession.TryOpenTerminalToBlocksPage();
        }

        public static void TryBlockDelete()
        {
            if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated)
            {
                return;
            }

            if (!GridManagerSession.CanSendRequests())
            {
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] not available on listen-server host", 5000);
                ShowListenHostBlockedPanel("block delete");
                return;
            }

            if (GridManagerUiSession.TargetGridId == 0L)
            {
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] set target: /gmg grid and /gmg block or the terminal fields", 6000);
                return;
            }

            if (string.IsNullOrEmpty(GridManagerUiSession.TargetBlockName))
            {
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] set block name: /gmg block <name> or the terminal field", 6000);
                return;
            }

            var p = MyAPIGateway.Session != null ? MyAPIGateway.Session.Player : null;
            if (p == null)
            {
                return;
            }

            MyAPIGateway.Utilities.ShowNotification("[SEGrid] block delete sent…", 3000);
            GridManagerSession.RequestBlockDelete(GridManagerUiSession.TargetGridId, GridManagerUiSession.TargetBlockName, p.SteamUserId);
        }

        public static void ShowListenHostBlockedPanel(string requestLabel)
        {
            if (MyAPIGateway.Utilities == null)
            {
                return;
            }

            var body =
                "This session is the multiplayer host (listen server). The client mod does not send grid requests from the host process — use a dedicated server or connect as a client to a remote server with the Gridmanager / Torch plugin.\n\n" +
                "Request: " + requestLabel;
            MyAPIGateway.Utilities.ShowMissionScreen(
                "SE Grid Manager",
                "Not available on host",
                " ",
                body,
                null,
                "OK");
        }
    }
}
