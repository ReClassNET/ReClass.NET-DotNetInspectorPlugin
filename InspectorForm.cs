using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BrightIdeasSoftware;
using Microsoft.Diagnostics.Runtime;
using ReClassNET.Memory;

namespace DotNetInspectorPlugin
{
	public partial class InspectorForm : Form
	{
		public InspectorForm(RemoteProcess process)
		{
			InitializeComponent();

			var renderer = treeListView.TreeColumnRenderer;
			renderer.IsShowLines = false;
			renderer.UseTriangles = true;
			treeListView.CellEditUseWholeCell = true;

			treeListView.CanExpandGetter = obj => ((DotNetObject)obj).Children.Any();
			treeListView.ChildrenGetter = obj => ((DotNetObject)obj).Children;
			treeListView.CellEditStarting += delegate (object sender, CellEditEventArgs e)
			{
				e.Cancel = ((DotNetObject)e.RowObject).IsValueType == false;
			};

			olvColumnValue.AspectGetter = obj => ((DotNetObject)obj).GetFormattedValue(false);

			olvColumnValue.AspectPutter = (obj, value) =>
			{
				((DotNetObject)obj).SetValue(value);
			};

			var dataTarget = DataTarget.CreateFromReader(new ReClassNetDataReader(process));
			var info = dataTarget.ClrVersions.FirstOrDefault();
			if (info == null)
			{
				return;
			}

			var runtime = info.CreateRuntime();
			if (runtime == null)
			{
				return;
			}

			var heap = runtime.GetHeap();
			if (heap == null)
			{
				return;
			}

			var collector = new DotNetObjectCollector(heap);
			var objects = collector.EnumerateObjects();

			treeListView.Roots = objects;
		}
	}
}
