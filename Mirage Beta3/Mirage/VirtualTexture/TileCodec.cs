using System;

namespace Mirage.VirtualTexture
{
	/// <summary>Per-tile payload codec, chosen per tile by whoever writes it.</summary>
	// Token: 0x0200003A RID: 58
	public enum TileCodec : byte
	{
		// Token: 0x0400011F RID: 287
		None,
		// Token: 0x04000120 RID: 288
		Lz4,
		// Token: 0x04000121 RID: 289
		Zstd,
		// Token: 0x04000122 RID: 290
		HeightPlaneSplitLz4,
		// Token: 0x04000123 RID: 291
		HeightVDeltaBitpack
	}
}
