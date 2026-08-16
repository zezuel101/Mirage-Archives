using System;
using System.IO;

namespace Mirage.VirtualTexture
{
	// Token: 0x02000046 RID: 70
	public struct TileHeader
	{
		// Token: 0x060001B6 RID: 438 RVA: 0x0000D190 File Offset: 0x0000B390
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

		// Token: 0x060001B7 RID: 439 RVA: 0x0000D1F8 File Offset: 0x0000B3F8
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

		// Token: 0x0400017E RID: 382
		public ulong key;

		// Token: 0x0400017F RID: 383
		public uint payloadLen;

		// Token: 0x04000180 RID: 384
		public TileCodec codec;

		// Token: 0x04000181 RID: 385
		public byte format;

		// Token: 0x04000182 RID: 386
		public uint crc32;
	}
}
