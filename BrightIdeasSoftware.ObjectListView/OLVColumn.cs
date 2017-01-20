using System;
using System.ComponentModel;
using System.Windows.Forms;
using System.Drawing;
using System.Collections;
using System.Diagnostics;
using System.Drawing.Design;

namespace BrightIdeasSoftware
{
	/// <summary>
	/// An OLVColumn knows which aspect of an object it should present.
	/// </summary>
	/// <remarks>
	/// The column knows how to:
	/// <list type="bullet">
	///	<item><description>extract its aspect from the row object</description></item>
	///	<item><description>convert an aspect to a string</description></item>
	///	<item><description>calculate the image for the row object</description></item>
	///	<item><description>extract a group "key" from the row object</description></item>
	///	<item><description>convert a group "key" into a title for the group</description></item>
	/// </list>
	/// <para>For sorting to work correctly, aspects from the same column
	/// must be of the same type, that is, the same aspect cannot sometimes
	/// return strings and other times integers.</para>
	/// </remarks>
	[Browsable(false)]
	public partial class OLVColumn : ColumnHeader
	{
		/// <summary>
		/// How should the button be sized?
		/// </summary>
		public enum ButtonSizingMode
		{
			/// <summary>
			/// Every cell will have the same sized button, as indicated by ButtonSize property
			/// </summary>
			FixedBounds,

			/// <summary>
			/// Every cell will draw a button that fills the cell, inset by ButtonPadding
			/// </summary>
			CellBounds,

			/// <summary>
			/// Each button will be resized to contain the text of the Aspect
			/// </summary>
			TextBounds
		}

		#region Life and death

		/// <summary>
		/// Create an OLVColumn
		/// </summary>
		public OLVColumn()
		{
		}

		#endregion

		#region Public Properties

		/// <summary>
		/// This delegate will be used to extract a value to be displayed in this column.
		/// </summary>
		/// <remarks>
		/// If this is set, AspectName is ignored.
		/// </remarks>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public AspectGetterDelegate AspectGetter
		{
			get { return aspectGetter; }
			set { aspectGetter = value; }
		}
		private AspectGetterDelegate aspectGetter;

		/// <summary>
		/// The name of the property or method that should be called to get the value to display in this column.
		/// This is only used if a ValueGetterDelegate has not been given.
		/// </summary>
		/// <remarks>This name can be dotted to chain references to properties or parameter-less methods.</remarks>
		/// <example>"DateOfBirth"</example>
		/// <example>"Owner.HomeAddress.Postcode"</example>
		[Category("ObjectListView"),
		 Description("The name of the property or method that should be called to get the aspect to display in this column"),
		 DefaultValue(null)]
		public string AspectName
		{
			get { return aspectName; }
			set
			{
				aspectName = value;
				aspectMunger = null;
			}
		}
		private string aspectName;

		/// <summary>
		/// This delegate will be used to put an edited value back into the model object.
		/// </summary>
		/// <remarks>
		/// This does nothing if IsEditable == false.
		/// </remarks>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public AspectPutterDelegate AspectPutter
		{
			get { return aspectPutter; }
			set { aspectPutter = value; }
		}
		private AspectPutterDelegate aspectPutter;

		/// <summary>
		/// The delegate that will be used to translate the aspect to display in this column into a string.
		/// </summary>
		/// <remarks>If this value is set, AspectToStringFormat will be ignored.</remarks>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public AspectToStringConverterDelegate AspectToStringConverter
		{
			get { return aspectToStringConverter; }
			set { aspectToStringConverter = value; }
		}
		private AspectToStringConverterDelegate aspectToStringConverter;

		/// <summary>
		/// This format string will be used to convert an aspect to its string representation.
		/// </summary>
		/// <remarks>
		/// This string is passed as the first parameter to the String.Format() method.
		/// This is only used if AspectToStringConverter has not been set.</remarks>
		/// <example>"{0:C}" to convert a number to currency</example>
		[Category("ObjectListView"),
		 Description("The format string that will be used to convert an aspect to its string representation"),
		 DefaultValue(null)]
		public string AspectToStringFormat
		{
			get { return aspectToStringFormat; }
			set { aspectToStringFormat = value; }
		}
		private string aspectToStringFormat;

		/// <summary>
		/// Gets whether this column can be hidden by user actions
		/// </summary>
		/// <remarks>This take into account both the Hideable property and whether this column
		/// is the primary column of the listview (column 0).</remarks>
		[Browsable(false)]
		public bool CanBeHidden
		{
			get
			{
				return Hideable && (Index != 0);
			}
		}

		/// <summary>
		/// When a cell is edited, should the whole cell be used (minus any space used by checkbox or image)?
		/// </summary>
		/// <remarks>
		/// <para>This is always treated as true when the control is NOT owner drawn.</para>
		/// <para>
		/// When this is false (the default) and the control is owner drawn, 
		/// ObjectListView will try to calculate the width of the cell's
		/// actual contents, and then size the editing control to be just the right width. If this is true,
		/// the whole width of the cell will be used, regardless of the cell's contents.
		/// </para>
		/// <para>If this property is not set on the column, the value from the control will be used
		/// </para>
		/// <para>This value is only used when the control is in Details view.</para>
		/// <para>Regardless of this setting, developers can specify the exact size of the editing control
		/// by listening for the CellEditStarting event.</para>
		/// </remarks>
		[Category("ObjectListView"),
		 Description("When a cell is edited, should the whole cell be used?"),
		 DefaultValue(null)]
		public virtual bool? CellEditUseWholeCell
		{
			get { return cellEditUseWholeCell; }
			set { cellEditUseWholeCell = value; }
		}
		private bool? cellEditUseWholeCell;

		/// <summary>
		/// Get whether the whole cell should be used when editing a cell in this column
		/// </summary>
		/// <remarks>This calculates the current effective value, which may be different to CellEditUseWholeCell</remarks>
		[Browsable(false),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public virtual bool CellEditUseWholeCellEffective
		{
			get
			{
				bool? columnSpecificValue = ListView.View == View.Details ? CellEditUseWholeCell : (bool?)null;
				return (columnSpecificValue ?? ((ObjectListView)ListView).CellEditUseWholeCell);
			}
		}

		/// <summary>
		/// Gets or sets how many pixels will be left blank around this cells in this column
		/// </summary>
		/// <remarks>This setting only takes effect when the control is owner drawn.</remarks>
		[Category("ObjectListView"),
		 Description("How many pixels will be left blank around the cells in this column?"),
		 DefaultValue(null)]
		public Rectangle? CellPadding
		{
			get { return cellPadding; }
			set { cellPadding = value; }
		}
		private Rectangle? cellPadding;

		/// <summary>
		/// Gets or sets how cells in this column will be vertically aligned.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This setting only takes effect when the control is owner drawn.
		/// </para>        
		/// <para>
		/// If this is not set, the value from the control itself will be used.
		/// </para>
		/// </remarks>
		[Category("ObjectListView"),
		 Description("How will cell values be vertically aligned?"),
		 DefaultValue(null)]
		public virtual StringAlignment? CellVerticalAlignment
		{
			get { return cellVerticalAlignment; }
			set { cellVerticalAlignment = value; }
		}
		private StringAlignment? cellVerticalAlignment;

		/// <summary>
		/// Gets or sets whether this column will show a checkbox.
		/// </summary>
		/// <remarks>
		/// Setting this on column 0 has no effect. Column 0 check box is controlled
		/// by the CheckBoxes property on the ObjectListView itself.
		/// </remarks>
		[Category("ObjectListView"),
		 Description("Should values in this column be treated as a checkbox, rather than a string?"),
		 DefaultValue(false)]
		public virtual bool CheckBoxes
		{
			get { return checkBoxes; }
			set
			{
				if (checkBoxes == value)
					return;

				checkBoxes = value;
				if (checkBoxes)
				{
					if (Renderer == null)
						Renderer = new CheckStateRenderer();
				}
				else
				{
					if (Renderer is CheckStateRenderer)
						Renderer = null;
				}
			}
		}
		private bool checkBoxes;

		/// <summary>
		/// Gets or sets whether the button in this column (if this column is drawing buttons) will be enabled
		/// even if the row itself is disabled
		/// </summary>
		[Category("ObjectListView"),
		 Description("If this column contains a button, should the button be enabled even if the row is disabled?"),
		 DefaultValue(false)]
		public bool EnableButtonWhenItemIsDisabled
		{
			get { return enableButtonWhenItemIsDisabled; }
			set { enableButtonWhenItemIsDisabled = value; }
		}
		private bool enableButtonWhenItemIsDisabled;

		/// <summary>
		/// Should this column resize to fill the free space in the listview?
		/// </summary>
		/// <remarks>
		/// <para>
		/// If you want two (or more) columns to equally share the available free space, set this property to True.
		/// If you want this column to have a larger or smaller share of the free space, you must
		/// set the FreeSpaceProportion property explicitly.
		/// </para>
		/// <para>
		/// Space filling columns are still governed by the MinimumWidth and MaximumWidth properties.
		/// </para>
		/// /// </remarks>
		[Category("ObjectListView"),
		 Description("Will this column resize to fill unoccupied horizontal space in the listview?"),
		 DefaultValue(false)]
		public bool FillsFreeSpace
		{
			get { return FreeSpaceProportion > 0; }
			set { FreeSpaceProportion = value ? 1 : 0; }
		}

		/// <summary>
		/// What proportion of the unoccupied horizontal space in the control should be given to this column?
		/// </summary>
		/// <remarks>
		/// <para>
		/// There are situations where it would be nice if a column (normally the rightmost one) would expand as
		/// the list view expands, so that as much of the column was visible as possible without having to scroll
		/// horizontally (you should never, ever make your users have to scroll anything horizontally!).
		/// </para>
		/// <para>
		/// A space filling column is resized to occupy a proportion of the unoccupied width of the listview (the
		/// unoccupied width is the width left over once all the the non-filling columns have been given their space).
		/// This property indicates the relative proportion of that unoccupied space that will be given to this column.
		/// The actual value of this property is not important -- only its value relative to the value in other columns.
		/// For example:
		/// <list type="bullet">
		/// <item><description>
		/// If there is only one space filling column, it will be given all the free space, regardless of the value in FreeSpaceProportion.
		/// </description></item>
		/// <item><description>
		/// If there are two or more space filling columns and they all have the same value for FreeSpaceProportion,
		/// they will share the free space equally.
		/// </description></item>
		/// <item><description>
		/// If there are three space filling columns with values of 3, 2, and 1
		/// for FreeSpaceProportion, then the first column with occupy half the free space, the second will
		/// occupy one-third of the free space, and the third column one-sixth of the free space.
		/// </description></item>
		/// </list>
		/// </para>
		/// </remarks>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public int FreeSpaceProportion
		{
			get { return freeSpaceProportion; }
			set { freeSpaceProportion = Math.Max(0, value); }
		}
		private int freeSpaceProportion;

		/// <summary>
		/// Gets or sets a delegate that will be used to own draw header column.
		/// </summary>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public HeaderDrawingDelegate HeaderDrawing
		{
			get { return headerDrawing; }
			set { headerDrawing = value; }
		}
		private HeaderDrawingDelegate headerDrawing;

		/// <summary>
		/// Gets or sets the style that will be used to draw the header for this column
		/// </summary>
		/// <remarks>This is only uses when the owning ObjectListView has HeaderUsesThemes set to false.</remarks>
		[Category("ObjectListView"),
		 Description("What style will be used to draw the header of this column"),
		 DefaultValue(null)]
		public HeaderFormatStyle HeaderFormatStyle
		{
			get { return headerFormatStyle; }
			set { headerFormatStyle = value; }
		}
		private HeaderFormatStyle headerFormatStyle;

		/// <summary>
		/// Gets or sets whether the text values in this column will act like hyperlinks
		/// </summary>
		/// <remarks>This is only taken into account when HeaderUsesThemes is false.</remarks>
		[Category("ObjectListView"),
		 Description("Name of the image that will be shown in the column header."),
		 DefaultValue(null),
		 TypeConverter(typeof(ImageKeyConverter)),
		 Editor("System.Windows.Forms.Design.ImageIndexEditor, System.Design, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", typeof(UITypeEditor)),
		 RefreshProperties(RefreshProperties.Repaint)]
		public string HeaderImageKey
		{
			get { return headerImageKey; }
			set { headerImageKey = value; }
		}
		private string headerImageKey;

		/// <summary>
		/// Return the text alignment of the header. This will either have been set explicitly,
		/// or will follow the alignment of the text in the column
		/// </summary>
		[Browsable(false)]
		public HorizontalAlignment HeaderTextAlignOrDefault
		{
			get { return TextAlign; }
		}

		/// <summary>
		/// Gets the header alignment converted to a StringAlignment
		/// </summary>
		[Browsable(false)]
		public StringAlignment HeaderTextAlignAsStringAlignment
		{
			get
			{
				switch (HeaderTextAlignOrDefault)
				{
					case HorizontalAlignment.Left: return StringAlignment.Near;
					case HorizontalAlignment.Center: return StringAlignment.Center;
					case HorizontalAlignment.Right: return StringAlignment.Far;
					default: return StringAlignment.Near;
				}
			}
		}

		/// <summary>
		/// Gets whether or not this column has an image in the header
		/// </summary>
		[Browsable(false)]
		public bool HasHeaderImage
		{
			get
			{
				return (ListView != null &&
					ListView.SmallImageList != null &&
					ListView.SmallImageList.Images.ContainsKey(HeaderImageKey));
			}
		}

		/// <summary>
		/// Gets or sets whether this header will place a checkbox in the header
		/// </summary>
		[Category("ObjectListView"),
		 Description("Draw a checkbox in the header of this column"),
		 DefaultValue(false)]
		public bool HeaderCheckBox
		{
			get { return headerCheckBox; }
			set { headerCheckBox = value; }
		}
		private bool headerCheckBox;

		/// <summary>
		/// Gets or sets whether this header will place a tri-state checkbox in the header
		/// </summary>
		[Category("ObjectListView"),
		Description("Draw a tri-state checkbox in the header of this column"),
		 DefaultValue(false)]
		public bool HeaderTriStateCheckBox
		{
			get { return headerTriStateCheckBox; }
			set { headerTriStateCheckBox = value; }
		}
		private bool headerTriStateCheckBox;

		/// <summary>
		/// Gets or sets the checkedness of the checkbox in the header of this column
		/// </summary>
		[Category("ObjectListView"),
		 Description("Checkedness of the header checkbox"),
		 DefaultValue(CheckState.Unchecked)]
		public CheckState HeaderCheckState
		{
			get { return headerCheckState; }
			set { headerCheckState = value; }
		}
		private CheckState headerCheckState = CheckState.Unchecked;

		/// <summary>
		/// Gets or sets whether the 
		/// checking/unchecking the value of the header's checkbox will result in the
		/// checkboxes for all cells in this column being set to the same checked/unchecked.
		/// Defaults to true.
		/// </summary>
		/// <remarks>
		/// <para>
		/// There is no reverse of this function that automatically updates the header when the 
		/// checkedness of a cell changes.
		/// </para>
		/// <para>
		/// This property's behaviour on a TreeListView is probably best describes as undefined 
		/// and should be avoided.
		/// </para>
		/// <para>
		/// The performance of this action (checking/unchecking all rows) is O(n) where n is the 
		/// number of rows. It will work on large virtual lists, but it may take some time.
		/// </para>
		/// </remarks>
		[Category("ObjectListView"),
		 Description("Update row checkboxs when the header checkbox is clicked by the user"),
		 DefaultValue(true)]
		public bool HeaderCheckBoxUpdatesRowCheckBoxes
		{
			get { return headerCheckBoxUpdatesRowCheckBoxes; }
			set { headerCheckBoxUpdatesRowCheckBoxes = value; }
		}
		private bool headerCheckBoxUpdatesRowCheckBoxes = true;

		/// <summary>
		/// Gets or sets whether the checkbox in the header is disabled
		/// </summary>
		/// <remarks>
		/// Clicking on a disabled checkbox does not change its value, though it does raise
		/// a HeaderCheckBoxChanging event, which allows the programmer the opportunity to do 
		/// something appropriate.</remarks>
		[Category("ObjectListView"),
		Description("Is the checkbox in the header of this column disabled"),
		 DefaultValue(false)]
		public bool HeaderCheckBoxDisabled
		{
			get { return headerCheckBoxDisabled; }
			set { headerCheckBoxDisabled = value; }
		}
		private bool headerCheckBoxDisabled;

		/// <summary>
		/// Gets or sets whether this column can be hidden by the user.
		/// </summary>
		/// <remarks>
		/// <para>Column 0 can never be hidden, regardless of this setting.</para>
		/// </remarks>
		[Category("ObjectListView"),
		 Description("Will the user be able to choose to hide this column?"),
		 DefaultValue(true)]
		public bool Hideable
		{
			get { return hideable; }
			set { hideable = value; }
		}
		private bool hideable = true;

		/// <summary>
		/// This is the name of property that will be invoked to get the image selector of the
		/// image that should be shown in this column.
		/// It can return an int, string, Image or null.
		/// </summary>
		/// <remarks>
		/// <para>This is ignored if ImageGetter is not null.</para>
		/// <para>The property can use these return value to identify the image:</para>
		/// <list type="bullet">
		/// <item><description>null or -1 -- indicates no image</description></item>
		/// <item><description>an int -- the int value will be used as an index into the image list</description></item>
		/// <item><description>a String -- the string value will be used as a key into the image list</description></item>
		/// <item><description>an Image -- the Image will be drawn directly (only in OwnerDrawn mode)</description></item>
		/// </list>
		/// </remarks>
		[Category("ObjectListView"),
		 Description("The name of the property that holds the image selector"),
		 DefaultValue(null)]
		public string ImageAspectName
		{
			get { return imageAspectName; }
			set { imageAspectName = value; }
		}
		private string imageAspectName;

		/// <summary>
		/// This delegate is called to get the image selector of the image that should be shown in this column.
		/// It can return an int, string, Image or null.
		/// </summary>
		/// <remarks><para>This delegate can use these return value to identify the image:</para>
		/// <list type="bullet">
		/// <item><description>null or -1 -- indicates no image</description></item>
		/// <item><description>an int -- the int value will be used as an index into the image list</description></item>
		/// <item><description>a String -- the string value will be used as a key into the image list</description></item>
		/// <item><description>an Image -- the Image will be drawn directly (only in OwnerDrawn mode)</description></item>
		/// </list>
		/// </remarks>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public ImageGetterDelegate ImageGetter
		{
			get { return imageGetter; }
			set { imageGetter = value; }
		}
		private ImageGetterDelegate imageGetter;

		/// <summary>
		/// Can the values shown in this column be edited?
		/// </summary>
		/// <remarks>This defaults to true, since the primary means to control the editability of a listview
		/// is on the listview itself. Once a listview is editable, all the columns are too, unless the
		/// programmer explicitly marks them as not editable</remarks>
		[Category("ObjectListView"),
		 Description("Can the value in this column be edited?"),
		 DefaultValue(true)]
		public bool IsEditable
		{
			get { return isEditable; }
			set { isEditable = value; }
		}
		private bool isEditable = true;

		/// <summary>
		/// Is this column a fixed width column?
		/// </summary>
		[Browsable(false)]
		public bool IsFixedWidth
		{
			get
			{
				return (MinimumWidth != -1 && MaximumWidth != -1 && MinimumWidth >= MaximumWidth);
			}
		}

		/// <summary>
		/// Get/set whether this column should be used when the view is switched to tile view.
		/// </summary>
		/// <remarks>Column 0 is always included in tileview regardless of this setting.
		/// Tile views do not work well with many "columns" of information. 
		/// Two or three works best.</remarks>
		[Category("ObjectListView"),
		 Description("Will this column be used when the view is switched to tile view"),
		 DefaultValue(false)]
		public bool IsTileViewColumn
		{
			get { return isTileViewColumn; }
			set { isTileViewColumn = value; }
		}
		private bool isTileViewColumn;

		/// <summary>
		/// Gets or sets whether the text of this header should be rendered vertically.
		/// </summary>
		/// <remarks>
		/// <para>If this is true, it is a good idea to set ToolTipText to the name of the column so it's easy to read.</para>
		/// <para>Vertical headers are text only. They do not draw their image.</para>
		/// </remarks>
		[Category("ObjectListView"),
		 Description("Will the header for this column be drawn vertically?"),
		 DefaultValue(false)]
		public bool IsHeaderVertical
		{
			get { return isHeaderVertical; }
			set { isHeaderVertical = value; }
		}
		private bool isHeaderVertical;

		/// <summary>
		/// Can this column be seen by the user?
		/// </summary>
		/// <remarks>After changing this value, you must call RebuildColumns() before the changes will take effect.</remarks>
		[Category("ObjectListView"),
		 Description("Can this column be seen by the user?"),
		 DefaultValue(true)]
		public bool IsVisible
		{
			get { return isVisible; }
			set
			{
				if (isVisible == value)
					return;

				isVisible = value;
				OnVisibilityChanged(EventArgs.Empty);
			}
		}
		private bool isVisible = true;

		/// <summary>
		/// Where was this column last positioned within the Detail view columns
		/// </summary>
		/// <remarks>DisplayIndex is volatile. Once a column is removed from the control,
		/// there is no way to discover where it was in the display order. This property
		/// guards that information even when the column is not in the listview's active columns.</remarks>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public int LastDisplayIndex
		{
			get { return lastDisplayIndex; }
			set { lastDisplayIndex = value; }
		}
		private int lastDisplayIndex = -1;

		/// <summary>
		/// What is the maximum width that the user can give to this column?
		/// </summary>
		/// <remarks>-1 means there is no maximum width. Give this the same value as MinimumWidth to make a fixed width column.</remarks>
		[Category("ObjectListView"),
		 Description("What is the maximum width to which the user can resize this column? -1 means no limit"),
		 DefaultValue(-1)]
		public int MaximumWidth
		{
			get { return maxWidth; }
			set
			{
				maxWidth = value;
				if (maxWidth != -1 && Width > maxWidth)
					Width = maxWidth;
			}
		}
		private int maxWidth = -1;

		/// <summary>
		/// What is the minimum width that the user can give to this column?
		/// </summary>
		/// <remarks>-1 means there is no minimum width. Give this the same value as MaximumWidth to make a fixed width column.</remarks>
		[Category("ObjectListView"),
		 Description("What is the minimum width to which the user can resize this column? -1 means no limit"),
		 DefaultValue(-1)]
		public int MinimumWidth
		{
			get { return minWidth; }
			set
			{
				minWidth = value;
				if (Width < minWidth)
					Width = minWidth;
			}
		}
		private int minWidth = -1;

		/// <summary>
		/// Get/set the renderer that will be invoked when a cell needs to be redrawn
		/// </summary>
		[Category("ObjectListView"),
		Description("The renderer will draw this column when the ListView is owner drawn"),
		DefaultValue(null)]
		public IRenderer Renderer
		{
			get { return renderer; }
			set { renderer = value; }
		}
		private IRenderer renderer;

		/// <summary>
		/// Gets or sets whether the header for this column will include the column's Text.
		/// </summary>
		/// <remarks>
		/// <para>
		/// If this is false, the only thing rendered in the column header will be the image from <see cref="HeaderImageKey"/>.
		/// </para>
		/// <para>This setting is only considered when <see cref="ObjectListView.HeaderUsesThemes"/> is false on the owning ObjectListView.</para>
		/// </remarks>
		[Category("ObjectListView"),
		 Description("Will the header for this column include text?"),
		 DefaultValue(true)]
		public bool ShowTextInHeader
		{
			get { return showTextInHeader; }
			set { showTextInHeader = value; }
		}
		private bool showTextInHeader = true;

		/// <summary>
		/// Gets or sets whether the contents of the list will be resorted when the user clicks the 
		/// header of this column.
		/// </summary>
		/// <remarks>
		/// <para>
		/// If this is false, clicking the header will not sort the list, but will not provide
		/// any feedback as to why the list is not being sorted. It is the programmers responsibility to
		/// provide appropriate feedback.
		/// </para>
		/// <para>When this is false, BeforeSorting events are still fired, which can be used to allow sorting
		/// or give feedback, on a case by case basis.</para>
		/// </remarks>
		[Category("ObjectListView"),
		 Description("Will clicking this columns header resort the list?"),
		 DefaultValue(true)]
		public bool Sortable
		{
			get { return sortable; }
			set { sortable = value; }
		}
		private bool sortable = true;

		/// <summary>
		/// Gets or sets the horizontal alignment of the contents of the column.
		/// </summary>
		/// <remarks>.NET will not allow column 0 to have any alignment except
		/// to the left. We can't change the basic behaviour of the listview,
		/// but when owner drawn, column 0 can now have other alignments.</remarks>
		new public HorizontalAlignment TextAlign
		{
			get
			{
				return textAlign.HasValue ? textAlign.Value : base.TextAlign;
			}
			set
			{
				textAlign = value;
				base.TextAlign = value;
			}
		}
		private HorizontalAlignment? textAlign;

		/// <summary>
		/// What string should be displayed when the mouse is hovered over the header of this column?
		/// </summary>
		/// <remarks>If a HeaderToolTipGetter is installed on the owning ObjectListView, this
		/// value will be ignored.</remarks>
		[Category("ObjectListView"),
		 Description("The tooltip to show when the mouse is hovered over the header of this column"),
		 DefaultValue((string)null),
		 Localizable(true)]
		public string ToolTipText
		{
			get { return toolTipText; }
			set { toolTipText = value; }
		}
		private string toolTipText;

		/// <summary>
		/// Should this column have a tri-state checkbox?
		/// </summary>
		/// <remarks>
		/// If this is true, the user can choose the third state (normally Indeterminate).
		/// </remarks>
		[Category("ObjectListView"),
		 Description("Should values in this column be treated as a tri-state checkbox?"),
		 DefaultValue(false)]
		public virtual bool TriStateCheckBoxes
		{
			get { return triStateCheckBoxes; }
			set
			{
				triStateCheckBoxes = value;
				if (value && !CheckBoxes)
					CheckBoxes = true;
			}
		}
		private bool triStateCheckBoxes;

		/// <summary>
		/// Group objects by the initial letter of the aspect of the column
		/// </summary>
		/// <remarks>
		/// One common pattern is to group column by the initial letter of the value for that group.
		/// The aspect must be a string (obviously).
		/// </remarks>
		[Category("ObjectListView"),
		 Description("The name of the property or method that should be called to get the aspect to display in this column"),
		 DefaultValue(false)]
		public bool UseInitialLetterForGroup
		{
			get { return useInitialLetterForGroup; }
			set { useInitialLetterForGroup = value; }
		}
		private bool useInitialLetterForGroup;

		/// <summary>
		/// What is the width of this column?
		/// </summary>
		[Category("ObjectListView"),
		Description("The width in pixels of this column"),
		DefaultValue(60)]
		new public int Width
		{
			get { return base.Width; }
			set
			{
				if (MaximumWidth != -1 && value > MaximumWidth)
					base.Width = MaximumWidth;
				else
					base.Width = Math.Max(MinimumWidth, value);
			}
		}

		/// <summary>
		/// Gets or set whether the contents of this column's cells should be word wrapped
		/// </summary>
		/// <remarks>If this column uses a custom IRenderer (that is, one that is not descended
		/// from BaseRenderer), then that renderer is responsible for implementing word wrapping.</remarks>
		[Category("ObjectListView"),
		 Description("Draw this column cell's word wrapped"),
		 DefaultValue(false)]
		public bool WordWrap
		{
			get { return wordWrap; }
			set
			{
				wordWrap = value;

				// If there isn't a renderer and they are turning word wrap off, we don't need to do anything
				if (Renderer == null && !wordWrap)
					return;

				// All other cases require a renderer of some sort
				if (Renderer == null)
					Renderer = new HighlightTextRenderer();

				BaseRenderer baseRenderer = Renderer as BaseRenderer;

				// If there is a custom renderer (not descended from BaseRenderer), 
				// we leave it up to them to implement wrapping
				if (baseRenderer == null)
					return;

				baseRenderer.CanWrap = wordWrap;
			}
		}
		private bool wordWrap;

		#endregion

		#region Object commands

		/// <summary>
		/// Get the checkedness of the given object for this column
		/// </summary>
		/// <param name="rowObject">The row object that is being displayed</param>
		/// <returns>The checkedness of the object</returns>
		public CheckState GetCheckState(object rowObject)
		{
			if (!CheckBoxes)
				return CheckState.Unchecked;

			bool? aspectAsBool = GetValue(rowObject) as bool?;
			if (aspectAsBool.HasValue)
			{
				if (aspectAsBool.Value)
					return CheckState.Checked;
				else
					return CheckState.Unchecked;
			}
			else
				return CheckState.Indeterminate;
		}

		/// <summary>
		/// Put the checkedness of the given object for this column
		/// </summary>
		/// <param name="rowObject">The row object that is being displayed</param>
		/// <param name="newState"></param>
		/// <returns>The checkedness of the object</returns>
		public void PutCheckState(object rowObject, CheckState newState)
		{
			if (newState == CheckState.Checked)
				PutValue(rowObject, true);
			else
				if (newState == CheckState.Unchecked)
				PutValue(rowObject, false);
			else
				PutValue(rowObject, null);
		}

		/// <summary>
		/// For a given row object, extract the value indicated by the AspectName property of this column.
		/// </summary>
		/// <param name="rowObject">The row object that is being displayed</param>
		/// <returns>An object, which is the aspect named by AspectName</returns>
		public object GetAspectByName(object rowObject)
		{
			if (aspectMunger == null)
				aspectMunger = new Munger(AspectName);

			return aspectMunger.GetValue(rowObject);
		}
		private Munger aspectMunger;

		/// <summary>
		/// For a given row object, return the image selector of the image that should displayed in this column.
		/// </summary>
		/// <param name="rowObject">The row object that is being displayed</param>
		/// <returns>int or string or Image. int or string will be used as index into image list. null or -1 means no image</returns>
		public object GetImage(object rowObject)
		{
			if (CheckBoxes)
				return GetCheckStateImage(rowObject);

			if (ImageGetter != null)
				return ImageGetter(rowObject);

			if (!string.IsNullOrEmpty(ImageAspectName))
			{
				if (imageAspectMunger == null)
					imageAspectMunger = new Munger(ImageAspectName);

				return imageAspectMunger.GetValue(rowObject);
			}

			// I think this is wrong. ImageKey is meant for the image in the header, not in the rows
			if (!string.IsNullOrEmpty(ImageKey))
				return ImageKey;

			return ImageIndex;
		}
		private Munger imageAspectMunger;

		/// <summary>
		/// Return the image that represents the check box for the given model
		/// </summary>
		/// <param name="rowObject"></param>
		/// <returns></returns>
		public string GetCheckStateImage(object rowObject)
		{
			CheckState checkState = GetCheckState(rowObject);

			if (checkState == CheckState.Checked)
				return ObjectListView.CHECKED_KEY;

			if (checkState == CheckState.Unchecked)
				return ObjectListView.UNCHECKED_KEY;

			return ObjectListView.INDETERMINATE_KEY;
		}

		/// <summary>
		/// For a given row object, return the string representation of the value shown in this column.
		/// </summary>
		/// <remarks>
		/// For aspects that are string (e.g. aPerson.Name), the aspect and its string representation are the same.
		/// For non-strings (e.g. aPerson.DateOfBirth), the string representation is very different.
		/// </remarks>
		/// <param name="rowObject"></param>
		/// <returns></returns>
		public string GetStringValue(object rowObject)
		{
			return ValueToString(GetValue(rowObject));
		}

		/// <summary>
		/// For a given row object, return the object that is to be displayed in this column.
		/// </summary>
		/// <param name="rowObject">The row object that is being displayed</param>
		/// <returns>An object, which is the aspect to be displayed</returns>
		public object GetValue(object rowObject)
		{
			if (AspectGetter == null)
				return GetAspectByName(rowObject);
			else
				return AspectGetter(rowObject);
		}

		/// <summary>
		/// Update the given model object with the given value using the column's
		/// AspectName.
		/// </summary>
		/// <param name="rowObject">The model object to be updated</param>
		/// <param name="newValue">The value to be put into the model</param>
		public void PutAspectByName(object rowObject, object newValue)
		{
			if (aspectMunger == null)
				aspectMunger = new Munger(AspectName);

			aspectMunger.PutValue(rowObject, newValue);
		}

		/// <summary>
		/// Update the given model object with the given value
		/// </summary>
		/// <param name="rowObject">The model object to be updated</param>
		/// <param name="newValue">The value to be put into the model</param>
		public void PutValue(object rowObject, object newValue)
		{
			if (aspectPutter == null)
				PutAspectByName(rowObject, newValue);
			else
				aspectPutter(rowObject, newValue);
		}

		/// <summary>
		/// Convert the aspect object to its string representation.
		/// </summary>
		/// <remarks>
		/// If the column has been given a AspectToStringConverter, that will be used to do
		/// the conversion, otherwise just use ToString(). 
		/// The returned value will not be null. Nulls are always converted
		/// to empty strings.
		/// </remarks>
		/// <param name="value">The value of the aspect that should be displayed</param>
		/// <returns>A string representation of the aspect</returns>
		public string ValueToString(object value)
		{
			// Give the installed converter a chance to work (even if the value is null)
			if (AspectToStringConverter != null)
				return AspectToStringConverter(value) ?? string.Empty;

			// Without a converter, nulls become simple empty strings
			if (value == null)
				return string.Empty;

			string fmt = AspectToStringFormat;
			if (string.IsNullOrEmpty(fmt))
				return value.ToString();
			else
				return string.Format(fmt, value);
		}

		#endregion

		#region Events

		/// <summary>
		/// This event is triggered when the visibility of this column changes.
		/// </summary>
		[Category("ObjectListView"),
		Description("This event is triggered when the visibility of the column changes.")]
		public event EventHandler<EventArgs> VisibilityChanged;

		/// <summary>
		/// Tell the world when visibility of a column changes.
		/// </summary>
		public virtual void OnVisibilityChanged(EventArgs e)
		{
			if (VisibilityChanged != null)
				VisibilityChanged(this, e);
		}

		#endregion
	}
}