using System;
using Mirage.VirtualTexture;

namespace Mirage.WebIngest
{
	/// <summary>One baked tile's payloads, ready for the web archive. Indexed by <see cref="T:Mirage.VirtualTexture.ArchiveLayer" />;
	/// a null entry means that layer isn't installed for the body and must not be written.</summary>
	// Token: 0x02000026 RID: 38
	public sealed class BakedTile
	{
		// Token: 0x040000A0 RID: 160
		public ulong key;

		// Token: 0x040000A1 RID: 161
		public int face;

		// Token: 0x040000A2 RID: 162
		public int level;

		// Token: 0x040000A3 RID: 163
		public int tx;

		// Token: 0x040000A4 RID: 164
		public int ty;

		// Token: 0x040000A5 RID: 165
		public IngestOutcome outcome;

		/// <summary>Raw (uncompressed) payload per layer, or null. The offline `--test-bake` harness inspects
		/// these directly, so they stay raw; the storage-ready form lives in <see cref="F:Mirage.WebIngest.BakedTile.stored" />.</summary>
		// Token: 0x040000A6 RID: 166
		public byte[][] payload = new byte[4][];

		/// <summary>Per-layer <see cref="T:Mirage.VirtualTexture.ArchiveTextureFormat" /> code, valid where payload is non-null.</summary>
		// Token: 0x040000A7 RID: 167
		public int[] format = new int[4];

		/// <summary>Storage-ready (<c>EncodeForWeb</c>-encoded) payload per layer, or null. Produced on the bake
		/// worker (<c>CubeTileBaker.EncodePayloadsForCommit</c>) alongside <see cref="F:Mirage.WebIngest.BakedTile.codec" />/<see cref="F:Mirage.WebIngest.BakedTile.crc" /> so
		/// the main-thread commit does no compression or checksum — that was <c>CommitBakedTile</c>'s whole cost
		/// (height vdelta-bitpack + CRC over ~200 KB × 3), a per-tile frame spike protecting a regenerable cache.
		/// For BC layers this aliases <see cref="F:Mirage.WebIngest.BakedTile.payload" /> (they store verbatim); only height allocates anew.</summary>
		// Token: 0x040000A8 RID: 168
		public byte[][] stored = new byte[4][];

		/// <summary>Per-layer storage codec chosen by <c>EncodeForWeb</c>, valid where <see cref="F:Mirage.WebIngest.BakedTile.stored" /> is non-null.</summary>
		// Token: 0x040000A9 RID: 169
		public TileCodec[] codec = new TileCodec[4];

		/// <summary>Per-layer CRC32 of <see cref="F:Mirage.WebIngest.BakedTile.stored" />, precomputed on the worker so <c>Append</c> need not.</summary>
		// Token: 0x040000AA RID: 170
		public uint[] crc = new uint[4];
	}
}
