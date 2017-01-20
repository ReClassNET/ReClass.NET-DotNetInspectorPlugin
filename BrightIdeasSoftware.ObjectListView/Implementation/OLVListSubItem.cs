using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.ComponentModel;

namespace BrightIdeasSoftware
{

	/// <summary>
	/// A ListViewSubItem that knows which image should be drawn against it.
	/// </summary>
	[Browsable(false)]
	public class OLVListSubItem : ListViewItem.ListViewSubItem
	{
		#region Constructors

		/// <summary>
		/// Create a OLVListSubItem that shows the given string and image
		/// </summary>
		public OLVListSubItem(object modelValue, string text, object image)
		{
			ModelValue = modelValue;
			Text = text;
			ImageSelector = image;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets how many pixels will be left blank around this cell
		/// </summary>
		/// <remarks>This setting only takes effect when the control is owner drawn.</remarks>
		public Rectangle? CellPadding
		{
			get { return cellPadding; }
			set { cellPadding = value; }
		}
		private Rectangle? cellPadding;

		/// <summary>
		/// Gets or sets how this cell will be vertically aligned
		/// </summary>
		/// <remarks>This setting only takes effect when the control is owner drawn.</remarks>
		public StringAlignment? CellVerticalAlignment
		{
			get { return cellVerticalAlignment; }
			set { cellVerticalAlignment = value; }
		}
		private StringAlignment? cellVerticalAlignment;

		/// <summary>
		/// Gets or sets the model value is being displayed by this subitem.
		/// </summary>
		public object ModelValue
		{
			get { return modelValue; }
			private set { modelValue = value; }
		}
		private object modelValue;

		/// <summary>
		/// Get or set the image that should be shown against this item
		/// </summary>
		/// <remarks><para>This can be an Image, a string or an int. A string or an int will
		/// be used as an index into the small image list.</para></remarks>
		public object ImageSelector
		{
			get { return imageSelector; }
			set { imageSelector = value; }
		}
		private object imageSelector;

		#endregion
	}
}