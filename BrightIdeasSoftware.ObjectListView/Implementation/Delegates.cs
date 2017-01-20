using System;
using System.Windows.Forms;
using System.Drawing;

namespace BrightIdeasSoftware
{

	#region Delegate declarations

	/// <summary>
	/// These delegates are used to extract an aspect from a row object
	/// </summary>
	public delegate object AspectGetterDelegate(object rowObject);

	/// <summary>
	/// These delegates are used to put a changed value back into a model object
	/// </summary>
	public delegate void AspectPutterDelegate(object rowObject, object newValue);

	/// <summary>
	/// These delegates can be used to convert an aspect value to a display string,
	/// instead of using the default ToString()
	/// </summary>
	public delegate string AspectToStringConverterDelegate(object value);

	/// <summary>
	/// These delegates are used to get the tooltip for a cell
	/// </summary>
	public delegate string CellToolTipGetterDelegate(OLVColumn column, object modelObject);

	/// <summary>
	/// These delegates are used to the state of the checkbox for a row object.
	/// </summary>
	/// <remarks><para>
	/// For reasons known only to someone in Microsoft, we can only set
	/// a boolean on the ListViewItem to indicate it's "checked-ness", but when
	/// we receive update events, we have to use a tristate CheckState. So we can
	/// be told about an indeterminate state, but we can't set it ourselves.
	/// </para>
	/// <para>As of version 2.0, we can now return indeterminate state.</para>
	/// </remarks>
	public delegate CheckState CheckStateGetterDelegate(object rowObject);

	/// <summary>
	/// These delegates are used to get the state of the checkbox for a row object.
	/// </summary>
	/// <param name="rowObject"></param>
	/// <returns></returns>
	public delegate bool BooleanCheckStateGetterDelegate(object rowObject);

	/// <summary>
	/// These delegates are used to put a changed check state back into a model object
	/// </summary>
	public delegate CheckState CheckStatePutterDelegate(object rowObject, CheckState newValue);

	/// <summary>
	/// These delegates are used to put a changed check state back into a model object
	/// </summary>
	/// <param name="rowObject"></param>
	/// <param name="newValue"></param>
	/// <returns></returns>
	public delegate bool BooleanCheckStatePutterDelegate(object rowObject, bool newValue);

	/// <summary>
	/// These delegates are used to get the renderer for a particular cell
	/// </summary>
	public delegate IRenderer CellRendererGetterDelegate(object rowObject, OLVColumn column);

	/// <summary>
	/// The callbacks for RightColumnClick events
	/// </summary>
	public delegate void ColumnRightClickEventHandler(object sender, ColumnClickEventArgs e);

	/// <summary>
	/// This delegate will be used to own draw header column.
	/// </summary>
	public delegate bool HeaderDrawingDelegate(Graphics g, Rectangle r, int columnIndex, OLVColumn column, bool isPressed, HeaderStateStyle stateStyle);

	/// <summary>
	/// These delegates are used to get the tooltip for a column header
	/// </summary>
	public delegate string HeaderToolTipGetterDelegate(OLVColumn column);

	/// <summary>
	/// These delegates are used to fetch the image selector that should be used
	/// to choose an image for this column.
	/// </summary>
	public delegate object ImageGetterDelegate(object rowObject);

	/// <summary>
	/// These delegates are used to fetch a row object for virtual lists
	/// </summary>
	public delegate object RowGetterDelegate(int rowIndex);

	/// <summary>
	/// These delegates are used to format a listviewitem before it is added to the control.
	/// </summary>
	public delegate void RowFormatterDelegate(OLVListItem olvItem);

	/// <summary>
	/// These delegates are used to sort the listview in some custom fashion
	/// </summary>
	public delegate void SortDelegate(OLVColumn column, SortOrder sortOrder);

	/// <summary>
	/// These delegates are used to order two strings.
	/// x cannot be null. y can be null.
	/// </summary>
	public delegate int StringCompareDelegate(string x, string y);

	#endregion
}