using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration.Install;

namespace EasyServiceHost
{
	public interface IManagedService
	{
		/// <summary>
		/// Method to start a separate thread that does the work of the service.
		/// This method runs on the service control manager's thread during service startup and needs to return quickly.
		/// </summary>
		void Start(IServiceHost host);

		/// <summary>
		/// Method to stop the service thread and cleanup resources.
		/// This method runs on the service control manager's thread during service stop and needs to return within the time limit specified.
		/// When running as a windows service, if this method throws any exceptions, the service will not be stopped.
		/// </summary>
		/// <param name="timeLimit"></param>
		void Stop(TimeSpan timeLimit);
	}

	public interface INeedEventLogSources
	{
		/// <summary>
		/// Return a list of Application event log source names that should be installed when the service is installed.
		/// </summary>
		string[] GetEventLogSources();
	}

	public interface IManagedServiceWithEventLogSource : INeedEventLogSources, IManagedService
	{
	}

	public interface IHaveCustomInstallers
	{
		IEnumerable<Installer> GetCustomInstallers(Hashtable savedState);
		IEnumerable<Installer> GetCustomUninstallers(Hashtable savedState);
	}
}