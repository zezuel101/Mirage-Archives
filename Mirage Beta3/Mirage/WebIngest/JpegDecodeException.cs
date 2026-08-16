using System;

namespace Mirage.WebIngest
{
	/// <summary>Thrown on a malformed or out-of-scope JPEG. Callers treat this like a failed download: the tile
	/// is not baked, and the VT indirection falls back to a coarser ancestor.</summary>
	// Token: 0x0200001E RID: 30
	public sealed class JpegDecodeException : Exception
	{
		// Token: 0x060000A0 RID: 160 RVA: 0x00005E2E File Offset: 0x0000402E
		public JpegDecodeException(string message) : base(message)
		{
		}
	}
}
