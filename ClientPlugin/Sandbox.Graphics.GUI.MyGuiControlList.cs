// Warning: Some assembly references could not be resolved automatically. This might lead to incorrect decompilation of some parts,
// for ex. property getter/setter access. To get optimal decompilation results, please manually add the missing references to the list of loaded assemblies.
// Sandbox.Graphics, Version=0.1.1.0, Culture=neutral, PublicKeyToken=null
// Sandbox.Graphics.GUI.MyGuiControlList
using System;
using System.Collections.Generic;
using Sandbox.Graphics;
using Sandbox.Graphics.GUI;
using VRage.Game;
using VRage.Utils;
using VRageMath;

[MyGuiControlType(typeof(MyObjectBuilder_GuiControlList))]
public class MyGuiControlList : MyGuiControlParent
{
	public class StyleDefinition
	{
		public MyGuiCompositeTexture Texture = new MyGuiCompositeTexture();

		public MyGuiBorderThickness ScrollbarMargin;

		public MyGuiBorderThickness ItemMargin;

		public bool ScrollbarEnabled;
	}

	private static StyleDefinition[] m_styles;

	private MyScrollbar m_scrollBar;

	private Vector2 m_realSize;

	private bool m_showScrollbar;

	private RectangleF m_itemsRectangle;

	private MyGuiBorderThickness m_itemMargin;

	private bool m_isChildFocused;

	private MyGuiControlListStyleEnum m_visualStyle;

	private StyleDefinition m_styleDef;

	public bool CompleteScissor;

	public MyGuiControlListStyleEnum VisualStyle
	{
		get
		{
			return m_visualStyle;
		}
		set
		{
			m_visualStyle = value;
			RefreshVisualStyle();
		}
	}

	static MyGuiControlList()
	{
		m_styles = new StyleDefinition[MyUtils.GetMaxValueFromEnum<MyGuiControlListStyleEnum>() + 1];
		m_styles[0] = new StyleDefinition
		{
			Texture = MyGuiConstants.TEXTURE_SCROLLABLE_LIST,
			ScrollbarMargin = new MyGuiBorderThickness
			{
				Left = 2f / MyGuiConstants.GUI_OPTIMAL_SIZE.X,
				Right = 1f / MyGuiConstants.GUI_OPTIMAL_SIZE.X,
				Top = 3f / MyGuiConstants.GUI_OPTIMAL_SIZE.Y,
				Bottom = 1f / MyGuiConstants.GUI_OPTIMAL_SIZE.Y
			},
			ItemMargin = new MyGuiBorderThickness(12f / MyGuiConstants.GUI_OPTIMAL_SIZE.X, 12f / MyGuiConstants.GUI_OPTIMAL_SIZE.Y),
			ScrollbarEnabled = true
		};
		m_styles[1] = new StyleDefinition
		{
			Texture = MyGuiConstants.TEXTURE_RECTANGLE_DARK_BORDER,
			ScrollbarMargin = new MyGuiBorderThickness
			{
				Left = 2f / MyGuiConstants.GUI_OPTIMAL_SIZE.X,
				Right = 1f / MyGuiConstants.GUI_OPTIMAL_SIZE.X,
				Top = 3f / MyGuiConstants.GUI_OPTIMAL_SIZE.Y,
				Bottom = 1f / MyGuiConstants.GUI_OPTIMAL_SIZE.Y
			},
			ItemMargin = new MyGuiBorderThickness(12f / MyGuiConstants.GUI_OPTIMAL_SIZE.X, 12f / MyGuiConstants.GUI_OPTIMAL_SIZE.Y),
			ScrollbarEnabled = true
		};
		m_styles[2] = new StyleDefinition
		{
			ScrollbarEnabled = true
		};
		m_styles[3] = new StyleDefinition
		{
			Texture = MyGuiConstants.TEXTURE_SCROLLABLE_WBORDER_LIST,
			ScrollbarMargin = new MyGuiBorderThickness
			{
				Left = 2f / MyGuiConstants.GUI_OPTIMAL_SIZE.X,
				Right = 1f / MyGuiConstants.GUI_OPTIMAL_SIZE.X,
				Top = 3f / MyGuiConstants.GUI_OPTIMAL_SIZE.Y,
				Bottom = 1f / MyGuiConstants.GUI_OPTIMAL_SIZE.Y
			},
			ItemMargin = new MyGuiBorderThickness(12f / MyGuiConstants.GUI_OPTIMAL_SIZE.X, 12f / MyGuiConstants.GUI_OPTIMAL_SIZE.Y),
			ScrollbarEnabled = true
		};
	}

	public static StyleDefinition GetVisualStyle(MyGuiControlListStyleEnum style)
	{
		return m_styles[(int)style];
	}

	public MyGuiControlList()
		: this(null, null, null, null, MyGuiControlListStyleEnum.Default)
	{
	}

	public MyGuiControlList(Vector2? position = null, Vector2? size = null, Vector4? backgroundColor = null, string toolTip = null, MyGuiControlListStyleEnum visualStyle = MyGuiControlListStyleEnum.Default)
		: base(position, size, backgroundColor, toolTip)
	{
		base.Name = "ControlList";
		m_realSize = size ?? Vector2.One;
		m_scrollBar = new MyVScrollbar(this);
		m_scrollBar.ValueChanged += ValueChanged;
		VisualStyle = visualStyle;
		RecalculateScrollbar();
		base.Controls.CollectionChanged += OnVisibleControlsChanged;
		base.Controls.CollectionMembersVisibleChanged += OnVisibleControlsChanged;
		base.GamepadHelpTextId = MyCommonTexts.Gamepad_help_ControlList;
	}

	public override void Init(MyObjectBuilder_GuiControlBase builder)
	{
		base.Init(builder);
		MyObjectBuilder_GuiControlList myObjectBuilder_GuiControlList = builder as MyObjectBuilder_GuiControlList;
		VisualStyle = myObjectBuilder_GuiControlList.VisualStyle;
	}

	public void InitControls(IEnumerable<MyGuiControlBase> controls)
	{
		base.Controls.CollectionMembersVisibleChanged -= OnVisibleControlsChanged;
		base.Controls.CollectionChanged -= OnVisibleControlsChanged;
		base.Controls.Clear();
		foreach (MyGuiControlBase control in controls)
		{
			if (control != null)
			{
				base.Controls.Add(control);
			}
		}
		base.Controls.CollectionChanged += OnVisibleControlsChanged;
		base.Controls.CollectionMembersVisibleChanged += OnVisibleControlsChanged;
		Recalculate();
	}

	public override MyObjectBuilder_GuiControlBase GetObjectBuilder()
	{
		MyObjectBuilder_GuiControlList obj = base.GetObjectBuilder() as MyObjectBuilder_GuiControlList;
		obj.VisualStyle = VisualStyle;
		return obj;
	}

	public override void Draw(float transitionAlpha, float backgroundTransitionAlpha)
	{
		DrawBorder(transitionAlpha);
		Vector2 positionAbsoluteTopLeft = GetPositionAbsoluteTopLeft();
		m_styleDef.Texture.Draw(positionAbsoluteTopLeft, base.Size, MyGuiControlBase.ApplyColorMaskModifiers(base.ColorMask, base.Enabled, backgroundTransitionAlpha));
		RectangleF normalizedRectangle = m_itemsRectangle;
		normalizedRectangle.Position += positionAbsoluteTopLeft;
		using (MyGuiManager.UsingScissorRectangle(ref normalizedRectangle))
		{
			foreach (MyGuiControlBase visibleControl in base.Controls.GetVisibleControls())
			{
				visibleControl.CheckIsWithinScissor(normalizedRectangle, CompleteScissor);
			}
			base.Draw(transitionAlpha, backgroundTransitionAlpha);
		}
		if (m_showScrollbar)
		{
			m_scrollBar.Draw(MyGuiControlBase.ApplyColorMaskModifiers(base.ColorMask, base.Enabled, transitionAlpha));
		}
		Vector2 positionAbsoluteTopRight = GetPositionAbsoluteTopRight();
		positionAbsoluteTopRight.X -= m_styleDef.ScrollbarMargin.HorizontalSum + m_scrollBar.Size.X;
		MyGuiManager.DrawSpriteBatch("Textures\\GUI\\Controls\\scrollable_list_line.dds", positionAbsoluteTopRight, new Vector2(0.001f, base.Size.Y), MyGuiControlBase.ApplyColorMaskModifiers(base.ColorMask, base.Enabled, transitionAlpha), MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
	}

	public override RectangleF? GetScissoringArea()
	{
		RectangleF itemsRectangle = m_itemsRectangle;
		itemsRectangle.Position += GetPositionAbsoluteTopLeft();
		return RectangleF.Min(base.GetScissoringArea(), itemsRectangle);
	}

	private void DebugDraw()
	{
		MyGuiManager.DrawBorders(GetPositionAbsoluteTopLeft() + m_itemsRectangle.Position, m_itemsRectangle.Size, Color.White, 1);
	}

	public override MyGuiControlBase HandleInput()
	{
		MyGuiControlBase myGuiControlBase = base.HandleInput();
		if (myGuiControlBase == null && m_showScrollbar && m_scrollBar != null && m_scrollBar.Visible)
		{
			if (CheckMouseOver())
			{
				if (m_scrollBar.HandleInput())
				{
					return this;
				}
				return myGuiControlBase;
			}
			if (CheckChildFocus() || base.HasFocus)
			{
				if (m_scrollBar.HandleInput(fakeFocus: true))
				{
					return this;
				}
				return myGuiControlBase;
			}
		}
		return myGuiControlBase;
	}

	private void FixControlSelection()
	{
		if (!m_isChildFocused)
		{
			return;
		}
		MyGuiScreenBase topMostOwnerScreen = GetTopMostOwnerScreen();
		MyGuiControlBase myGuiControlBase = topMostOwnerScreen?.FocusedControl;
		if (myGuiControlBase == null)
		{
			return;
		}
		RectangleF itemsRectangle = m_itemsRectangle;
		Vector2 positionAbsoluteTopLeft = GetPositionAbsoluteTopLeft();
		Vector2 vector = GetPositionAbsoluteTopLeft() + itemsRectangle.Size;
		bool flag = false;
		bool flag2 = false;
		while (true)
		{
			Vector2 positionAbsoluteTopLeft2 = myGuiControlBase.GetPositionAbsoluteTopLeft();
			Vector2 vector2 = positionAbsoluteTopLeft2 + myGuiControlBase.Size;
			bool flag3 = vector2.X > positionAbsoluteTopLeft.X;
			bool flag4 = positionAbsoluteTopLeft2.X < vector.X;
			bool flag5 = vector2.Y > positionAbsoluteTopLeft.Y;
			bool flag6 = positionAbsoluteTopLeft2.Y < vector.Y;
			if (flag5 && flag6 && flag3 && flag4)
			{
				if (topMostOwnerScreen.FocusedControl != myGuiControlBase)
				{
					topMostOwnerScreen.FocusedControl = myGuiControlBase;
				}
				return;
			}
			MyGuiControlBase myGuiControlBase2 = null;
			if (!flag5)
			{
				flag = true;
				if (flag2)
				{
					topMostOwnerScreen.FocusedControl = myGuiControlBase;
					return;
				}
				myGuiControlBase2 = GetNextFocusControl(myGuiControlBase, MyDirection.Down, page: false);
			}
			else
			{
				if (flag6)
				{
					return;
				}
				flag2 = true;
				if (flag)
				{
					topMostOwnerScreen.FocusedControl = myGuiControlBase;
					return;
				}
				myGuiControlBase2 = GetNextFocusControl(myGuiControlBase, MyDirection.Up, page: false);
			}
			if (myGuiControlBase2 == null)
			{
				m_isChildFocused = false;
				topMostOwnerScreen.FocusedControl = this;
				return;
			}
			MyGuiControlBase myGuiControlBase3 = myGuiControlBase2;
			m_isChildFocused = false;
			while (myGuiControlBase3.Owner != null)
			{
				if (myGuiControlBase3.Owner == this)
				{
					m_isChildFocused = true;
					break;
				}
				myGuiControlBase3 = myGuiControlBase3.Owner as MyGuiControlBase;
				if (myGuiControlBase3 == null)
				{
					break;
				}
			}
			if (!m_isChildFocused)
			{
				break;
			}
			myGuiControlBase = myGuiControlBase2;
		}
		m_isChildFocused = false;
		topMostOwnerScreen.FocusedControl = this;
	}

	protected override void OnPositionChanged()
	{
		base.OnPositionChanged();
		RecalculateScrollbar();
		CalculateNewPositionsForControls((m_scrollBar != null) ? m_scrollBar.Value : 0f);
	}

	protected override void OnSizeChanged()
	{
		base.OnSizeChanged();
		RefreshInternals();
	}

	public void Recalculate()
	{
		_ = m_realSize;
		CalculateRealSize();
		m_itemsRectangle.Position = m_itemMargin.TopLeftOffset;
		m_itemsRectangle.Size = base.Size - (m_itemMargin.SizeChange + new Vector2(m_styleDef.ScrollbarMargin.HorizontalSum + (m_showScrollbar ? m_scrollBar.Size.X : 0f), 0f));
		RecalculateScrollbar();
		CalculateNewPositionsForControls((m_scrollBar != null) ? m_scrollBar.Value : 0f);
	}

	public MyGuiBorderThickness GetItemMargins()
	{
		return m_itemMargin;
	}

	private void RecalculateScrollbar()
	{
		if (m_showScrollbar)
		{
			m_scrollBar.Visible = base.Size.Y < m_realSize.Y;
			m_scrollBar.Init(m_realSize.Y, m_itemsRectangle.Size.Y);
			Vector2 vector = base.Size * new Vector2(0.5f, -0.5f);
			MyGuiBorderThickness scrollbarMargin = m_styleDef.ScrollbarMargin;
			Vector2 position = new Vector2(vector.X - (scrollbarMargin.Right + m_scrollBar.Size.X), vector.Y + scrollbarMargin.Top);
			m_scrollBar.Layout(position, base.Size.Y - scrollbarMargin.VerticalSum);
		}
	}

	private void ValueChanged(MyScrollbar scrollbar)
	{
		CalculateNewPositionsForControls(scrollbar.Value);
	}

	private void CalculateNewPositionsForControls(float offset)
	{
		Vector2 marginStep = m_itemMargin.MarginStep;
		Vector2 topLeft = -0.5f * base.Size + 0.001f + m_itemMargin.TopLeftOffset - new Vector2(-1f / MyGuiConstants.GUI_OPTIMAL_SIZE.X, offset);
		foreach (MyGuiControlBase visibleControl in base.Controls.GetVisibleControls())
		{
			Vector2 size = visibleControl.Size;
			size.X = m_itemsRectangle.Size.X;
			visibleControl.Position = MyUtils.GetCoordAlignedFromTopLeft(topLeft, size, visibleControl.OriginAlign);
			topLeft.Y += size.Y + marginStep.Y;
		}
	}

	private void CalculateRealSize()
	{
		Vector2 zero = Vector2.Zero;
		float y = m_itemMargin.MarginStep.Y;
		foreach (MyGuiControlBase visibleControl in base.Controls.GetVisibleControls())
		{
			Vector2 value = visibleControl.GetSize().Value;
			zero.Y += value.Y + y;
			zero.X = Math.Max(zero.X, value.X);
		}
		zero.Y -= y;
		m_realSize.X = Math.Max(base.Size.X, zero.X);
		m_realSize.Y = Math.Max(base.Size.Y, zero.Y);
	}

	private void RefreshVisualStyle()
	{
		m_styleDef = GetVisualStyle(VisualStyle);
		m_itemMargin = m_styleDef.ItemMargin;
		m_showScrollbar = m_styleDef.ScrollbarEnabled;
		base.MinSize = m_styleDef.Texture.MinSizeGui;
		base.MaxSize = m_styleDef.Texture.MaxSizeGui;
		RefreshInternals();
	}

	private void RefreshInternals()
	{
		Recalculate();
	}

	private void OnVisibleControlsChanged(MyGuiControls sender)
	{
		Recalculate();
	}

	protected override void OnHasHighlightChanged()
	{
		base.OnHasHighlightChanged();
		m_scrollBar.HasHighlight = base.HasHighlight;
	}

	public void SetScrollBarPage(float page = 0f)
	{
		m_scrollBar.SetPage(page);
	}

	public MyScrollbar GetScrollBar()
	{
		return m_scrollBar;
	}

	public override void OnFocusChanged(MyGuiControlBase control, bool focus)
	{
		if (focus && !m_primaryMouseButtonPressed && !m_secondaryMouseButtonPressed)
		{
			RectangleF itemsRectangle = m_itemsRectangle;
			itemsRectangle.Position += GetPositionAbsoluteTopLeft();
			RectangleF rectangle = control.Rectangle;
			if (rectangle.Y < itemsRectangle.Y)
			{
				m_scrollBar.Value += rectangle.Y - itemsRectangle.Y;
			}
			else if (rectangle.Y + rectangle.Size.Y > itemsRectangle.Y + itemsRectangle.Height)
			{
				m_scrollBar.Value += rectangle.Y + rectangle.Size.Y - itemsRectangle.Y - itemsRectangle.Height;
			}
		}
		base.Owner?.OnFocusChanged(control, focus);
	}
}
