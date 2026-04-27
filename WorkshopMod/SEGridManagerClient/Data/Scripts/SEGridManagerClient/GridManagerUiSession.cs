using System;
using System.Collections.Generic;
using System.Text;
using RichHudFramework.Client;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Input;

namespace SEGridManagerClient
{
    /// <summary>
    /// Mission + notifications (Data/Scripts whitelist does not allow MyGui). Parsed JSON via GridManagerParse.
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public sealed class GridManagerUiSession : MySessionComponentBase
    {
        private const string ChatPrefix0 = "/gmg";
        private const string ChatPrefix1 = "!gmg";
        private const int KeyMenuDurationSeconds = 30;
        private const int MissionBodyMaxChars = 4000;

        private bool _eventsAttached;
        private DateTime _keyMenuValidUntil;

        private readonly List<GridManagerParse.GridRow> _workGrids = new List<GridManagerParse.GridRow>();

        /// <summary>Target grid for get-blocks and delete (set with /gmg grid id).</summary>
        public static long TargetGridId { get; set; }

        /// <summary>Target block name for delete (set with /gmg block name).</summary>
        public static string TargetBlockName { get; set; } = string.Empty;

        public override void HandleInput()
        {
            if (MyAPIGateway.Utilities.IsDedicated)
            {
                return;
            }

            if (MyParticlesManager.Paused)
            {
                return;
            }

            if (MyAPIGateway.Session == null || MyAPIGateway.Session.Player == null)
            {
                return;
            }

            GridManagerSession.TryRegisterIfNeeded();

            if (!_eventsAttached)
            {
                MyAPIGateway.Utilities.MessageEntered += OnMessageEntered;
                GridManagerSession.ServerReply += ProcessServerReply;
                _eventsAttached = true;
            }

            if (!MyAPIGateway.Gui.IsCursorVisible && !MyAPIGateway.Gui.ChatEntryVisible)
            {
                TryHotkeyMenu();
            }
        }

        private void ProcessServerReply(string kind, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var p = MyAPIGateway.Session?.Player;
            if (p == null)
            {
                return;
            }

            if (string.Equals(kind, "get-grids", StringComparison.OrdinalIgnoreCase))
            {
                _workGrids.Clear();
                if (!GridManagerParse.TryParseGetGrids(text, _workGrids))
                {
                    MyAPIGateway.Utilities.ShowNotification("[SEGrid] could not parse grid list", 5000);
                    return;
                }

                GridManagerClientUiState.SetLastGridsFromWorkList(_workGrids);
                if (RichHudClient.Registered)
                {
                    GridManagerClientUiState.PendingTerminalPage = "grids";
                }

                GridManagerClientUiState.RaiseStateChanged();
                if (!RichHudClient.Registered)
                {
                    ShowGridsInMission();
                }

                return;
            }

            if (string.Equals(kind, "get-blocks", StringComparison.OrdinalIgnoreCase))
            {
                string owner;
                Dictionary<string, int> blocks;
                if (!GridManagerParse.TryParseGetBlocks(text, out owner, out blocks))
                {
                    MyAPIGateway.Utilities.ShowNotification("[SEGrid] could not parse block list", 5000);
                    return;
                }

                var gid = GridManagerClientUiState.PendingGetBlocksForGridId;
                var gname = GridManagerClientUiState.PendingGetBlocksGridName;
                GridManagerClientUiState.PendingGetBlocksForGridId = 0L;
                GridManagerClientUiState.PendingGetBlocksGridName = string.Empty;
                if (gid == 0L)
                {
                    return;
                }

                GridManagerClientUiState.CurrentDetailGridId = gid;
                GridManagerClientUiState.CurrentDetailGridName = gname;
                GridManagerClientUiState.SetLastBlocks(owner, blocks);
                if (RichHudClient.Registered)
                {
                    GridManagerClientUiState.PendingTerminalPage = "blocks";
                }

                GridManagerClientUiState.RaiseStateChanged();
                if (!RichHudClient.Registered)
                {
                    ShowBlocksInMission(gid, gname, owner ?? string.Empty, blocks);
                }

                return;
            }

            if (string.Equals(kind, "block-delete", StringComparison.OrdinalIgnoreCase))
            {
                bool ok;
                if (GridManagerParse.TryParseBlockDeleteOk(text, out ok))
                {
                    MyAPIGateway.Utilities.ShowNotification(
                        ok ? "[SEGrid] block delete: ok" : "[SEGrid] block delete: failed",
                        4000);
                    if (ok && GridManagerClientUiState.CurrentDetailGridId != 0L)
                    {
                        GridManagerClientUiState.PendingGetBlocksForGridId = GridManagerClientUiState.CurrentDetailGridId;
                        GridManagerClientUiState.PendingGetBlocksGridName = GridManagerClientUiState.CurrentDetailGridName;
                        GridManagerSession.RequestGetBlocks(GridManagerClientUiState.CurrentDetailGridId, p.SteamUserId);
                    }
                }

                return;
            }
        }

        private void ShowGridsInMission()
        {
            var sb = new StringBuilder(4096);
            for (int i = 0; i < GridManagerClientUiState.LastGrids.Count; i++)
            {
                var r = GridManagerClientUiState.LastGrids[i];
                var name = string.IsNullOrEmpty(r.Name) ? "(unnamed)" : r.Name;
                sb.AppendLine((i + 1) + ". " + name);
                sb.AppendLine("   entity_id: " + r.EntityId);
            }

            if (GridManagerClientUiState.LastGrids.Count == 0)
            {
                sb.AppendLine("(no grids in reply)");
            }

            sb.AppendLine();
            sb.AppendLine("Set target, then get blocks: /gmg grid 12345  (use entity_id above)");
            sb.AppendLine("Or use Ctrl+Shift+2 (after /gmg grid … set TargetGrid for hotkeys).");
            var body = TruncateForUi(sb.ToString(), MissionBodyMaxChars);
            MyAPIGateway.Utilities.ShowMissionScreen("SE Grid Manager", "Your grids", " ", body, null, "OK");
        }

        private static void ShowBlocksInMission(long gridId, string gridName, string mainOwner, Dictionary<string, int> blocks)
        {
            var title = string.IsNullOrEmpty(gridName) ? "(grid)" : gridName;
            if (title.Length > 40)
            {
                title = title.Substring(0, 37) + "…";
            }

            var sb = new StringBuilder(4096);
            sb.AppendLine("EntityId: " + gridId);
            sb.AppendLine("Main owner: " + (string.IsNullOrEmpty(mainOwner) ? "—" : mainOwner));
            sb.AppendLine();

            var keys = new List<string>(blocks.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            const int maxBlockLines = 80;
            for (int i = 0; i < keys.Count; i++)
            {
                if (i >= maxBlockLines)
                {
                    sb.AppendLine("… " + (keys.Count - maxBlockLines) + " more types (truncated) …");
                    break;
                }

                int cnt;
                blocks.TryGetValue(keys[i], out cnt);
                var k = keys[i];
                if (k.Length > 64)
                {
                    k = k.Substring(0, 61) + "…";
                }

                sb.AppendLine("x" + cnt + "  " + k);
            }

            sb.AppendLine();
            sb.AppendLine("Delete: /gmg block \"ExactTypeId\"  (copy from list), then /gmg delete or Ctrl+Shift+3");
            var body = TruncateForUi(sb.ToString(), MissionBodyMaxChars);
            MyAPIGateway.Utilities.ShowMissionScreen("SE Grid — " + title, "Blocks on grid", " ", body, null, "OK");
        }

        private static string TruncateForUi(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLen)
            {
                return text ?? string.Empty;
            }

            return text.Substring(0, maxLen) + "\n... (truncated)";
        }

        private void OpenOrRefreshGrids()
        {
            GridManagerClientUiCommands.OpenOrRefreshGrids();
        }

        private void OnMessageEntered(string messageText, ref bool sendToOthers)
        {
            if (string.IsNullOrWhiteSpace(messageText))
            {
                return;
            }

            var trimmed = messageText.Trim();
            if (!IsGridManagerChatLine(trimmed))
            {
                return;
            }

            sendToOthers = false;
            var after = GetChatCommandBody(trimmed);
            if (string.IsNullOrEmpty(after))
            {
                OpenHelpMissionScreen();
                return;
            }

            var firstSpace = after.IndexOf(' ');
            var verb = (firstSpace < 0 ? after : after.Substring(0, firstSpace)).Trim();
            var rest = (firstSpace < 0 ? string.Empty : after.Substring(firstSpace + 1).Trim());

            if (string.IsNullOrEmpty(verb) || string.Equals(verb, "help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(verb, "panel", StringComparison.OrdinalIgnoreCase))
            {
                OpenHelpMissionScreen();
                return;
            }

            if (string.Equals(verb, "getgrids", StringComparison.OrdinalIgnoreCase))
            {
                OpenOrRefreshGrids();
                return;
            }

            if (string.Equals(verb, "getblocks", StringComparison.OrdinalIgnoreCase))
            {
                TryGetBlocksFromChat();
                return;
            }

            if (string.Equals(verb, "delete", StringComparison.OrdinalIgnoreCase))
            {
                TryBlockDelete();
                return;
            }

            if (string.Equals(verb, "grid", StringComparison.OrdinalIgnoreCase))
            {
                long gid;
                if (!long.TryParse(rest, out gid))
                {
                    MyAPIGateway.Utilities.ShowNotification("[SEGrid] /gmg grid: need numeric grid id", 4000);
                    return;
                }

                TargetGridId = gid;
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] target grid: " + gid, 3000);
                if (RichHudClient.Registered)
                {
                    GridManagerClientUiState.RaiseStateChanged();
                }

                return;
            }

            if (string.Equals(verb, "block", StringComparison.OrdinalIgnoreCase))
            {
                var name = UnquoteBlockName(rest);
                TargetBlockName = name ?? string.Empty;
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] target block: " + (string.IsNullOrEmpty(TargetBlockName) ? "(empty)" : TargetBlockName), 3000);
                if (RichHudClient.Registered)
                {
                    GridManagerClientUiState.RaiseStateChanged();
                }

                return;
            }

            MyAPIGateway.Utilities.ShowNotification("[SEGrid] unknown /gmg command — use Ctrl+Shift+M for help or /gmg help", 5000);
        }

        private void TryGetBlocksFromChat()
        {
            GridManagerClientUiCommands.TryGetBlocksFromChat();
        }

        private void TryHotkeyMenu()
        {
            var input = MyAPIGateway.Input;
            if (input == null)
            {
                return;
            }

            if (ChordCtrlShiftLetter(input, MyKeys.G) || ChordAltShiftLetter(input, MyKeys.G))
            {
                OpenOrRefreshGrids();
                return;
            }

            if (ChordCtrlShiftLetter(input, MyKeys.M) || ChordAltShiftLetter(input, MyKeys.M))
            {
                OpenHelpAndKeyMenu();
                return;
            }

            if (ChordCtrlShiftDigit1To3(input, 1) || ChordAltShiftDigit1To3(input, 1))
            {
                OpenOrRefreshGrids();
                return;
            }

            if (ChordCtrlShiftDigit1To3(input, 2) || ChordAltShiftDigit1To3(input, 2))
            {
                TryGetBlocksForHotkey();
                return;
            }

            if (ChordCtrlShiftDigit1To3(input, 3) || ChordAltShiftDigit1To3(input, 3))
            {
                TryBlockDelete();
                return;
            }

            if (DateTime.UtcNow > _keyMenuValidUntil)
            {
                return;
            }

            if (input.IsAnyCtrlKeyPressed() || input.IsAnyAltKeyPressed() || input.IsAnyShiftKeyPressed())
            {
                return;
            }

            if (input.IsNewKeyPressed(MyKeys.D1) || input.IsNewKeyPressed(MyKeys.NumPad1))
            {
                OpenOrRefreshGrids();
            }
            else if (input.IsNewKeyPressed(MyKeys.D2) || input.IsNewKeyPressed(MyKeys.NumPad2))
            {
                TryGetBlocksForHotkey();
            }
            else if (input.IsNewKeyPressed(MyKeys.D3) || input.IsNewKeyPressed(MyKeys.NumPad3))
            {
                TryBlockDelete();
            }
        }

        private void OpenHelpAndKeyMenu()
        {
            _keyMenuValidUntil = DateTime.UtcNow.AddSeconds(KeyMenuDurationSeconds);
            OpenHelpMissionScreen();
            MyAPIGateway.Utilities.ShowNotification("[SEGrid] plain 1/2/3 (30s) or Ctrl+Shift+1…3 — /gmg …", 6000);
        }

        private static bool ChordCtrlShiftLetter(VRage.ModAPI.IMyInput input, MyKeys key)
        {
            return input.IsAnyCtrlKeyPressed() && input.IsAnyShiftKeyPressed() && input.IsNewKeyPressed(key);
        }

        private static bool ChordAltShiftLetter(VRage.ModAPI.IMyInput input, MyKeys key)
        {
            return input.IsAnyAltKeyPressed() && input.IsAnyShiftKeyPressed() && !input.IsAnyCtrlKeyPressed() && input.IsNewKeyPressed(key);
        }

        private static bool ChordCtrlShiftDigit1To3(VRage.ModAPI.IMyInput input, int oneToThree)
        {
            if (!input.IsAnyCtrlKeyPressed() || !input.IsAnyShiftKeyPressed())
            {
                return false;
            }

            switch (oneToThree)
            {
                case 1: return input.IsNewKeyPressed(MyKeys.D1) || input.IsNewKeyPressed(MyKeys.NumPad1);
                case 2: return input.IsNewKeyPressed(MyKeys.D2) || input.IsNewKeyPressed(MyKeys.NumPad2);
                case 3: return input.IsNewKeyPressed(MyKeys.D3) || input.IsNewKeyPressed(MyKeys.NumPad3);
                default: return false;
            }
        }

        private static bool ChordAltShiftDigit1To3(VRage.ModAPI.IMyInput input, int oneToThree)
        {
            if (!input.IsAnyAltKeyPressed() || !input.IsAnyShiftKeyPressed() || input.IsAnyCtrlKeyPressed())
            {
                return false;
            }

            switch (oneToThree)
            {
                case 1: return input.IsNewKeyPressed(MyKeys.D1) || input.IsNewKeyPressed(MyKeys.NumPad1);
                case 2: return input.IsNewKeyPressed(MyKeys.D2) || input.IsNewKeyPressed(MyKeys.NumPad2);
                case 3: return input.IsNewKeyPressed(MyKeys.D3) || input.IsNewKeyPressed(MyKeys.NumPad3);
                default: return false;
            }
        }

        private static void OpenHelpMissionScreen()
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine("SE Grid Manager (script mod): mission panels + /gmg — same message IDs as the Torch server plugin.");
            sb.AppendLine();
            sb.AppendLine("With Rich HUD Master (1965654081) loaded, use the Rich HUD terminal (SE Grid Manager) for the same actions.");
            sb.AppendLine();
            sb.AppendLine("Custom-button MyGui is not available in Data/Scripts (game whitelist).");
            sb.AppendLine("The optional SEGridManagerClient.csproj build can use MyGui if loaded as a DLL in supported setups.");
            sb.AppendLine();
            sb.AppendLine("Ctrl+Shift+G / Alt+Shift+G — request grid list (mission panel with parsed list).");
            sb.AppendLine("Ctrl+Shift+M / Alt+Shift+M — this help, then 30s plain 1/2/3, or chord 1/2/3.");
            sb.AppendLine("Chat: /gmg getgrids, grid (id), getblocks, block (name), delete");
            sb.AppendLine();
            sb.AppendLine("Listen-server host: client send is disabled from the host process.");
            MyAPIGateway.Utilities.ShowMissionScreen("SE Grid Manager", "Client", " ", sb.ToString(), null, "OK");
        }


        private void TryGetBlocksForHotkey()
        {
            GridManagerClientUiCommands.TryGetBlocksFromChat();
        }

        private static void TryBlockDelete()
        {
            GridManagerClientUiCommands.TryBlockDelete();
        }

        private static string UnquoteBlockName(string rest)
        {
            if (string.IsNullOrEmpty(rest))
            {
                return string.Empty;
            }

            var s = rest.Trim();
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
            {
                return s.Substring(1, s.Length - 2);
            }

            return s;
        }

        private static bool IsGridManagerChatLine(string line)
        {
            return line.StartsWith(ChatPrefix0, StringComparison.OrdinalIgnoreCase)
                   || line.StartsWith(ChatPrefix1, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetChatCommandBody(string line)
        {
            if (line.StartsWith(ChatPrefix0, StringComparison.OrdinalIgnoreCase))
            {
                return line.Length <= ChatPrefix0.Length ? string.Empty : line.Substring(ChatPrefix0.Length).Trim();
            }

            if (line.StartsWith(ChatPrefix1, StringComparison.OrdinalIgnoreCase))
            {
                return line.Length <= ChatPrefix1.Length ? string.Empty : line.Substring(ChatPrefix1.Length).Trim();
            }

            return string.Empty;
        }

        protected override void UnloadData()
        {
            if (_eventsAttached)
            {
                MyAPIGateway.Utilities.MessageEntered -= OnMessageEntered;
                GridManagerSession.ServerReply -= ProcessServerReply;
                _eventsAttached = false;
            }

            base.UnloadData();
        }
    }
}
