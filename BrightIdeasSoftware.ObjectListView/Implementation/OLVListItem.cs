using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using System.Drawing;

namespace BrightIdeasSoftware
{
	/// <summary>
	/// OLVListItems are specialized ListViewItems that know which row object they came from,
	/// and the row index at which they are displayed, even when in group view mode. They
	/// also know the image they should draw against themselves
	/// </summary>
	public class OLVListItem : ListViewItem
	{
		#region Constructors

		/// <summary>
		/// Create a OLVListItem for the given row object
		/// </summary>
		public OLVListItem(object rowObject)
		{
			this.rowObject = rowObject;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the bounding rectangle of the item, including all subitems
		/// </summary>
		new public Rectangle Bounds
		{
			get
			{
				try
				{
					return base.Bounds;
				}
				catch (System.ArgumentException)
				{
					// If the item is part of a collapsed group, Bounds will throw an exception
					return Rectangle.Empty;
				}
			}
		}

		/// <summary>
		/// Gets or sets how many pixels will be left blank around each cell of this item
		/// </summary>
		/// <remarks>This setting only takes effect when the control is owner drawn.</remarks>
		public Rectangle? CellPadding
		{
			get { return cellPadding; }
			set { cellPadding = value; }
		}
		private Rectangle? cellPadding;

		/// <summary>
		/// Gets or sets how the cells of this item will be vertically aligned
		/// </summary>
		/// <remarks>This setting only takes effect when the control is owner drawn.</remarks>
		public StringAlignment? CellVerticalAlignment
		{
			get { return cellVerticalAlignment; }
			set { cellVerticalAlignment = value; }
		}
		private StringAlignment? cellVerticalAlignment;

		/// <summary>
		/// Gets or sets the checkedness of this item.
		/// </summary>
		/// <remarks>
		/// Virtual lists don't handle checkboxes well, so we have to intercept attempts to change them
		/// through the items, and change them into something that will work.
		/// Unfortunately, this won't work if this property is set through the base class, since
		/// the property is not declared as virtual.
		/// </remarks>
		new public bool Checked
		{
			get
			{
				return base.Checked;
			}
			set
			{
				if (Checked != value)
				{
					if (value)
						((ObjectListView)ListView).CheckObject(RowObject);
					else
						((ObjectListView)ListView).UncheckObject(RowObject);
				}
			}
		}

		/// <summary>
		/// Enable tri-state checkbox.
		/// </summary>
		/// <remarks>.NET's Checked property was not built to handle tri-state checkboxes,
		/// and will return True for both Checked and Indeterminate states.</remarks>
		public CheckState CheckState
		{
			get
			{
				switch (StateImageIndex)
				{
					case 0:
						return System.Windows.Forms.CheckState.Unchecked;
					case 1:
						return System.Windows.Forms.CheckState.Checked;
					case 2:
						return System.Windows.Forms.CheckState.Indeterminate;
					default:
						return System.Windows.Forms.CheckState.Unchecked;
				}
			}
			set
			{
				switch (value)
				{
					case System.Windows.Forms.CheckState.Unchecked:
						StateImageIndex = 0;
						break;
					case System.Windows.Forms.CheckState.Checked:
						StateImageIndex = 1;
						break;
					case System.Windows.Forms.CheckState.Indeterminate:
						StateImageIndex = 2;
						break;
				}
			}
		}

		/// <summary>
		/// Gets whether or not this row can be selected and activated
		/// </summary>
		public bool Enabled
		{
			get { return enabled; }
			internal set { enabled = value; }
		}
		private bool enabled;

		/// <summary>
		/// Get or set the image that should be shown against this item
		/// </summary>
		/// <remarks><para>This can be an Image, a string or an int. A string or an int will
		/// be used as an index into the small image list.</para></remarks>
		public object ImageSelector
		{
			get { return imageSelector; }
			set
			{
				imageSelector = value;
				if (value is Int32)
					ImageIndex = (Int32)value;
				else if (value is string)
					ImageKey = (string)value;
				else
					ImageIndex = -1;
			}
		}
		private object imageSelector;

		/// <summary>
		/// Gets or sets the the model object that is source of the data for this list item.
		/// </summary>
		public object RowObject
		{
			get { return rowObject; }
			set { rowObject = value; }
		}
		private object rowObject;

		/// <summary>
		/// Gets or sets the color that will be used for this row's background when it is selected and 
		/// the control is focused.
		/// </summary>
		/// <remarks>
		/// <para>To work reliably, this property must be set during a FormatRow event.</para>
		/// <para>
		/// If this is not set, the normal selection BackColor will be used.
		/// </para>
		/// </remarks>
		public Color? SelectedBackColor
		{
			get { return selectedBackColor; }
			set { selectedBackColor = value; }
		}
		private Color? selectedBackColor;

		/// <summary>
		/// Gets or sets the color that will be used for this row's foreground when it is selected and 
		/// the control is focused.
		/// </summary>
		/// <remarks>
		/// <para>To work reliably, this property must be set during a FormatRow event.</para>
		/// <para>
		/// If this is not set, the normal selection ForeColor will be used.
		/// </para>
		/// </remarks>
		public Color? SelectedForeColor
		{
			get { return selectedForeColor; }
			set { selectedForeColor = value; }
		}
		private Color? selectedForeColor;

		#endregion

		#region Accessing

		/// <summary>
		/// Return the sub item at the given index
		/// </summary>
		/// <param name="index">Index of the subitem to be returned</param>
		/// <returns>An OLVListSubItem</returns>
		public virtual OLVListSubItem GetSubItem(int index)
		{
			if (index >= 0 && index < SubItems.Count)
				return (OLVListSubItem)SubItems[index];

			return null;
		}

		/// <summary>
		/// Return bounds of the given subitem
		/// </summary>
		/// <remarks>This correctly calculates the bounds even for column 0.</remarks>
		public virtual Rectangle GetSubItemBounds(int subItemIndex)
		{
			if (subItemIndex == 0)
			{
				Rectangle r = Bounds;
				Point sides = NativeMethods.GetScrolledColumnSides(ListView, subItemIndex);
				r.X = sides.X + 1;
				r.Width = sides.Y - sides.X;
				return r;
			}

			OLVListSubItem subItem = GetSubItem(subItemIndex);
			return subItem == null ? new Rectangle() : subItem.Bounds;
		}

		#endregion
	}
}