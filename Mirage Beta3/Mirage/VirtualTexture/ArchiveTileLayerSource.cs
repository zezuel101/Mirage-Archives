using System;
using KSPTextureLoader;

namespace Mirage.VirtualTexture
{
	/// <summary>Reads one layer's tiles from the canonical archive, with an optional web tier
	/// second.</summary>
	// Token: 0x02000037 RID: 55
	public sealed class ArchiveTileLayerSource : ITileLayerSource, IDisposable
	{
		// Token: 0x17000041 RID: 65
		// (get) Token: 0x06000155 RID: 341 RVA: 0x0000ACF0 File Offset: 0x00008EF0
		public bool Linear { get; }

		/// <summary>Finest canonical level installed, or -1 if this layer has no archive.</summary>
		// Token: 0x17000042 RID: 66
		// (get) Token: 0x06000156 RID: 342 RVA: 0x0000ACF8 File Offset: 0x00008EF8
		public int MaxResidentLevel
		{
			get
			{
				return this.canonical.MaxResidentLevel;
			}
		}

		// Token: 0x06000157 RID: 343 RVA: 0x0000AD08 File Offset: 0x00008F08
		public ArchiveTileLayerSource(string archiveDir, ArchiveLayer layer, bool linear, int slotDim)
		{
			this.layer = layer;
			this.slotDim = slotDim;
			this.Linear = linear;
			SeekingReadBackend.Force();
			this.canonical = new CanonicalIndex(archiveDir, layer);
			MirageDebug.Log(string.Format("TileArchive: {0} merged K={1}, {2} tiles resident ", layer, this.MaxResidentLevel, this.canonical.Count) + "from " + TileArchivePaths.Label(archiveDir) + ".");
		}

		/// <summary>Attach this layer's writable web tier.</summary>
		// Token: 0x06000158 RID: 344 RVA: 0x0000AD8C File Offset: 0x00008F8C
		public void AttachWebArchive(WebTileArchive webArchive)
		{
			bool flag = webArchive != null && webArchive.Layer != this.layer;
			if (flag)
			{
				throw new ArgumentException(string.Format("AttachWebArchive: {0} archive attached to a {1} source.", webArchive.Layer, this.layer));
			}
			this.web = webArchive;
		}

		// Token: 0x06000159 RID: 345 RVA: 0x0000ADE4 File Offset: 0x00008FE4
		public bool Exists(int face, int level, int tx, int ty)
		{
			string text;
			IndexEntry indexEntry;
			return this.TryResolve(face, level, tx, ty, out text, out indexEntry);
		}

		// Token: 0x0600015A RID: 346 RVA: 0x0000AE00 File Offset: 0x00009000
		public TileReadHandle BeginLoad(int face, int level, int tx, int ty)
		{
			string blobPath;
			IndexEntry entry;
			return this.TryResolve(face, level, tx, ty, out blobPath, out entry) ? this.MakeHandle(blobPath, entry) : ArchiveReadHandle.Missing;
		}

		// Token: 0x0600015B RID: 347 RVA: 0x0000AE2D File Offset: 0x0000902D
		private bool TryResolve(int face, int level, int tx, int ty, out string blobPath, out IndexEntry entry)
		{
			return this.canonical.TryResolve(MirageArchiveFormat.PackKey(face, level, tx, ty), this.web, out blobPath, out entry);
		}

		// Token: 0x0600015C RID: 348 RVA: 0x0000AE50 File Offset: 0x00009050
		private TileReadHandle MakeHandle(string blobPath, IndexEntry entry)
		{
			Texture2DConfig texture2DConfig = default(Texture2DConfig);
			texture2DConfig.Width = this.slotDim;
			texture2DConfig.Height = this.slotDim;
			texture2DConfig.MipCount = 1;
			texture2DConfig.Format = entry.format;
			texture2DConfig.Readable = false;
			texture2DConfig.Linear = this.Linear;
			Texture2DConfig config = texture2DConfig;
			long payloadOffset = (long)(entry.offset + 24UL);
			bool flag = entry.codec == TileCodec.None;
			TileReadHandle result;
			if (flag)
			{
				result = new ArchiveReadHandle(TextureLoader.LoadOwnedTexture2D(config, blobPath, payloadOffset, (long)((ulong)entry.length)));
			}
			else
			{
				int rawLen = MirageArchiveFormat.RawPayloadBytes((int)entry.format, this.slotDim, this.slotDim);
				result = new CompressedReadHandle(config, blobPath, payloadOffset, (int)entry.length, entry.codec, rawLen);
			}
			return result;
		}

		// Token: 0x0600015D RID: 349 RVA: 0x0000AF17 File Offset: 0x00009117
		public void Dispose()
		{
		}

		// Token: 0x04000112 RID: 274
		private readonly ArchiveLayer layer;

		// Token: 0x04000113 RID: 275
		private readonly int slotDim;

		// Token: 0x04000114 RID: 276
		private readonly CanonicalIndex canonical;

		// Token: 0x04000115 RID: 277
		private WebTileArchive web;
	}
}
