using System;
using System.Drawing;
using ReClassNET;
using ReClassNET.Plugins;

namespace DotNetInspectorPlugin
{
	public class DotNetInspectorPluginExt : Plugin
	{
		private IPluginHost host;

		public override Image Icon => Properties.Resources.logo;

		public override bool Initialize(IPluginHost pluginHost)
		{
			//System.Diagnostics.Debugger.Launch();

			if (host != null)
			{
				Terminate();
			}

			if (pluginHost == null)
			{
				throw new ArgumentNullException(nameof(pluginHost));
			}

			host = pluginHost;

			return true;
		}

		public override void Terminate()
		{
			host = null;
		}
	}
}
