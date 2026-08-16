using System;
using Mirage.VirtualTexture;

namespace Mirage.WebIngest
{
	/// <summary>One baked tile's payloads, ready for the web archive. Indexed by <see cref="T:Mirage.VirtualTexture.ArchiveLayer" />;
	/// a null entry means that layer isn't installed for the body and must not be written.</summary>
	// Token: 0x0200002D RID: 45
	public sealed class BakedTile
	{
		// Token: 0x040000CD RID: 205
		public ulong key;

		// Token: 0x040000CE RID: 206
		public int face;

		// Token: 0x040000CF RID: 207
		public int level;

		// Token: 0x040000D0 RID: 208
		public int tx;

		// Token: 0x040000D1 RID: 209
		public int ty;

		// Token: 0x040000D2 RID: 210
		public IngestOutcome outcome;

		/// <summary>Raw (uncompressed) payload per layer, or null. The offline `--test-bake` harness inspects
		/// these directly, so they stay raw; the storage-ready form lives in <see cref="F:Mirage.WebIngest.BakedTile.stored" />.</summary>
		// Token: 0x040000D3 RID: 211
		public byte[][] payload = new byte[3][];

		/// <summary>Per-layer <see cref="T:Mirage.VirtualTexture.ArchiveTextureFormat" /> code, valid where payload is non-null.</summary>
		// Token: 0x040000D4 RID: 212
		public int[] format = new int[3];

		/// <summary>Storage-ready (<c>EncodeForWeb</c>-encoded) payload per layer, or null. Produced on the bake
		/// worker (<c>CubeTileBaker.EncodePayloadsForCommit</c>) alongside <see cref="F:Mirage.WebIngest.BakedTile.codec" />/<see cref="F:Mirage.WebIngest.BakedTile.crc" /> so
		/// the main-thread commit does no compression or checksum — that was <c>CommitBakedTile</c>'s whole cost
		/// (height vdelta-bitpack + CRC over ~200 KB × 3), a per-tile frame spike protecting a regenerable cache.
		/// For BC layers this aliases <see cref="F:Mirage.WebIngest.BakedTile.payload" /> (they store verbatim); only height allocates anew.</summary>
		// Token: 0x040000D5 RID: 213
		public byte[][] stored = new byte[3][];

		/// <summary>Per-layer storage codec chosen by <c>EncodeForWeb</c>, valid where <see cref="F:Mirage.WebIngest.BakedTile.stored" /> is non-null.</summary>
		// Token: 0x040000D6 RID: 214
		public TileCodec[] codec = new TileCodec[3];

		/// <summary>Per-layer CRC32 of <see cref="F:Mirage.WebIngest.BakedTile.stored" />, precomputed on the worker so <c>Append</c> need not.</summary>
		// Token: 0x040000D7 RID: 215
		public uint[] crc = new uint[3];
	}
}
