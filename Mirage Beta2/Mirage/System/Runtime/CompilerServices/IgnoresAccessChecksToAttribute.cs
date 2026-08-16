using System;

namespace System.Runtime.CompilerServices
{
	// Token: 0x02000076 RID: 118
	[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
	internal sealed class IgnoresAccessChecksToAttribute : Attribute
	{
		// Token: 0x06000421 RID: 1057 RVA: 0x0001BB05 File Offset: 0x00019D05
		internal IgnoresAccessChecksToAttribute(string assemblyName)
		{
		}
	}
}
