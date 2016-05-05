namespace EasyServiceHost
{
	public interface IServiceHost
	{
		void Stop();

		string ServiceName { get; }

		string ServiceDisplayName { get; }

		bool IsService { get; }

		bool HasConsole { get; }

		string[] Arguments { get; }
	}
}