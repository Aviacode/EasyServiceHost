using System.Configuration;

namespace EasyServiceHost
{
	/// <summary>
	/// This class reads and holds the config value that identifies the type that 
	/// implements IManagedService which should start and stop this service.
	/// </summary>
	public class ManagedServiceSettings : ConfigurationSection
	{
		private static readonly ManagedServiceSettings sSettings =
			ConfigurationManager.GetSection("ManagedServiceSettings") as ManagedServiceSettings;

		/// <summary>
		/// The current settings values
		/// </summary>
		public static ManagedServiceSettings Settings { get { return sSettings; } }

		/// <summary>
		/// The type name of the bootstrap class that implements IManagedService
		/// </summary>
		[ConfigurationProperty("BootstrapClass", IsRequired = true)]
		public string BootstrapType
		{
			get { return (string)this["BootstrapClass"]; }
			set { this["BootstrapClass"] = value; }
		}
	}
}