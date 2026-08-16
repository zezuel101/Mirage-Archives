using System;

namespace Mirage.WebIngest
{
	/// <summary>Thrown on a malformed or out-of-scope JPEG. Callers treat this like a failed download: the tile
	/// is not baked, and the VT indirection falls back to a coarser ancestor.</summary>
	// Token: 0x0200000B RID: 11
	public sealed class JpegDecodeException : Exception
	{
		// Token: 0x0600004D RID: 77 RVA: 0x00002DB8 File Offset: 0x00000FB8
		public JpegDecodeException(string message) : base(message)
		{
		}
	}
}
