using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceProcess;
using System.Threading;

namespace EasyServiceHost
{
	internal class MultipleServiceHostService : ServiceBase
	{
		readonly List<SingleDomainManager> mRunningAppDomains = new List<SingleDomainManager>();

		public MultipleServiceHostService(string installedServiceName)
		{
			ServiceName = installedServiceName;
			CanStop = true;
			CanHandlePowerEvent = false;
			CanPauseAndContinue = false;
			CanShutdown = false;
			AutoLog = true;
			CanHandleSessionChangeEvent = false;
		}


		protected override void OnStart(string[] args)
		{
			HandleStart();
		}

		protected override void OnStop()
		{
			HandleStop();
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(true);
		}


		public void RunAsService()
		{
			//Allows the service control manager to call OnStart, which in turn calls Begin which does our actual startup.
			Run(this);
		}

		public void HandleStart()
		{
			/* This method is running in the service control manager's thread, we need to return quickly. */
			var serviceMainThread = new Thread(FullStartup);
			serviceMainThread.Start();
		}

		public void HandleStop()
		{
			// iterate the managers in all the appdomains and terminate each of them.
			foreach (SingleDomainManager runningAppDomain in mRunningAppDomains)
			{
				//QUESTION: should these all run in sequence or in parallel? What about exception handling?
/*
				runningAppDomain.Stop();
*/
			}
		}

		private void FullStartup()
		{
			//TODO: If this method fails catastrophically, we need to stop the service.
			string[] dirsToStart = Directory.GetDirectories("Services");
			//Spawn a new appdomain for each and call into our initialization class in that other domain to get it running.
			foreach (string path in dirsToStart)
			{
				try
				{
					SingleDomainManager domainManager = StartAppFromSingleDirectory(path);
					mRunningAppDomains.Add(domainManager);
				}
				catch (Exception e)
				{
					// log the failure
					throw;
				}
			}
		}

		private SingleDomainManager StartAppFromSingleDirectory(string path)
		{
			string serviceconfigfile = new DirectoryInfo(path).Name;

			string appEntryFilePath = Path.Combine(path, "app.entry");
			if (File.Exists(Path.Combine(path, serviceconfigfile + ".config")))
			{
				string appEntryFileContents = File.ReadAllText(appEntryFilePath);
				var parts = appEntryFileContents.Split('\t');
				var assemblyFileName = parts[0];
				var assemblyName = Path.GetFileNameWithoutExtension(assemblyFileName);
				var typeName = parts[1];

				var adSetup =
					new AppDomainSetup
					{
						ShadowCopyFiles = "true",
						ApplicationBase = path,
						ConfigurationFile = Path.Combine(path, assemblyFileName + ".config"),
					};
				var newDomain = AppDomain.CreateDomain("Other Domain", null, adSetup);
				var mgr = (SingleDomainManager)newDomain.CreateInstanceFromAndUnwrap(typeof(SingleDomainManager).Assembly.Location, typeof(SingleDomainManager).FullName);
/*
				mgr.Go(assemblyName, typeName);
*/
				Console.WriteLine(typeName + " started from " + path);
				return mgr;
			}
			throw new ArgumentException("No entry point in " + path);
		}
	}
}