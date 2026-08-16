using System;
using System.IO;

namespace Mirage.VirtualTexture
{
	// Token: 0x02000047 RID: 71
	public struct IndexHeader
	{
		// Token: 0x060001B8 RID: 440 RVA: 0x0000D26C File Offset: 0x0000B46C
		public void Write(BinaryWriter w)
		{
			MirageArchiveFormat.WriteMagic(w, 826889293U);
			w.Write(this.version);
			w.Write((byte)this.layer);
			w.Write(this.level);
			w.Write(this.entryCount);
			w.Write(this.blobLength);
		}

		// Token: 0x060001B9 RID: 441 RVA: 0x0000D2C8 File Offset: 0x0000B4C8
		public static IndexHeader Read(BinaryReader r)
		{
			MirageArchiveFormat.ExpectMagic(r, 826889293U, "index");
			return new IndexHeader
			{
				version = r.ReadUInt16(),
				layer = (ArchiveLayer)r.ReadByte(),
				level = r.ReadInt32(),
				entryCount = r.ReadInt32(),
				blobLength = r.ReadInt64()
			};
		}

		// Token: 0x04000183 RID: 387
		public ushort version;

		// Token: 0x04000184 RID: 388
		public ArchiveLayer layer;

		// Token: 0x04000185 RID: 389
		public int level;

		// Token: 0x04000186 RID: 390
		public int entryCount;

		// Token: 0x04000187 RID: 391
		public long blobLength;
	}
}
