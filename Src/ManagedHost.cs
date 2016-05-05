using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Xml;

namespace EasyServiceHost
{
	/// <summary>
	/// 
	/// </summary>
	public static class ManagedHost
	{
		/// <summary>
		/// Run a single service in the current AppDomain.
		/// Uses the bootstrap type specified in the ManagedServiceSettings configuration section.
		/// </summary>
		/// <param name="arguments"></param>
		public static int RunOne(string[] arguments)
		{
			var args = new ServiceHostCommandLineArgs(arguments);

			if (args.Help)
			{
				//print help
				var configName = Environment.GetCommandLineArgs()[0] + ".config";
				Console.Out.WriteLine(
					@"
Usage: {0} 
[-install [-noservice] [-servicename <service name>] [-servicedisplayname <service display name>] [-account <LocalSystem|LocalService|NetworkService>] ] |
[-uninstall [-servicename <service name>] ] |
[-service]

  -install             Installs this executable as a Windows service, using default values
                       for serviceName, serviceDisplayName and account read from
                       the WindowsServiceSettings in
                       {1}.
                       Command line arguments can be provided for serviceName,
                       serviceDisplayName and account to override config settings.

                       Also installs event log sources needed by the managed service as
                       returned from the IManagedService implementation.
              
                       The information about what was installed is kept in
                       {0}
                       and is read during uninstall to determine what needs to be removed.

                       After a successful installation, it starts the service.

  -noservice           Does not install the Windows service. Only installs the event 
                       log sources.

  -servicename         Provide a service name to enable multi-instance deployments of
                       a single managed service.  This value is used to find or create the
                       "".installed"" file and must be unique and cannot conflict with other
                       installed service names.
                       
  -servicedisplayname  Provide a service display name when doing multi-instance deployments.
                       This display name value must be unique and cannot conflict with other 
                       installed service display names.

  -account             Set the service account to LocalSystem, LocalService or
                       NetworkService.  When provided, it overrides the config value for
                       WindowsServiceSettings.Account.  The default value is NetworkService.

  -uninstall           Uninstalls the service and/or the event log sources based on 
                       whatever was last installed.  When provided it uses the command line
                       value for serviceName to determine which service to uninstall.  If
                       no command line arguments are provided it defaults to using the
                       WindowsServiceSettings.ServiceName config value.

  -service             Runs the executable as a service using the values.

  -[optional]          When any other arguments are provided, they will be passed through to
                       the ManagedServiceSettings.BootstrapClass listed in
                       {1}.",
					args.InstallFile, configName);
				return 0;
			}

			var bootstrapType = Type.GetType(ManagedServiceSettings.Settings.BootstrapType, false);
			if (bootstrapType == null)
				throw new InvalidOperationException("Type not found: " + ManagedServiceSettings.Settings.BootstrapType);

			IManagedService instance = Activator.CreateInstance(bootstrapType) as IManagedService;
			if (instance == null)
				throw new InvalidOperationException(bootstrapType + " is not IManagedService");


			if (args.InstallService)
			{
				// we are trying to automatically detect if we need to elevate and then do so if necessary
				WindowsIdentity user = WindowsIdentity.GetCurrent();
				var princ = new WindowsPrincipal(user);
				var isAdmin = princ.IsInRole(WindowsBuiltInRole.Administrator);
				if (!isAdmin)
				{
					return Elevate(arguments);
				}

				//Now install the service and start it up
				return HandleSingleInstall(instance, !args.NoService, args.ServiceName, args.InstallFile, 
					args.ServiceDisplayName, args.AccountName, args.ArgsToPassThrough);
			}

			if (args.UninstallService)
			{
				// we are trying to automatically detect if we need to elevate and then do so if necessary
				WindowsIdentity user = WindowsIdentity.GetCurrent();
				var princ = new WindowsPrincipal(user);
				var isAdmin = princ.IsInRole(WindowsBuiltInRole.Administrator);
				if (!isAdmin)
				{
					return Elevate(arguments);
				}
				return HandleSingleUninstall(instance, args.InstallFile);
			}

			//TODO: support start, stop, etc.

			if (args.RunAsService)
			{
				return HandleRunSingleAsService(instance, args.ServiceName, args.ServiceDisplayName, args.ArgsToPassThrough);
			}

			//If none of the other options are specified, run as a console app
			return HandleRunSingleAsConsole(instance, args.ServiceName, args.ServiceDisplayName, arguments);
		}

		private static int Elevate(string [] args)
		{
			var currentProcess = Process.GetCurrentProcess();
			var psi =
				new ProcessStartInfo
					{
						FileName = currentProcess.MainModule.FileName,
						WorkingDirectory = Environment.CurrentDirectory,
						Verb = "runas",
						Arguments = ArgvToCommandLine(args),
					};
			try
			{
				var process = Process.Start(psi);
				process.WaitForExit();
				return process.ExitCode;
			}
			catch (Exception e)
			{
				Console.Error.WriteLine(e.ToString());
				return 1;
			}
		}

		private static string ArgvToCommandLine(IEnumerable<string> args)
		{
			StringBuilder sb = new StringBuilder();
			foreach (string s in args)
			{
				sb.Append('"');
				// Escape double quotes (") and backslashes (\).
				int searchIndex = 0;
				while (true)
				{
					// Put this test first to support zero length strings.
					if (searchIndex >= s.Length)
					{
						break;
					}
					int quoteIndex = s.IndexOf('"', searchIndex);
					if (quoteIndex < 0)
					{
						break;
					}
					sb.Append(s, searchIndex, quoteIndex - searchIndex);
					EscapeBackslashes(sb, s, quoteIndex - 1);
					sb.Append('\\');
					sb.Append('"');
					searchIndex = quoteIndex + 1;
				}
				sb.Append(s, searchIndex, s.Length - searchIndex);
				EscapeBackslashes(sb, s, s.Length - 1);
				sb.Append(@""" ");
			}
			return sb.ToString(0, Math.Max(0, sb.Length - 1));
		}

		private static void EscapeBackslashes(StringBuilder sb, string s, int lastSearchIndex)
		{
			// Backslashes must be escaped if and only if they precede a double quote.
			for (int i = lastSearchIndex; i >= 0; i--)
			{
				if (s[i] != '\\')
				{
					break;
				}
				sb.Append('\\');
			}
		}

		private static int HandleRunSingleAsService(IManagedService instance, string serviceName, string serviceDisplayName, string[] args)
		{
			var serviceInstance = new SingleServiceHostService(serviceName, instance, serviceDisplayName, args);
			serviceInstance.RunAsService();
			return 0;
		}

		private static int HandleRunSingleAsConsole(IManagedService instance, string serviceName, string displayName, string[] args)
		{
			instance.Start(new ConsoleHost(serviceName, displayName, args));

			try
			{
				Console.TreatControlCAsInput = true;
				Console.Title = instance.GetType().FullName + " started: " + "Press Ctrl+C to end.";
				while (true)
				{
					var key = Console.ReadKey(true);
					if (key.Modifiers == ConsoleModifiers.Control && key.Key == ConsoleKey.C)
						break;
				}
			}
			catch (IOException)
			{
				// this will fire when the service is invoked without opening a console window				
				Console.WriteLine("Press enter to exit.");
				var key = Console.ReadLine();
			}

			if (instance != null)
				instance.Stop(TimeSpan.FromSeconds(30));

			return 0;
		}

		private static int HandleSingleInstall(IManagedService instance, bool installService, string serviceName, string installedFile, string serviceDisplayName, string accountName, string[] args)
		{
			string path = Assembly.GetEntryAssembly().Location;

			// look for an "installstate" file. If it's there, we don't want to install again.
			if (File.Exists(installedFile))
			{
				Console.Error.WriteLine("This is already installed.");
				return 1;
			}

			var savedState = new Hashtable();

			var rootInstaller = new Installer();
			rootInstaller.Context = new InstallContext(null, null);
			rootInstaller.Context.Parameters["LogToConsole"] = "true";

			// prepare the service installer
			ServiceInstaller serviceInstaller = null;
			if (installService)
			{
				savedState["ServiceName"] = serviceName;
				Installer serviceProcessInstaller = GetConfiguredServiceInstaller(serviceName, serviceDisplayName, accountName, out serviceInstaller, args);
				rootInstaller.Context.Parameters["assemblypath"] = path;
				rootInstaller.Installers.Add(serviceProcessInstaller);
			}

			// prepare the event log source installers
			if (instance is INeedEventLogSources)
			{
				var sourceNames = ((INeedEventLogSources)instance).GetEventLogSources();
				// if we're installing a service, the service installer already makes an application event log source for the service name. We don't want to duplicate that.
				string[] eventlogsources;
				if (installService)
				{
					eventlogsources = sourceNames.Except(Enumerable.Repeat(serviceName, 1)).ToArray();
				}
				else
				{
					eventlogsources = sourceNames;
				}
				savedState["EventLogSources"] = eventlogsources;
				foreach (var sourceName in eventlogsources)
				{
					rootInstaller.Installers.Add(
						new EventLogInstaller
							{
								Source = sourceName,
								Log = "Application",
								UninstallAction = UninstallAction.Remove,
							});
				}
			}

			if (instance is IHaveCustomInstallers)
			{
				var installers = ((IHaveCustomInstallers)instance).GetCustomInstallers(savedState);
				foreach (var installer in installers)
				{
					rootInstaller.Installers.Add(installer);
				}
			}

			try
			{
				rootInstaller.Install(savedState);
				rootInstaller.Commit(savedState);

				//now record our install state in a file
				FileStream fileStream = new FileStream(installedFile, FileMode.Create);
				XmlWriter xmlWriter = XmlWriter.Create(
					fileStream,
					new XmlWriterSettings
						{
							Encoding = Encoding.UTF8,
							CheckCharacters = false,
							CloseOutput = false
						}
					);
				try
				{
					var netDataContractSerializer = new NetDataContractSerializer();
					netDataContractSerializer.WriteObject(xmlWriter, savedState);
				}
				finally
				{
					xmlWriter.Close();
					fileStream.Close();
				}
			}
			catch (Exception e)
			{
				Console.Error.WriteLine(e);
				try
				{
					rootInstaller.Rollback(savedState);
				}
				catch (Exception exception)
				{
					Console.Error.WriteLine(exception);
				}
				return 1;
			}

			try
			{
				if (serviceInstaller != null)
				{
					// start the service that was just installed
					using (var sc = new ServiceController(serviceInstaller.ServiceName))
					{
						Console.Out.WriteLine("Attempting to start the service: " + serviceInstaller.ServiceName + "...");
						sc.Start();
						Console.Out.WriteLine("Service started.");
					}
				}
			}
			catch (Exception e)
			{
				Console.Error.WriteLine(e);
				HandleSingleUninstall(instance, installedFile);
				return 1;
			}

			return 0;
		}

		private static int HandleSingleUninstall(IManagedService instance, string installedFile)
		{
			// read our saved state
			if (!File.Exists(installedFile))
			{
				Console.Error.WriteLine("Not already installed.");
				return 1;
			}
			Hashtable savedState;
			using (var fileStream = new FileStream(installedFile, FileMode.Open, FileAccess.Read))
			{
				using (XmlReader xmlReader = XmlReader.Create(fileStream,
																							 new XmlReaderSettings { CheckCharacters = false, CloseInput = false }))
				{
					var netDataContractSerializer = new NetDataContractSerializer();
					savedState = (Hashtable)netDataContractSerializer.ReadObject(xmlReader);
				}
			}

			var rootInstaller = new Installer();
			rootInstaller.Context = new InstallContext(null, null);

			//the service installer
			if (savedState.ContainsKey("ServiceName"))
			{
				rootInstaller.Installers.Add(
					new ServiceProcessInstaller
						{
							Installers = { new ServiceInstaller { ServiceName = (string)savedState["ServiceName"] } }
						});
			}
			//event log sources
			if (savedState.ContainsKey("EventLogSources"))
			{
				string[] sourceNames = (string[])savedState["EventLogSources"];
				foreach (var sourceName in sourceNames)
				{
					rootInstaller.Installers.Add(
						new EventLogInstaller
							{
								Source = sourceName,
								Log = "Application",
								UninstallAction = UninstallAction.Remove,
							}
						);
				}
			}

			if (instance is IHaveCustomInstallers)
			{
				var installers = ((IHaveCustomInstallers)instance).GetCustomUninstallers(savedState);
				foreach (var installer in installers)
				{
					rootInstaller.Installers.Add(installer);
				}
			}

			rootInstaller.Uninstall(savedState);
			File.Delete(installedFile);

			return 0;
		}

		private static Installer GetConfiguredServiceInstaller(string serviceName, string displayName, string accountName, out ServiceInstaller serviceInstaller, string[] args)
		{
			ServiceAccount account = ServiceAccount.NetworkService;
			if (!string.IsNullOrEmpty(accountName))
			{
				switch (accountName)
				{
					case "LocalSystem":
						account = ServiceAccount.LocalSystem;
						break;
					case "LocalService":
						account = ServiceAccount.LocalService;
						break;
					case "NetworkService":
						account = ServiceAccount.NetworkService;
						break;
					//TODO: support a named user account and password (passed in from the command-line)
					default:
						throw new NotSupportedException("Must be \"LocalSystem\", \"LocalService\", or \"NetworkService\"");
				}
			}
			serviceInstaller =
				new ServiceInstaller
					{

						ServiceName = serviceName,
						DisplayName = displayName,
						Description = "",
						//TODO: get this info from configuration
						ServicesDependedOn = new string[0],
						//TODO: get this info from the config file
						StartType = ServiceStartMode.Automatic,
#if DOTNET35
#else
						DelayedAutoStart = true,
#endif
					};


			var arguments = args.ToList();
			arguments.Insert(0, "/service");

			var serviceProcessInstaller =
				new ServiceProcessWithParametersInstaller
					{
						ProcessCommandLineParameters = arguments.ToArray(),
						Account = account,
						Installers =
							{
								serviceInstaller,
							}
					};

			return serviceProcessInstaller;
		}
	}

	internal class ServiceHostCommandLineArgs
	{
		public string ServiceName { get; private set; }
		public string InstallFile { get; private set; }
		public string ServiceDisplayName { get; private set; }
		public string AccountName { get; private set; }
		public bool InstallService { get; private set; }
		public bool UninstallService { get; private set; }
		public bool NoService { get; private set; }
		public bool RunAsService { get; private set; }
		public bool Help { get; private set; }
		public string[] ArgsToPassThrough { get; private set; }

		public ServiceHostCommandLineArgs(string[] args)
		{
			var argsToPass = new List<string>();
			for (var i = 0; i < args.Length; i++)
			{
				var arg = args[i];
				switch (arg.ToLower())
				{
					case "/help":
					case "-help":
						Help = true;
						break;
					case "/install":
					case "-install":
						InstallService = true;
						break;
					case "/noservice":
					case "-noservice":
						NoService = true;
						break;
					case "/uninstall":
					case "-uninstall":
						UninstallService = true;
						break;
					case "/service":
					case "-service":
						RunAsService = true;
						break;
					case "/servicename":
					case "-servicename":
						ServiceName = args[i + 1];
						//If the servicename is supplied from the command line, we need to name the installed file the same
						var filePath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
						InstallFile = Path.Combine(filePath, ServiceName + ".installed");
						i += 1; //Advance it one more to get past the value
						break;
					case "/servicedisplayname":
					case "-servicedisplayname":
						ServiceDisplayName = args[i + 1];
						i += 1; //Advance it one more to get past the value
						break;
					case "/account":
					case "-account":
						AccountName = args[i + 1];
						i += 1; //Advance it one more to get past the value
						break;
					default:
						argsToPass.Add(arg);
						break;
				}
			}

			//If we don't have values for serviceName, serviceDisplayName or account we need to use the default values
			if (string.IsNullOrEmpty(ServiceName))
			{
				ServiceName = WindowsServiceSettings.Settings.ServiceName;
				//If the servicename is defaulted to the config value, the installed name is based on the executable.
				InstallFile = Assembly.GetEntryAssembly().Location + ".installed";
			}
			if (string.IsNullOrEmpty(ServiceDisplayName))
			{
				ServiceDisplayName = WindowsServiceSettings.Settings.DisplayName;
			}
			if (string.IsNullOrEmpty(AccountName))
			{
				AccountName = WindowsServiceSettings.Settings.Account;
			}

			//If we have any extra arguments like -tenant, add them to this list so they can be passed through to the bootstrap class
			ArgsToPassThrough = argsToPass.Any() ? argsToPass.ToArray() : new string[0];
		}
	}

	internal class ConsoleHost : IServiceHost
	{
		public ConsoleHost(string serviceName, string displayName, string[] args)
		{
			ServiceName = serviceName;
			ServiceDisplayName = displayName;
			Arguments = args;
		}

		public void Stop()
		{
			Environment.Exit(0);
		}

		public string ServiceName { get; private set; }
		public string ServiceDisplayName { get; private set; }

		public bool IsService
		{
			get { return false; }
		}

		public bool HasConsole
		{
			get { return true; }
		}

		public string[] Arguments { get; private set; }
	}
}