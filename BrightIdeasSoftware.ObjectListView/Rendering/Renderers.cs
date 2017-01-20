using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using Timer = System.Threading.Timer;

namespace BrightIdeasSoftware
{
	/// <summary>
	/// Renderers are the mechanism used for owner drawing cells. As such, they can also handle
	/// hit detection and positioning of cell editing rectangles.
	/// </summary>
	public interface IRenderer
	{
		/// <summary>
		/// Render the whole item within an ObjectListView. This is only used in non-Details views.
		/// </summary>
		/// <param name="e">The event</param>
		/// <param name="g">A Graphics for rendering</param>
		/// <param name="itemBounds">The bounds of the item</param>
		/// <param name="rowObject">The model object to be drawn</param>
		/// <returns>Return true to indicate that the event was handled and no further processing is needed.</returns>
		bool RenderItem(DrawListViewItemEventArgs e, Graphics g, Rectangle itemBounds, object rowObject);

		/// <summary>
		/// Render one cell within an ObjectListView when it is in Details mode.
		/// </summary>
		/// <param name="e">The event</param>
		/// <param name="g">A Graphics for rendering</param>
		/// <param name="cellBounds">The bounds of the cell</param>
		/// <param name="rowObject">The model object to be drawn</param>
		/// <returns>Return true to indicate that the event was handled and no further processing is needed.</returns>
		bool RenderSubItem(DrawListViewSubItemEventArgs e, Graphics g, Rectangle cellBounds, object rowObject);

		/// <summary>
		/// What is under the given point?
		/// </summary>
		/// <param name="hti"></param>
		/// <param name="x">x co-ordinate</param>
		/// <param name="y">y co-ordinate</param>
		/// <remarks>This method should only alter HitTestLocation and/or UserData.</remarks>
		void HitTest(OlvListViewHitTestInfo hti, int x, int y);

		/// <summary>
		/// When the value in the given cell is to be edited, where should the edit rectangle be placed?
		/// </summary>
		/// <param name="g"></param>
		/// <param name="cellBounds"></param>
		/// <param name="item"></param>
		/// <param name="subItemIndex"></param>
		/// <param name="preferredSize"> </param>
		/// <returns></returns>
		Rectangle GetEditRectangle(Graphics g, Rectangle cellBounds, OLVListItem item, int subItemIndex, Size preferredSize);
	}

	/// <summary>
	/// An AbstractRenderer is a do-nothing implementation of the IRenderer interface.
	/// </summary>
	[Browsable(true),
	 ToolboxItem(false)]
	public class AbstractRenderer : Component, IRenderer
	{
		#region IRenderer Members

		/// <summary>
		/// Render the whole item within an ObjectListView. This is only used in non-Details views.
		/// </summary>
		/// <param name="e">The event</param>
		/// <param name="g">A Graphics for rendering</param>
		/// <param name="itemBounds">The bounds of the item</param>
		/// <param name="rowObject">The model object to be drawn</param>
		/// <returns>Return true to indicate that the event was handled and no further processing is needed.</returns>
		public virtual bool RenderItem(DrawListViewItemEventArgs e, Graphics g, Rectangle itemBounds, object rowObject)
		{
			return true;
		}

		/// <summary>
		/// Render one cell within an ObjectListView when it is in Details mode.
		/// </summary>
		/// <param name="e">The event</param>
		/// <param name="g">A Graphics for rendering</param>
		/// <param name="cellBounds">The bounds of the cell</param>
		/// <param name="rowObject">The model object to be drawn</param>
		/// <returns>Return true to indicate that the event was handled and no further processing is needed.</returns>
		public virtual bool RenderSubItem(DrawListViewSubItemEventArgs e, Graphics g, Rectangle cellBounds, object rowObject)
		{
			return false;
		}

		/// <summary>
		/// What is under the given point?
		/// </summary>
		/// <param name="hti"></param>
		/// <param name="x">x co-ordinate</param>
		/// <param name="y">y co-ordinate</param>
		/// <remarks>This method should only alter HitTestLocation and/or UserData.</remarks>
		public virtual void HitTest(OlvListViewHitTestInfo hti, int x, int y) { }

		/// <summary>
		/// When the value in the given cell is to be edited, where should the edit rectangle be placed?
		/// </summary>
		/// <param name="g"></param>
		/// <param name="cellBounds"></param>
		/// <param name="item"></param>
		/// <param name="subItemIndex"></param>
		/// <param name="preferredSize"> </param>
		/// <returns></returns>
		public virtual Rectangle GetEditRectangle(Graphics g, Rectangle cellBounds, OLVListItem item, int subItemIndex, Size preferredSize)
		{
			return cellBounds;
		}

		#endregion
	}

	/// <summary>
	/// A BaseRenderer provides useful base level functionality for any custom renderer.
	/// </summary>
	/// <remarks>
	/// <para>Subclasses will normally override the Render or OptionalRender method, and use the other
	/// methods as helper functions.</para>
	/// </remarks>
	[Browsable(true),
	 ToolboxItem(true)]
	public class BaseRenderer : AbstractRenderer
	{
		internal const TextFormatFlags NormalTextFormatFlags = TextFormatFlags.NoPrefix |
															   TextFormatFlags.EndEllipsis |
															   TextFormatFlags.PreserveGraphicsTranslateTransform;

		#region Configuration Properties

		/// <summary>
		/// Can the renderer wrap lines that do not fit completely within the cell?
		/// </summary>
		/// <remarks>Wrapping text doesn't work with the GDI renderer.</remarks>
		[Category("Appearance"),
		 Description("Can the renderer wrap text that does not fit completely within the cell"),
		 DefaultValue(false)]
		public bool CanWrap
		{
			get { return canWrap; }
			set
			{
				canWrap = value;
				if (canWrap)
					UseGdiTextRendering = false;
			}
		}
		private bool canWrap;

		/// <summary>
		/// Gets the horiztonal alignment of the column
		/// </summary>
		[Browsable(false)]
		public HorizontalAlignment CellHorizontalAlignment
		{
			get { return Column == null ? HorizontalAlignment.Left : Column.TextAlign; }
		}

		/// <summary>
		/// Gets the optional padding that this renderer should apply before drawing.
		/// This property considers all possible sources of padding
		/// </summary>
		[Browsable(false)]
		protected virtual Rectangle? EffectiveCellPadding
		{
			get
			{
				if (OLVSubItem != null && OLVSubItem.CellPadding.HasValue)
					return OLVSubItem.CellPadding.Value;

				if (ListItem != null && ListItem.CellPadding.HasValue)
					return ListItem.CellPadding.Value;

				if (Column != null && Column.CellPadding.HasValue)
					return Column.CellPadding.Value;

				if (ListView != null && ListView.CellPadding.HasValue)
					return ListView.CellPadding.Value;

				return null;
			}
		}

		/// <summary>
		/// Gets the vertical cell alignment that should govern the rendering.
		/// This property considers all possible sources.
		/// </summary>
		[Browsable(false)]
		protected virtual StringAlignment EffectiveCellVerticalAlignment
		{
			get
			{
				if (OLVSubItem != null && OLVSubItem.CellVerticalAlignment.HasValue)
					return OLVSubItem.CellVerticalAlignment.Value;

				if (ListItem != null && ListItem.CellVerticalAlignment.HasValue)
					return ListItem.CellVerticalAlignment.Value;

				if (Column != null && Column.CellVerticalAlignment.HasValue)
					return Column.CellVerticalAlignment.Value;

				if (ListView != null)
					return ListView.CellVerticalAlignment;

				return StringAlignment.Center;
			}
		}

		/// <summary>
		/// Gets or sets the image list from which keyed images will be fetched
		/// </summary>
		[Category("Appearance"),
		 Description("The image list from which keyed images will be fetched for drawing. If this is not given, the small ImageList from the ObjectListView will be used"),
		 DefaultValue(null)]
		public ImageList ImageList
		{
			get { return imageList; }
			set { imageList = value; }
		}

		private ImageList imageList;

		/// <summary>
		/// When rendering multiple images, how many pixels should be between each image?
		/// </summary>
		[Category("Appearance"),
		 Description("When rendering multiple images, how many pixels should be between each image?"),
		 DefaultValue(1)]
		public int Spacing
		{
			get { return spacing; }
			set { spacing = value; }
		}

		private int spacing = 1;

		/// <summary>
		/// Should text be rendered using GDI routines? This makes the text look more
		/// like a native List view control.
		/// </summary>
		[Category("Appearance"),
		 Description("Should text be rendered using GDI routines?"),
		 DefaultValue(true)]
		public virtual bool UseGdiTextRendering
		{
			get
			{
				// Can't use GDI routines on a GDI+ printer context
				return !IsPrinting && useGdiTextRendering;
			}
			set { useGdiTextRendering = value; }
		}
		private bool useGdiTextRendering = true;

		#endregion

		#region State Properties

		/// <summary>
		/// Get or set the aspect of the model object that this renderer should draw
		/// </summary>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public object Aspect
		{
			get
			{
				if (aspect == null)
					aspect = column.GetValue(rowObject);
				return aspect;
			}
			set { aspect = value; }
		}

		private object aspect;

		/// <summary>
		/// What are the bounds of the cell that is being drawn?
		/// </summary>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Rectangle Bounds
		{
			get { return bounds; }
			set { bounds = value; }
		}

		private Rectangle bounds;

		/// <summary>
		/// Get or set the OLVColumn that this renderer will draw
		/// </summary>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public OLVColumn Column
		{
			get { return column; }
			set { column = value; }
		}

		private OLVColumn column;

		/// <summary>
		/// Get/set the event that caused this renderer to be called
		/// </summary>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public DrawListViewItemEventArgs DrawItemEvent
		{
			get { return drawItemEventArgs; }
			set { drawItemEventArgs = value; }
		}

		private DrawListViewItemEventArgs drawItemEventArgs;

		/// <summary>
		/// Get/set the event that caused this renderer to be called
		/// </summary>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public DrawListViewSubItemEventArgs Event
		{
			get { return eventArgs; }
			set { eventArgs = value; }
		}

		private DrawListViewSubItemEventArgs eventArgs;

		/// <summary>
		/// Gets or  sets the font to be used for text in this cell
		/// </summary>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Font Font
		{
			get
			{
				if (font != null || ListItem == null)
					return font;

				if (SubItem == null || ListItem.UseItemStyleForSubItems)
					return ListItem.Font;

				return SubItem.Font;
			}
			set { font = value; }
		}

		private Font font;

		/// <summary>
		/// Gets the image list from which keyed images will be fetched
		/// </summary>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public ImageList ImageListOrDefault
		{
			get { return ImageList ?? ListView.SmallImageList; }
		}

		/// <summary>
		/// Should this renderer fill in the background before drawing?
		/// </summary>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool IsDrawBackground
		{
			get { return !IsPrinting; }
		}

		/// <summary>
		/// Cache whether or not our item is selected
		/// </summary>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool IsItemSelected
		{
			get { return isItemSelected; }
			set { isItemSelected = value; }
		}

		private bool isItemSelected;

		/// <summary>
		/// Is this renderer being used on a printer context?
		/// </summary>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool IsPrinting
		{
			get { return isPrinting; }
			set { isPrinting = value; }
		}

		private bool isPrinting;

		/// <summary>
		/// Get or set the listitem that this renderer will be drawing
		/// </summary>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public OLVListItem ListItem
		{
			get { return listItem; }
			set { listItem = value; }
		}

		private OLVListItem listItem;

		/// <summary>
		/// Get/set the listview for which the drawing is to be done
		/// </summary>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public ObjectListView ListView
		{
			get { return objectListView; }
			set { objectListView = value; }
		}

		private ObjectListView objectListView;

		/// <summary>
		/// Get the specialized OLVSubItem that this renderer is drawing
		/// </summary>
		/// <remarks>This returns null for column 0.</remarks>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public OLVListSubItem OLVSubItem
		{
			get { return listSubItem as OLVListSubItem; }
		}

		/// <summary>
		/// Get or set the model object that this renderer should draw
		/// </summary>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public object RowObject
		{
			get { return rowObject; }
			set { rowObject = value; }
		}

		private object rowObject;

		/// <summary>
		/// Get or set the list subitem that this renderer will be drawing
		/// </summary>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public OLVListSubItem SubItem
		{
			get { return listSubItem; }
			set { listSubItem = value; }
		}

		private OLVListSubItem listSubItem;

		/// <summary>
		/// The brush that will be used to paint the text
		/// </summary>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Brush TextBrush
		{
			get
			{
				if (textBrush == null)
					return new SolidBrush(GetForegroundColor());
				else
					return textBrush;
			}
			set { textBrush = value; }
		}

		private Brush textBrush;

		/// <summary>
		/// Will this renderer use the custom images from the parent ObjectListView
		/// to draw the checkbox images.
		/// </summary>
		/// <remarks>
		/// <para>
		/// If this is true, the renderer will use the images from the 
		/// StateImageList to represent checkboxes. 0 - unchecked, 1 - checked, 2 - indeterminate.
		/// </para>
		/// <para>If this is false (the default), then the renderer will use .NET's standard
		/// CheckBoxRenderer.</para>
		/// </remarks>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool UseCustomCheckboxImages
		{
			get { return useCustomCheckboxImages; }
			set { useCustomCheckboxImages = value; }
		}

		private bool useCustomCheckboxImages;

		private void ClearState()
		{
			Event = null;
			DrawItemEvent = null;
			Aspect = null;
			Font = null;
			TextBrush = null;
		}

		#endregion

		#region Utilities

		/// <summary>
		/// Align the second rectangle with the first rectangle,
		/// according to the alignment of the column
		/// </summary>
		/// <param name="outer">The cell's bounds</param>
		/// <param name="inner">The rectangle to be aligned within the bounds</param>
		/// <returns>An aligned rectangle</returns>
		protected virtual Rectangle AlignRectangle(Rectangle outer, Rectangle inner)
		{
			Rectangle r = new Rectangle(outer.Location, inner.Size);

			// Align horizontally depending on the column alignment
			if (inner.Width < outer.Width)
			{
				r.X = AlignHorizontally(outer, inner);
			}

			// Align vertically too
			if (inner.Height < outer.Height)
			{
				r.Y = AlignVertically(outer, inner);
			}

			return r;
		}

		/// <summary>
		/// Calculate the left edge of the rectangle that aligns the outer rectangle with the inner one 
		/// according to this renderer's horizontal alignment
		/// </summary>
		/// <param name="outer"></param>
		/// <param name="inner"></param>
		/// <returns></returns>
		protected int AlignHorizontally(Rectangle outer, Rectangle inner)
		{
			HorizontalAlignment alignment = CellHorizontalAlignment;
			switch (alignment)
			{
				case HorizontalAlignment.Left:
					return outer.Left + 1;
				case HorizontalAlignment.Center:
					return outer.Left + ((outer.Width - inner.Width) / 2);
				case HorizontalAlignment.Right:
					return outer.Right - inner.Width - 1;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}


		/// <summary>
		/// Calculate the top of the rectangle that aligns the outer rectangle with the inner rectangle
		/// according to this renders vertical alignment
		/// </summary>
		/// <param name="outer"></param>
		/// <param name="inner"></param>
		/// <returns></returns>
		protected int AlignVertically(Rectangle outer, Rectangle inner)
		{
			return AlignVertically(outer, inner.Height);
		}

		/// <summary>
		/// Calculate the top of the rectangle that aligns the outer rectangle with a rectangle of the given height
		/// according to this renderer's vertical alignment
		/// </summary>
		/// <param name="outer"></param>
		/// <param name="innerHeight"></param>
		/// <returns></returns>
		protected int AlignVertically(Rectangle outer, int innerHeight)
		{
			switch (EffectiveCellVerticalAlignment)
			{
				case StringAlignment.Near:
					return outer.Top + 1;
				case StringAlignment.Center:
					return outer.Top + ((outer.Height - innerHeight) / 2);
				case StringAlignment.Far:
					return outer.Bottom - innerHeight - 1;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		/// <summary>
		/// Calculate the space that our rendering will occupy and then align that space
		/// with the given rectangle, according to the Column alignment
		/// </summary>
		/// <param name="g"></param>
		/// <param name="r">Pre-padded bounds of the cell</param>
		/// <returns></returns>
		protected virtual Rectangle CalculateAlignedRectangle(Graphics g, Rectangle r)
		{
			if (Column == null)
				return r;

			Rectangle contentRectangle = new Rectangle(Point.Empty, CalculateContentSize(g, r));
			return AlignRectangle(r, contentRectangle);
		}

		/// <summary>
		/// Calculate the size of the content of this cell.
		/// </summary>
		/// <param name="g"></param>
		/// <param name="r">Pre-padded bounds of the cell</param>
		/// <returns>The width and height of the content</returns>
		protected virtual Size CalculateContentSize(Graphics g, Rectangle r)
		{
			Size checkBoxSize = CalculatePrimaryCheckBoxSize(g);
			Size imageSize = CalculateImageSize(g, GetImageSelector());
			Size textSize = CalculateTextSize(g, GetText(), r.Width - (checkBoxSize.Width + imageSize.Width));

			// If the combined width is greater than the whole cell,  we just use the cell itself

			int width = Math.Min(r.Width, checkBoxSize.Width + imageSize.Width + textSize.Width);
			int componentMaxHeight = Math.Max(checkBoxSize.Height, Math.Max(imageSize.Height, textSize.Height));
			int height = Math.Min(r.Height, componentMaxHeight);

			return new Size(width, height);
		}

		/// <summary>
		/// Calculate the bounds of a checkbox given the (pre-padded) cell bounds
		/// </summary>
		/// <param name="g"></param>
		/// <param name="cellBounds">Pre-padded cell bounds</param>
		/// <returns></returns>
		protected Rectangle CalculateCheckBoxBounds(Graphics g, Rectangle cellBounds)
		{
			Size checkBoxSize = CalculateCheckBoxSize(g);
			return AlignRectangle(cellBounds, new Rectangle(0, 0, checkBoxSize.Width, checkBoxSize.Height));
		}


		/// <summary>
		/// How much space will the check box for this cell occupy?
		/// </summary>
		/// <remarks>Only column 0 can have check boxes. Sub item checkboxes are
		/// treated as images</remarks>
		/// <param name="g"></param>
		/// <returns></returns>
		protected virtual Size CalculateCheckBoxSize(Graphics g)
		{
			if (UseCustomCheckboxImages && ListView.StateImageList != null)
				return ListView.StateImageList.ImageSize;

			return CheckBoxRenderer.GetGlyphSize(g, CheckBoxState.UncheckedNormal);
		}

		/// <summary>
		/// How much space will the check box for this row occupy? 
		/// If the list doesn't have checkboxes, or this isn't the primary column,
		/// this returns an empty size.
		/// </summary>
		/// <param name="g"></param>
		/// <returns></returns>
		protected virtual Size CalculatePrimaryCheckBoxSize(Graphics g)
		{
			if (!ListView.CheckBoxes || !ColumnIsPrimary)
				return Size.Empty;

			Size size = CalculateCheckBoxSize(g);
			size.Width += 6;
			return size;
		}

		/// <summary>
		/// How much horizontal space will the image of this cell occupy?
		/// </summary>
		/// <param name="g"></param>
		/// <param name="imageSelector"></param>
		/// <returns></returns>
		protected virtual int CalculateImageWidth(Graphics g, object imageSelector)
		{
			return CalculateImageSize(g, imageSelector).Width + 2;
		}

		/// <summary>
		/// How much space will the image of this cell occupy?
		/// </summary>
		/// <param name="g"></param>
		/// <param name="imageSelector"></param>
		/// <returns></returns>
		protected virtual Size CalculateImageSize(Graphics g, object imageSelector)
		{
			if (imageSelector == null || imageSelector == DBNull.Value)
				return Size.Empty;

			// Check for the image in the image list (most common case)
			ImageList il = ImageListOrDefault;
			if (il != null)
			{
				int selectorAsInt = -1;

				if (imageSelector is Int32)
					selectorAsInt = (Int32)imageSelector;
				else
				{
					string selectorAsString = imageSelector as string;
					if (selectorAsString != null)
						selectorAsInt = il.Images.IndexOfKey(selectorAsString);
				}
				if (selectorAsInt >= 0)
					return il.ImageSize;
			}

			// Is the selector actually an image?
			Image image = imageSelector as Image;
			if (image != null)
				return image.Size;

			return Size.Empty;
		}

		/// <summary>
		/// How much horizontal space will the text of this cell occupy?
		/// </summary>
		/// <param name="g"></param>
		/// <param name="txt"></param>
		/// <param name="width"></param>
		/// <returns></returns>
		protected virtual int CalculateTextWidth(Graphics g, string txt, int width)
		{
			if (string.IsNullOrEmpty(txt))
				return 0;

			return CalculateTextSize(g, txt, width).Width;
		}

		/// <summary>
		/// How much space will the text of this cell occupy?
		/// </summary>
		/// <param name="g"></param>
		/// <param name="txt"></param>
		/// <param name="width"></param>
		/// <returns></returns>
		protected virtual Size CalculateTextSize(Graphics g, string txt, int width)
		{
			if (string.IsNullOrEmpty(txt))
				return Size.Empty;

			if (UseGdiTextRendering)
			{
				Size proposedSize = new Size(width, Int32.MaxValue);
				return TextRenderer.MeasureText(g, txt, Font, proposedSize, NormalTextFormatFlags);
			}

			// Using GDI+ renderering
			using (StringFormat fmt = new StringFormat())
			{
				fmt.Trimming = StringTrimming.EllipsisCharacter;
				SizeF sizeF = g.MeasureString(txt, Font, width, fmt);
				return new Size(1 + (int)sizeF.Width, 1 + (int)sizeF.Height);
			}
		}

		/// <summary>
		/// Return the Color that is the background color for this item's cell
		/// </summary>
		/// <returns>The background color of the subitem</returns>
		public virtual Color GetBackgroundColor()
		{
			if (!ListView.Enabled)
				return SystemColors.Control;

			if (IsItemSelected && ListView.FullRowSelect)
				return GetSelectedBackgroundColor();

			if (SubItem == null || ListItem.UseItemStyleForSubItems)
				return ListItem.BackColor;

			return SubItem.BackColor;
		}

		/// <summary>
		/// Return the color of the background color when the item is selected
		/// </summary>
		/// <returns>The background color of the subitem</returns>
		public virtual Color GetSelectedBackgroundColor()
		{
			if (ListView.Focused)
				return ListItem.SelectedBackColor ?? ListView.SelectedBackColorOrDefault;

			/*if (!this.ListView.HideSelection)
				return this.ListView.UnfocusedSelectedBackColorOrDefault;*/

			return ListItem.BackColor;
		}

		/// <summary>
		/// Return the color to be used for text in this cell
		/// </summary>
		/// <returns>The text color of the subitem</returns>
		public virtual Color GetForegroundColor()
		{
			if (IsItemSelected && (ColumnIsPrimary || ListView.FullRowSelect))
				return GetSelectedForegroundColor();

			return SubItem == null || ListItem.UseItemStyleForSubItems ? ListItem.ForeColor : SubItem.ForeColor;
		}

		/// <summary>
		/// Return the color of the foreground color when the item is selected
		/// </summary>
		/// <returns>The foreground color of the subitem</returns>
		public virtual Color GetSelectedForegroundColor()
		{
			if (ListView.Focused)
				return ListItem.SelectedForeColor ?? ListView.SelectedForeColorOrDefault;

			return SubItem == null || ListItem.UseItemStyleForSubItems ? ListItem.ForeColor : SubItem.ForeColor;
		}

		/// <summary>
		/// Return the actual image that should be drawn when keyed by the given image selector.
		/// An image selector can be: <list type="bullet">
		/// <item><description>an int, giving the index into the image list</description></item>
		/// <item><description>a string, giving the image key into the image list</description></item>
		/// <item><description>an Image, being the image itself</description></item>
		/// </list>
		/// </summary>
		/// <param name="imageSelector">The value that indicates the image to be used</param>
		/// <returns>An Image or null</returns>
		protected virtual Image GetImage(object imageSelector)
		{
			if (imageSelector == null || imageSelector == DBNull.Value)
				return null;

			ImageList il = ImageListOrDefault;
			if (il != null)
			{
				if (imageSelector is Int32)
				{
					Int32 index = (Int32)imageSelector;
					if (index < 0 || index >= il.Images.Count)
						return null;

					return il.Images[index];
				}

				string str = imageSelector as string;
				if (str != null)
				{
					if (il.Images.ContainsKey(str))
						return il.Images[str];

					return null;
				}
			}

			return imageSelector as Image;
		}

		/// <summary>
		/// </summary>
		protected virtual object GetImageSelector()
		{
			return ColumnIsPrimary ? ListItem.ImageSelector : OLVSubItem.ImageSelector;
		}

		/// <summary>
		/// Return the string that should be drawn within this
		/// </summary>
		/// <returns></returns>
		protected virtual string GetText()
		{
			return SubItem == null ? ListItem.Text : SubItem.Text;
		}

		#endregion

		#region IRenderer members

		/// <summary>
		/// Render the whole item in a non-details view.
		/// </summary>
		/// <param name="e"></param>
		/// <param name="g"></param>
		/// <param name="itemBounds"></param>
		/// <param name="model"></param>
		/// <returns></returns>
		public override bool RenderItem(DrawListViewItemEventArgs e, Graphics g, Rectangle itemBounds, object model)
		{
			ConfigureItem(e, itemBounds, model);
			return OptionalRender(g, itemBounds);
		}

		/// <summary>
		/// Prepare this renderer to draw in response to the given event
		/// </summary>
		/// <param name="e"></param>
		/// <param name="itemBounds"></param>
		/// <param name="model"></param>
		/// <remarks>Use this if you want to chain a second renderer within a primary renderer.</remarks>
		public virtual void ConfigureItem(DrawListViewItemEventArgs e, Rectangle itemBounds, object model)
		{
			ClearState();

			DrawItemEvent = e;
			ListItem = (OLVListItem)e.Item;
			SubItem = null;
			ListView = (ObjectListView)ListItem.ListView;
			Column = ListView.GetColumn(0);
			RowObject = model;
			Bounds = itemBounds;
			IsItemSelected = ListItem.Selected && ListItem.Enabled;
		}

		/// <summary>
		/// Render one cell
		/// </summary>
		/// <param name="e"></param>
		/// <param name="g"></param>
		/// <param name="cellBounds"></param>
		/// <param name="model"></param>
		/// <returns></returns>
		public override bool RenderSubItem(DrawListViewSubItemEventArgs e, Graphics g, Rectangle cellBounds, object model)
		{
			ConfigureSubItem(e, cellBounds, model);
			return OptionalRender(g, cellBounds);
		}

		/// <summary>
		/// Prepare this renderer to draw in response to the given event
		/// </summary>
		/// <param name="e"></param>
		/// <param name="cellBounds"></param>
		/// <param name="model"></param>
		/// <remarks>Use this if you want to chain a second renderer within a primary renderer.</remarks>
		public virtual void ConfigureSubItem(DrawListViewSubItemEventArgs e, Rectangle cellBounds, object model)
		{
			ClearState();

			Event = e;
			ListItem = (OLVListItem)e.Item;
			SubItem = (OLVListSubItem)e.SubItem;
			ListView = (ObjectListView)ListItem.ListView;
			Column = (OLVColumn)e.Header;
			RowObject = model;
			Bounds = cellBounds;
			IsItemSelected = ListItem.Selected && ListItem.Enabled;
		}

		/// <summary>
		/// Calculate which part of this cell was hit
		/// </summary>
		/// <param name="hti"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		public override void HitTest(OlvListViewHitTestInfo hti, int x, int y)
		{
			ClearState();

			ListView = hti.ListView;
			ListItem = hti.Item;
			SubItem = hti.SubItem;
			Column = hti.Column;
			RowObject = hti.RowObject;
			IsItemSelected = ListItem.Selected && ListItem.Enabled;
			if (SubItem == null)
				Bounds = ListItem.Bounds;
			else
				Bounds = ListItem.GetSubItemBounds(Column.Index);

			using (Graphics g = ListView.CreateGraphics())
			{
				HandleHitTest(g, hti, x, y);
			}
		}

		/// <summary>
		/// Calculate the edit rectangle
		/// </summary>
		/// <param name="g"></param>
		/// <param name="cellBounds"></param>
		/// <param name="item"></param>
		/// <param name="subItemIndex"></param>
		/// <param name="preferredSize"> </param>
		/// <returns></returns>
		public override Rectangle GetEditRectangle(Graphics g, Rectangle cellBounds, OLVListItem item, int subItemIndex, Size preferredSize)
		{
			ClearState();

			ListView = (ObjectListView)item.ListView;
			ListItem = item;
			SubItem = item.GetSubItem(subItemIndex);
			Column = ListView.GetColumn(subItemIndex);
			RowObject = item.RowObject;
			IsItemSelected = ListItem.Selected && ListItem.Enabled;
			Bounds = cellBounds;

			return HandleGetEditRectangle(g, cellBounds, item, subItemIndex, preferredSize);
		}

		#endregion

		#region IRenderer implementation

		// Subclasses will probably want to override these methods rather than the IRenderer
		// interface methods.

		/// <summary>
		/// Draw our data into the given rectangle using the given graphics context.
		/// </summary>
		/// <remarks>
		/// <para>Subclasses should override this method.</para></remarks>
		/// <param name="g">The graphics context that should be used for drawing</param>
		/// <param name="r">The bounds of the subitem cell</param>
		/// <returns>Returns whether the rendering has already taken place.
		/// If this returns false, the default processing will take over.
		/// </returns>
		public virtual bool OptionalRender(Graphics g, Rectangle r)
		{
			if (ListView.View != View.Details)
				return false;

			Render(g, r);
			return true;
		}

		/// <summary>
		/// Draw our data into the given rectangle using the given graphics context.
		/// </summary>
		/// <remarks>
		/// <para>Subclasses should override this method if they never want
		/// to fall back on the default processing</para></remarks>
		/// <param name="g">The graphics context that should be used for drawing</param>
		/// <param name="r">The bounds of the subitem cell</param>
		public virtual void Render(Graphics g, Rectangle r)
		{
			StandardRender(g, r);
		}

		/// <summary>
		/// Do the actual work of hit testing. Subclasses should override this rather than HitTest()
		/// </summary>
		/// <param name="g"></param>
		/// <param name="hti"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		protected virtual void HandleHitTest(Graphics g, OlvListViewHitTestInfo hti, int x, int y)
		{
			Rectangle r = CalculateAlignedRectangle(g, ApplyCellPadding(Bounds));
			StandardHitTest(g, hti, r, x, y);
		}

		/// <summary>
		/// Handle a HitTest request after all state information has been initialized
		/// </summary>
		/// <param name="g"></param>
		/// <param name="cellBounds"></param>
		/// <param name="item"></param>
		/// <param name="subItemIndex"></param>
		/// <param name="preferredSize"> </param>
		/// <returns></returns>
		protected virtual Rectangle HandleGetEditRectangle(Graphics g, Rectangle cellBounds, OLVListItem item, int subItemIndex, Size preferredSize)
		{
			// MAINTAINER NOTE: This type testing is wrong (design-wise). The base class should return cell bounds,
			// and a more specialized class should return StandardGetEditRectangle(). But BaseRenderer is used directly
			// to draw most normal cells, as well as being directly subclassed for user implemented renderers. And this
			// method needs to return different bounds in each of those cases. We should have a StandardRenderer and make
			// BaseRenderer into an ABC -- but that would break too much existing code. And so we have this hack :(

			// If we are a standard renderer, return the position of the text, otherwise, use the whole cell.
			if (GetType() == typeof(BaseRenderer))
				return StandardGetEditRectangle(g, cellBounds, preferredSize);

			// Center the editor vertically
			if (cellBounds.Height != preferredSize.Height)
				cellBounds.Y += (cellBounds.Height - preferredSize.Height) / 2;

			return cellBounds;
		}

		#endregion

		#region Standard IRenderer implementations

		/// <summary>
		/// Draw the standard "[checkbox] [image] [text]" cell after the state properties have been initialized.
		/// </summary>
		/// <param name="g"></param>
		/// <param name="r"></param>
		protected void StandardRender(Graphics g, Rectangle r)
		{
			DrawBackground(g, r);

			// Adjust the first columns rectangle to match the padding used by the native mode of the ListView
			if (ColumnIsPrimary && CellHorizontalAlignment == HorizontalAlignment.Left)
			{
				r.X += 3;
				r.Width -= 1;
			}
			r = ApplyCellPadding(r);
			DrawAlignedImageAndText(g, r);

			// Show where the bounds of the cell padding are (debugging)
			if (ObjectListView.ShowCellPaddingBounds)
				g.DrawRectangle(Pens.Purple, r);
		}

		/// <summary>
		/// Change the bounds of the given rectangle to take any cell padding into account
		/// </summary>
		/// <param name="r"></param>
		/// <returns></returns>
		public virtual Rectangle ApplyCellPadding(Rectangle r)
		{
			Rectangle? padding = EffectiveCellPadding;
			if (!padding.HasValue)
				return r;
			// The two subtractions below look wrong, but are correct!
			Rectangle paddingRectangle = padding.Value;
			r.Width -= paddingRectangle.Right;
			r.Height -= paddingRectangle.Bottom;
			r.Offset(paddingRectangle.Location);
			return r;
		}

		/// <summary>
		/// Perform normal hit testing relative to the given aligned content bounds
		/// </summary>
		/// <param name="g"></param>
		/// <param name="hti"></param>
		/// <param name="bounds"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		protected virtual void StandardHitTest(Graphics g, OlvListViewHitTestInfo hti, Rectangle alignedContentRectangle, int x, int y)
		{
			Rectangle r = alignedContentRectangle;

			// Match tweaking from renderer
			if (ColumnIsPrimary && CellHorizontalAlignment == HorizontalAlignment.Left && !(this is TreeListView.TreeRenderer))
			{
				r.X += 3;
				r.Width -= 1;
			}
			int width = 0;

			// Did they hit a check box on the primary column?
			if (ColumnIsPrimary && ListView.CheckBoxes)
			{
				Size checkBoxSize = CalculateCheckBoxSize(g);
				int checkBoxTop = AlignVertically(r, checkBoxSize.Height);
				Rectangle r3 = new Rectangle(r.X, checkBoxTop, checkBoxSize.Width, checkBoxSize.Height);
				width = r3.Width + 6;
				// g.DrawRectangle(Pens.DarkGreen, r3);
				if (r3.Contains(x, y))
				{
					hti.HitTestLocation = HitTestLocation.CheckBox;
					return;
				}
			}

			// Did they hit the image? If they hit the image of a 
			// non-primary column that has a checkbox, it counts as a 
			// checkbox hit
			r.X += width;
			r.Width -= width;
			width = CalculateImageWidth(g, GetImageSelector());
			Rectangle rTwo = r;
			rTwo.Width = width;
			// g.DrawRectangle(Pens.Red, rTwo);
			if (rTwo.Contains(x, y))
			{
				if (Column != null && (Column.Index > 0 && Column.CheckBoxes))
					hti.HitTestLocation = HitTestLocation.CheckBox;
				else
					hti.HitTestLocation = HitTestLocation.Image;
				return;
			}

			// Did they hit the text?
			r.X += width;
			r.Width -= width;
			width = CalculateTextWidth(g, GetText(), r.Width);
			rTwo = r;
			rTwo.Width = width;
			// g.DrawRectangle(Pens.Blue, rTwo);
			if (rTwo.Contains(x, y))
			{
				hti.HitTestLocation = HitTestLocation.Text;
				return;
			}

			hti.HitTestLocation = HitTestLocation.InCell;
		}

		/// <summary>
		/// This method calculates the bounds of the text within a standard layout
		/// (i.e. optional checkbox, optional image, text)
		/// </summary>
		/// <remarks>This method only works correctly if the state of the renderer
		/// has been fully initialized (see BaseRenderer.GetEditRectangle)</remarks>
		/// <param name="g"></param>
		/// <param name="cellBounds"></param>
		/// <param name="preferredSize"> </param>
		/// <returns></returns>
		protected virtual Rectangle StandardGetEditRectangle(Graphics g, Rectangle cellBounds, Size preferredSize)
		{

			Size contentSize = CalculateContentSize(g, cellBounds);
			int contentWidth = Column.CellEditUseWholeCellEffective ? cellBounds.Width : contentSize.Width;
			Rectangle editControlBounds = CalculatePaddedAlignedBounds(g, cellBounds, new Size(contentWidth, preferredSize.Height));

			Size checkBoxSize = CalculatePrimaryCheckBoxSize(g);
			int imageWidth = CalculateImageWidth(g, GetImageSelector());

			int width = checkBoxSize.Width + imageWidth;

			editControlBounds.X += width;
			editControlBounds.Width -= width;

			if (editControlBounds.Width < 50)
				editControlBounds.Width = 50;
			if (editControlBounds.Right > cellBounds.Right)
				editControlBounds.Width = cellBounds.Right - editControlBounds.Left;

			return editControlBounds;
		}

		/// <summary>
		/// Apply any padding to the given bounds, and then align a rectangle of the given
		/// size within that padded area.
		/// </summary>
		/// <param name="g"></param>
		/// <param name="cellBounds"></param>
		/// <param name="preferredSize"></param>
		/// <returns></returns>
		protected Rectangle CalculatePaddedAlignedBounds(Graphics g, Rectangle cellBounds, Size preferredSize)
		{
			Rectangle r = ApplyCellPadding(cellBounds);
			r = AlignRectangle(r, new Rectangle(Point.Empty, preferredSize));
			return r;
		}

		#endregion

		#region Drawing routines

		/// <summary>
		/// Draw the given image aligned horizontally within the column.
		/// </summary>
		/// <remarks>
		/// Over tall images are scaled to fit. Over-wide images are
		/// truncated. This is by design!
		/// </remarks>
		/// <param name="g">Graphics context to use for drawing</param>
		/// <param name="r">Bounds of the cell</param>
		/// <param name="image">The image to be drawn</param>
		protected virtual void DrawAlignedImage(Graphics g, Rectangle r, Image image)
		{
			if (image == null)
				return;

			// By default, the image goes in the top left of the rectangle
			Rectangle imageBounds = new Rectangle(r.Location, image.Size);

			// If the image is too tall to be drawn in the space provided, proportionally scale it down.
			// Too wide images are not scaled.
			if (image.Height > r.Height)
			{
				float scaleRatio = (float)r.Height / (float)image.Height;
				imageBounds.Width = (int)((float)image.Width * scaleRatio);
				imageBounds.Height = r.Height - 1;
			}

			// Align and draw our (possibly scaled) image
			Rectangle alignRectangle = AlignRectangle(r, imageBounds);
			if (ListItem.Enabled)
				g.DrawImage(image, alignRectangle);
			else
				ControlPaint.DrawImageDisabled(g, image, alignRectangle.X, alignRectangle.Y, GetBackgroundColor());
		}

		/// <summary>
		/// Draw our subitems image and text
		/// </summary>
		/// <param name="g">Graphics context to use for drawing</param>
		/// <param name="r">Pre-padded bounds of the cell</param>
		protected virtual void DrawAlignedImageAndText(Graphics g, Rectangle r)
		{
			DrawImageAndText(g, CalculateAlignedRectangle(g, r));
		}

		/// <summary>
		/// Fill in the background of this cell
		/// </summary>
		/// <param name="g">Graphics context to use for drawing</param>
		/// <param name="r">Bounds of the cell</param>
		protected virtual void DrawBackground(Graphics g, Rectangle r)
		{
			if (!IsDrawBackground)
				return;

			Color backgroundColor = GetBackgroundColor();

			using (Brush brush = new SolidBrush(backgroundColor))
			{
				g.FillRectangle(brush, r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2);
			}
		}

		/// <summary>
		/// Draw the primary check box of this row (checkboxes in other sub items use a different method)
		/// </summary>
		/// <param name="g">Graphics context to use for drawing</param>
		/// <param name="r">The pre-aligned and padded target rectangle</param>
		protected virtual int DrawCheckBox(Graphics g, Rectangle r)
		{
			// The odd constants are to match checkbox placement in native mode (on XP at least)
			// TODO: Unify this with CheckStateRenderer

			// The rectangle r is already horizontally aligned. We still need to align it vertically.
			Size checkBoxSize = CalculateCheckBoxSize(g);
			Point checkBoxLocation = new Point(r.X, AlignVertically(r, checkBoxSize.Height));

			if (IsPrinting || UseCustomCheckboxImages)
			{
				int imageIndex = ListItem.StateImageIndex;
				if (ListView.StateImageList == null || imageIndex < 0 || imageIndex >= ListView.StateImageList.Images.Count)
					return 0;

				return DrawImage(g, new Rectangle(checkBoxLocation, checkBoxSize), ListView.StateImageList.Images[imageIndex]) + 4;
			}

			CheckBoxState boxState = GetCheckBoxState(ListItem.CheckState);
			CheckBoxRenderer.DrawCheckBox(g, checkBoxLocation, boxState);
			return checkBoxSize.Width;
		}

		/// <summary>
		/// Calculate the CheckBoxState we need to correctly draw the given state
		/// </summary>
		/// <param name="checkState"></param>
		/// <returns></returns>
		protected virtual CheckBoxState GetCheckBoxState(CheckState checkState)
		{

			// Should the checkbox be drawn as disabled?
			if (IsCheckBoxDisabled)
			{
				switch (checkState)
				{
					case CheckState.Checked:
						return CheckBoxState.CheckedDisabled;
					case CheckState.Unchecked:
						return CheckBoxState.UncheckedDisabled;
					default:
						return CheckBoxState.MixedDisabled;
				}
			}

			// Not hot and not disabled -- just draw it normally
			switch (checkState)
			{
				case CheckState.Checked:
					return CheckBoxState.CheckedNormal;
				case CheckState.Unchecked:
					return CheckBoxState.UncheckedNormal;
				default:
					return CheckBoxState.MixedNormal;
			}

		}

		/// <summary>
		/// Should this checkbox be drawn as disabled?
		/// </summary>
		protected virtual bool IsCheckBoxDisabled
		{
			get
			{
				if (ListItem != null && !ListItem.Enabled)
					return true;

				return (ListView.CellEditActivation == ObjectListView.CellEditActivateMode.None ||
						(Column != null && !Column.IsEditable));
			}
		}

		/// <summary>
		/// Draw the given text and optional image in the "normal" fashion
		/// </summary>
		/// <param name="g">Graphics context to use for drawing</param>
		/// <param name="r">Bounds of the cell</param>
		/// <param name="imageSelector">The optional image to be drawn</param>
		protected virtual int DrawImage(Graphics g, Rectangle r, object imageSelector)
		{
			if (imageSelector == null || imageSelector == DBNull.Value)
				return 0;

			// Draw from the image list (most common case)
			ImageList il = ImageListOrDefault;
			if (il != null)
			{

				// Try to translate our imageSelector into a valid ImageList index
				int selectorAsInt = -1;
				if (imageSelector is Int32)
				{
					selectorAsInt = (Int32)imageSelector;
					if (selectorAsInt >= il.Images.Count)
						selectorAsInt = -1;
				}
				else
				{
					string selectorAsString = imageSelector as string;
					if (selectorAsString != null)
						selectorAsInt = il.Images.IndexOfKey(selectorAsString);
				}

				// If we found a valid index into the ImageList, draw it.
				// We want to draw using the native DrawImageList calls, since that let's us do some nice effects
				// But the native call does not work on PrinterDCs, so if we're printing we have to skip this bit.
				if (selectorAsInt >= 0)
				{
					if (!IsPrinting)
					{
						if (il.ImageSize.Height < r.Height)
							r.Y = AlignVertically(r, new Rectangle(Point.Empty, il.ImageSize));

						// If we are not printing, it's probable that the given Graphics object is double buffered using a BufferedGraphics object.
						// But the ImageList.Draw method doesn't honor the Translation matrix that's probably in effect on the buffered
						// graphics. So we have to calculate our drawing rectangle, relative to the cells natural boundaries.
						// This effectively simulates the Translation matrix.

						Rectangle r2 = new Rectangle(r.X - Bounds.X, r.Y - Bounds.Y, r.Width, r.Height);
						NativeMethods.DrawImageList(g, il, selectorAsInt, r2.X, r2.Y, IsItemSelected, !ListItem.Enabled);
						return il.ImageSize.Width;
					}

					// For some reason, printing from an image list doesn't work onto a printer context
					// So get the image from the list and FALL THROUGH to the "print an image" case
					imageSelector = il.Images[selectorAsInt];
				}
			}

			// Is the selector actually an image?
			Image image = imageSelector as Image;
			if (image == null)
				return 0; // no, give up

			if (image.Size.Height < r.Height)
				r.Y = AlignVertically(r, new Rectangle(Point.Empty, image.Size));

			if (ListItem.Enabled)
				g.DrawImageUnscaled(image, r.X, r.Y);
			else
				ControlPaint.DrawImageDisabled(g, image, r.X, r.Y, GetBackgroundColor());

			return image.Width;
		}

		/// <summary>
		/// Draw our subitems image and text
		/// </summary>
		/// <param name="g">Graphics context to use for drawing</param>
		/// <param name="r">Bounds of the cell</param>
		protected virtual void DrawImageAndText(Graphics g, Rectangle r)
		{
			int offset = 0;
			if (ListView.CheckBoxes && ColumnIsPrimary)
			{
				offset = DrawCheckBox(g, r) + 6;
				r.X += offset;
				r.Width -= offset;
			}

			offset = DrawImage(g, r, GetImageSelector());
			r.X += offset;
			r.Width -= offset;

			DrawText(g, r, GetText());
		}

		/// <summary>
		/// Draw the given collection of image selectors
		/// </summary>
		/// <param name="g"></param>
		/// <param name="r"></param>
		/// <param name="imageSelectors"></param>
		protected virtual int DrawImages(Graphics g, Rectangle r, ICollection imageSelectors)
		{
			// Collect the non-null images
			List<Image> images = new List<Image>();
			foreach (object selector in imageSelectors)
			{
				Image image = GetImage(selector);
				if (image != null)
					images.Add(image);
			}

			// Figure out how much space they will occupy
			int width = 0;
			int height = 0;
			foreach (Image image in images)
			{
				width += (image.Width + Spacing);
				height = Math.Max(height, image.Height);
			}

			// Align the collection of images within the cell
			Rectangle r2 = AlignRectangle(r, new Rectangle(0, 0, width, height));

			// Finally, draw all the images in their correct location
			Color backgroundColor = GetBackgroundColor();
			Point pt = r2.Location;
			foreach (Image image in images)
			{
				if (ListItem.Enabled)
					g.DrawImage(image, pt);
				else
					ControlPaint.DrawImageDisabled(g, image, pt.X, pt.Y, backgroundColor);
				pt.X += (image.Width + Spacing);
			}

			// Return the width that the images occupy
			return width;
		}

		/// <summary>
		/// Draw the given text and optional image in the "normal" fashion
		/// </summary>
		/// <param name="g">Graphics context to use for drawing</param>
		/// <param name="r">Bounds of the cell</param>
		/// <param name="txt">The string to be drawn</param>
		public virtual void DrawText(Graphics g, Rectangle r, string txt)
		{
			if (string.IsNullOrEmpty(txt))
				return;

			if (UseGdiTextRendering)
				DrawTextGdi(g, r, txt);
			else
				DrawTextGdiPlus(g, r, txt);
		}

		/// <summary>
		/// Print the given text in the given rectangle using only GDI routines
		/// </summary>
		/// <param name="g"></param>
		/// <param name="r"></param>
		/// <param name="txt"></param>
		/// <remarks>
		/// The native list control uses GDI routines to do its drawing, so using them
		/// here makes the owner drawn mode looks more natural.
		/// <para>This method doesn't honour the CanWrap setting on the renderer. All
		/// text is single line</para>
		/// </remarks>
		protected virtual void DrawTextGdi(Graphics g, Rectangle r, string txt)
		{
			Color backColor = Color.Transparent;
			if (IsDrawBackground && IsItemSelected && ColumnIsPrimary && !ListView.FullRowSelect)
				backColor = GetSelectedBackgroundColor();

			TextFormatFlags flags = NormalTextFormatFlags | CellVerticalAlignmentAsTextFormatFlag;

			// I think there is a bug in the TextRenderer. Setting or not setting SingleLine doesn't make 
			// any difference -- it is always single line.
			if (!CanWrap)
				flags |= TextFormatFlags.SingleLine;
			TextRenderer.DrawText(g, txt, Font, r, GetForegroundColor(), backColor, flags);
		}

		private bool ColumnIsPrimary
		{
			get { return Column != null && Column.Index == 0; }
		}

		/// <summary>
		/// Gets the cell's vertical alignment as a TextFormatFlag
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		protected TextFormatFlags CellVerticalAlignmentAsTextFormatFlag
		{
			get
			{
				switch (EffectiveCellVerticalAlignment)
				{
					case StringAlignment.Near:
						return TextFormatFlags.Top;
					case StringAlignment.Center:
						return TextFormatFlags.VerticalCenter;
					case StringAlignment.Far:
						return TextFormatFlags.Bottom;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}

		/// <summary>
		/// Gets the StringFormat needed when drawing text using GDI+
		/// </summary>
		protected virtual StringFormat StringFormatForGdiPlus
		{
			get
			{
				StringFormat fmt = new StringFormat();
				fmt.LineAlignment = EffectiveCellVerticalAlignment;
				fmt.Trimming = StringTrimming.EllipsisCharacter;
				fmt.Alignment = StringAlignment.Near;
				if (!CanWrap)
					fmt.FormatFlags = StringFormatFlags.NoWrap;
				return fmt;
			}
		}

		/// <summary>
		/// Print the given text in the given rectangle using normal GDI+ .NET methods
		/// </summary>
		/// <remarks>Printing to a printer dc has to be done using this method.</remarks>
		protected virtual void DrawTextGdiPlus(Graphics g, Rectangle r, string txt)
		{
			using (StringFormat fmt = StringFormatForGdiPlus)
			{
				// Draw the background of the text as selected, if it's the primary column
				// and it's selected and it's not in FullRowSelect mode.
				Font f = Font;
				if (IsDrawBackground && IsItemSelected && ColumnIsPrimary && !ListView.FullRowSelect)
				{
					SizeF size = g.MeasureString(txt, f, r.Width, fmt);
					Rectangle r2 = r;
					r2.Width = (int)size.Width + 1;
					using (Brush brush = new SolidBrush(GetSelectedBackgroundColor()))
					{
						g.FillRectangle(brush, r2);
					}
				}
				RectangleF rf = r;
				g.DrawString(txt, f, TextBrush, rf, fmt);
			}

			// We should put a focus rectangle around the column 0 text if it's selected --
			// but we don't because:
			// - I really dislike this UI convention
			// - we are using buffered graphics, so the DrawFocusRecatangle method of the event doesn't work

			//if (this.ColumnIsPrimary) {
			//    Size size = TextRenderer.MeasureText(this.SubItem.Text, this.ListView.ListFont);
			//    if (r.Width > size.Width)
			//        r.Width = size.Width;
			//    this.Event.DrawFocusRectangle(r);
			//}
		}

		#endregion
	}

	/// <summary>
	/// This renderer highlights substrings that match a given text filter. 
	/// </summary>
	public class HighlightTextRenderer : BaseRenderer
	{
		#region Life and death

		/// <summary>
		/// Create a HighlightTextRenderer
		/// </summary>
		public HighlightTextRenderer()
		{
			FramePen = Pens.DarkGreen;
			FillBrush = Brushes.Yellow;
		}

		#endregion

		#region Configuration properties

		/// <summary>
		/// Gets or set the brush will be used to paint behind the matched substrings.
		/// Set this to null to not fill the frame.
		/// </summary>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Brush FillBrush
		{
			get { return fillBrush; }
			set { fillBrush = value; }
		}

		private Brush fillBrush;

		/// <summary>
		/// Gets or set the pen will be used to frame the matched substrings.
		/// Set this to null to not draw a frame.
		/// </summary>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Pen FramePen
		{
			get { return framePen; }
			set { framePen = value; }
		}

		private Pen framePen;

		#endregion

		#region IRenderer interface overrides

		/// <summary>
		/// Handle a HitTest request after all state information has been initialized
		/// </summary>
		/// <param name="g"></param>
		/// <param name="cellBounds"></param>
		/// <param name="item"></param>
		/// <param name="subItemIndex"></param>
		/// <param name="preferredSize"> </param>
		/// <returns></returns>
		protected override Rectangle HandleGetEditRectangle(Graphics g, Rectangle cellBounds, OLVListItem item, int subItemIndex, Size preferredSize)
		{
			return StandardGetEditRectangle(g, cellBounds, preferredSize);
		}

		#endregion
	}

	/// <summary>
	/// This renderer draws just a checkbox to match the check state of our model object.
	/// </summary>
	public class CheckStateRenderer : BaseRenderer
	{
		/// <summary>
		/// Draw our cell
		/// </summary>
		/// <param name="g"></param>
		/// <param name="r"></param>
		public override void Render(Graphics g, Rectangle r)
		{
			DrawBackground(g, r);
			if (Column == null)
				return;
			r = ApplyCellPadding(r);
			CheckState state = Column.GetCheckState(RowObject);
			if (IsPrinting)
			{
				// Renderers don't work onto printer DCs, so we have to draw the image ourselves
				string key = ObjectListView.CHECKED_KEY;
				if (state == CheckState.Unchecked)
					key = ObjectListView.UNCHECKED_KEY;
				if (state == CheckState.Indeterminate)
					key = ObjectListView.INDETERMINATE_KEY;
				DrawAlignedImage(g, r, ImageListOrDefault.Images[key]);
			}
			else
			{
				r = CalculateCheckBoxBounds(g, r);
				CheckBoxRenderer.DrawCheckBox(g, r.Location, GetCheckBoxState(state));
			}
		}


		/// <summary>
		/// Handle the GetEditRectangle request
		/// </summary>
		/// <param name="g"></param>
		/// <param name="cellBounds"></param>
		/// <param name="item"></param>
		/// <param name="subItemIndex"></param>
		/// <param name="preferredSize"> </param>
		/// <returns></returns>
		protected override Rectangle HandleGetEditRectangle(Graphics g, Rectangle cellBounds, OLVListItem item, int subItemIndex, Size preferredSize)
		{
			return CalculatePaddedAlignedBounds(g, cellBounds, preferredSize);
		}

		/// <summary>
		/// Handle the HitTest request
		/// </summary>
		/// <param name="g"></param>
		/// <param name="hti"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		protected override void HandleHitTest(Graphics g, OlvListViewHitTestInfo hti, int x, int y)
		{
			Rectangle r = CalculateCheckBoxBounds(g, Bounds);
			if (r.Contains(x, y))
				hti.HitTestLocation = HitTestLocation.CheckBox;
		}
	}

	/// <summary>
	/// This renderer draws a functioning button in its cell
	/// </summary>
	public class ColumnButtonRenderer : BaseRenderer
	{

		#region Properties

		/// <summary>
		/// Gets or sets how each button will be sized
		/// </summary>
		[Category("ObjectListView"),
		Description("How each button will be sized"),
		DefaultValue(OLVColumn.ButtonSizingMode.TextBounds)]
		public OLVColumn.ButtonSizingMode SizingMode
		{
			get { return sizingMode; }
			set { sizingMode = value; }
		}
		private OLVColumn.ButtonSizingMode sizingMode = OLVColumn.ButtonSizingMode.TextBounds;

		/// <summary>
		/// Gets or sets the size of the button when the SizingMode is FixedBounds
		/// </summary>
		/// <remarks>If this is not set, the bounds of the cell will be used</remarks>
		[Category("ObjectListView"),
		Description("The size of the button when the SizingMode is FixedBounds"),
		DefaultValue(null)]
		public Size? ButtonSize
		{
			get { return buttonSize; }
			set { buttonSize = value; }
		}
		private Size? buttonSize;

		/// <summary>
		/// Gets or sets the extra space that surrounds the cell when the SizingMode is TextBounds
		/// </summary>
		[Category("ObjectListView"),
		Description("The extra space that surrounds the cell when the SizingMode is TextBounds")]
		public Size? ButtonPadding
		{
			get { return buttonPadding; }
			set { buttonPadding = value; }
		}
		private Size? buttonPadding = new Size(10, 10);

		private Size ButtonPaddingOrDefault
		{
			get { return ButtonPadding ?? new Size(10, 10); }
		}

		/// <summary>
		/// Gets or sets the maximum width that a button can occupy.
		/// -1 means there is no maximum width.
		/// </summary>
		/// <remarks>This is only considered when the SizingMode is TextBounds</remarks>
		[Category("ObjectListView"),
		Description("The maximum width that a button can occupy when the SizingMode is TextBounds"),
		DefaultValue(-1)]
		public int MaxButtonWidth
		{
			get { return maxButtonWidth; }
			set { maxButtonWidth = value; }
		}
		private int maxButtonWidth = -1;

		/// <summary>
		/// Gets or sets the minimum width that a button can occupy.
		/// -1 means there is no minimum width.
		/// </summary>
		/// <remarks>This is only considered when the SizingMode is TextBounds</remarks>
		[Category("ObjectListView"),
		 Description("The minimum width that a button can be when the SizingMode is TextBounds"),
		 DefaultValue(-1)]
		public int MinButtonWidth
		{
			get { return minButtonWidth; }
			set { minButtonWidth = value; }
		}
		private int minButtonWidth = -1;

		#endregion

		#region Rendering

		/// <summary>
		/// Calculate the size of the contents
		/// </summary>
		/// <param name="g"></param>
		/// <param name="r"></param>
		/// <returns></returns>
		protected override Size CalculateContentSize(Graphics g, Rectangle r)
		{
			if (SizingMode == OLVColumn.ButtonSizingMode.CellBounds)
				return r.Size;

			if (SizingMode == OLVColumn.ButtonSizingMode.FixedBounds)
				return ButtonSize ?? r.Size;

			// Ok, SizingMode must be TextBounds. So figure out the size of the text
			Size textSize = CalculateTextSize(g, GetText(), r.Width);

			// Allow for padding and max width
			textSize.Height += ButtonPaddingOrDefault.Height * 2;
			textSize.Width += ButtonPaddingOrDefault.Width * 2;
			if (MaxButtonWidth != -1 && textSize.Width > MaxButtonWidth)
				textSize.Width = MaxButtonWidth;
			if (textSize.Width < MinButtonWidth)
				textSize.Width = MinButtonWidth;

			return textSize;
		}

		/// <summary>
		/// Draw the button
		/// </summary>
		/// <param name="g"></param>
		/// <param name="r"></param>
		protected override void DrawImageAndText(Graphics g, Rectangle r)
		{
			TextFormatFlags textFormatFlags = TextFormatFlags.HorizontalCenter |
											  TextFormatFlags.VerticalCenter |
											  TextFormatFlags.EndEllipsis |
											  TextFormatFlags.NoPadding |
											  TextFormatFlags.SingleLine |
											  TextFormatFlags.PreserveGraphicsTranslateTransform;
			if (ListView.RightToLeftLayout)
				textFormatFlags |= TextFormatFlags.RightToLeft;

			string buttonText = GetText();
			if (!string.IsNullOrEmpty(buttonText))
				ButtonRenderer.DrawButton(g, r, buttonText, Font, textFormatFlags, false, CalculatePushButtonState());
		}

		/// <summary>
		/// What part of the control is under the given point?
		/// </summary>
		/// <param name="g"></param>
		/// <param name="hti"></param>
		/// <param name="bounds"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		protected override void StandardHitTest(Graphics g, OlvListViewHitTestInfo hti, Rectangle bounds, int x, int y)
		{
			Rectangle r = ApplyCellPadding(bounds);
			if (r.Contains(x, y))
				hti.HitTestLocation = HitTestLocation.Button;
		}

		/// <summary>
		/// What is the state of the button?
		/// </summary>
		/// <returns></returns>
		protected PushButtonState CalculatePushButtonState()
		{
			if (!ListItem.Enabled && !Column.EnableButtonWhenItemIsDisabled)
				return PushButtonState.Disabled;

			return PushButtonState.Normal;
		}

		#endregion
	}
}