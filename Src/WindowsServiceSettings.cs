using System.Configuration;

namespace EasyServiceHost
{
	/// <summary>
	/// This class reads and holds the settings for the windows service that would be installed by this executable
	/// </summary>
	public class WindowsServiceSettings : ConfigurationSection
	{
		private static readonly WindowsServiceSettings sSettings = ConfigurationManager.GetSection("WindowsServiceSettings") as WindowsServiceSettings;

		/// <summary>
		/// The current settings
		/// </summary>
		public static WindowsServiceSettings Settings
		{
			get { return sSettings; }
		}

		/// <summary>
		/// The name of the service (as used to identify it for service control operations)
		/// </summary>
		[ConfigurationProperty("ServiceName", DefaultValue = "EasyServiceHostService", IsRequired = false)]
		public string ServiceName
		{
			get { return (string) this["ServiceName"]; }
			set { this["ServiceName"] = value; }
		}

		/// <summary>
		/// The display name of the service (as shown in the Services administrative tool)
		/// </summary>
		[ConfigurationProperty("DisplayName", DefaultValue = "Easy Service Host Service", IsRequired = false)]
		public string DisplayName
		{
			get { return (string)this["DisplayName"]; }
			set { this["DisplayName"] = value; }
		}

		/// <summary>
		/// The logon account under which to run the service. Defaults to NetworkService. Could also be "LocalSystem".
		/// </summary>
		[ConfigurationProperty("Account", DefaultValue = "NetworkService", IsRequired = false)]
		public string Account
		{
			get { return (string)this["Account"]; }
			set { this["Account"] = value; }
		}

	}
}