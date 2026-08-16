using System;
using System.IO;

namespace Mirage.VirtualTexture
{
	/// <summary>On-disk index entry (22 B). The file-local <see cref="F:Mirage.VirtualTexture.IndexEntry.offset" /> points
	/// at the tile's <see cref="T:Mirage.VirtualTexture.TileHeader" /> in the paired blob.</summary>
	// Token: 0x02000048 RID: 72
	public struct IndexEntry
	{
		// Token: 0x060001BA RID: 442 RVA: 0x0000D338 File Offset: 0x0000B538
		public void Write(BinaryWriter w)
		{
			w.Write(this.key);
			w.Write(this.offset);
			w.Write(this.length);
			w.Write((byte)this.codec);
			w.Write(this.format);
		}

		// Token: 0x060001BB RID: 443 RVA: 0x0000D388 File Offset: 0x0000B588
		public static IndexEntry Read(BinaryReader r)
		{
			return new IndexEntry
			{
				key = r.ReadUInt64(),
				offset = r.ReadUInt64(),
				length = r.ReadUInt32(),
				codec = (TileCodec)r.ReadByte(),
				format = r.ReadByte()
			};
		}

		// Token: 0x04000188 RID: 392
		public ulong key;

		// Token: 0x04000189 RID: 393
		public ulong offset;

		// Token: 0x0400018A RID: 394
		public uint length;

		// Token: 0x0400018B RID: 395
		public TileCodec codec;

		// Token: 0x0400018C RID: 396
		public byte format;
	}
}
