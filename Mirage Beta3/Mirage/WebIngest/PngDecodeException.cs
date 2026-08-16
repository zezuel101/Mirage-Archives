using System;

namespace Mirage.WebIngest
{
	/// <summary>Thrown on a malformed or out-of-scope PNG. Treated like a failed fetch: the tile is not baked.</summary>
	// Token: 0x02000020 RID: 32
	public sealed class PngDecodeException : Exception
	{
		// Token: 0x060000B0 RID: 176 RVA: 0x00006F1E File Offset: 0x0000511E
		public PngDecodeException(string message) : base(message)
		{
		}
	}
}
