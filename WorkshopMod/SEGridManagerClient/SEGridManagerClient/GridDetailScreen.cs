using System;
using System.Collections.Generic;
using System.Linq;
using VRage;
using VRageMath;
using VRage.Utils;
using Sandbox.Graphics.GUI;

namespace SEGridManagerClient
{
    /// <summary>Per-block-type delete buttons and Back.</summary>
    internal sealed class GridDetailScreen : MyGuiScreenBase
    {
        public override string GetFriendlyName() => "SEGridManager.GridDetail";

        private const float RowStep = 0.034f;
        private const int MaxBlockRows = 16;

        public static GridDetailScreen Active;

        private long _gridId;
        private string _gridName = string.Empty;
        private string _mainOwner = string.Empty;
        private readonly List<string> _defOrder = new List<string>();
        private Dictionary<string, int> _blocks = new Dictionary<string, int>(StringComparer.Ordinal);
        private Action<string> _onDelete;
        private Action _onBack;

        public GridDetailScreen(
            long gridId,
            string gridName,
            string mainOwner,
            IReadOnlyDictionary<string, int> blocks,
            Action<string> onDelete,
            Action onBack)
            : base(new Vector2(0.5f, 0.5f), null, new Vector2(0.6f, 0.75f), true, null, 0f, 0f)
        {
            _gridId = gridId;
            _gridName = gridName ?? string.Empty;
            _mainOwner = mainOwner ?? string.Empty;
            _onDelete = onDelete;
            _onBack = onBack;
            _blocks = new Dictionary<string, int>(StringComparer.Ordinal);
            if (blocks != null)
            {
                foreach (var kv in blocks)
                {
                    _blocks[kv.Key] = kv.Value;
                }
            }

            _defOrder.Clear();
            if (_blocks.Count > 0)
            {
                _defOrder.AddRange(_blocks.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
            }
        }

        protected override void OnClosed()
        {
            if (ReferenceEquals(Active, this))
            {
                Active = null;
            }

            base.OnClosed();
        }

        public static void Open(
            long gridId,
            string gridName,
            string mainOwner,
            IReadOnlyDictionary<string, int> blocks,
            Action<string> onDelete,
            Action onBack)
        {
            if (Active != null)
            {
                Active.CloseScreen();
                Active = null;
            }

            var s = new GridDetailScreen(gridId, gridName, mainOwner, blocks, onDelete, onBack);
            s.RecreateControls(true);
            MyScreenManager.AddScreen(s);
            Active = s;
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            Controls.Clear();
            var title = string.IsNullOrEmpty(_gridName) ? "(unnamed grid)" : _gridName;
            AddCaption(
                "SE Grid — " + (title.Length > 36 ? title.Substring(0, 33) + "…" : title),
                (Vector4?)new Vector4(0.9f, 0.95f, 1f, 1f),
                (Vector2?)new Vector2(0f, 0.01f),
                MyGuiConstants.DEFAULT_TEXT_SCALE * 0.8f);

            var info = "EntityId: " + _gridId + "   Main owner: " + (string.IsNullOrEmpty(_mainOwner) ? "—" : _mainOwner);
            var infoLbl = new MyGuiControlLabel();
            infoLbl.Text = info;
            infoLbl.TextScale = 0.55f;
            infoLbl.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP;
            infoLbl.Position = new Vector2(-0.28f, -0.2f);
            Controls.Add(infoLbl);

            float y = -0.14f;
            int n = Math.Min(_defOrder.Count, MaxBlockRows);
            for (int i = 0; i < n; i++)
            {
                var def = _defOrder[i];
                int cnt;
                _blocks.TryGetValue(def, out cnt);
                var line = (def.Length > 52 ? def.Substring(0, 49) + "…" : def) + "  x" + cnt;
                var lbl = new MyGuiControlLabel();
                lbl.Text = line;
                lbl.TextScale = 0.52f;
                lbl.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP;
                lbl.Position = new Vector2(-0.28f, y);
                lbl.Size = new Vector2(0.4f, 0.03f);
                Controls.Add(lbl);

                var del = new MyGuiControlButton();
                del.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP;
                del.Text = "Delete";
                del.TextScale = 0.55f;
                del.Position = new Vector2(0.12f, y);
                del.Size = new Vector2(0.1f, 0.03f);
                var d = def;
                del.ButtonClicked += _ => { _onDelete?.Invoke(d); };
                Controls.Add(del);
                y += RowStep;
            }

            if (_defOrder.Count > MaxBlockRows)
            {
                var more = new MyGuiControlLabel();
                more.Text = "…" + (_defOrder.Count - MaxBlockRows) + " more types not shown (scroll in a future build)";
                more.TextScale = 0.45f;
                more.Position = new Vector2(-0.28f, y);
                Controls.Add(more);
            }

            var back = new MyGuiControlButton();
            back.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP;
            back.Text = "Back to list";
            back.TextScale = 0.65f;
            back.Position = new Vector2(-0.2f, 0.3f);
            back.Size = new Vector2(0.2f, 0.04f);
            back.ButtonClicked += _ => { _onBack?.Invoke(); };
            Controls.Add(back);
        }
    }
}
