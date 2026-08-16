using System;

namespace Mirage.WebIngest
{
	/// <summary>How a tile fetch ended.</summary>
	// Token: 0x0200002A RID: 42
	public enum TileFetchOutcome
	{
		/// <summary>Real imagery. <see cref="F:Mirage.WebIngest.TileFetchResult.Bytes" /> is a validated JPEG.</summary>
		// Token: 0x040000C3 RID: 195
		Success,
		/// <summary>The provider explicitly has no imagery here — the container disagrees with what the endpoint
		/// serves (see <see cref="M:Mirage.WebIngest.JpegProbe.ContainerMatches(Mirage.WebIngest.TileImageFormat,System.Byte[],System.Int32)" /> and <see cref="P:Mirage.WebIngest.ImageryProvider.Format" />).
		/// NOT an error and never retried: the tile does not exist and never will at this zoom. The baker must
		/// skip it — the VT indirection then falls back to a coarser resident ancestor on its own, which is
		/// exactly the right result and needs no extra machinery.</summary>
		// Token: 0x040000C4 RID: 196
		NoCoverage,
		/// <summary>Network/HTTP/validation failure that survived every retry.</summary>
		// Token: 0x040000C5 RID: 197
		Failed
	}
}
