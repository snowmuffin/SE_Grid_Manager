using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;
using VRageMath;

namespace ClientPlugin.GridList
{
    public class GridDetailScreen : MyGuiScreenBase
    {
        public readonly string FriendlyName;
        public override string GetFriendlyName() => FriendlyName;
        public GridDetailScreen(string friendlyName,Func<List<MyGuiControlBase>> controlsFactory)
            : base(new Vector2(0.5f, 0.5f), MyGuiConstants.SCREEN_BACKGROUND_COLOR, new Vector2(0.6f, 0.8f))
        {
            FriendlyName = friendlyName;
            EnabledBackgroundFade = true;
            CanHaveFocus = true;
            CloseButtonEnabled = true;
            RecreateControls(true);
            m_closeOnEsc = true;
            m_isTopMostScreen = true;
            m_isTopScreen = true;
            m_drawEvenWithoutFocus = true;
            var controls = controlsFactory?.Invoke() ?? new List<MyGuiControlBase>();

            // 컨트롤이 MyGuiControlList 하나만 있다고 가정하고, 중앙에 배치
            if (controls.Count > 0)
            {
                var list = controls[0];
                list.Position = new Vector2(0.05f, 0.02f);
                Controls.Add(list);
            }
        }

    }
}
