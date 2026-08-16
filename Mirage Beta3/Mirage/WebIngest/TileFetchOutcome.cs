using System;

namespace Mirage.WebIngest
{
	/// <summary>How a tile fetch ended.</summary>
	// Token: 0x02000016 RID: 22
	public enum TileFetchOutcome
	{
		/// <summary>Real imagery. <see cref="F:Mirage.WebIngest.TileFetchResult.Bytes" /> is a validated JPEG.</summary>
		// Token: 0x04000072 RID: 114
		Success,
		/// <summary>The provider explicitly has no imagery here — the container disagrees with what the endpoint
		/// serves (see <see cref="M:Mirage.WebIngest.JpegProbe.ContainerMatches(Mirage.WebIngest.TileImageFormat,System.Byte[],System.Int32)" /> and <see cref="P:Mirage.WebIngest.ImageryProvider.Format" />).
		/// NOT an error and never retried: the tile does not exist and never will at this zoom. The baker must
		/// skip it — the VT indirection then falls back to a coarser resident ancestor on its own, which is
		/// exactly the right result and needs no extra machinery.</summary>
		// Token: 0x04000073 RID: 115
		NoCoverage,
		/// <summary>Network/HTTP/validation failure that survived every retry.</summary>
		// Token: 0x04000074 RID: 116
		Failed
	}
}
