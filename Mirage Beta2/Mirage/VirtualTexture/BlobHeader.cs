using System;
using System.IO;

namespace Mirage.VirtualTexture
{
	// Token: 0x02000045 RID: 69
	public struct BlobHeader
	{
		// Token: 0x060001B4 RID: 436 RVA: 0x0000D090 File Offset: 0x0000B290
		public void Write(BinaryWriter w)
		{
			MirageArchiveFormat.WriteMagic(w, 826365005U);
			w.Write(this.version);
			w.Write((byte)this.layer);
			w.Write(this.format);
			w.Write(this.tileSize);
			w.Write(this.borderPx);
			w.Write(this.faceCount);
			w.Write(this.flags);
		}

		// Token: 0x060001B5 RID: 437 RVA: 0x0000D108 File Offset: 0x0000B308
		public static BlobHeader Read(BinaryReader r)
		{
			MirageArchiveFormat.ExpectMagic(r, 826365005U, "blob");
			return new BlobHeader
			{
				version = r.ReadUInt16(),
				layer = (ArchiveLayer)r.ReadByte(),
				format = r.ReadInt32(),
				tileSize = r.ReadUInt16(),
				borderPx = r.ReadUInt16(),
				faceCount = r.ReadByte(),
				flags = r.ReadUInt32()
			};
		}

		// Token: 0x04000177 RID: 375
		public ushort version;

		// Token: 0x04000178 RID: 376
		public ArchiveLayer layer;

		// Token: 0x04000179 RID: 377
		public int format;

		// Token: 0x0400017A RID: 378
		public ushort tileSize;

		// Token: 0x0400017B RID: 379
		public ushort borderPx;

		// Token: 0x0400017C RID: 380
		public byte faceCount;

		// Token: 0x0400017D RID: 381
		public uint flags;
	}
}
