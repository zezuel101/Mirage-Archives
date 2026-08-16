using System;
using System.Collections.Generic;

namespace Mirage.VirtualTexture
{
	/// <summary>One layer's merged canonical residency — shared so GPU and CPU readers agree key-for-key.</summary>
	// Token: 0x02000043 RID: 67
	public sealed class CanonicalIndex
	{
		// Token: 0x17000044 RID: 68
		// (get) Token: 0x06000198 RID: 408 RVA: 0x0000C874 File Offset: 0x0000AA74
		public int MaxResidentLevel { get; }

		// Token: 0x17000045 RID: 69
		// (get) Token: 0x06000199 RID: 409 RVA: 0x0000C87C File Offset: 0x0000AA7C
		public int Count
		{
			get
			{
				return this.index.Count;
			}
		}

		// Token: 0x0600019A RID: 410 RVA: 0x0000C889 File Offset: 0x0000AA89
		public CanonicalIndex(string archiveDir, ArchiveLayer layer)
		{
			this.MaxResidentLevel = TileArchivePaths.MergeCanonical(archiveDir, layer, this.index, this.blobByLevel);
		}

		/// <summary>Resolve canonical-first, then web. False when neither tier holds the key.</summary>
		// Token: 0x0600019B RID: 411 RVA: 0x0000C8C4 File Offset: 0x0000AAC4
		public bool TryResolve(ulong key, WebTileArchive web, out string blobPath, out IndexEntry entry)
		{
			int level = MirageArchiveFormat.KeyLevel(key);
			bool flag = this.index.TryGetValue(key, out entry) && level < this.blobByLevel.Count;
			bool result;
			if (flag)
			{
				blobPath = this.blobByLevel[level];
				result = true;
			}
			else
			{
				bool flag2 = web != null && web.TryResolve(key, out entry);
				if (flag2)
				{
					blobPath = web.BlobPath;
					result = true;
				}
				else
				{
					blobPath = null;
					entry = default(IndexEntry);
					result = false;
				}
			}
			return result;
		}

		// Token: 0x04000150 RID: 336
		private readonly Dictionary<ulong, IndexEntry> index = new Dictionary<ulong, IndexEntry>();

		// Token: 0x04000151 RID: 337
		private readonly List<string> blobByLevel = new List<string>();
	}
}
