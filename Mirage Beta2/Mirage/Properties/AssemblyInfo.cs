using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;

[assembly: AssemblyVersion("0.1.0.0")]
[assembly: IgnoresAccessChecksTo("Assembly-CSharp")]
[assembly: AssemblyCompany("Mirage")]
[assembly: AssemblyConfiguration("Debug")]
[assembly: AssemblyDescription("GPU-based PQS rendering and virtual texturing for KSP")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: AssemblyInformationalVersion("0.1.0+5031f2f9fa0fd40e900766a76386600843261d16")]
[assembly: AssemblyProduct("Mirage")]
[assembly: AssemblyTitle("Mirage")]
[assembly: KSPAssembly("Mirage", 0, 1, 0)]
[assembly: KSPAssemblyDependency("KSPBurst", 1, 5, 5)]
[assembly: KSPAssemblyDependency("KSPTextureLoader", 0, 0)]
[assembly: KSPAssemblyDependency("Kopernicus", 0, 0)]
[assembly: KSPAssemblyDependency("BurstPQS", 0, 0)]
[assembly: KSPAssemblyDependency("Shabby", 0, 0)]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[module: RefSafetyRules(11)]
