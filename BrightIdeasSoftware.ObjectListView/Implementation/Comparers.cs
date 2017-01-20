using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;

namespace BrightIdeasSoftware
{
	/// <summary>
	/// ColumnComparer is the workhorse for all comparison between two values of a particular column.
	/// If the column has a specific comparer, use that to compare the values. Otherwise, do
	/// a case insensitive string compare of the string representations of the values.
	/// </summary>
	/// <remarks><para>This class inherits from both IComparer and its generic counterpart
	/// so that it can be used on untyped and typed collections.</para>
	/// <para>This is used by normal (non-virtual) ObjectListViews. Virtual lists use
	/// ModelObjectComparer</para>
	/// </remarks>
	public class ColumnComparer : IComparer, IComparer<OLVListItem>
	{
		/// <summary>
		/// Gets or sets the method that will be used to compare two strings.
		/// The default is to compare on the current culture, case-insensitive
		/// </summary>
		public static StringCompareDelegate StringComparer
		{
			get { return stringComparer; }
			set { stringComparer = value; }
		}
		private static StringCompareDelegate stringComparer;

		/// <summary>
		/// Create a ColumnComparer that will order the rows in a list view according
		/// to the values in a given column
		/// </summary>
		/// <param name="col">The column whose values will be compared</param>
		/// <param name="order">The ordering for column values</param>
		public ColumnComparer(OLVColumn col, SortOrder order)
		{
			column = col;
			sortOrder = order;
		}

		/// <summary>
		/// Create a ColumnComparer that will order the rows in a list view according
		/// to the values in a given column, and by a secondary column if the primary
		/// column is equal.
		/// </summary>
		/// <param name="col">The column whose values will be compared</param>
		/// <param name="order">The ordering for column values</param>
		/// <param name="col2">The column whose values will be compared for secondary sorting</param>
		/// <param name="order2">The ordering for secondary column values</param>
		public ColumnComparer(OLVColumn col, SortOrder order, OLVColumn col2, SortOrder order2)
			: this(col, order)
		{
			// There is no point in secondary sorting on the same column
			if (col != col2)
				secondComparer = new ColumnComparer(col2, order2);
		}

		/// <summary>
		/// Compare two rows
		/// </summary>
		/// <param name="x">row1</param>
		/// <param name="y">row2</param>
		/// <returns>An ordering indication: -1, 0, 1</returns>
		public int Compare(object x, object y)
		{
			return Compare((OLVListItem)x, (OLVListItem)y);
		}

		/// <summary>
		/// Compare two rows
		/// </summary>
		/// <param name="x">row1</param>
		/// <param name="y">row2</param>
		/// <returns>An ordering indication: -1, 0, 1</returns>
		public int Compare(OLVListItem x, OLVListItem y)
		{
			if (sortOrder == SortOrder.None)
				return 0;

			int result = 0;
			object x1 = column.GetValue(x.RowObject);
			object y1 = column.GetValue(y.RowObject);

			// Handle nulls. Null values come last
			bool xIsNull = (x1 == null || x1 == System.DBNull.Value);
			bool yIsNull = (y1 == null || y1 == System.DBNull.Value);
			if (xIsNull || yIsNull)
			{
				if (xIsNull && yIsNull)
					result = 0;
				else
					result = (xIsNull ? -1 : 1);
			}
			else
			{
				result = CompareValues(x1, y1);
			}

			if (sortOrder == SortOrder.Descending)
				result = 0 - result;

			// If the result was equality, use the secondary comparer to resolve it
			if (result == 0 && secondComparer != null)
				result = secondComparer.Compare(x, y);

			return result;
		}

		/// <summary>
		/// Compare the actual values to be used for sorting
		/// </summary>
		/// <param name="x">The aspect extracted from the first row</param>
		/// <param name="y">The aspect extracted from the second row</param>
		/// <returns>An ordering indication: -1, 0, 1</returns>
		public int CompareValues(object x, object y)
		{
			// Force case insensitive compares on strings
			string xAsString = x as string;
			if (xAsString != null)
				return CompareStrings(xAsString, y as string);

			IComparable comparable = x as IComparable;
			return comparable != null ? comparable.CompareTo(y) : 0;
		}

		private static int CompareStrings(string x, string y)
		{
			if (StringComparer == null)
				return string.Compare(x, y, StringComparison.CurrentCultureIgnoreCase);
			else
				return StringComparer(x, y);
		}

		private OLVColumn column;
		private SortOrder sortOrder;
		private ColumnComparer secondComparer;
	}

	/// <summary>
	/// This comparer can be used to sort a collection of model objects by a given column
	/// </summary>
	/// <remarks>
	/// <para>This is used by virtual ObjectListViews. Non-virtual lists use
	/// ColumnComparer</para>
	/// </remarks>
	public class ModelObjectComparer : IComparer, IComparer<object>
	{
		/// <summary>
		/// Gets or sets the method that will be used to compare two strings.
		/// The default is to compare on the current culture, case-insensitive
		/// </summary>
		public static StringCompareDelegate StringComparer
		{
			get { return stringComparer; }
			set { stringComparer = value; }
		}
		private static StringCompareDelegate stringComparer;

		/// <summary>
		/// Create a model object comparer
		/// </summary>
		/// <param name="col"></param>
		/// <param name="order"></param>
		public ModelObjectComparer(OLVColumn col, SortOrder order)
		{
			column = col;
			sortOrder = order;
		}

		/// <summary>
		/// Create a model object comparer with a secondary sorting column
		/// </summary>
		/// <param name="col"></param>
		/// <param name="order"></param>
		/// <param name="col2"></param>
		/// <param name="order2"></param>
		public ModelObjectComparer(OLVColumn col, SortOrder order, OLVColumn col2, SortOrder order2)
			: this(col, order)
		{
			// There is no point in secondary sorting on the same column
			if (col != col2 && col2 != null && order2 != SortOrder.None)
				secondComparer = new ModelObjectComparer(col2, order2);
		}

		/// <summary>
		/// Compare the two model objects
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public int Compare(object x, object y)
		{
			int result = 0;
			object x1 = column.GetValue(x);
			object y1 = column.GetValue(y);

			if (sortOrder == SortOrder.None)
				return 0;

			// Handle nulls. Null values come last
			bool xIsNull = (x1 == null || x1 == System.DBNull.Value);
			bool yIsNull = (y1 == null || y1 == System.DBNull.Value);
			if (xIsNull || yIsNull)
			{
				if (xIsNull && yIsNull)
					result = 0;
				else
					result = (xIsNull ? -1 : 1);
			}
			else
			{
				result = CompareValues(x1, y1);
			}

			if (sortOrder == SortOrder.Descending)
				result = 0 - result;

			// If the result was equality, use the secondary comparer to resolve it
			if (result == 0 && secondComparer != null)
				result = secondComparer.Compare(x, y);

			return result;
		}

		/// <summary>
		/// Compare the actual values
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public int CompareValues(object x, object y)
		{
			// Force case insensitive compares on strings
			string xStr = x as string;
			if (xStr != null)
				return CompareStrings(xStr, y as string);

			IComparable comparable = x as IComparable;
			return comparable != null ? comparable.CompareTo(y) : 0;
		}

		private static int CompareStrings(string x, string y)
		{
			if (StringComparer == null)
				return string.Compare(x, y, StringComparison.CurrentCultureIgnoreCase);
			else
				return StringComparer(x, y);
		}

		private OLVColumn column;
		private SortOrder sortOrder;
		private ModelObjectComparer secondComparer;

		#region IComparer<object> Members

		#endregion
	}
}