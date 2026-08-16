using System;

namespace Mirage.WebIngest
{
	/// <summary>Thrown when a fetched file is not the exact COG flavour this reader supports. Treated by callers
	/// like a failed fetch: the WorldCover mask for that tile is skipped, not faked.</summary>
	// Token: 0x02000031 RID: 49
	public sealed class CogFormatException : Exception
	{
		// Token: 0x0600012F RID: 303 RVA: 0x00009F76 File Offset: 0x00008176
		public CogFormatException(string message) : base(message)
		{
		}
	}
}
