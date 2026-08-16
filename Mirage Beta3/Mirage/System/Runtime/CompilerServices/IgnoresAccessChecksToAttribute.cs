using System;

namespace System.Runtime.CompilerServices
{
	// Token: 0x02000089 RID: 137
	[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
	internal sealed class IgnoresAccessChecksToAttribute : Attribute
	{
		// Token: 0x06000404 RID: 1028 RVA: 0x0001C8D6 File Offset: 0x0001AAD6
		internal IgnoresAccessChecksToAttribute(string assemblyName)
		{
		}
	}
}
