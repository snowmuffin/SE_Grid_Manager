using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Input;

namespace SEGridManagerClient
{
    /// <summary>
    /// Client UI: Ctrl+Shift / Alt+Shift chords, post-help number keys, and /gmg chat. See OpenHelpMissionScreen.
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public sealed class GridManagerUiSession : MySessionComponentBase
    {
        private const string ChatPrefix0 = "/gmg";
        private const string ChatPrefix1 = "!gmg";
        private const int ShortReplyForNotification = 200;
        private const int KeyMenuDurationSeconds = 30;
        private const int MissionBodyMaxChars = 4000;

        private static readonly object PendingLock = new object();
        private static readonly List<PendingReply> PendingReplies = new List<PendingReply>();

        private bool _eventsAttached;
        private DateTime _keyMenuValidUntil;

        /// <summary>Target grid for get-blocks and delete (set with /gmg grid &lt;id&gt;).</summary>
        public static long TargetGridId { get; set; }

        /// <summary>Target block name for delete (set with /gmg block &lt;name&gt;).</summary>
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

            if (!_eventsAttached)
            {
                MyAPIGateway.Utilities.MessageEntered += OnMessageEntered;
                GridManagerSession.ServerReply += OnServerReply;
                _eventsAttached = true;
            }

            // Marshal replies on the game / input path (server handlers may be on other threads)
            FlushServerReplies();

            if (!MyAPIGateway.Gui.IsCursorVisible && !MyAPIGateway.Gui.ChatEntryVisible)
            {
                TryHotkeyMenu();
            }
        }

        private static void OnServerReply(string kind, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            lock (PendingLock)
            {
                PendingReplies.Add(new PendingReply { Kind = kind, Text = text });
            }
        }

        private void FlushServerReplies()
        {
            List<PendingReply> batch = null;
            lock (PendingLock)
            {
                if (PendingReplies.Count == 0)
                {
                    return;
                }

                batch = new List<PendingReply>(PendingReplies);
                PendingReplies.Clear();
            }

            for (int i = 0; i < batch.Count; i++)
            {
                var item = batch[i];
                var body = TruncateForUi(item.Text, MissionBodyMaxChars);
                // Grid list is always shown in a mission screen so the JSON list stays readable
                var useMissionScreen = body.Length > ShortReplyForNotification
                    || string.Equals(item.Kind, "get-grids", StringComparison.OrdinalIgnoreCase);
                if (useMissionScreen)
                {
                    MyAPIGateway.Utilities.ShowMissionScreen(
                        "SE Grid Manager — " + item.Kind,
                        "Server reply",
                        " ",
                        body,
                        null,
                        "OK");
                }
                else
                {
                    var line = body.Replace("\r", " ").Replace("\n", " ");
                    MyAPIGateway.Utilities.ShowNotification("[SEGrid] " + item.Kind + ": " + line, 10000);
                }
            }
        }

        private static string TruncateForUi(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLen)
            {
                return text ?? string.Empty;
            }

            return text.Substring(0, maxLen) + "\n... (truncated)";
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
                TryGetGrids();
                return;
            }

            if (string.Equals(verb, "getblocks", StringComparison.OrdinalIgnoreCase))
            {
                TryGetBlocks();
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
                return;
            }

            if (string.Equals(verb, "block", StringComparison.OrdinalIgnoreCase))
            {
                var name = UnquoteBlockName(rest);
                TargetBlockName = name ?? string.Empty;
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] target block: " + (string.IsNullOrEmpty(TargetBlockName) ? "(empty)" : TargetBlockName), 3000);
                return;
            }

            MyAPIGateway.Utilities.ShowNotification("[SEGrid] unknown /gmg command — use Ctrl+Shift+M for help or /gmg help", 5000);
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

        private void TryHotkeyMenu()
        {
            var input = MyAPIGateway.Input;
            if (input == null)
            {
                return;
            }

            // Chords: Ctrl+Shift+… and Alt+Shift+… to avoid clashing with vanilla F-keys and single-modifier game binds
            if (ChordCtrlShiftLetter(input, MyKeys.G) || ChordAltShiftLetter(input, MyKeys.G))
            {
                TryGetGrids();
                return;
            }

            if (ChordCtrlShiftLetter(input, MyKeys.M) || ChordAltShiftLetter(input, MyKeys.M))
            {
                OpenHelpAndKeyMenu();
                return;
            }

            if (ChordCtrlShiftDigit1To3(input, 1) || ChordAltShiftDigit1To3(input, 1))
            {
                TryGetGrids();
                return;
            }

            if (ChordCtrlShiftDigit1To3(input, 2) || ChordAltShiftDigit1To3(input, 2))
            {
                TryGetBlocks();
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
                TryGetGrids();
            }
            else if (input.IsNewKeyPressed(MyKeys.D2) || input.IsNewKeyPressed(MyKeys.NumPad2))
            {
                TryGetBlocks();
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
            MyAPIGateway.Utilities.ShowNotification("[SEGrid] plain 1/2/3 (30s, no ctrl/alt/shift) or Ctrl+Shift+1…3 / Alt+Shift+1…3 — /gmg …", 6000);
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
            sb.AppendLine("Torch Gridmanager client — same secure message IDs as the server plugin.");
            sb.AppendLine();
            sb.AppendLine("Default chords (rare in vanilla — change MyKeys in script if you still clash):");
            sb.AppendLine("Ctrl+Shift+G  or  Alt+Shift+G  —  grid list (get-grids; opens mission panel).");
            sb.AppendLine("Ctrl+Shift+M  or  Alt+Shift+M  —  this help; then 30s of plain 1/2/3 (no other modifiers),");
            sb.AppendLine("  or use Ctrl+Shift+1/2/3  or  Alt+Shift+1/2/3  anytime: grids / blocks / delete.");
            sb.AppendLine("Chat /gmg … still works if keys conflict.");
            sb.AppendLine();
            sb.AppendLine("Chat: /gmg or !gmg");
            sb.AppendLine("  getgrids          — list grids (your Steam id from session)");
            sb.AppendLine("  grid <EntityId>   — set target for getblocks / delete");
            sb.AppendLine("  block <name>      — block name for delete; quote if it has spaces");
            sb.AppendLine("  getblocks         — need target grid (grid ...)");
            sb.AppendLine("  delete            — need grid and block (grid ... / block ...)");
            sb.AppendLine("  help | panel      — this panel");
            sb.AppendLine();
            sb.AppendLine("Listen-server host: send from this mod is disabled (Torch already handles messages on host).");
            MyAPIGateway.Utilities.ShowMissionScreen("SE Grid Manager", "Client", " ", sb.ToString(), null, "OK");
        }

        private static void TryGetGrids()
        {
            if (!GridManagerSession.CanSendRequests())
            {
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] not available on listen-server host", 5000);
                return;
            }

            var p = MyAPIGateway.Session?.Player;
            if (p == null)
            {
                return;
            }

            MyAPIGateway.Utilities.ShowNotification("[SEGrid] get grids sent…", 2000);
            GridManagerSession.RequestGetGrids(p.SteamUserId);
        }

        private static void TryGetBlocks()
        {
            if (!GridManagerSession.CanSendRequests())
            {
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] not available on listen-server host", 5000);
                return;
            }

            if (TargetGridId == 0)
            {
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] set target grid: /gmg grid <id>", 6000);
                return;
            }

            var p = MyAPIGateway.Session?.Player;
            if (p == null)
            {
                return;
            }

            MyAPIGateway.Utilities.ShowNotification("[SEGrid] get blocks sent…", 2000);
            GridManagerSession.RequestGetBlocks(TargetGridId, p.SteamUserId);
        }

        private static void TryBlockDelete()
        {
            if (!GridManagerSession.CanSendRequests())
            {
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] not available on listen-server host", 5000);
                return;
            }

            if (TargetGridId == 0)
            {
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] set target: /gmg grid and /gmg block", 6000);
                return;
            }

            if (string.IsNullOrEmpty(TargetBlockName))
            {
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] set block name: /gmg block <name>", 6000);
                return;
            }

            var p = MyAPIGateway.Session?.Player;
            if (p == null)
            {
                return;
            }

            MyAPIGateway.Utilities.ShowNotification("[SEGrid] block delete sent…", 3000);
            GridManagerSession.RequestBlockDelete(TargetGridId, TargetBlockName, p.SteamUserId);
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

        private sealed class PendingReply
        {
            public string Kind;
            public string Text;
        }

        protected override void UnloadData()
        {
            if (_eventsAttached)
            {
                MyAPIGateway.Utilities.MessageEntered -= OnMessageEntered;
                GridManagerSession.ServerReply -= OnServerReply;
                _eventsAttached = false;
            }

            base.UnloadData();
        }
    }
}
