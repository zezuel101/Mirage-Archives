using System;
using System.IO;

namespace Mirage.VirtualTexture
{
	/// <summary>Index header — one per <c>.idx</c>, followed by sorted entries.</summary>
	// Token: 0x0200003F RID: 63
	public struct IndexHeader
	{
		// Token: 0x06000187 RID: 391 RVA: 0x0000C160 File Offset: 0x0000A360
		public void Write(BinaryWriter w)
		{
			MirageArchiveFormat.WriteMagic(w, 826889293U);
			w.Write(this.version);
			w.Write((byte)this.layer);
			w.Write(this.level);
			w.Write(this.entryCount);
			w.Write(this.blobLength);
		}

		// Token: 0x06000188 RID: 392 RVA: 0x0000C1BC File Offset: 0x0000A3BC
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

		// Token: 0x04000143 RID: 323
		public ushort version;

		// Token: 0x04000144 RID: 324
		public ArchiveLayer layer;

		// Token: 0x04000145 RID: 325
		public int level;

		// Token: 0x04000146 RID: 326
		public int entryCount;

		/// <summary>Size of the paired <c>.bin</c> — staleness sentinel (mismatch drops the level).</summary>
		// Token: 0x04000147 RID: 327
		public long blobLength;
	}
}
