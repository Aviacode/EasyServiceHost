# Easy Service Host
Easy Service Host is a library that makes it easy to develop, install, and run your .NET executable as a windows service. It also makes debugging easy by allowing you to run the same executable as a normal command-line program whether attached to your debugger or not.

## License
MIT License

## Usage 

 - Create a console exectuable project
 - Install the EasyServiceHost NuGet package
 - Create a class that implements `IManagedService`
 - Update the properties in the `WindowsServiceSettings` and `ManagedServiceSettings` sections in app.config
 - Make your `Main` entrypoint method as below. Your startup code will go into your Bootstrap Class.

```C#
using EasyServiceHost;
namespace ManualTester
{
	static class Program
	{
		static int Main(string[] args)
		{
			return ManagedHost.RunOne(args);
		}
	}
}
```
