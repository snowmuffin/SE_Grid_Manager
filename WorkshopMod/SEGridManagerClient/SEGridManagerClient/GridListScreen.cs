using System;
using System.Collections.Generic;
using VRage;
using VRageMath;
using VRage.Utils;
using Sandbox.Graphics.GUI;

namespace SEGridManagerClient
{
    /// <summary>Grid list with one MyGui button per row (Detail) plus Refresh / Close.</summary>
    internal sealed class GridListScreen : MyGuiScreenBase
    {
        public override string GetFriendlyName() => "SEGridManager.GridList";

        private const float RowStep = 0.04f;
        private const int MaxRows = 12;
        private const float TextScale = 0.62f;

        private List<GridManagerParse.GridRow> _grids = new List<GridManagerParse.GridRow>();
        private readonly Action<long> _onDetail;
        private readonly Action _onClose;
        private readonly Action _onRefresh;

        public static GridListScreen Active;

        public GridListScreen(Action<long> onDetail, Action onClose, Action onRefresh)
            : base(new Vector2(0.5f, 0.5f), null, new Vector2(0.58f, 0.72f), true, null, 0f, 0f)
        {
            _onDetail = onDetail;
            _onClose = onClose;
            _onRefresh = onRefresh;
        }

        public void SetGrids(IReadOnlyList<GridManagerParse.GridRow> grids)
        {
            _grids = grids == null ? new List<GridManagerParse.GridRow>() : new List<GridManagerParse.GridRow>(grids);
            RecreateControls(true);
        }

        protected override void OnClosed()
        {
            if (ReferenceEquals(Active, this))
            {
                Active = null;
            }

            base.OnClosed();
        }

        public static void OpenOrReplace(
            Action<long> onDetail,
            Action onClose,
            Action onRefresh,
            IReadOnlyList<GridManagerParse.GridRow> initial)
        {
            if (Active != null)
            {
                Active.CloseScreen();
                Active = null;
            }

            var s = new GridListScreen(onDetail, onClose, onRefresh);
            if (initial != null)
            {
                s._grids = new List<GridManagerParse.GridRow>(initial);
            }

            s.RecreateControls(true);
            MyScreenManager.AddScreen(s);
            Active = s;
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            Controls.Clear();
            AddCaption(
                "SE Grid Manager — your grids (Torch + plugin)",
                (Vector4?)new Vector4(0.85f, 0.9f, 1f, 1f),
                (Vector2?)new Vector2(0f, 0.01f),
                MyGuiConstants.DEFAULT_TEXT_SCALE * 0.82f);

            float y = -0.2f;
            int count = Math.Min(_grids.Count, MaxRows);
            for (int i = 0; i < count; i++)
            {
                var g = _grids[i];
                long id = g.EntityId;
                var line = (string.IsNullOrEmpty(g.Name) ? "(unnamed)" : g.Name) + "  [id: " + g.EntityId + "]";

                var row = new MyGuiControlLabel
                {
                    Text = line,
                    TextScale = TextScale,
                    OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                    Position = new Vector2(-0.25f, y),
                };
                Controls.Add(row);

                var det = new MyGuiControlButton();
                det.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP;
                det.Text = "Detail";
                det.TextScale = TextScale * 1.1f;
                det.Position = new Vector2(0.12f, y);
                det.Size = new Vector2(0.1f, 0.034f);
                var captured = id;
                det.ButtonClicked += _ => { _onDetail?.Invoke(captured); };
                Controls.Add(det);
                y += RowStep;
            }

            if (_grids.Count == 0)
            {
                var empty = new MyGuiControlLabel
                {
                    Text = "No grids (or still loading)…",
                    TextScale = 0.7f,
                    Position = new Vector2(0f, 0f),
                };
                Controls.Add(empty);
            }
            else if (_grids.Count > MaxRows)
            {
                var more = new MyGuiControlLabel
                {
                    Text = "… first " + MaxRows + " only — narrow list in a future build …",
                    TextScale = 0.55f,
                    OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                    Position = new Vector2(-0.25f, y),
                };
                Controls.Add(more);
            }

            var refresh = new MyGuiControlButton();
            refresh.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP;
            refresh.Text = "Refresh list";
            refresh.TextScale = 0.65f;
            refresh.Position = new Vector2(-0.22f, 0.28f);
            refresh.Size = new Vector2(0.18f, 0.04f);
            refresh.ButtonClicked += _ => { _onRefresh?.Invoke(); };
            Controls.Add(refresh);

            var close = new MyGuiControlButton();
            close.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP;
            close.Text = "Close";
            close.TextScale = 0.65f;
            close.Position = new Vector2(0.05f, 0.28f);
            close.Size = new Vector2(0.14f, 0.04f);
            close.ButtonClicked += _ => { _onClose?.Invoke(); };
            Controls.Add(close);
        }
    }
}
