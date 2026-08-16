using System;
using System.IO;

namespace Mirage.VirtualTexture
{
	/// <summary>Tile header — 24 bytes, preceding every payload in a blob.</summary>
	// Token: 0x0200003E RID: 62
	public struct TileHeader
	{
		// Token: 0x06000185 RID: 389 RVA: 0x0000C084 File Offset: 0x0000A284
		public void Write(BinaryWriter w)
		{
			w.Write(this.key);
			w.Write(this.payloadLen);
			w.Write((byte)this.codec);
			w.Write(this.format);
			w.Write(this.crc32);
			w.Write(0);
			w.Write(0);
			w.Write(0);
		}

		// Token: 0x06000186 RID: 390 RVA: 0x0000C0EC File Offset: 0x0000A2EC
		public static TileHeader Read(BinaryReader r)
		{
			TileHeader h = new TileHeader
			{
				key = r.ReadUInt64(),
				payloadLen = r.ReadUInt32(),
				codec = (TileCodec)r.ReadByte(),
				format = r.ReadByte(),
				crc32 = r.ReadUInt32()
			};
			r.ReadUInt16();
			r.ReadUInt16();
			r.ReadUInt16();
			return h;
		}

		// Token: 0x0400013E RID: 318
		public ulong key;

		// Token: 0x0400013F RID: 319
		public uint payloadLen;

		// Token: 0x04000140 RID: 320
		public TileCodec codec;

		// Token: 0x04000141 RID: 321
		public byte format;

		// Token: 0x04000142 RID: 322
		public uint crc32;
	}
}
