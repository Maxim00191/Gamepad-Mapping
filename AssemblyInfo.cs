using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Windows;

// Single assembly metadata source (GenerateAssemblyInfo=false) avoids duplicate attributes in the WPF wpftmp project.
[assembly: AssemblyCompany("Gamepad Mapping")]
[assembly: AssemblyProduct("Gamepad Mapping")]
[assembly: AssemblyTitle("Gamepad Mapping")]
[assembly: AssemblyVersion("2.3.0.0")]
[assembly: AssemblyFileVersion("2.3.0.0")]
[assembly: AssemblyInformationalVersion("2.3.0-beta")]
#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif
[assembly: TargetPlatform("Windows10.0.19041.0")]
[assembly: SupportedOSPlatform("Windows10.0.17763.0")]
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v9.0", FrameworkDisplayName = ".NET 9.0")]

[assembly: InternalsVisibleTo("GamepadMapping.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,
    ResourceDictionaryLocation.SourceAssembly)]
