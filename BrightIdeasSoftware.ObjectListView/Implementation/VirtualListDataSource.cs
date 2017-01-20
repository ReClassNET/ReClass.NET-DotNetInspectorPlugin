using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace BrightIdeasSoftware
{
	/// <summary>
	/// A VirtualListDataSource is a complete manner to provide functionality to a virtual list.
	/// An object that implements this interface provides a VirtualObjectListView with all the
	/// information it needs to be fully functional.
	/// </summary>
	/// <remarks>Implementors must provide functioning implementations of at least GetObjectCount()
	/// and GetNthObject(), otherwise nothing will appear in the list.</remarks>
	public interface IVirtualListDataSource
	{
		/// <summary>
		/// Return the object that should be displayed at the n'th row.
		/// </summary>
		/// <param name="n">The index of the row whose object is to be returned.</param>
		/// <returns>The model object at the n'th row, or null if the fetching was unsuccessful.</returns>
		object GetNthObject(int n);

		/// <summary>
		/// Return the number of rows that should be visible in the virtual list
		/// </summary>
		/// <returns>The number of rows the list view should have.</returns>
		int GetObjectCount();

		/// <summary>
		/// Get the index of the row that is showing the given model object
		/// </summary>
		/// <param name="model">The model object sought</param>
		/// <returns>The index of the row showing the model, or -1 if the object could not be found.</returns>
		int GetObjectIndex(object model);

		/// <summary>
		/// The ListView is about to request the given range of items. Do
		/// whatever caching seems appropriate.
		/// </summary>
		/// <param name="first"></param>
		/// <param name="last"></param>
		void PrepareCache(int first, int last);

		/// <summary>
		/// Find the first row that "matches" the given text in the given range.
		/// </summary>
		/// <param name="value">The text typed by the user</param>
		/// <param name="first">Start searching from this index. This may be greater than the 'to' parameter, 
		/// in which case the search should descend</param>
		/// <param name="last">Do not search beyond this index. This may be less than the 'from' parameter.</param>
		/// <param name="column">The column that should be considered when looking for a match.</param>
		/// <returns>Return the index of row that was matched, or -1 if no match was found</returns>
		int SearchText(string value, int first, int last, OLVColumn column);

		/// <summary>
		/// Sort the model objects in the data source.
		/// </summary>
		/// <param name="column"></param>
		/// <param name="order"></param>
		void Sort(OLVColumn column, SortOrder order);

		/// <summary>
		/// Add the given collection of model objects to this control.
		/// </summary>
		/// <param name="modelObjects">A collection of model objects</param>
		void AddObjects(ICollection<object> modelObjects);

		/// <summary>
		/// Insert the given collection of model objects to this control at the position
		/// </summary>
		/// <param name="index">Index where the collection will be added</param>
		/// <param name="modelObjects">A collection of model objects</param>
		void InsertObjects(int index, ICollection<object> modelObjects);

		/// <summary>
		/// Remove all of the given objects from the control
		/// </summary>
		/// <param name="modelObjects">Collection of objects to be removed</param>
		void RemoveObjects(ICollection<object> modelObjects);

		/// <summary>
		/// Set the collection of objects that this control will show.
		/// </summary>
		/// <param name="collection"></param>
		void SetObjects(IEnumerable<object> collection);

		/// <summary>
		/// Update/replace the nth object with the given object
		/// </summary>
		/// <param name="index"></param>
		/// <param name="modelObject"></param>
		void UpdateObject(int index, object modelObject);
	}

	/// <summary>
	/// A do-nothing implementation of the VirtualListDataSource interface.
	/// </summary>
	public class AbstractVirtualListDataSource : IVirtualListDataSource
	{
		/// <summary>
		/// Creates an AbstractVirtualListDataSource
		/// </summary>
		/// <param name="listView"></param>
		public AbstractVirtualListDataSource(VirtualObjectListView listView)
		{
			this.listView = listView;
		}

		/// <summary>
		/// The list view that this data source is giving information to.
		/// </summary>
		protected VirtualObjectListView listView;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="n"></param>
		/// <returns></returns>
		public virtual object GetNthObject(int n)
		{
			return null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public virtual int GetObjectCount()
		{
			return -1;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="model"></param>
		/// <returns></returns>
		public virtual int GetObjectIndex(object model)
		{
			return -1;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		public virtual void PrepareCache(int from, int to)
		{
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="value"></param>
		/// <param name="first"></param>
		/// <param name="last"></param>
		/// <param name="column"></param>
		/// <returns></returns>
		public virtual int SearchText(string value, int first, int last, OLVColumn column)
		{
			return -1;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="column"></param>
		/// <param name="order"></param>
		public virtual void Sort(OLVColumn column, SortOrder order)
		{
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="modelObjects"></param>
		public virtual void AddObjects(ICollection<object> modelObjects)
		{
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="index"></param>
		/// <param name="modelObjects"></param>
		public virtual void InsertObjects(int index, ICollection<object> modelObjects)
		{
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="modelObjects"></param>
		public virtual void RemoveObjects(ICollection<object> modelObjects)
		{
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="collection"></param>
		public virtual void SetObjects(IEnumerable<object> collection)
		{
		}

		/// <summary>
		/// Update/replace the nth object with the given object
		/// </summary>
		/// <param name="index"></param>
		/// <param name="modelObject"></param>
		public virtual void UpdateObject(int index, object modelObject)
		{
		}

		/// <summary>
		/// This is a useful default implementation of SearchText method, intended to be called
		/// by implementors of IVirtualListDataSource.
		/// </summary>
		/// <param name="value"></param>
		/// <param name="first"></param>
		/// <param name="last"></param>
		/// <param name="column"></param>
		/// <param name="source"></param>
		/// <returns></returns>
		static public int DefaultSearchText(string value, int first, int last, OLVColumn column, IVirtualListDataSource source)
		{
			if (first <= last)
			{
				for (int i = first; i <= last; i++)
				{
					string data = column.GetStringValue(source.GetNthObject(i));
					if (data.StartsWith(value, StringComparison.CurrentCultureIgnoreCase))
						return i;
				}
			}
			else
			{
				for (int i = first; i >= last; i--)
				{
					string data = column.GetStringValue(source.GetNthObject(i));
					if (data.StartsWith(value, StringComparison.CurrentCultureIgnoreCase))
						return i;
				}
			}

			return -1;
		}
	}

	/// <summary>
	/// This class mimics the behavior of VirtualObjectListView v1.x.
	/// </summary>
	public class VirtualListVersion1DataSource : AbstractVirtualListDataSource
	{
		/// <summary>
		/// Creates a VirtualListVersion1DataSource
		/// </summary>
		/// <param name="listView"></param>
		public VirtualListVersion1DataSource(VirtualObjectListView listView)
			: base(listView)
		{
		}

		#region Public properties

		/// <summary>
		/// How will the n'th object of the data source be fetched?
		/// </summary>
		public RowGetterDelegate RowGetter
		{
			get { return rowGetter; }
			set { rowGetter = value; }
		}
		private RowGetterDelegate rowGetter;

		#endregion

		#region IVirtualListDataSource implementation

		/// <summary>
		/// 
		/// </summary>
		/// <param name="n"></param>
		/// <returns></returns>
		public override object GetNthObject(int n)
		{
			if (RowGetter == null)
				return null;
			else
				return RowGetter(n);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="value"></param>
		/// <param name="first"></param>
		/// <param name="last"></param>
		/// <param name="column"></param>
		/// <returns></returns>
		public override int SearchText(string value, int first, int last, OLVColumn column)
		{
			return DefaultSearchText(value, first, last, column, this);
		}

		#endregion
	}
}