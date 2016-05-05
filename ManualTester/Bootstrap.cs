using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Messaging;
using EasyServiceHost;

namespace ManualTester
{
	public class Bootstrap : IManagedService, IHaveCustomInstallers
	{
		/// <summary>
		/// Method to start a separate thread that does the work of the service.
		/// This method runs on the service control manager's thread during service startup and needs to return quickly.
		/// </summary>
		public void Start(IServiceHost host)
		{
			Console.Out.WriteLine(string.Join(" ", host.Arguments));
		}

		/// <summary>
		/// Method to stop the service thread and cleanup resources.
		/// This method runs on the service control manager's thread during service stop and needs to return within the time limit specified.
		/// </summary>
		/// <param name="timeLimit"></param>
		public void Stop(TimeSpan timeLimit)
		{
			
		}

		public IEnumerable<Installer> GetCustomInstallers(Hashtable savedState)
		{
			var mqinstaller =
				new MessageQueueInstaller
					{
						Authenticate = true,
						Transactional = true,
						Path = Environment.MachineName + "\\private$\\testqueue",
						UninstallAction = UninstallAction.NoAction
					};
			mqinstaller.Permissions =
				new AccessControlList
					{
						new MessageQueueAccessControlEntry(new Trustee("Administrators"), MessageQueueAccessRights.FullControl),
						new MessageQueueAccessControlEntry(new Trustee(Environment.MachineName + "$"),
						                                   MessageQueueAccessRights.ReceiveMessage | MessageQueueAccessRights.WriteMessage),
						new MessageQueueAccessControlEntry(new Trustee("Everyone"), MessageQueueAccessRights.WriteMessage)
					};
			yield return mqinstaller;

			var mqInstaller2 =
				new MessageQueueInstaller
					{
						Authenticate = true,
						Transactional = true,
						Path = Environment.MachineName + "\\private$\\testqueue.2",
						UninstallAction = UninstallAction.NoAction
					};
			mqInstaller2.Permissions = mqinstaller.Permissions;
			yield return mqInstaller2;
		}

		public IEnumerable<Installer> GetCustomUninstallers(Hashtable savedState)
		{
			var mqinstaller =
				new MessageQueueInstaller
				{
					Path = Environment.MachineName + "\\private$\\testqueue",
					UninstallAction = UninstallAction.Remove,
				};
			yield return mqinstaller;
			var mqInstaller2 =
				new MessageQueueInstaller
				{
					Path = Environment.MachineName + "\\private$\\testqueue.2",
					UninstallAction = UninstallAction.Remove,
				};
			yield return mqInstaller2;
		}
	}

}