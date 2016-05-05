using System;
using System.ServiceProcess;

namespace EasyServiceHost
{
	internal class SingleServiceHostService : ServiceBase, IServiceHost
	{
		private readonly IManagedService mInstance;

		public SingleServiceHostService(string installedServiceName, IManagedService instance, string displayName, string[] args)
		{
			mInstance = instance;
			ServiceName = installedServiceName;
			CanStop = true;
			CanHandlePowerEvent = false;
			CanPauseAndContinue = false;
			CanShutdown = false;
			AutoLog = true;
			CanHandleSessionChangeEvent = false;
			ServiceDisplayName = displayName;
			Arguments = args;
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
			//Allows the service control manager to call OnStart, which in turn calls HandleStart which asks our configured bootstrapper to startup.
			Run(this);
		}

		private void HandleStart()
		{
			/* This method is running in the service control manager's thread, we need to return quickly. */
			mInstance.Start(this);
		}

		private void HandleStop()
		{
			mInstance.Stop(TimeSpan.FromSeconds(4));
		}

		public string ServiceDisplayName { get; private set; }

		public bool IsService
		{
			get { return true; }
		}

		public bool HasConsole
		{
			get { return false; }
		}

		public string[] Arguments { get; private set; }
	}
}