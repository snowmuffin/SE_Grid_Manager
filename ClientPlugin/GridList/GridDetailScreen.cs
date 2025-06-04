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
        public GridDetailScreen(string friendlyName)
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


        }
        public void RecreateControlsWithGridId(bool constructor, string gridid)
        {
            base.RecreateControls(constructor);
            AddCaption(gridid);
        }
        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);


        }
    }

}
