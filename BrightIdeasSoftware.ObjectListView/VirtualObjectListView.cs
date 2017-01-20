using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace BrightIdeasSoftware
{
	/// <summary>
	/// A virtual object list view operates in virtual mode, that is, it only gets model objects for
	/// a row when it is needed. This gives it the ability to handle very large numbers of rows with
	/// minimal resources.
	/// </summary>
	/// <remarks><para>A listview is not a great user interface for a large number of items. But if you've
	/// ever wanted to have a list with 10 million items, go ahead, knock yourself out.</para>
	/// <para>Virtual lists can never iterate their contents. That would defeat the whole purpose.</para>
	/// <para>Animated GIFs should not be used in virtual lists. Animated GIFs require some state
	/// information to be stored for each animation, but virtual lists specifically do not keep any state information.
	/// In any case, you really do not want to keep state information for 10 million animations!</para>
	/// <para>
	/// Although it isn't documented, .NET virtual lists cannot have checkboxes. This class codes around this limitation,
	/// but you must use the functions provided by ObjectListView: CheckedObjects, CheckObject(), UncheckObject() and their friends. 
	/// If you use the normal check box properties (CheckedItems or CheckedIndicies), they will throw an exception, since the
	/// list is in virtual mode, and .NET "knows" it can't handle checkboxes in virtual mode.
	/// </para>
	/// <para>Due to the limits of the underlying Windows control, virtual lists do not trigger ItemCheck/ItemChecked events. 
	/// Use a CheckStatePutter instead.</para>
	/// <para>To enable grouping, you must provide an implmentation of IVirtualGroups interface, via the GroupingStrategy property.</para>
	/// <para>Similarly, to enable filtering on the list, your VirtualListDataSource must also implement the IFilterableDataSource interface.</para>
	/// </remarks>
	public class VirtualObjectListView : ObjectListView
	{
		/// <summary>
		/// Create a VirtualObjectListView
		/// </summary>
		public VirtualObjectListView()
			: base()
		{
			VirtualMode = true; // Virtual lists have to be virtual -- no prizes for guessing that :)

			CacheVirtualItems += new CacheVirtualItemsEventHandler(HandleCacheVirtualItems);
			RetrieveVirtualItem += new RetrieveVirtualItemEventHandler(HandleRetrieveVirtualItem);
			SearchForVirtualItem += new SearchForVirtualItemEventHandler(HandleSearchForVirtualItem);

			// At the moment, we don't need to handle this event. But we'll keep this comment to remind us about it.
			//this.VirtualItemsSelectionRangeChanged += new ListViewVirtualItemsSelectionRangeChangedEventHandler(VirtualObjectListView_VirtualItemsSelectionRangeChanged);

			VirtualListDataSource = new VirtualListVersion1DataSource(this);
		}

		#region Public Properties

		/// <summary>
		/// Get or set the collection of model objects that are checked.
		/// When setting this property, any row whose model object isn't
		/// in the given collection will be unchecked. Setting to null is
		/// equivilent to unchecking all.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This property returns a simple collection. Changes made to the returned
		/// collection do NOT affect the list. This is different to the behaviour of
		/// CheckedIndicies collection.
		/// </para>
		/// <para>
		/// When getting CheckedObjects, the performance of this method is O(n) where n is the number of checked objects.
		/// When setting CheckedObjects, the performance of this method is O(n) where n is the number of checked objects plus
		/// the number of objects to be checked.
		/// </para>
		/// <para>
		/// If the ListView is not currently showing CheckBoxes, this property does nothing. It does
		/// not remember any check box settings made.
		/// </para>
		/// <para>
		/// This class optimizes the management of CheckStates so that it will work efficiently even on
		/// large lists of item. However, those optimizations are impossible if you install a CheckStateGetter.
		/// With a CheckStateGetter installed, the performance of this method is O(n) where n is the size 
		/// of the list. This could be painfully slow.</para>
		/// </remarks>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public override IList<object> CheckedObjects
		{
			get
			{
				// If we aren't should checkboxes, then no objects can be checked
				if (!CheckBoxes)
					return new List<object>();

				// If the data source has somehow vanished, we can't do anything
				if (VirtualListDataSource == null)
					return new List<object>();

				// If a custom check state getter is install, we can't use our check state management
				// We have to use the (slower) base version.
				if (CheckStateGetter != null)
					return base.CheckedObjects;

				// Collect items that are checked AND that still exist in the list.
				var objects = new List<object>();
				foreach (KeyValuePair<object, CheckState> kvp in CheckStateMap)
				{
					if (kvp.Value == CheckState.Checked &&
						(!CheckedObjectsMustStillExistInList ||
						 VirtualListDataSource.GetObjectIndex(kvp.Key) >= 0))
						objects.Add(kvp.Key);
				}
				return objects;
			}
			set
			{
				if (!CheckBoxes)
					return;

				// If a custom check state getter is install, we can't use our check state management
				// We have to use the (slower) base version.
				if (CheckStateGetter != null)
				{
					base.CheckedObjects = value;
					return;
				}

				// Set up an efficient way of testing for the presence of a particular model
				Hashtable table = new Hashtable(GetItemCount());
				if (value != null)
				{
					foreach (object x in value)
						table[x] = true;
				}

				BeginUpdate();

				// Uncheck anything that is no longer checked
				object[] keys = new object[CheckStateMap.Count];
				CheckStateMap.Keys.CopyTo(keys, 0);
				foreach (object key in keys)
				{
					if (!table.Contains(key))
						SetObjectCheckedness(key, CheckState.Unchecked);
				}

				// Check all the new checked objects
				foreach (object x in table.Keys)
					SetObjectCheckedness(x, CheckState.Checked);

				EndUpdate();
			}
		}

		/// <summary>
		/// Gets or sets whether or not an object will be included in the CheckedObjects
		/// collection, even if it is not present in the control at the moment
		/// </summary>
		/// <remarks>
		/// This property is an implementation detail and should not be altered.
		/// </remarks>
		protected internal bool CheckedObjectsMustStillExistInList
		{
			get { return checkedObjectsMustStillExistInList; }
			set { checkedObjectsMustStillExistInList = value; }
		}
		private bool checkedObjectsMustStillExistInList = true;

		/// <summary>
		/// Get/set the data source that is behind this virtual list
		/// </summary>
		/// <remarks>Setting this will cause the list to redraw.</remarks>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public virtual IVirtualListDataSource VirtualListDataSource
		{
			get
			{
				return virtualListDataSource;
			}
			set
			{
				virtualListDataSource = value;
				CustomSorter = delegate (OLVColumn column, SortOrder sortOrder)
				{
					ClearCachedInfo();
					virtualListDataSource.Sort(column, sortOrder);
				};
				BuildList(false);
			}
		}
		private IVirtualListDataSource virtualListDataSource;

		/// <summary>
		/// Gets or sets the number of rows in this virtual list.
		/// </summary>
		/// <remarks>
		/// There is an annoying feature/bug in the .NET ListView class. 
		/// When you change the VirtualListSize property, it always scrolls so
		/// that the focused item is the top item. This is annoying since it makes
		/// the virtual list seem to flicker as the control scrolls to show the focused
		/// item and then scrolls back to where ObjectListView wants it to be.
		/// </remarks>
		[Browsable(false),
		 DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		protected new virtual int VirtualListSize
		{
			get { return base.VirtualListSize; }
			set
			{
				if (value == VirtualListSize || value < 0)
					return;

				// Get around the 'private' marker on 'virtualListSize' field using reflection
				if (virtualListSizeFieldInfo == null)
				{
					virtualListSizeFieldInfo = typeof(ListView).GetField("virtualListSize", BindingFlags.NonPublic | BindingFlags.Instance);
					System.Diagnostics.Debug.Assert(virtualListSizeFieldInfo != null);
				}

				// Set the base class private field so that it keeps on working
				virtualListSizeFieldInfo.SetValue(this, value);

				// Send a raw message to change the virtual list size *without* changing the scroll position
				if (IsHandleCreated && !DesignMode)
					NativeMethods.SetItemCount(this, value);
			}
		}
		static private FieldInfo virtualListSizeFieldInfo;

		#endregion

		#region OLV accessing

		/// <summary>
		/// Return the number of items in the list
		/// </summary>
		/// <returns>the number of items in the list</returns>
		public override int GetItemCount()
		{
			return VirtualListSize;
		}

		/// <summary>
		/// Return the model object at the given index
		/// </summary>
		/// <param name="index">Index of the model object to be returned</param>
		/// <returns>A model object</returns>
		public override object GetModelObject(int index)
		{
			if (VirtualListDataSource != null && index >= 0 && index < GetItemCount())
				return VirtualListDataSource.GetNthObject(index);
			else
				return null;
		}

		/// <summary>
		/// Find the given model object within the listview and return its index
		/// </summary>
		/// <param name="modelObject">The model object to be found</param>
		/// <returns>The index of the object. -1 means the object was not present</returns>
		public override int IndexOf(object modelObject)
		{
			if (VirtualListDataSource == null || modelObject == null)
				return -1;

			return VirtualListDataSource.GetObjectIndex(modelObject);
		}

		/// <summary>
		/// Return the OLVListItem that displays the given model object
		/// </summary>
		/// <param name="modelObject">The modelObject whose item is to be found</param>
		/// <returns>The OLVListItem that displays the model, or null</returns>
		/// <remarks>This method has O(n) performance.</remarks>
		public override OLVListItem ModelToItem(object modelObject)
		{
			if (VirtualListDataSource == null || modelObject == null)
				return null;

			int index = VirtualListDataSource.GetObjectIndex(modelObject);
			return index >= 0 ? GetItem(index) : null;
		}

		#endregion

		#region Object manipulation

		/// <summary>
		/// Add the given collection of model objects to this control.
		/// </summary>
		/// <param name="modelObjects">A collection of model objects</param>
		/// <remarks>
		/// <para>The added objects will appear in their correct sort position, if sorting
		/// is active. Otherwise, they will appear at the end of the list.</para>
		/// <para>No check is performed to see if any of the objects are already in the ListView.</para>
		/// <para>Null objects are silently ignored.</para>
		/// </remarks>
		public override void AddObjects(ICollection<object> modelObjects)
		{
			if (VirtualListDataSource == null)
				return;

			// Give the world a chance to cancel or change the added objects
			ItemsAddingEventArgs args = new ItemsAddingEventArgs(modelObjects);
			OnItemsAdding(args);
			if (args.Canceled)
				return;

			try
			{
				BeginUpdate();
				VirtualListDataSource.AddObjects(args.ObjectsToAdd);
				BuildList();
			}
			finally
			{
				EndUpdate();
			}
		}

		/// <summary>
		/// Remove all items from this list
		/// </summary>
		/// <remark>This method can safely be called from background threads.</remark>
		public override void ClearObjects()
		{
			if (InvokeRequired)
				Invoke(new MethodInvoker(ClearObjects));
			else
			{
				CheckStateMap.Clear();
				SetObjects(new List<object>());
			}
		}

		/// <summary>
		/// Inserts the given collection of model objects to this control at hte given location
		/// </summary>
		/// <param name="modelObjects">A collection of model objects</param>
		/// <remarks>
		/// <para>The added objects will appear in their correct sort position, if sorting
		/// is active. Otherwise, they will appear at the given position of the list.</para>
		/// <para>No check is performed to see if any of the objects are already in the ListView.</para>
		/// <para>Null objects are silently ignored.</para>
		/// </remarks>
		public override void InsertObjects(int index, ICollection<object> modelObjects)
		{
			if (VirtualListDataSource == null)
				return;

			// Give the world a chance to cancel or change the added objects
			ItemsAddingEventArgs args = new ItemsAddingEventArgs(index, modelObjects);
			OnItemsAdding(args);
			if (args.Canceled)
				return;

			try
			{
				BeginUpdate();
				VirtualListDataSource.InsertObjects(index, args.ObjectsToAdd);
				BuildList();
			}
			finally
			{
				EndUpdate();
			}
		}

		/// <summary>
		/// Update the rows that are showing the given objects
		/// </summary>
		/// <remarks>This method does not resort the items.</remarks>
		public override void RefreshObjects(IList<object> modelObjects)
		{
			if (InvokeRequired)
			{
				Invoke((MethodInvoker)delegate { RefreshObjects(modelObjects); });
				return;
			}

			// Without a data source, we can't do this.
			if (VirtualListDataSource == null)
				return;

			try
			{
				BeginUpdate();
				ClearCachedInfo();
				foreach (object modelObject in modelObjects)
				{
					int index = VirtualListDataSource.GetObjectIndex(modelObject);
					if (index >= 0)
					{
						VirtualListDataSource.UpdateObject(index, modelObject);
						RedrawItems(index, index, true);
					}
				}
			}
			finally
			{
				EndUpdate();
			}
		}

		/// <summary>
		/// Update the rows that are selected
		/// </summary>
		/// <remarks>This method does not resort or regroup the view.</remarks>
		public override void RefreshSelectedObjects()
		{
			foreach (int index in SelectedIndices)
				RedrawItems(index, index, true);
		}

		/// <summary>
		/// Remove all of the given objects from the control
		/// </summary>
		/// <param name="modelObjects">Collection of objects to be removed</param>
		/// <remarks>
		/// <para>Nulls and model objects that are not in the ListView are silently ignored.</para>
		/// <para>Due to problems in the underlying ListView, if you remove all the objects from
		/// the control using this method and the list scroll vertically when you do so,
		/// then when you subsequenially add more objects to the control,
		/// the vertical scroll bar will become confused and the control will draw one or more
		/// blank lines at the top of the list. </para>
		/// </remarks>
		public override void RemoveObjects(ICollection<object> modelObjects)
		{
			if (VirtualListDataSource == null)
				return;

			// Give the world a chance to cancel or change the removed objects
			ItemsRemovingEventArgs args = new ItemsRemovingEventArgs(modelObjects);
			OnItemsRemoving(args);
			if (args.Canceled)
				return;

			try
			{
				BeginUpdate();
				VirtualListDataSource.RemoveObjects(args.ObjectsToRemove);
				BuildList();
			}
			finally
			{
				EndUpdate();
			}
		}

		/// <summary>
		/// Select the row that is displaying the given model object. All other rows are deselected.
		/// </summary>
		/// <param name="modelObject">Model object to select</param>
		/// <param name="setFocus">Should the object be focused as well?</param>
		public override void SelectObject(object modelObject, bool setFocus)
		{
			// Without a data source, we can't do this.
			if (VirtualListDataSource == null)
				return;

			// Check that the object is in the list (plus not all data sources can locate objects)
			int index = VirtualListDataSource.GetObjectIndex(modelObject);
			if (index < 0 || index >= VirtualListSize)
				return;

			// If the given model is already selected, don't do anything else (prevents an flicker)
			if (SelectedIndices.Count == 1 && SelectedIndices[0] == index)
				return;

			// Finally, select the row
			SelectedIndices.Clear();
			SelectedIndices.Add(index);
			if (setFocus && SelectedItem != null)
				SelectedItem.Focused = true;
		}

		/// <summary>
		/// Select the rows that is displaying any of the given model object. All other rows are deselected.
		/// </summary>
		/// <param name="modelObjects">A collection of model objects</param>
		/// <remarks>This method has O(n) performance where n is the number of model objects passed.
		/// Do not use this to select all the rows in the list -- use SelectAll() for that.</remarks>
		public override void SelectObjects(IList<object> modelObjects)
		{
			// Without a data source, we can't do this.
			if (VirtualListDataSource == null)
				return;

			SelectedIndices.Clear();

			if (modelObjects == null)
				return;

			foreach (object modelObject in modelObjects)
			{
				int index = VirtualListDataSource.GetObjectIndex(modelObject);
				if (index >= 0 && index < VirtualListSize)
					SelectedIndices.Add(index);
			}
		}

		/// <summary>
		/// Set the collection of objects that this control will show.
		/// </summary>
		/// <param name="collection"></param>
		/// <param name="preserveState">Should the state of the list be preserved as far as is possible.</param>
		public override void SetObjects(IEnumerable<object> collection, bool preserveState)
		{
			if (InvokeRequired)
			{
				Invoke((MethodInvoker)delegate { SetObjects(collection, preserveState); });
				return;
			}

			if (VirtualListDataSource == null)
				return;

			// Give the world a chance to cancel or change the assigned collection
			ItemsChangingEventArgs args = new ItemsChangingEventArgs(null, collection);
			OnItemsChanging(args);
			if (args.Canceled)
				return;

			BeginUpdate();
			try
			{
				VirtualListDataSource.SetObjects(args.NewObjects);
				BuildList();
			}
			finally
			{
				EndUpdate();
			}
		}

		#endregion

		#region Check boxes

		/// <summary>
		/// Get the checkedness of an object from the model. Returning null means the
		/// model does know and the value from the control will be used.
		/// </summary>
		/// <param name="modelObject"></param>
		/// <returns></returns>
		protected override CheckState? GetCheckState(object modelObject)
		{
			if (CheckStateGetter != null)
				return base.GetCheckState(modelObject);

			CheckState state;
			if (modelObject != null && CheckStateMap.TryGetValue(modelObject, out state))
				return state;
			return CheckState.Unchecked;
		}

		#endregion

		#region Implementation

		/// <summary>
		/// Rebuild the list with its current contents.
		/// </summary>
		/// <remarks>
		/// Invalidate any cached information when we rebuild the list.
		/// </remarks>
		public override void BuildList(bool shouldPreserveSelection)
		{
			UpdateVirtualListSize();
			ClearCachedInfo();
			Sort();
			Invalidate();
		}

		/// <summary>
		/// Clear any cached info this list may have been using
		/// </summary>
		public override void ClearCachedInfo()
		{
			lastRetrieveVirtualItemIndex = -1;
		}

		/// <summary>
		/// Return the position of the given itemIndex in the list as it currently shown to the user.
		/// If the control is not grouped, the display order is the same as the
		/// sorted list order. But if the list is grouped, the display order is different.
		/// </summary>
		/// <param name="itemIndex"></param>
		/// <returns></returns>
		public override int GetDisplayOrderOfItemIndex(int itemIndex)
		{
			return itemIndex;
		}

		/// <summary>
		/// Return the n'th item (0-based) in the order they are shown to the user.
		/// If the control is not grouped, the display order is the same as the
		/// sorted list order. But if the list is grouped, the display order is different.
		/// </summary>
		/// <param name="n"></param>
		/// <returns></returns>
		public override OLVListItem GetNthItemInDisplayOrder(int n)
		{
			return GetItem(n);
		}

		/// <summary>
		/// Create a OLVListItem for given row index
		/// </summary>
		/// <param name="itemIndex">The index of the row that is needed</param>
		/// <returns>An OLVListItem</returns>
		public virtual OLVListItem MakeListViewItem(int itemIndex)
		{
			OLVListItem olvi = new OLVListItem(GetModelObject(itemIndex));
			FillInValues(olvi, olvi.RowObject);

			PostProcessOneRow(itemIndex, GetDisplayOrderOfItemIndex(itemIndex), olvi);

			return olvi;
		}

		/// <summary>
		/// On virtual lists, this cannot work.
		/// </summary>
		protected override void PostProcessRows()
		{
		}

		/// <summary>
		/// Record the change of checkstate for the given object in the model.
		/// This does not update the UI -- only the model
		/// </summary>
		/// <param name="modelObject"></param>
		/// <param name="state"></param>
		/// <returns>The check state that was recorded and that should be used to update
		/// the control.</returns>
		protected override CheckState PutCheckState(object modelObject, CheckState state)
		{
			state = base.PutCheckState(modelObject, state);
			CheckStateMap[modelObject] = state;
			return state;
		}

		/// <summary>
		/// Refresh the given item in the list
		/// </summary>
		/// <param name="olvi">The item to refresh</param>
		public override void RefreshItem(OLVListItem olvi)
		{
			ClearCachedInfo();
			RedrawItems(olvi.Index, olvi.Index, true);
		}

		/// <summary>
		/// Change the size of the list
		/// </summary>
		/// <param name="newSize"></param>
		protected virtual void SetVirtualListSize(int newSize)
		{
			if (newSize < 0 || VirtualListSize == newSize)
				return;

			int oldSize = VirtualListSize;

			ClearCachedInfo();

			// There is a bug in .NET when a virtual ListView is cleared
			// (i.e. VirtuaListSize set to 0) AND it is scrolled vertically: the scroll position 
			// is wrong when the list is next populated. To avoid this, before 
			// clearing a virtual list, we make sure the list is scrolled to the top.
			// [6 weeks later] Damn this is a pain! There are cases where this can also throw exceptions!
			try
			{
				if (newSize == 0 && TopItemIndex > 0)
					TopItemIndex = 0;
			}
			catch (Exception)
			{
				// Ignore any failures
			}

			// In strange cases, this can throw the exceptions too. The best we can do is ignore them :(
			try
			{
				VirtualListSize = newSize;
			}
			catch (ArgumentOutOfRangeException)
			{
				// pass
			}
			catch (NullReferenceException)
			{
				// pass
			}

			// Tell the world that the size of the list has changed
			OnItemsChanged(new ItemsChangedEventArgs(oldSize, VirtualListSize));
		}

		/// <summary>
		/// Take ownership of the 'objects' collection. This separates our collection from the source.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This method
		/// separates the 'objects' instance variable from its source, so that any AddObject/RemoveObject
		/// calls will modify our collection and not the original colleciton.
		/// </para>
		/// <para>
		/// VirtualObjectListViews always own their collections, so this is a no-op.
		/// </para>
		/// </remarks>
		protected override void TakeOwnershipOfObjects()
		{
		}

		/// <summary>
		/// Change the size of the virtual list so that it matches its data source
		/// </summary>
		public virtual void UpdateVirtualListSize()
		{
			if (VirtualListDataSource != null)
				SetVirtualListSize(VirtualListDataSource.GetObjectCount());
		}

		#endregion

		#region Event handlers

		/// <summary>
		/// Handle the CacheVirtualItems event
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		protected virtual void HandleCacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
		{
			if (VirtualListDataSource != null)
				VirtualListDataSource.PrepareCache(e.StartIndex, e.EndIndex);
		}

		/// <summary>
		/// Handle a RetrieveVirtualItem
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		protected virtual void HandleRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
		{
			// .NET 2.0 seems to generate a lot of these events. Before drawing *each* sub-item,
			// this event is triggered 4-8 times for the same index. So we save lots of CPU time
			// by caching the last result.
			//System.Diagnostics.Debug.WriteLine(String.Format("HandleRetrieveVirtualItem({0})", e.ItemIndex));

			if (lastRetrieveVirtualItemIndex != e.ItemIndex)
			{
				lastRetrieveVirtualItemIndex = e.ItemIndex;
				lastRetrieveVirtualItem = MakeListViewItem(e.ItemIndex);
			}
			e.Item = lastRetrieveVirtualItem;
		}

		/// <summary>
		/// Handle the SearchForVirtualList event, which is called when the user types into a virtual list
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		protected virtual void HandleSearchForVirtualItem(object sender, SearchForVirtualItemEventArgs e)
		{
			// The event has e.IsPrefixSearch, but as far as I can tell, this is always false (maybe that's different under Vista)
			// So we ignore IsPrefixSearch and IsTextSearch and always to a case insensitve prefix match.

			// We can't do anything if we don't have a data source
			if (VirtualListDataSource == null)
				return;

			// Where should we start searching? If the last row is focused, the SearchForVirtualItemEvent starts searching
			// from the next row, which is actually an invalidate index -- so we make sure we never go past the last object.
			int start = Math.Min(e.StartIndex, VirtualListDataSource.GetObjectCount() - 1);

			// Give the world a chance to fiddle with or completely avoid the searching process
			BeforeSearchingEventArgs args = new BeforeSearchingEventArgs(e.Text, start);
			OnBeforeSearching(args);
			if (args.Canceled)
				return;

			// Do the search
			int i = FindMatchingRow(args.StringToFind, args.StartSearchFrom, e.Direction);

			// Tell the world that a search has occurred
			AfterSearchingEventArgs args2 = new AfterSearchingEventArgs(args.StringToFind, i);
			OnAfterSearching(args2);

			// If we found a match, tell the event
			if (i != -1)
				e.Index = i;
		}

		/// <summary>
		/// Find the first row in the given range of rows that prefix matches the string value of the given column.
		/// </summary>
		/// <param name="text"></param>
		/// <param name="first"></param>
		/// <param name="last"></param>
		/// <param name="column"></param>
		/// <returns>The index of the matched row, or -1</returns>
		protected override int FindMatchInRange(string text, int first, int last, OLVColumn column)
		{
			return VirtualListDataSource.SearchText(text, first, last, column);
		}

		#endregion

		#region Variable declaractions

		private OLVListItem lastRetrieveVirtualItem;
		private int lastRetrieveVirtualItemIndex = -1;

		#endregion
	}
}