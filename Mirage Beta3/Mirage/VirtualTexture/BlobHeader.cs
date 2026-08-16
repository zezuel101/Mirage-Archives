using System;
using System.IO;

namespace Mirage.VirtualTexture
{
	/// <summary>Blob header — one per <c>.bin</c>, followed by tightly packed tiles.</summary>
	// Token: 0x0200003D RID: 61
	public struct BlobHeader
	{
		// Token: 0x06000183 RID: 387 RVA: 0x0000BF84 File Offset: 0x0000A184
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

		// Token: 0x06000184 RID: 388 RVA: 0x0000BFFC File Offset: 0x0000A1FC
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

		// Token: 0x04000137 RID: 311
		public ushort version;

		// Token: 0x04000138 RID: 312
		public ArchiveLayer layer;

		// Token: 0x04000139 RID: 313
		public int format;

		// Token: 0x0400013A RID: 314
		public ushort tileSize;

		// Token: 0x0400013B RID: 315
		public ushort borderPx;

		// Token: 0x0400013C RID: 316
		public byte faceCount;

		// Token: 0x0400013D RID: 317
		public uint flags;
	}
}
