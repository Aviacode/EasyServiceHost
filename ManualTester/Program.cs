using System;
using EasyServiceHost;

namespace ManualTester
{
	class Program
	{
		static int Main(string[] args)
		{
			Console.Out.WriteLine("Manual Tester Arguments: " + string.Join(", ", args));
			return ManagedHost.RunOne(args);
		}
	}
}
