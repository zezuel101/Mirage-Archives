using System;

namespace Mirage.VirtualTexture
{
	/// <summary>Per-layer tile source for one payload atlas: color, height, normal, or
	/// emissive.</summary>
	// Token: 0x0200004A RID: 74
	public interface ITileLayerSource : IDisposable
	{
		// Token: 0x17000053 RID: 83
		// (get) Token: 0x060001D2 RID: 466
		bool Linear { get; }

		/// <summary>O(1) residency check — callers rely on that guarantee.</summary>
		// Token: 0x060001D3 RID: 467
		bool Exists(int face, int level, int tx, int ty);

		/// <summary>Begin an async load. Never blocks; missing tiles return an already-faulted handle.</summary>
		// Token: 0x060001D4 RID: 468
		TileReadHandle BeginLoad(int face, int level, int tx, int ty);
	}
}
