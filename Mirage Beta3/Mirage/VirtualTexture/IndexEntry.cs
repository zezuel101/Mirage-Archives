using System;
using System.IO;

namespace Mirage.VirtualTexture
{
	/// <summary>On-disk index entry, 22 bytes.</summary>
	// Token: 0x02000040 RID: 64
	public struct IndexEntry
	{
		// Token: 0x06000189 RID: 393 RVA: 0x0000C22C File Offset: 0x0000A42C
		public void Write(BinaryWriter w)
		{
			w.Write(this.key);
			w.Write(this.offset);
			w.Write(this.length);
			w.Write((byte)this.codec);
			w.Write(this.format);
		}

		// Token: 0x0600018A RID: 394 RVA: 0x0000C27C File Offset: 0x0000A47C
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

		// Token: 0x04000148 RID: 328
		public ulong key;

		// Token: 0x04000149 RID: 329
		public ulong offset;

		// Token: 0x0400014A RID: 330
		public uint length;

		// Token: 0x0400014B RID: 331
		public TileCodec codec;

		// Token: 0x0400014C RID: 332
		public byte format;
	}
}
