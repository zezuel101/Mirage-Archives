using System;

namespace Mirage.WebIngest
{
	/// <summary>Thrown when a fetched file is not the exact COG flavour this reader supports. Treated by callers
	/// like a failed fetch: the WorldCover mask for that tile is skipped, not faked.</summary>
	// Token: 0x02000010 RID: 16
	public sealed class CogFormatException : Exception
	{
		// Token: 0x0600006F RID: 111 RVA: 0x00004A69 File Offset: 0x00002C69
		public CogFormatException(string message) : base(message)
		{
		}
	}
}
