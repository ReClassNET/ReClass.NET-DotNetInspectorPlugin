namespace DotNetInspectorPlugin
{
	partial class InspectorForm
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			this.treeListView = new BrightIdeasSoftware.TreeListView();
			this.olvColumnName = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
			this.olvColumnValue = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
			this.olvColumnType = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
			((System.ComponentModel.ISupportInitialize)(this.treeListView)).BeginInit();
			this.SuspendLayout();
			// 
			// treeListView
			// 
			this.treeListView.AllColumns.Add(this.olvColumnName);
			this.treeListView.AllColumns.Add(this.olvColumnValue);
			this.treeListView.AllColumns.Add(this.olvColumnType);
			this.treeListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.treeListView.CellEditActivation = BrightIdeasSoftware.ObjectListView.CellEditActivateMode.DoubleClick;
			this.treeListView.CellEditUseWholeCell = false;
			this.treeListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.olvColumnName,
            this.olvColumnValue,
            this.olvColumnType});
			this.treeListView.Cursor = System.Windows.Forms.Cursors.Default;
			this.treeListView.FullRowSelect = true;
			this.treeListView.HideSelection = false;
			this.treeListView.Location = new System.Drawing.Point(12, 12);
			this.treeListView.MultiSelect = false;
			this.treeListView.Name = "treeListView";
			this.treeListView.ShowGroups = false;
			this.treeListView.ShowImagesOnSubItems = true;
			this.treeListView.ShowItemToolTips = true;
			this.treeListView.Size = new System.Drawing.Size(654, 243);
			this.treeListView.TabIndex = 30;
			this.treeListView.UseCompatibleStateImageBehavior = false;
			this.treeListView.View = System.Windows.Forms.View.Details;
			this.treeListView.VirtualMode = true;
			// 
			// olvColumnName
			// 
			this.olvColumnName.AspectName = "Name";
			this.olvColumnName.IsEditable = false;
			this.olvColumnName.IsTileViewColumn = true;
			this.olvColumnName.Text = "Name";
			this.olvColumnName.UseInitialLetterForGroup = true;
			this.olvColumnName.Width = 180;
			this.olvColumnName.WordWrap = true;
			// 
			// olvColumnValue
			// 
			this.olvColumnValue.AspectName = "";
			this.olvColumnValue.CellEditUseWholeCell = true;
			this.olvColumnValue.FillsFreeSpace = true;
			this.olvColumnValue.Text = "Value";
			this.olvColumnValue.Width = 131;
			// 
			// olvColumnType
			// 
			this.olvColumnType.AspectName = "Type";
			this.olvColumnType.IsEditable = false;
			this.olvColumnType.IsTileViewColumn = true;
			this.olvColumnType.Text = "Type";
			this.olvColumnType.Width = 145;
			// 
			// InspectorForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(895, 586);
			this.Controls.Add(this.treeListView);
			this.Name = "InspectorForm";
			this.Text = "InspectorForm";
			((System.ComponentModel.ISupportInitialize)(this.treeListView)).EndInit();
			this.ResumeLayout(false);

		}

		#endregion

		private BrightIdeasSoftware.TreeListView treeListView;
		private BrightIdeasSoftware.OLVColumn olvColumnName;
		private BrightIdeasSoftware.OLVColumn olvColumnValue;
		private BrightIdeasSoftware.OLVColumn olvColumnType;
	}
}