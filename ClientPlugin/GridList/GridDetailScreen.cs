using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;
using VRageMath;

namespace ClientPlugin.GridList
{
    public class GridDetailScreen : MyGuiScreenBase
    {
        public GridDetailScreen(string title, Func<List<MyGuiControlBase>> controlsFactory)
            : base(Vector2.Zero, null, new Vector2(0.5f, 0.7f))
        {
            EnabledBackgroundFade = true;
            CanHaveFocus = true;
            CloseButtonEnabled = true;
            RecreateControls(true);
            m_closeOnEsc = true;
            m_isTopMostScreen = true;
            m_isTopScreen = true;
            m_drawEvenWithoutFocus = true;
            var controls = controlsFactory?.Invoke() ?? new List<MyGuiControlBase>();
            float y = 0.05f;
            var titleLabel = new MyGuiControlLabel(text: title) { Position = new Vector2(0.05f, y) };
            Controls.Add(titleLabel);
            y += titleLabel.Size.Y + 0.01f;
            foreach (var c in controls)
            {
                c.Position = new Vector2(0.05f, y);
                Controls.Add(c);
                y += c.Size.Y + 0.01f;
            }
        }

        public override string GetFriendlyName() => "GridDetailScreen";
    }
}
