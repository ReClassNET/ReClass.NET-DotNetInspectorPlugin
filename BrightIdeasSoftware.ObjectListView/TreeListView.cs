using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace BrightIdeasSoftware
{
	/// <summary>
	/// A TreeListView combines an expandable tree structure with list view columns.
	/// </summary>
	/// <remarks>
	/// <para>To support tree operations, two delegates must be provided:</para>
	/// <list type="table">
	/// <item>
	/// <term>
	/// CanExpandGetter
	/// </term> 
	/// <description>
	/// This delegate must accept a model object and return a boolean indicating
	/// if that model should be expandable. 
	/// </description>
	/// </item>
	/// <item>
	/// <term>
	/// ChildrenGetter
	/// </term> 
	/// <description>
	/// This delegate must accept a model object and return an IEnumerable of model
	/// objects that will be displayed as children of the parent model. This delegate will only be called
	/// for a model object if the CanExpandGetter has already returned true for that model.
	/// </description>
	/// </item>
	/// <item>
	/// <term>
	/// ParentGetter
	/// </term> 
	/// <description>
	/// This delegate must accept a model object and return the parent model. 
	/// This delegate will only be called when HierarchicalCheckboxes is true OR when Reveal() is called. 
	/// </description>
	/// </item>
	/// </list>
	/// <para>
	/// The top level branches of the tree are set via the Roots property. SetObjects(), AddObjects() 
	/// and RemoveObjects() are interpreted as operations on this collection of roots.
	/// </para>
	/// <para>
	/// To add new children to an existing branch, make changes to your model objects and then
	/// call RefreshObject() on the parent.
	/// </para>
	/// <para>The tree must be a directed acyclic graph -- no cycles are allowed. Put more mundanely, 
	/// each model object must appear only once in the tree. If the same model object appears in two
	/// places in the tree, the control will become confused.</para>
	/// </remarks>
	public partial class TreeListView : VirtualObjectListView
	{
		/// <summary>
		/// Make a default TreeListView
		/// </summary>
		public TreeListView()
		{
			OwnerDraw = true;
			View = View.Details;
			CheckedObjectsMustStillExistInList = false;

			// ReSharper disable DoNotCallOverridableMethodsInConstructor
			RegenerateTree();
			TreeColumnRenderer = new TreeRenderer();
			// ReSharper restore DoNotCallOverridableMethodsInConstructor

			// This improves hit detection even if we don't have any state image
			SmallImageList = new ImageList();
			// this.StateImageList.ImageSize = new Size(6, 6);
		}

		//------------------------------------------------------------------------------------------
		// Properties

		/// <summary>
		/// This is the delegate that will be used to decide if a model object can be expanded.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This is called *often* -- on every mouse move when required. It must be fast.
		/// Don't do database lookups, linear searches, or pi calculations. Just return the
		/// value of a property.
		/// </para>
		/// <para>
		/// When this delegate is called, the TreeListView is not in a stable state. Don't make
		/// calls back into the control.
		/// </para>
		/// </remarks>
		[Browsable(false),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public virtual CanExpandGetterDelegate CanExpandGetter
		{
			get { return TreeModel.CanExpandGetter; }
			set { TreeModel.CanExpandGetter = value; }
		}

		/// <summary>
		/// This is the delegate that will be used to fetch the children of a model object
		/// </summary>
		/// <remarks>
		/// <para>
		/// This delegate will only be called if the CanExpand delegate has 
		/// returned true for the model object.
		/// </para>
		/// <para>
		/// When this delegate is called, the TreeListView is not in a stable state. Don't do anything
		/// that will result in calls being made back into the control.
		/// </para>
		/// </remarks>
		[Browsable(false),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public virtual ChildrenGetterDelegate ChildrenGetter
		{
			get { return TreeModel.ChildrenGetter; }
			set { TreeModel.ChildrenGetter = value; }
		}

		/// <summary>
		/// Get or set the collection of model objects that are checked.
		/// When setting this property, any row whose model object isn't
		/// in the given collection will be unchecked. Setting to null is
		/// equivalent to unchecking all.
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
		/// </remarks>
		public override IList<object> CheckedObjects
		{
			get
			{
				return base.CheckedObjects;
			}
			set
			{
				var objectsToRecalculate = new List<object>(CheckedObjects);
				if (value != null)
					objectsToRecalculate.AddRange(value);

				base.CheckedObjects = value;

				if (HierarchicalCheckboxes)
					RecalculateHierarchicalCheckBoxGraph(objectsToRecalculate);
			}
		}

		/// <summary>
		/// Gets or sets the model objects that are expanded.
		/// </summary>
		/// <remarks>
		/// <para>This can be used to expand model objects before they are seen.</para>
		/// <para>
		/// Setting this does *not* force the control to rebuild
		/// its display. You need to call RebuildAll(true).
		/// </para>
		/// </remarks>
		[Browsable(false),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public IEnumerable<object> ExpandedObjects
		{
			get
			{
				return TreeModel.mapObjectToExpanded.Keys;
			}
			set
			{
				TreeModel.mapObjectToExpanded.Clear();
				if (value != null)
				{
					foreach (object x in value)
						TreeModel.SetModelExpanded(x, true);
				}
			}
		}

		/// <summary>
		/// Gets or sets whether this tree list view will display hierarchical checkboxes.
		/// Hierarchical checkboxes is when a parent's "checkedness" is calculated from
		/// the "checkedness" of its children. If all children are checked, the parent
		/// will be checked. If all children are unchecked, the parent will also be unchecked.
		/// If some children are checked and others are not, the parent will be indeterminate.
		/// </summary>
		/// <remarks>
		/// Hierarchical checkboxes don't work with either CheckStateGetters or CheckedAspectName
		/// (which is basically the same thing). This is because it is too expensive to build the 
		/// initial state of the control if these are installed, since the control would have to walk
		/// *every* branch recursively since a single bottom level leaf could change the checkedness
		/// of the top root.
		/// </remarks>
		[Category("ObjectListView"),
		 Description("Show hierarchical checkboxes be enabled?"),
		 DefaultValue(false)]
		public virtual bool HierarchicalCheckboxes
		{
			get { return hierarchicalCheckboxes; }
			set
			{
				if (hierarchicalCheckboxes == value)
					return;

				hierarchicalCheckboxes = value;
				CheckBoxes = value;
				if (value)
					TriStateCheckBoxes = false;
			}
		}
		private bool hierarchicalCheckboxes;

		/// <summary>
		/// Gets or sets the collection of root objects of the tree
		/// </summary>
		[Browsable(false),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public override IEnumerable<object> Objects
		{
			get { return Roots; }
			set { Roots = value; }
		}

		/// <summary>
		/// After expanding a branch, should the TreeListView attempts to show as much of the 
		/// revealed descendents as possible.
		/// </summary>
		[Category("ObjectListView"),
		 Description("Should the parent of an expand subtree be scrolled to the top revealing the children?"),
		 DefaultValue(true)]
		public bool RevealAfterExpand
		{
			get { return revealAfterExpand; }
			set { revealAfterExpand = value; }
		}
		private bool revealAfterExpand = true;

		/// <summary>
		/// The model objects that form the top level branches of the tree.
		/// </summary>
		/// <remarks>Setting this does <b>NOT</b> reset the state of the control.
		/// In particular, it does not collapse branches.</remarks>
		[Browsable(false),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public virtual IEnumerable<object> Roots
		{
			get { return TreeModel.RootObjects; }
			set
			{
				TreeColumnRenderer = TreeColumnRenderer;
				TreeModel.RootObjects = value ?? new List<object>();
				UpdateVirtualListSize();
			}
		}

		/// <summary>
		/// Make sure that at least one column is displaying a tree. 
		/// If no columns is showing the tree, make column 0 do it.
		/// </summary>
		protected virtual void EnsureTreeRendererPresent(TreeRenderer renderer)
		{
			if (Columns.Count == 0)
				return;

			foreach (OLVColumn col in Columns)
			{
				if (col.Renderer is TreeRenderer)
				{
					col.Renderer = renderer;
					return;
				}
			}

			// No column held a tree renderer, so give column 0 one
			OLVColumn columnZero = GetColumn(0);
			columnZero.Renderer = renderer;
			columnZero.WordWrap = columnZero.WordWrap;
		}

		/// <summary>
		/// Gets or sets the renderer that will be used to draw the tree structure.
		/// Setting this to null resets the renderer to default.
		/// </summary>
		/// <remarks>If a column is currently rendering the tree, the renderer
		/// for that column will be replaced. If no column is rendering the tree,
		/// column 0 will be given this renderer.</remarks>
		[Browsable(false),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public virtual TreeRenderer TreeColumnRenderer
		{
			get { return treeRenderer ?? (treeRenderer = new TreeRenderer()); }
			set
			{
				treeRenderer = value ?? new TreeRenderer();
				EnsureTreeRendererPresent(treeRenderer);
			}
		}
		private TreeRenderer treeRenderer;

		/// <summary>
		/// This is the delegate that will be used to create the underlying Tree structure
		/// that the TreeListView uses to manage the information about the tree.
		/// </summary>
		/// <remarks>
		/// <para>The factory must not return null. </para>
		/// <para>
		/// Most users of TreeListView will never have to use this delegate.
		/// </para>
		/// </remarks>
		[Browsable(false),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public TreeFactoryDelegate TreeFactory
		{
			get { return treeFactory; }
			set { treeFactory = value; }
		}
		private TreeFactoryDelegate treeFactory;

		/// <summary>
		/// Should a wait cursor be shown when a branch is being expanded?
		/// </summary>
		/// <remarks>When this is true, the wait cursor will be shown whilst the children of the 
		/// branch are being fetched. If the children of the branch have already been cached, 
		/// the cursor will not change.</remarks>
		[Category("ObjectListView"),
		Description("Should a wait cursor be shown when a branch is being expanded?"),
		DefaultValue(true)]
		public virtual bool UseWaitCursorWhenExpanding
		{
			get { return useWaitCursorWhenExpanding; }
			set { useWaitCursorWhenExpanding = value; }
		}
		private bool useWaitCursorWhenExpanding = true;

		/// <summary>
		/// Gets the model that is used to manage the tree structure
		/// </summary>
		/// <remarks>
		/// Don't mess with this property unless you really know what you are doing.
		/// If you don't already know what it's for, you don't need it.</remarks>
		[Browsable(false),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Tree TreeModel
		{
			get { return treeModel; }
			protected set { treeModel = value; }
		}
		private Tree treeModel;

		/// <summary>
		/// Return true if the branch at the given model is expanded
		/// </summary>
		/// <param name="model"></param>
		/// <returns></returns>
		public virtual bool IsExpanded(object model)
		{
			var br = TreeModel.GetBranch(model);
			return br != null && br.IsExpanded;
		}

		//------------------------------------------------------------------------------------------
		// Commands

		/// <summary>
		/// Collapse the subtree underneath the given model
		/// </summary>
		/// <param name="model"></param>
		public virtual void Collapse(object model)
		{
			if (GetItemCount() == 0)
				return;

			OLVListItem item = ModelToItem(model);
			TreeBranchCollapsingEventArgs args = new TreeBranchCollapsingEventArgs(model, item);
			OnCollapsing(args);
			if (args.Canceled)
				return;

			IList<object> selection = SelectedObjects;
			int index = TreeModel.Collapse(model);
			if (index >= 0)
			{
				UpdateVirtualListSize();
				SelectedObjects = selection;
				if (index < GetItemCount())
					RedrawItems(index, GetItemCount() - 1, true);
				OnCollapsed(new TreeBranchCollapsedEventArgs(model, item));
			}
		}

		/// <summary>
		/// Collapse all subtrees within this control
		/// </summary>
		public virtual void CollapseAll()
		{
			if (GetItemCount() == 0)
				return;

			TreeBranchCollapsingEventArgs args = new TreeBranchCollapsingEventArgs(null, null);
			OnCollapsing(args);
			if (args.Canceled)
				return;

			var selection = SelectedObjects;
			int index = TreeModel.CollapseAll();
			if (index >= 0)
			{
				UpdateVirtualListSize();
				SelectedObjects = selection;
				if (index < GetItemCount())
					RedrawItems(index, GetItemCount() - 1, true);
				OnCollapsed(new TreeBranchCollapsedEventArgs(null, null));
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
				Roots = null;
				DiscardAllState();
			}
		}

		/// <summary>
		/// Collapse all roots and forget everything we know about all models
		/// </summary>
		public virtual void DiscardAllState()
		{
			CheckStateMap.Clear();
			RebuildAll(false);
		}

		/// <summary>
		/// Expand the subtree underneath the given model object
		/// </summary>
		/// <param name="model"></param>
		public virtual void Expand(object model)
		{
			if (GetItemCount() == 0)
				return;

			// Give the world a chance to cancel the expansion
			OLVListItem item = ModelToItem(model);
			TreeBranchExpandingEventArgs args = new TreeBranchExpandingEventArgs(model, item);
			OnExpanding(args);
			if (args.Canceled)
				return;

			// Remember the selection so we can put it back later
			var selection = SelectedObjects;

			// Expand the model first
			int index = TreeModel.Expand(model);
			if (index < 0)
				return;

			// Update the size of the list and restore the selection
			UpdateVirtualListSize();
			using (SuspendSelectionEventsDuring())
				SelectedObjects = selection;

			// Redraw the items that were changed by the expand operation
			RedrawItems(index, GetItemCount() - 1, true);

			OnExpanded(new TreeBranchExpandedEventArgs(model, item));

			if (RevealAfterExpand && index > 0)
			{
				// TODO: This should be a separate method
				BeginUpdate();
				try
				{
					int countPerPage = NativeMethods.GetCountPerPage(this);
					int descedentCount = TreeModel.GetVisibleDescendentCount(model);
					// If all of the descendents can be shown in the window, make sure that last one is visible.
					// If all the descendents can't fit into the window, move the model to the top of the window
					// (which will show as many of the descendents as possible)
					if (descedentCount < countPerPage)
					{
						EnsureVisible(index + descedentCount);
					}
					else
					{
						TopItemIndex = index;
					}
				}
				finally
				{
					EndUpdate();
				}
			}
		}

		/// <summary>
		/// Expand all the branches within this tree recursively.
		/// </summary>
		/// <remarks>Be careful: this method could take a long time for large trees.</remarks>
		public virtual void ExpandAll()
		{
			if (GetItemCount() == 0)
				return;

			// Give the world a chance to cancel the expansion
			TreeBranchExpandingEventArgs args = new TreeBranchExpandingEventArgs(null, null);
			OnExpanding(args);
			if (args.Canceled)
				return;

			var selection = SelectedObjects;
			int index = TreeModel.ExpandAll();
			if (index < 0)
				return;

			UpdateVirtualListSize();
			using (SuspendSelectionEventsDuring())
				SelectedObjects = selection;
			RedrawItems(index, GetItemCount() - 1, true);
			OnExpanded(new TreeBranchExpandedEventArgs(null, null));
		}

		/// <summary>
		/// Completely rebuild the tree structure
		/// </summary>
		/// <param name="preserveState">If true, the control will try to preserve selection and expansion</param>
		public virtual void RebuildAll(bool preserveState)
		{
			int previousTopItemIndex = preserveState ? TopItemIndex : -1;

			RebuildAll(
				preserveState ? SelectedObjects : null,
				preserveState ? ExpandedObjects : null,
				preserveState ? CheckedObjects : null);

			if (preserveState)
				TopItemIndex = previousTopItemIndex;
		}

		/// <summary>
		/// Completely rebuild the tree structure
		/// </summary>
		/// <param name="selected">If not null, this list of objects will be selected after the tree is rebuilt</param>
		/// <param name="expanded">If not null, this collection of objects will be expanded after the tree is rebuilt</param>
		/// <param name="checkedObjects">If not null, this collection of objects will be checked after the tree is rebuilt</param>
		protected virtual void RebuildAll(IList<object> selected, IEnumerable<object> expanded, IList<object> checkedObjects)
		{
			// Remember the bits of info we don't want to forget (anyone ever see Memento?)
			IEnumerable<object> roots = Roots;
			CanExpandGetterDelegate canExpand = CanExpandGetter;
			ChildrenGetterDelegate childrenGetter = ChildrenGetter;

			try
			{
				BeginUpdate();

				// Give ourselves a new data structure
				RegenerateTree();

				// Put back the bits we didn't want to forget
				CanExpandGetter = canExpand;
				ChildrenGetter = childrenGetter;
				if (expanded != null)
					ExpandedObjects = expanded;
				Roots = roots;
				if (selected != null)
					SelectedObjects = selected;
				if (checkedObjects != null)
					CheckedObjects = checkedObjects;
			}
			finally
			{
				EndUpdate();
			}
		}

		/// <summary>
		/// Update the rows that are showing the given objects
		/// </summary>
		public override void RefreshObjects(IList<object> modelObjects)
		{
			if (InvokeRequired)
			{
				Invoke((MethodInvoker)delegate { RefreshObjects(modelObjects); });
				return;
			}
			// There is no point in refreshing anything if the list is empty
			if (GetItemCount() == 0)
				return;

			// Remember the selection so we can put it back later
			var selection = SelectedObjects;

			// We actually need to refresh the parents.
			// Refreshes on root objects have to be handled differently
			ArrayList updatedRoots = new ArrayList();
			Hashtable modelsAndParents = new Hashtable();
			foreach (object model in modelObjects)
			{
				if (model == null)
					continue;
				modelsAndParents[model] = true;
				object parent = GetParent(model);
				if (parent == null)
				{
					updatedRoots.Add(model);
				}
				else
				{
					modelsAndParents[parent] = true;
				}
			}

			// Update any changed roots
			if (updatedRoots.Count > 0)
			{
				var newRoots = EnumerableToArray(Roots, false);
				bool changed = false;
				foreach (object model in updatedRoots)
				{
					int index = newRoots.IndexOf(model);
					if (index >= 0 && !ReferenceEquals(newRoots[index], model))
					{
						newRoots[index] = model;
						changed = true;
					}
				}
				if (changed)
					Roots = newRoots;
			}

			// Refresh each object, remembering where the first update occurred
			int firstChange = Int32.MaxValue;
			foreach (object model in modelsAndParents.Keys)
			{
				if (model != null)
				{
					int index = TreeModel.RebuildChildren(model);
					if (index >= 0)
						firstChange = Math.Min(firstChange, index);
				}
			}

			// If we didn't refresh any objects, don't do anything else
			if (firstChange >= GetItemCount())
				return;

			ClearCachedInfo();
			UpdateVirtualListSize();
			SelectedObjects = selection;

			// Redraw everything from the first update to the end of the list
			RedrawItems(firstChange, GetItemCount() - 1, true);
		}

		/// <summary>
		/// Change the check state of the given object to be the given state.
		/// </summary>
		/// <remarks>
		/// If the given model object isn't in the list, we still try to remember
		/// its state, in case it is referenced in the future.</remarks>
		/// <param name="modelObject"></param>
		/// <param name="state"></param>
		/// <returns>True if the checkedness of the model changed</returns>
		protected override bool SetObjectCheckedness(object modelObject, CheckState state)
		{
			// If the checkedness of the given model changes AND this tree has 
			// hierarchical checkboxes, then we need to update the checkedness of 
			// its children, and recalculate the checkedness of the parent (recursively)
			if (!base.SetObjectCheckedness(modelObject, state))
				return false;

			if (!HierarchicalCheckboxes)
				return true;

			// Give each child the same checkedness as the model

			CheckState? checkedness = GetCheckState(modelObject);
			if (!checkedness.HasValue || checkedness.Value == CheckState.Indeterminate)
				return true;

			foreach (object child in GetChildrenWithoutExpanding(modelObject))
			{
				SetObjectCheckedness(child, checkedness.Value);
			}

			var args = new List<object>();
			args.Add(modelObject);
			RecalculateHierarchicalCheckBoxGraph(args);

			return true;
		}

		private IEnumerable GetChildrenWithoutExpanding(object model)
		{
			Branch br = TreeModel.GetBranch(model);
			if (br == null || !br.CanExpand)
				return new ArrayList();

			return br.Children;
		}

		/// <summary>
		/// Toggle the expanded state of the branch at the given model object
		/// </summary>
		/// <param name="model"></param>
		public virtual void ToggleExpansion(object model)
		{
			if (IsExpanded(model))
				Collapse(model);
			else
				Expand(model);
		}

		/// <summary>
		/// Return the model object that is the parent of the given model object.
		/// </summary>
		/// <param name="model"></param>
		/// <returns></returns>
		/// <remarks>The given model must have already been seen in the tree.</remarks>
		public virtual object GetParent(object model)
		{
			Branch br = TreeModel.GetBranch(model);
			return br == null || br.ParentBranch == null ? null : br.ParentBranch.Model;
		}

		/// <summary>
		/// Delegates of this type are use to decide if the given model object can be expanded
		/// </summary>
		/// <param name="model">The model under consideration</param>
		/// <returns>Can the given model be expanded?</returns>
		public delegate bool CanExpandGetterDelegate(object model);

		/// <summary>
		/// Delegates of this type are used to fetch the children of the given model object
		/// </summary>
		/// <param name="model">The parent whose children should be fetched</param>
		/// <returns>An enumerable over the children</returns>
		public delegate IEnumerable<object> ChildrenGetterDelegate(object model);

		/// <summary>
		/// Delegates of this type are used to fetch the parent of the given model object.
		/// </summary>
		/// <param name="model">The child whose parent should be fetched</param>
		/// <returns>The parent of the child or null if the child is a root</returns>
		public delegate object ParentGetterDelegate(object model);

		/// <summary>
		/// Delegates of this type are used to create a new underlying Tree structure.
		/// </summary>
		/// <param name="view">The view for which the Tree is being created</param>
		/// <returns>A subclass of Tree</returns>
		public delegate Tree TreeFactoryDelegate(TreeListView view);

		#region Implementation

		/// <summary>
		/// Handle a left button down event
		/// </summary>
		/// <param name="hti"></param>
		/// <returns></returns>
		protected override bool ProcessLButtonDown(OlvListViewHitTestInfo hti)
		{
			// Did they click in the expander?
			if (hti.HitTestLocation == HitTestLocation.ExpandButton)
			{
				PossibleFinishCellEditing();
				ToggleExpansion(hti.RowObject);
				return true;
			}

			return base.ProcessLButtonDown(hti);
		}

		/// <summary>
		/// Create a OLVListItem for given row index
		/// </summary>
		/// <param name="itemIndex">The index of the row that is needed</param>
		/// <returns>An OLVListItem</returns>
		/// <remarks>This differs from the base method by also setting up the IndentCount property.</remarks>
		public override OLVListItem MakeListViewItem(int itemIndex)
		{
			OLVListItem olvItem = base.MakeListViewItem(itemIndex);
			Branch br = TreeModel.GetBranch(olvItem.RowObject);
			if (br != null && itemIndex == 0)
				olvItem.IndentCount = br.Level;
			return olvItem;
		}

		/// <summary>
		/// Reinitialize the Tree structure
		/// </summary>
		protected virtual void RegenerateTree()
		{
			TreeModel = TreeFactory == null ? new Tree(this) : TreeFactory(this);
			Trace.Assert(TreeModel != null);
			VirtualListDataSource = TreeModel;
		}

		/// <summary>
		/// Recalculate the state of the checkboxes of all the items in the given list
		/// and their ancestors.
		/// </summary>
		/// <remarks>This only makes sense when HierarchicalCheckboxes is true.</remarks>
		/// <param name="toCheck"></param>
		protected virtual void RecalculateHierarchicalCheckBoxGraph(IList<object> toCheck)
		{
			if (toCheck == null || toCheck.Count == 0)
				return;

			// Avoid recursive calculations
			if (isRecalculatingHierarchicalCheckBox)
				return;

			try
			{
				isRecalculatingHierarchicalCheckBox = true;
				foreach (object ancestor in CalculateDistinctAncestors(toCheck))
					RecalculateSingleHierarchicalCheckBox(ancestor);
			}
			finally
			{
				isRecalculatingHierarchicalCheckBox = false;
			}

		}
		private bool isRecalculatingHierarchicalCheckBox;

		/// <summary>
		/// Recalculate the hierarchy state of the given item and its ancestors
		/// </summary>
		/// <remarks>This only makes sense when HierarchicalCheckboxes is true.</remarks>
		/// <param name="modelObject"></param>
		protected virtual void RecalculateSingleHierarchicalCheckBox(object modelObject)
		{

			if (modelObject == null)
				return;

			// Only branches have calculated check states. Leaf node checkedness is not calculated
			if (!CanExpandUncached(modelObject))
				return;

			// Set the checkedness of the given model based on the state of its children.
			CheckState? aggregate = null;
			foreach (object child in GetChildrenUncached(modelObject))
			{
				CheckState? checkedness = GetCheckState(child);
				if (!checkedness.HasValue)
					continue;

				if (aggregate.HasValue)
				{
					if (aggregate.Value != checkedness.Value)
					{
						aggregate = CheckState.Indeterminate;
						break;
					}
				}
				else
					aggregate = checkedness;
			}

			base.SetObjectCheckedness(modelObject, aggregate ?? CheckState.Indeterminate);
		}

		private bool CanExpandUncached(object model)
		{
			return CanExpandGetter != null && model != null && CanExpandGetter(model);
		}

		private IEnumerable GetChildrenUncached(object model)
		{
			return ChildrenGetter != null && model != null ? ChildrenGetter(model) : new List<object>();
		}

		/// <summary>
		/// Yield the unique ancestors of the given collection of objects.
		/// The order of the ancestors is guaranteed to be deeper objects first.
		/// Roots will always be last.
		/// </summary>
		/// <param name="toCheck"></param>
		/// <returns>Unique ancestors of the given objects</returns>
		protected virtual IEnumerable CalculateDistinctAncestors(IList<object> toCheck)
		{
			if (toCheck.Count == 1)
			{
				foreach (object ancestor in GetAncestors(toCheck[0]))
				{
					yield return ancestor;
				}
			}
			else
			{
				ArrayList allAncestors = new ArrayList();
				foreach (object child in toCheck)
				{
					foreach (object ancestor in GetAncestors(child))
					{
						allAncestors.Add(ancestor);
					}
				}

				allAncestors.Reverse();

				ArrayList uniqueAncestors = new ArrayList();
				Dictionary<object, bool> alreadySeen = new Dictionary<object, bool>();
				foreach (object ancestor in allAncestors)
				{
					if (!alreadySeen.ContainsKey(ancestor))
					{
						alreadySeen[ancestor] = true;
						uniqueAncestors.Add(ancestor);
					}
				}

				uniqueAncestors.Reverse();
				foreach (object x in uniqueAncestors)
					yield return x;
			}
		}

		/// <summary>
		/// Return all the ancestors of the given model
		/// </summary>
		/// <remarks>
		/// <para>
		/// This uses ParentGetter if possible.
		/// </para>
		/// <para>If the given model is a root OR if the model doesn't exist, the collection will be empty</para>
		/// </remarks>
		/// <param name="model">The model whose ancestors should be calculated</param>
		/// <returns>Return a collection of ancestors of the given model.</returns>
		protected virtual IEnumerable GetAncestors(object model)
		{
			object parent = GetParent(model);
			while (parent != null)
			{
				yield return parent;
				parent = GetParent(parent);
			}
		}

		#endregion

		#region Event handlers

		/// <summary>
		/// The application is idle and a SelectionChanged event has been scheduled
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		protected override void HandleApplicationIdle(object sender, EventArgs e)
		{
			base.HandleApplicationIdle(sender, e);

			// There is an annoying redraw issue on ListViews that use indentation and
			// that have full row select enabled. When the selection reduces to a subset
			// of previously selected rows, or when the selection is extended using
			// shift-pageup/down, then the space occupied by the indentation is not
			// invalidated, and hence remains highlighted.
			// Ideally we'd want to know exactly which rows were selected or deselected
			// and then invalidate just the indentation region of those rows,
			// but that's too much work. So just redraw the control.
			// Actually... the selection issues show just slightly for non-full row select
			// controls as well. So, always redraw the control after the selection
			// changes.
			Invalidate();
		}

		/// <summary>
		/// Decide if the given key event should be handled as a normal key input to the control?
		/// </summary>
		/// <param name="keyData"></param>
		/// <returns></returns>
		protected override bool IsInputKey(Keys keyData)
		{
			// We want to handle Left and Right keys within the control
			Keys key = keyData & Keys.KeyCode;
			if (key == Keys.Left || key == Keys.Right)
				return true;

			return base.IsInputKey(keyData);
		}

		/// <summary>
		/// Handle focus being lost, including making sure that the whole control is redrawn.
		/// </summary>
		/// <param name="e"></param>
		protected override void OnLostFocus(EventArgs e)
		{
			// When this focus is lost, the normal invalidation logic doesn't invalid the region
			// of the control created by the IndentLevel on each row. This makes the control 
			// look wrong when HideSelection is false, since part of the selected row's background
			// correctly changes colour to the "inactive" colour, but the left part of the row
			// created by IndentLevel doesn't change colour.
			// SF #135.

			Invalidate();
		}

		/// <summary>
		/// Handle the keyboard input to mimic a TreeView.
		/// </summary>
		/// <param name="e"></param>
		/// <returns>Was the key press handled?</returns>
		protected override void OnKeyDown(KeyEventArgs e)
		{
			OLVListItem focused = FocusedItem as OLVListItem;
			if (focused == null)
			{
				base.OnKeyDown(e);
				return;
			}

			object modelObject = focused.RowObject;
			Branch br = TreeModel.GetBranch(modelObject);

			switch (e.KeyCode)
			{
				case Keys.Left:
					// If the branch is expanded, collapse it. If it's collapsed,
					// select the parent of the branch.
					if (br.IsExpanded)
						Collapse(modelObject);
					else
					{
						if (br.ParentBranch != null && br.ParentBranch.Model != null)
							SelectObject(br.ParentBranch.Model, true);
					}
					e.Handled = true;
					break;

				case Keys.Right:
					// If the branch is expanded, select the first child.
					// If it isn't expanded and can be, expand it.
					if (br.IsExpanded)
					{
						List<Branch> filtered = br.FilteredChildBranches;
						if (filtered.Count > 0)
							SelectObject(filtered[0].Model, true);
					}
					else
					{
						if (br.CanExpand)
							Expand(modelObject);
					}
					e.Handled = true;
					break;
			}

			base.OnKeyDown(e);
		}

		#endregion

		/// <summary>
		/// A Tree object represents a tree structure data model that supports both 
		/// tree and flat list operations as well as fast access to branches.
		/// </summary>
		/// <remarks>If you create a subclass of Tree, you must install it in the TreeListView
		/// via the TreeFactory delegate.</remarks>
		public class Tree : IVirtualListDataSource
		{
			/// <summary>
			/// Create a Tree
			/// </summary>
			/// <param name="treeView"></param>
			public Tree(TreeListView treeView)
			{
				this.treeView = treeView;
				trunk = new Branch(null, this, null);
				trunk.IsExpanded = true;
			}

			//------------------------------------------------------------------------------------------
			// Properties

			/// <summary>
			/// This is the delegate that will be used to decide if a model object can be expanded.
			/// </summary>
			public CanExpandGetterDelegate CanExpandGetter
			{
				get { return canExpandGetter; }
				set { canExpandGetter = value; }
			}
			private CanExpandGetterDelegate canExpandGetter;

			/// <summary>
			/// This is the delegate that will be used to fetch the children of a model object
			/// </summary>
			/// <remarks>This delegate will only be called if the CanExpand delegate has 
			/// returned true for the model object.</remarks>
			public ChildrenGetterDelegate ChildrenGetter
			{
				get { return childrenGetter; }
				set { childrenGetter = value; }
			}
			private ChildrenGetterDelegate childrenGetter;


			/// <summary>
			/// Get or return the top level model objects in the tree
			/// </summary>
			public IEnumerable<object> RootObjects
			{
				get { return trunk.Children; }
				set
				{
					trunk.Children = value;
					foreach (Branch br in trunk.ChildBranches)
						br.RefreshChildren();
					RebuildList();
				}
			}

			/// <summary>
			/// What tree view is this Tree the model for?
			/// </summary>
			public TreeListView TreeView
			{
				get { return treeView; }
			}

			//------------------------------------------------------------------------------------------
			// Commands

			/// <summary>
			/// Collapse the subtree underneath the given model
			/// </summary>
			/// <param name="model">The model to be collapsed. If the model isn't in the tree,
			/// or if it is already collapsed, the command does nothing.</param>
			/// <returns>The index of the model in flat list version of the tree</returns>
			public virtual int Collapse(object model)
			{
				Branch br = GetBranch(model);
				if (br == null || !br.IsExpanded)
					return -1;

				// Remember that the branch is collapsed, even if it's currently not visible
				if (!br.Visible)
				{
					br.Collapse();
					return -1;
				}

				int count = br.NumberVisibleDescendents;
				br.Collapse();

				// Remove the visible descendents from after the branch itself
				int index = GetObjectIndex(model);
				objectList.RemoveRange(index + 1, count);
				RebuildObjectMap(0);
				return index;
			}

			/// <summary>
			/// Collapse all branches in this tree
			/// </summary>
			/// <returns>Nothing useful</returns>
			public virtual int CollapseAll()
			{
				trunk.CollapseAll();
				RebuildList();
				return 0;
			}

			/// <summary>
			/// Expand the subtree underneath the given model object
			/// </summary>
			/// <param name="model">The model to be expanded.</param> 
			/// <returns>The index of the model in flat list version of the tree</returns>
			/// <remarks>
			/// If the model isn't in the tree,
			/// if it cannot be expanded or if it is already expanded, the command does nothing.
			/// </remarks>
			public virtual int Expand(object model)
			{
				Branch br = GetBranch(model);
				if (br == null || !br.CanExpand || br.IsExpanded)
					return -1;

				// Remember that the branch is expanded, even if it's currently not visible
				br.Expand();
				if (!br.Visible)
				{
					return -1;
				}

				int index = GetObjectIndex(model);
				InsertChildren(br, index + 1);
				return index;
			}

			/// <summary>
			/// Expand all branches in this tree
			/// </summary>
			/// <returns>Return the index of the first branch that was expanded</returns>
			public virtual int ExpandAll()
			{
				trunk.ExpandAll();
				Sort(lastSortColumn, lastSortOrder);
				return 0;
			}

			/// <summary>
			/// Return the Branch object that represents the given model in the tree
			/// </summary>
			/// <param name="model">The model whose branches is to be returned</param>
			/// <returns>The branch that represents the given model, or null if the model
			/// isn't in the tree.</returns>
			public virtual Branch GetBranch(object model)
			{
				if (model == null)
					return null;

				Branch br;
				mapObjectToBranch.TryGetValue(model, out br);
				return br;
			}

			/// <summary>
			/// Return the number of visible descendents that are below the given model.
			/// </summary>
			/// <param name="model">The model whose descendent count is to be returned</param>
			/// <returns>The number of visible descendents. 0 if the model doesn't exist or is collapsed</returns>
			public virtual int GetVisibleDescendentCount(object model)
			{
				Branch br = GetBranch(model);
				return br == null || !br.IsExpanded ? 0 : br.NumberVisibleDescendents;
			}

			/// <summary>
			/// Rebuild the children of the given model, refreshing any cached information held about the given object
			/// </summary>
			/// <param name="model"></param>
			/// <returns>The index of the model in flat list version of the tree</returns>
			public virtual int RebuildChildren(object model)
			{
				Branch br = GetBranch(model);
				if (br == null || !br.Visible)
					return -1;

				int count = br.NumberVisibleDescendents;

				// Remove the visible descendents from after the branch itself
				int index = GetObjectIndex(model);
				if (count > 0)
					objectList.RemoveRange(index + 1, count);

				// Refresh our knowledge of our children (do this even if CanExpand is false, because
				// the branch have already collected some children and that information could be stale)
				br.RefreshChildren();

				// Insert the refreshed children if the branch can expand and is expanded
				if (br.CanExpand && br.IsExpanded)
					InsertChildren(br, index + 1);
				else
					RebuildObjectMap(index);

				return index;
			}

			/// <summary>
			/// Is the given model expanded?
			/// </summary>
			/// <param name="model"></param>
			/// <returns></returns>
			internal bool IsModelExpanded(object model)
			{
				// Special case: model == null is the container for the roots. This is always expanded
				if (model == null)
					return true;
				bool isExpanded;
				mapObjectToExpanded.TryGetValue(model, out isExpanded);
				return isExpanded;
			}

			/// <summary>
			/// Remember whether or not the given model was expanded
			/// </summary>
			/// <param name="model"></param>
			/// <param name="isExpanded"></param>
			internal void SetModelExpanded(object model, bool isExpanded)
			{
				if (model == null) return;

				if (isExpanded)
					mapObjectToExpanded[model] = true;
				else
					mapObjectToExpanded.Remove(model);
			}

			/// <summary>
			/// Insert the children of the given branch into the given position
			/// </summary>
			/// <param name="br">The branch whose children should be inserted</param>
			/// <param name="index">The index where the children should be inserted</param>
			protected virtual void InsertChildren(Branch br, int index)
			{
				// Expand the branch
				br.Expand();
				br.Sort(GetBranchComparer());

				// Insert the branch's visible descendents after the branch itself
				objectList.InsertRange(index, br.Flatten());
				RebuildObjectMap(index);
			}

			/// <summary>
			/// Rebuild our flat internal list of objects.
			/// </summary>
			protected virtual void RebuildList()
			{
				objectList = trunk.Flatten().ToList();
				List<Branch> filtered = trunk.FilteredChildBranches;
				if (filtered.Count > 0)
				{
					filtered[0].IsFirstBranch = true;
					filtered[0].IsOnlyBranch = (filtered.Count == 1);
				}
				RebuildObjectMap(0);
			}

			/// <summary>
			/// Rebuild our reverse index that maps an object to its location
			/// in the filteredObjectList array.
			/// </summary>
			/// <param name="startIndex"></param>
			protected virtual void RebuildObjectMap(int startIndex)
			{
				if (startIndex == 0)
					mapObjectToIndex.Clear();
				for (int i = startIndex; i < objectList.Count; i++)
					mapObjectToIndex[objectList[i]] = i;
			}

			/// <summary>
			/// Create a new branch within this tree
			/// </summary>
			/// <param name="parent"></param>
			/// <param name="model"></param>
			/// <returns></returns>
			internal Branch MakeBranch(Branch parent, object model)
			{
				Branch br = new Branch(parent, this, model);

				// Remember that the given branch is part of this tree.
				mapObjectToBranch[model] = br;
				return br;
			}

			#region IVirtualListDataSource Members

			/// <summary>
			/// 
			/// </summary>
			/// <param name="n"></param>
			/// <returns></returns>
			public virtual object GetNthObject(int n)
			{
				return objectList[n];
			}

			/// <summary>
			/// 
			/// </summary>
			/// <returns></returns>
			public virtual int GetObjectCount()
			{
				return trunk.NumberVisibleDescendents;
			}

			/// <summary>
			/// 
			/// </summary>
			/// <param name="model"></param>
			/// <returns></returns>
			public virtual int GetObjectIndex(object model)
			{
				int index;
				if (model != null && mapObjectToIndex.TryGetValue(model, out index))
					return index;

				return -1;
			}

			/// <summary>
			/// 
			/// </summary>
			/// <param name="first"></param>
			/// <param name="last"></param>
			public virtual void PrepareCache(int first, int last)
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
				return AbstractVirtualListDataSource.DefaultSearchText(value, first, last, column, this);
			}

			/// <summary>
			/// Sort the tree on the given column and in the given order
			/// </summary>
			/// <param name="column"></param>
			/// <param name="order"></param>
			public virtual void Sort(OLVColumn column, SortOrder order)
			{
				lastSortColumn = column;
				lastSortOrder = order;

				// TODO: Need to raise an AboutToSortEvent here

				// Sorting is going to change the order of the branches so clear
				// the "first branch" flag
				foreach (Branch b in trunk.ChildBranches)
					b.IsFirstBranch = false;

				trunk.Sort(GetBranchComparer());
				RebuildList();
			}

			/// <summary>
			/// 
			/// </summary>
			/// <returns></returns>
			protected virtual BranchComparer GetBranchComparer()
			{
				if (lastSortColumn == null)
					return null;

				return new BranchComparer(new ModelObjectComparer(
					lastSortColumn,
					lastSortOrder,
					treeView.SecondarySortColumn ?? treeView.GetColumn(0),
					treeView.SecondarySortColumn == null ? lastSortOrder : treeView.SecondarySortOrder));
			}

			/// <summary>
			/// Add the given collection of objects to the roots of this tree
			/// </summary>
			/// <param name="modelObjects"></param>
			public virtual void AddObjects(ICollection<object> modelObjects)
			{
				var newRoots = EnumerableToArray(treeView.Roots, true);
				foreach (object x in modelObjects)
					newRoots.Add(x);
				SetObjects(newRoots);
			}

			/// <summary>
			/// 
			/// </summary>
			/// <param name="index"></param>
			/// <param name="modelObjects"></param>
			public void InsertObjects(int index, ICollection<object> modelObjects)
			{
				throw new NotImplementedException();
			}

			/// <summary>
			/// Remove all of the given objects from the roots of the tree.
			/// Any objects that is not already in the roots collection is ignored.
			/// </summary>
			/// <param name="modelObjects"></param>
			public virtual void RemoveObjects(ICollection<object> modelObjects)
			{
				var newRoots = new List<object>();
				foreach (object x in treeView.Roots)
					newRoots.Add(x);
				foreach (object x in modelObjects)
				{
					newRoots.Remove(x);
					mapObjectToIndex.Remove(x);
				}
				SetObjects(newRoots);
			}

			/// <summary>
			/// Set the roots of this tree to be the given collection
			/// </summary>
			/// <param name="collection"></param>
			public virtual void SetObjects(IEnumerable<object> collection)
			{
				// We interpret a SetObjects() call as setting the roots of the tree
				treeView.Roots = collection;
			}

			/// <summary>
			/// Update/replace the nth object with the given object
			/// </summary>
			/// <param name="index"></param>
			/// <param name="modelObject"></param>
			public void UpdateObject(int index, object modelObject)
			{
				var newRoots = EnumerableToArray(treeView.Roots, false);
				if (index < newRoots.Count)
					newRoots[index] = modelObject;
				SetObjects(newRoots);
			}

			#endregion

			private OLVColumn lastSortColumn;
			private SortOrder lastSortOrder;
			private readonly Dictionary<object, Branch> mapObjectToBranch = new Dictionary<object, Branch>();
			internal Dictionary<object, bool> mapObjectToExpanded = new Dictionary<object, bool>();
			private readonly Dictionary<object, int> mapObjectToIndex = new Dictionary<object, int>();
			private List<object> objectList = new List<object>();
			private readonly TreeListView treeView;
			private readonly Branch trunk;
		}

		/// <summary>
		/// A Branch represents a sub-tree within a tree
		/// </summary>
		public class Branch
		{
			/// <summary>
			/// Indicators for branches
			/// </summary>
			[Flags]
			public enum BranchFlags
			{
				/// <summary>
				/// FirstBranch of tree
				/// </summary>
				FirstBranch = 1,

				/// <summary>
				/// LastChild of parent
				/// </summary>
				LastChild = 2,

				/// <summary>
				/// OnlyBranch of tree
				/// </summary>
				OnlyBranch = 4
			}

			#region Life and death

			/// <summary>
			/// Create a Branch
			/// </summary>
			/// <param name="parent"></param>
			/// <param name="tree"></param>
			/// <param name="model"></param>
			public Branch(Branch parent, Tree tree, object model)
			{
				ParentBranch = parent;
				Tree = tree;
				Model = model;
			}

			#endregion

			#region Public properties

			/// <summary>
			/// Get the ancestor branches of this branch, with the 'oldest' ancestor first.
			/// </summary>
			public virtual IList<Branch> Ancestors
			{
				get
				{
					List<Branch> ancestors = new List<Branch>();
					if (ParentBranch != null)
						ParentBranch.PushAncestors(ancestors);
					return ancestors;
				}
			}

			private void PushAncestors(IList<Branch> list)
			{
				// This is designed to ignore the trunk (which has no parent)
				if (ParentBranch != null)
				{
					ParentBranch.PushAncestors(list);
					list.Add(this);
				}
			}

			/// <summary>
			/// Can this branch be expanded?
			/// </summary>
			public virtual bool CanExpand
			{
				get
				{
					if (Tree.CanExpandGetter == null || Model == null)
						return false;

					return Tree.CanExpandGetter(Model);
				}
			}

			/// <summary>
			/// Gets or sets our children
			/// </summary>
			public List<Branch> ChildBranches
			{
				get { return childBranches; }
				set { childBranches = value; }
			}
			private List<Branch> childBranches = new List<Branch>();

			/// <summary>
			/// Get/set the model objects that are beneath this branch
			/// </summary>
			public virtual IEnumerable<object> Children
			{
				get
				{
					return ChildBranches.Select(b => b.Model).ToList();
				}
				set
				{
					ChildBranches.Clear();

					TreeListView treeListView = Tree.TreeView;
					CheckState? checkedness = null;
					if (treeListView != null && treeListView.HierarchicalCheckboxes)
						checkedness = treeListView.GetCheckState(Model);
					foreach (object x in value)
					{
						AddChild(x);

						// If the tree view is showing hierarchical checkboxes, then
						// when a child object is first added, it has the same checkedness as this branch
						if (checkedness.HasValue && checkedness.Value == CheckState.Checked)
							treeListView.SetObjectCheckedness(x, checkedness.Value);
					}
				}
			}

			private void AddChild(object childModel)
			{
				Branch br = Tree.GetBranch(childModel);
				if (br == null)
					br = Tree.MakeBranch(this, childModel);
				else
				{
					br.ParentBranch = this;
					br.Model = childModel;
					br.ClearCachedInfo();
				}
				ChildBranches.Add(br);
			}

			/// <summary>
			/// Gets a list of all the branches that survive filtering
			/// </summary>
			public List<Branch> FilteredChildBranches
			{
				get
				{
					if (!IsExpanded)
						return new List<Branch>();

					return ChildBranches;
				}
			}

			/// <summary>
			/// Gets or set whether this branch is expanded
			/// </summary>
			public bool IsExpanded
			{
				get { return Tree.IsModelExpanded(Model); }
				set { Tree.SetModelExpanded(Model, value); }
			}

			/// <summary>
			/// Return true if this branch is the first branch of the entire tree
			/// </summary>
			public virtual bool IsFirstBranch
			{
				get
				{
					return ((flags & Branch.BranchFlags.FirstBranch) != 0);
				}
				set
				{
					if (value)
						flags |= Branch.BranchFlags.FirstBranch;
					else
						flags &= ~Branch.BranchFlags.FirstBranch;
				}
			}

			/// <summary>
			/// Return true if this branch is the last child of its parent
			/// </summary>
			public virtual bool IsLastChild
			{
				get
				{
					return ((flags & Branch.BranchFlags.LastChild) != 0);
				}
				set
				{
					if (value)
						flags |= Branch.BranchFlags.LastChild;
					else
						flags &= ~Branch.BranchFlags.LastChild;
				}
			}

			/// <summary>
			/// Return true if this branch is the only top level branch
			/// </summary>
			public virtual bool IsOnlyBranch
			{
				get
				{
					return ((flags & Branch.BranchFlags.OnlyBranch) != 0);
				}
				set
				{
					if (value)
						flags |= Branch.BranchFlags.OnlyBranch;
					else
						flags &= ~Branch.BranchFlags.OnlyBranch;
				}
			}

			/// <summary>
			/// Gets the depth level of this branch
			/// </summary>
			public int Level
			{
				get
				{
					if (ParentBranch == null)
						return 0;

					return ParentBranch.Level + 1;
				}
			}

			/// <summary>
			/// Gets or sets which model is represented by this branch
			/// </summary>
			public object Model
			{
				get { return model; }
				set { model = value; }
			}
			private object model;

			/// <summary>
			/// Return the number of descendents of this branch that are currently visible
			/// </summary>
			/// <returns></returns>
			public virtual int NumberVisibleDescendents
			{
				get
				{
					if (!IsExpanded)
						return 0;

					List<Branch> filtered = FilteredChildBranches;
					int count = filtered.Count;
					foreach (Branch br in filtered)
						count += br.NumberVisibleDescendents;
					return count;
				}
			}

			/// <summary>
			/// Gets or sets our parent branch
			/// </summary>
			public Branch ParentBranch
			{
				get { return parentBranch; }
				set { parentBranch = value; }
			}
			private Branch parentBranch;

			/// <summary>
			/// Gets or sets our overall tree
			/// </summary>
			public Tree Tree
			{
				get { return tree; }
				set { tree = value; }
			}
			private Tree tree;

			/// <summary>
			/// Is this branch currently visible? A branch is visible
			/// if it has no parent (i.e. it's a root), or its parent
			/// is visible and expanded.
			/// </summary>
			public virtual bool Visible
			{
				get
				{
					if (ParentBranch == null)
						return true;

					return ParentBranch.IsExpanded && ParentBranch.Visible;
				}
			}

			#endregion

			#region Commands

			/// <summary>
			/// Clear any cached information that this branch is holding
			/// </summary>
			public virtual void ClearCachedInfo()
			{
				Children = new List<object>();
				alreadyHasChildren = false;
			}

			/// <summary>
			/// Collapse this branch
			/// </summary>
			public virtual void Collapse()
			{
				IsExpanded = false;
			}

			/// <summary>
			/// Expand this branch
			/// </summary>
			public virtual void Expand()
			{
				if (CanExpand)
				{
					IsExpanded = true;
					FetchChildren();
				}
			}

			/// <summary>
			/// Expand this branch recursively
			/// </summary>
			public virtual void ExpandAll()
			{
				Expand();
				foreach (Branch br in ChildBranches)
				{
					if (br.CanExpand)
						br.ExpandAll();
				}
			}

			/// <summary>
			/// Collapse all branches in this tree
			/// </summary>
			/// <returns>Nothing useful</returns>
			public virtual void CollapseAll()
			{
				Collapse();
				foreach (Branch br in ChildBranches)
				{
					if (br.IsExpanded)
						br.CollapseAll();
				}
			}

			/// <summary>
			/// Fetch the children of this branch.
			/// </summary>
			/// <remarks>This should only be called when CanExpand is true.</remarks>
			public virtual void FetchChildren()
			{
				if (alreadyHasChildren)
					return;

				alreadyHasChildren = true;

				if (Tree.ChildrenGetter == null)
					return;

				Cursor previous = Cursor.Current;
				try
				{
					if (Tree.TreeView.UseWaitCursorWhenExpanding)
						Cursor.Current = Cursors.WaitCursor;
					Children = Tree.ChildrenGetter(Model);
				}
				finally
				{
					Cursor.Current = previous;
				}
			}

			/// <summary>
			/// Collapse the visible descendents of this branch into list of model objects
			/// </summary>
			/// <returns></returns>
			public virtual IList<object> Flatten()
			{
				var flatList = new List<object>();
				if (IsExpanded)
					FlattenOnto(flatList);
				return flatList;
			}

			/// <summary>
			/// Flatten this branch's visible descendents onto the given list.
			/// </summary>
			/// <param name="flatList"></param>
			/// <remarks>The branch itself is <b>not</b> included in the list.</remarks>
			public virtual void FlattenOnto(IList<object> flatList)
			{
				Branch lastBranch = null;
				foreach (Branch br in FilteredChildBranches)
				{
					lastBranch = br;
					br.IsLastChild = false;
					flatList.Add(br.Model);
					if (br.IsExpanded)
					{
						br.FetchChildren(); // make sure we have the branches children
						br.FlattenOnto(flatList);
					}
				}
				if (lastBranch != null)
					lastBranch.IsLastChild = true;
			}

			/// <summary>
			/// Force a refresh of all children recursively
			/// </summary>
			public virtual void RefreshChildren()
			{

				// Forget any previous children. We always do this so that if
				// IsExpanded or CanExpand have changed, we aren't left with stale information.
				ClearCachedInfo();

				if (!IsExpanded || !CanExpand)
					return;

				FetchChildren();
				foreach (Branch br in ChildBranches)
					br.RefreshChildren();
			}

			/// <summary>
			/// Sort the sub-branches and their descendents so they are ordered according
			/// to the given comparer.
			/// </summary>
			/// <param name="comparer">The comparer that orders the branches</param>
			public virtual void Sort(BranchComparer comparer)
			{
				if (ChildBranches.Count == 0)
					return;

				if (comparer != null)
					ChildBranches.Sort(comparer);

				foreach (Branch br in ChildBranches)
					br.Sort(comparer);
			}

			#endregion

			private bool alreadyHasChildren;
			private BranchFlags flags;
		}

		/// <summary>
		/// This class sorts branches according to how their respective model objects are sorted
		/// </summary>
		public class BranchComparer : IComparer<Branch>
		{
			/// <summary>
			/// Create a BranchComparer
			/// </summary>
			/// <param name="actualComparer"></param>
			public BranchComparer(IComparer actualComparer)
			{
				this.actualComparer = actualComparer;
			}

			/// <summary>
			/// Order the two branches
			/// </summary>
			/// <param name="x"></param>
			/// <param name="y"></param>
			/// <returns></returns>
			public int Compare(Branch x, Branch y)
			{
				return actualComparer.Compare(x.Model, y.Model);
			}

			private readonly IComparer actualComparer;
		}
	}
}