using Sandbox;
using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;
using ClientPlugin.GridList.Elements;
using VRageMath;

namespace ClientPlugin.GridList
{
    internal class GridListScreen : MyGuiScreenBase
    {
        public readonly string FriendlyName;
        public Func<List<MyGuiControlBase>> GetControls;

        public override string GetFriendlyName() => FriendlyName;

        public GridListScreen(
            string friendlyName,
            Func<List<MyGuiControlBase>> getControls,
            Vector2? position = null,
            Vector2? size = null

            ) : base(
                position ?? new Vector2(0.5f, 0.5f),
                MyGuiConstants.SCREEN_BACKGROUND_COLOR,
                size ?? new Vector2(0.3f, 0.42f),
                false,
                null,
                MySandboxGame.Config.UIBkOpacity,
                MySandboxGame.Config.UIOpacity)
        {
            FriendlyName = friendlyName;
            GetControls = getControls;

            EnabledBackgroundFade = true;
            m_closeOnEsc = true;
            m_drawEvenWithoutFocus = true;
            CanHideOthers = true;
            CanBeHidden = true;
            CloseButtonEnabled = true;
        }

        public void UpdateSize(Vector2 screenSize)
        {
            Size = screenSize;
            CloseButtonEnabled = CloseButtonEnabled; // Force close button to update
        }

        public override void LoadContent()
        {
            base.LoadContent();
            RecreateControls(true);
        }


        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            AddCaption(Name);

            foreach (var item in GetControls())
            {
                Controls.Add(item);
            }
        }
    }
}
