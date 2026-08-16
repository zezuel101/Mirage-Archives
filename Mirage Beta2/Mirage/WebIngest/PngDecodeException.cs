using System;

namespace Mirage.WebIngest
{
	/// <summary>Thrown on a malformed or out-of-scope PNG. Treated like a failed fetch: the tile is not baked.</summary>
	// Token: 0x02000024 RID: 36
	public sealed class PngDecodeException : Exception
	{
		// Token: 0x060000E1 RID: 225 RVA: 0x00008720 File Offset: 0x00006920
		public PngDecodeException(string message) : base(message)
		{
		}
	}
}
