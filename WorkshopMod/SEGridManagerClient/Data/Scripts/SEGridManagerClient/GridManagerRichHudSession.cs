using System;
using System.Collections.Generic;
using System.Text;
using RichHudFramework;
using RichHudFramework.Client;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Utils;

namespace SEGridManagerClient
{
    /// <summary>Registers with Rich HUD Master and shows grid list, block summary, and help in the RHF shared terminal.</summary>
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public sealed class GridManagerRichHudSession : MySessionComponentBase
    {
        // Static handles so hotkeys can open the RHF window without a session instance (same assembly).
        private static ControlPage s_gridsListPage;
        private static ControlPage s_blocksListPage;

        private ControlPage _gridsListPage;
        private TerminalList<long> _gridPicker;
        private ControlPage _blocksListPage;
        private ControlCategory _blocksInfoCategory;
        private TerminalList<string> _blockTypePicker;
        private TextPage _helpPage;
        private bool _subscribedState;
        private bool _deleteArmed;
        private string _armedBlockType = string.Empty;
        private long _lastBlocksListGridId;

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            base.Init(sessionComponent);

            if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated)
            {
                return;
            }

            RichHudClient.Init("SE Grid Manager", OnRichHudRegistered, OnRichHudReset);
        }

        private void OnRichHudRegistered()
        {
            try
            {
                var root = RichHudTerminal.Root;
                var category = new TerminalPageCategory { Name = "SE Grid Manager", Enabled = true };

                _gridsListPage = new ControlPage
                {
                    Name = "Grid list"
                };
                var catGrids = new ControlCategory
                {
                    HeaderText = "Your grids",
                    SubheaderText = "Select a row, then tap Block details (loads blocks for that grid)",
                    Enabled = true
                };
                _gridPicker = new TerminalList<long> { Name = "Owned grids", Enabled = true };
                var tPick = new ControlTile();
                tPick.Add(_gridPicker);
                catGrids.Add(tPick);
                var btnGridDetails = new TerminalButton { Name = "Block details", Enabled = true };
                btnGridDetails.ControlChanged += OnGridListBlockDetailsClicked;
                var tDetails = new ControlTile();
                tDetails.Add(btnGridDetails);
                catGrids.Add(tDetails);
                _gridsListPage.Add(catGrids);
                s_gridsListPage = _gridsListPage;

                _blocksListPage = new ControlPage
                {
                    Name = "Blocks"
                };
                _blocksInfoCategory = new ControlCategory
                {
                    HeaderText = "Block summary",
                    SubheaderText = "Open Grid list → Block details to load a grid first.",
                    Enabled = true
                };
                _blockTypePicker = new TerminalList<string> { Name = "Block types (this grid)", Enabled = true };
                var tBlist = new ControlTile();
                tBlist.Add(_blockTypePicker);
                _blocksInfoCategory.Add(tBlist);

                var tPrep = new ControlTile();
                var btnPrepare = new TerminalButton { Name = "Prepare delete", Enabled = true };
                btnPrepare.ControlChanged += OnBlockDeletePrepare;
                tPrep.Add(btnPrepare);
                _blocksInfoCategory.Add(tPrep);

                var tConf = new ControlTile();
                var btnConfirm = new TerminalButton { Name = "Confirm delete (server)", Enabled = true };
                btnConfirm.ControlChanged += OnBlockDeleteConfirm;
                tConf.Add(btnConfirm);
                _blocksInfoCategory.Add(tConf);

                var tCan = new ControlTile();
                var btnCancel = new TerminalButton { Name = "Cancel delete", Enabled = true };
                btnCancel.ControlChanged += OnBlockDeleteCancel;
                tCan.Add(btnCancel);
                _blocksInfoCategory.Add(tCan);

                _blocksListPage.Add(_blocksInfoCategory);
                s_blocksListPage = _blocksListPage;

                _helpPage = new TextPage
                {
                    Name = "Help",
                    HeaderText = new RichText("SE Grid Manager"),
                    SubHeaderText = new RichText("Targets and chat")
                };
                _helpPage.Text = new RichText(BuildHelpText());

                category.Add(_gridsListPage);
                category.Add(_blocksListPage);
                category.Add(_helpPage);
                root.Add(category);

                GridManagerClientUiState.StateChanged += OnSharedStateChanged;
                _subscribedState = true;
                OnSharedStateChanged();
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine("[SEGridManagerClient] RHF UI build: " + ex);
                if (MyAPIGateway.Utilities != null)
                {
                    MyAPIGateway.Utilities.ShowNotification("[SEGrid] RHF UI build failed (see log)", 8000);
                }
            }
        }

        private void ClearDeleteArmed()
        {
            _deleteArmed = false;
            _armedBlockType = string.Empty;
        }

        private void OnBlockDeletePrepare(object sender, EventArgs e)
        {
            if (MyAPIGateway.Utilities == null)
            {
                return;
            }

            if (GridManagerClientUiState.CurrentDetailGridId == 0L)
            {
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] load blocks (Grid list → Block details) first", 5000);
                return;
            }

            if (_blockTypePicker == null)
            {
                return;
            }

            var sel = _blockTypePicker.List.Selection;
            if (sel == null)
            {
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] select a block type in the list first", 5000);
                return;
            }

            var typeId = sel.AssocObject;
            if (string.IsNullOrEmpty(typeId))
            {
                return;
            }

            _armedBlockType = typeId;
            _deleteArmed = true;
            MyAPIGateway.Utilities.ShowNotification("[SEGrid] prepared. Tap \"Confirm delete (server)\" to send, or Cancel.", 7000);
        }

        private void OnBlockDeleteConfirm(object sender, EventArgs e)
        {
            if (MyAPIGateway.Utilities == null)
            {
                return;
            }

            if (!_deleteArmed || string.IsNullOrEmpty(_armedBlockType))
            {
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] use Prepare delete on a selected type first", 5000);
                return;
            }

            if (_blockTypePicker == null)
            {
                return;
            }

            var sel = _blockTypePicker.List.Selection;
            if (sel == null)
            {
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] block type not selected (prepare again)", 5000);
                return;
            }

            var typeId = sel.AssocObject;
            if (string.IsNullOrEmpty(typeId) || !string.Equals(typeId, _armedBlockType, StringComparison.Ordinal))
            {
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] selection must match the prepared type — prepare again", 6000);
                return;
            }

            if (GridManagerClientUiState.CurrentDetailGridId == 0L)
            {
                ClearDeleteArmed();
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] no grid loaded; cancelled", 4000);
                return;
            }

            GridManagerUiSession.TargetGridId = GridManagerClientUiState.CurrentDetailGridId;
            GridManagerUiSession.TargetBlockName = typeId;
            GridManagerClientUiState.RaiseStateChanged();
            GridManagerClientUiCommands.TryBlockDelete();
            ClearDeleteArmed();
        }

        private void OnBlockDeleteCancel(object sender, EventArgs e)
        {
            ClearDeleteArmed();
            if (MyAPIGateway.Utilities != null)
            {
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] delete prepared state cleared", 3000);
            }
        }

        private void OnRichHudReset()
        {
            if (_subscribedState)
            {
                GridManagerClientUiState.StateChanged -= OnSharedStateChanged;
                _subscribedState = false;
            }

            _gridsListPage = null;
            _gridPicker = null;
            _blocksListPage = null;
            _blocksInfoCategory = null;
            _blockTypePicker = null;
            _helpPage = null;
            s_gridsListPage = null;
            s_blocksListPage = null;
            _lastBlocksListGridId = 0L;
            ClearDeleteArmed();
        }

        /// <summary>Opens the Rich HUD shared terminal to the grid list page (no-op if RHF not registered or UI not built).</summary>
        public static void TryOpenTerminalToGridsPage()
        {
            OpenRichHudToPage("grids");
        }

        /// <summary>Opens the Rich HUD shared terminal to the block summary page (no-op if RHF not registered or UI not built).</summary>
        public static void TryOpenTerminalToBlocksPage()
        {
            OpenRichHudToPage("blocks");
        }

        private static void OpenRichHudToPage(string which)
        {
            if (!RichHudClient.Registered)
            {
                return;
            }

            TerminalPageBase page = null;
            if (string.Equals(which, "grids", StringComparison.OrdinalIgnoreCase))
            {
                page = s_gridsListPage;
            }
            else if (string.Equals(which, "blocks", StringComparison.OrdinalIgnoreCase))
            {
                page = s_blocksListPage;
            }

            if (page == null)
            {
                return;
            }

            try
            {
                RichHudTerminal.OpenMenu();
                RichHudTerminal.OpenToPage(page);
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine("[SEGridManagerClient] RichHudTerminal open: " + ex);
            }
        }

        private void OnGridListBlockDetailsClicked(object sender, EventArgs e)
        {
            if (_gridPicker == null || MyAPIGateway.Utilities == null)
            {
                return;
            }

            var sel = _gridPicker.List.Selection;
            if (sel == null)
            {
                MyAPIGateway.Utilities.ShowNotification("[SEGrid] select a grid in the list first", 4000);
                return;
            }

            long gid = sel.AssocObject;
            ClearDeleteArmed();
            GridManagerUiSession.TargetGridId = gid;
            GridManagerClientUiState.RaiseStateChanged();
            GridManagerClientUiCommands.TryGetBlocksFromChat();
        }

        protected override void UnloadData()
        {
            OnRichHudReset();
            base.UnloadData();
        }

        private void OnSharedStateChanged()
        {
            if (!RichHudClient.Registered)
            {
                return;
            }

            RefreshRhfContent();

            var focus = GridManagerClientUiState.PendingTerminalPage;
            if (!string.IsNullOrEmpty(focus))
            {
                GridManagerClientUiState.PendingTerminalPage = string.Empty;
                OpenRichHudToPage(focus);
            }
        }

        private void RefreshRhfContent()
        {
            RefreshGridPickerList();
            UpdateBlocksInfoHeaders();
            RefreshBlockPickerList();
        }

        private void UpdateBlocksInfoHeaders()
        {
            if (_blocksInfoCategory == null)
            {
                return;
            }

            if (GridManagerClientUiState.CurrentDetailGridId == 0L)
            {
                _blocksInfoCategory.HeaderText = "Block summary";
                _blocksInfoCategory.SubheaderText = "Open Grid list → Block details to load a grid first.";
                return;
            }

            var title = string.IsNullOrEmpty(GridManagerClientUiState.CurrentDetailGridName) ? "(grid)" : GridManagerClientUiState.CurrentDetailGridName;
            if (title.Length > 40)
            {
                title = title.Substring(0, 37) + "…";
            }

            _blocksInfoCategory.HeaderText = "Block summary";
            _blocksInfoCategory.SubheaderText =
                "Grid: " + title
                + "  |  id: " + GridManagerClientUiState.CurrentDetailGridId
                + "  |  owner: " + (string.IsNullOrEmpty(GridManagerClientUiState.LastBlocksMainOwner) ? "—" : GridManagerClientUiState.LastBlocksMainOwner);
        }

        private void RefreshBlockPickerList()
        {
            if (_blockTypePicker == null)
            {
                return;
            }

            long gid = GridManagerClientUiState.CurrentDetailGridId;
            if (gid != _lastBlocksListGridId)
            {
                ClearDeleteArmed();
                _lastBlocksListGridId = gid;
            }

            if (gid == 0L)
            {
                _blockTypePicker.List.Clear();
                return;
            }

            _blockTypePicker.List.Clear();

            var keys = new List<string>(GridManagerClientUiState.LastBlockCounts.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            const int maxBlockLines = 80;
            for (int i = 0; i < keys.Count; i++)
            {
                if (i >= maxBlockLines)
                {
                    break;
                }

                int cnt;
                GridManagerClientUiState.LastBlockCounts.TryGetValue(keys[i], out cnt);
                var key = keys[i];
                var displayKey = key;
                if (displayKey.Length > 64)
                {
                    displayKey = displayKey.Substring(0, 61) + "…";
                }

                var line = "x" + cnt + "  " + displayKey;
                _blockTypePicker.List.Add(new RichText(line), key);
            }
        }

        private void RefreshGridPickerList()
        {
            if (_gridPicker == null)
            {
                return;
            }

            _gridPicker.List.Clear();
            for (int i = 0; i < GridManagerClientUiState.LastGrids.Count; i++)
            {
                var r = GridManagerClientUiState.LastGrids[i];
                var name = string.IsNullOrEmpty(r.Name) ? "(unnamed)" : r.Name;
                var line = name + "\n  entity_id: " + r.EntityId;
                _gridPicker.List.Add(new RichText(line), r.EntityId);
            }
        }

        private static string BuildHelpText()
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine("This panel uses the same ModAPI messages as /gmg chat.");
            sb.AppendLine();
            sb.AppendLine("Grid list → Block details loads blocks. Pick a type, then Prepare delete, then Confirm delete (server).");
            sb.AppendLine("Or use /gmg block \"TypeId\" and hotkeys as before.");
            sb.AppendLine();
            sb.AppendLine("The Rich HUD terminal opens on hotkeys or when a server reply arrives (if RHF registered).");
            sb.AppendLine("You can also open it from Rich HUD Master keybinds / mod menu, then choose SE Grid Manager.");
            sb.AppendLine("Without Rich HUD Master, the mod uses mission screens + /gmg instead.");
            return sb.ToString();
        }
    }
}
