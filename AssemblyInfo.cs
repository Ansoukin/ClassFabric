using System.Reflection;
using System.Runtime.Versioning;
// SimpleGitInfoGenerator 生成的 GitInfo 类位于 ClassIsland 命名空间
using ClassIsland;

#if NIX
[assembly: AssemblyVersion("0.0.0.0")]
[assembly: AssemblyInformationalVersion("NIXBUILD+NIXBUILD_LONG_HASH")]
#else
[assembly: AssemblyVersion(GitInfo.Tag)]
[assembly: AssemblyInformationalVersion($"{GitInfo.Tag}+{GitInfo.CommitHash}")]
#endif

[assembly: AssemblyTitle("ClassFabric")]
[assembly: AssemblyProduct("ClassFabric")]
#if NETCOREAPP
// [assembly: SupportedOSPlatform("Windows")]
#endif
 
