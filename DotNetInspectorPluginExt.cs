using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ReClassNET.Plugins;

namespace DotNetInspectorPlugin
{
	public class DotNetInspectorPluginExt : Plugin
	{
		private IPluginHost host;

		public override Image Icon => Properties.Resources.logo;

		public override bool Initialize(IPluginHost pluginHost)
		{
			System.Diagnostics.Debugger.Launch();

			if (host != null)
			{
				Terminate();
			}

			host = pluginHost ?? throw new ArgumentNullException(nameof(pluginHost));

			var menuItem = host.MainWindow.MainMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(i => i.Text == "Process");
			if (menuItem != null)
			{
				var showInspectorItem = new ToolStripMenuItem
				{
					Text = ".NET Inspector"
				};
				showInspectorItem.Click += (s, e) => new InspectorForm(host.Process).Show();

				menuItem.DropDownItems.Add(showInspectorItem);
			}

			return true;
		}

		public override void Terminate()
		{
			host = null;
		}
	}
}
