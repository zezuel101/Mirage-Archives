using System;

namespace Mirage.VirtualTexture
{
	/// <summary>Per-tile payload codec. v1 writes only <see cref="F:Mirage.VirtualTexture.TileCodec.None" />; the other
	/// values are reserved so adding compression is an index-compatible change (no
	/// format bump).</summary>
	// Token: 0x02000042 RID: 66
	public enum TileCodec : byte
	{
		// Token: 0x04000160 RID: 352
		None,
		// Token: 0x04000161 RID: 353
		Lz4,
		// Token: 0x04000162 RID: 354
		Zstd,
		// Token: 0x04000163 RID: 355
		HeightPlaneSplitLz4,
		// Token: 0x04000164 RID: 356
		HeightVDeltaBitpack
	}
}
