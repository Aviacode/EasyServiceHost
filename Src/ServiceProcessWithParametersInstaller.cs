using System.Collections;
using System.ServiceProcess;
using System.Text;

namespace EasyServiceHost
{
	public class ServiceProcessWithParametersInstaller : ServiceProcessInstaller
	{
		public string[] ProcessCommandLineParameters { get; set; }

		protected override void OnBeforeInstall(IDictionary savedState)
		{
			var originalpath = Context.Parameters["assemblypath"];
			if (ProcessCommandLineParameters.Length > 0)
			{
				StringBuilder newAssemblyPath = new StringBuilder("\"" + originalpath + "\" ");
				foreach (string parameter in ProcessCommandLineParameters)
				{
					string paramString = parameter;
					if (parameter.Contains(" ") && !(parameter.StartsWith("\"") || parameter.EndsWith("\"")))
						paramString = "\"" + parameter + "\"";
					newAssemblyPath.AppendFormat("{0} ", paramString);
				}
				Context.Parameters["assemblypath"] = newAssemblyPath.ToString(0, newAssemblyPath.Length - 1);
			}
			base.OnBeforeInstall(savedState);
		}
	}
}