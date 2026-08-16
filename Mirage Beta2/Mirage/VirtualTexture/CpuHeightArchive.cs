using System;
using System.Collections.Generic;
using System.IO;

namespace Mirage.VirtualTexture
{
	/// <summary>
	/// CPU-side reader for the <b>height</b> archive — the backing that lets
	/// <see cref="T:Mirage.VirtualTexture.HeightTileLayer" /> sample the SAME R16 bytes the GPU displaces, so the CPU PQS collision
	/// mesh and the GPU heightmap can't drift (design §11). Merges the per-level <c>canonical.height.L&lt;N&gt;.idx</c>
	/// into one residency map (the shared <see cref="M:Mirage.VirtualTexture.TileArchivePaths.MergeCanonical(System.String,Mirage.VirtualTexture.ArchiveLayer,System.Collections.Generic.Dictionary{System.UInt64,Mirage.VirtualTexture.IndexEntry},System.Collections.Generic.List{System.String})" />), then reads each tile's
	/// raw payload straight from the blob at its byte offset and unpacks R16 → height fraction.
	///
	/// It resolves canonical-then-web with the same precedence as the GPU source, and that is not a nicety: once
	/// height has a writable web tier, the GPU displaces terrain from web tiles finer than canonical's K, and a
	/// canonical-only CPU reader would collide against the coarser surface — reintroducing exactly the
	/// GPU/CPU divergence §11 exists to close. Both sides must resolve the same key the same way.
	///
	/// Reads open the blob per tile (the caller's LRU cache bounds the miss rate), so no file handles are held
	/// open — and in particular the CPU sampler never holds a handle to the writable web blob.
	/// </summary>
	// Token: 0x02000036 RID: 54
	public sealed class CpuHeightArchive
	{
		// Token: 0x1700003F RID: 63
		// (get) Token: 0x0600014C RID: 332 RVA: 0x0000ADB6 File Offset: 0x00008FB6
		public int MaxResidentLevel { get; }

		// Token: 0x0600014D RID: 333 RVA: 0x0000ADC0 File Offset: 0x00008FC0
		public CpuHeightArchive(string archiveDir, WebTileArchive webHeight = null)
		{
			this.MaxResidentLevel = TileArchivePaths.MergeCanonical(archiveDir, ArchiveLayer.Height, this.index, this.blobByLevel);
			bool flag = webHeight != null && webHeight.Layer != ArchiveLayer.Height;
			if (flag)
			{
				throw new ArgumentException(string.Format("CpuHeightArchive: web archive is {0}, expected Height.", webHeight.Layer));
			}
			this.web = webHeight;
			MirageDebug.Log(string.Format("CpuHeightArchive: merged K={0}, {1} height tiles resident", this.MaxResidentLevel, this.index.Count) + ((this.web != null) ? string.Format(" (+{0} web).", this.web.Count) : "."));
		}

		/// <summary>Read one height tile as a row-major <c>slotDim*slotDim</c> array of height fractions in
		/// [0,1] (R16 value / 65535), matching the GPU's read. Returns null if the tile isn't in the archive
		/// or isn't the expected R16 layout.</summary>
		// Token: 0x0600014E RID: 334 RVA: 0x0000AE98 File Offset: 0x00009098
		public float[] LoadHeightTile(int face, int level, int tx, int ty, int slotDim)
		{
			ulong key = MirageArchiveFormat.PackKey(face, level, tx, ty);
			IndexEntry e;
			bool flag = this.index.TryGetValue(key, out e) && level < this.blobByLevel.Count;
			string blobPath;
			if (flag)
			{
				blobPath = this.blobByLevel[level];
			}
			else
			{
				bool flag2 = this.web != null && this.web.TryResolve(key, out e);
				if (!flag2)
				{
					return null;
				}
				blobPath = this.web.BlobPath;
			}
			bool flag3 = e.format != 9;
			float[] result;
			if (flag3)
			{
				MirageDebug.LogError(string.Format("CpuHeightArchive: tile L{0} f{1} {2},{3} format {4} is not R16 — ", new object[]
				{
					level,
					face,
					tx,
					ty,
					e.format
				}) + "CPU height sampling expects R16.");
				result = null;
			}
			else
			{
				int rawLen = slotDim * slotDim * 2;
				byte[] stored = new byte[e.length];
				long payloadOffset = (long)(e.offset + 24UL);
				using (FileStream fs = new FileStream(blobPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					fs.Position = payloadOffset;
					int i;
					for (int read = 0; read < stored.Length; read += i)
					{
						i = fs.Read(stored, read, stored.Length - read);
						bool flag4 = i <= 0;
						if (flag4)
						{
							MirageDebug.LogError(string.Format("CpuHeightArchive: short read for L{0} f{1} {2},{3}.", new object[]
							{
								level,
								face,
								tx,
								ty
							}));
							return null;
						}
					}
				}
				byte[] r16 = new byte[rawLen];
				try
				{
					MirageArchiveFormat.DecodeTilePayload(e.codec, stored, (int)e.length, r16, rawLen);
				}
				catch (Exception ex)
				{
					MirageDebug.LogError(string.Format("CpuHeightArchive: decode failed for L{0} f{1} {2},{3} (codec {4}): {5}", new object[]
					{
						level,
						face,
						tx,
						ty,
						e.codec,
						ex.Message
					}));
					return null;
				}
				int count = slotDim * slotDim;
				float[] outp = new float[count];
				for (int j = 0; j < count; j++)
				{
					int v = (int)r16[2 * j] | (int)r16[2 * j + 1] << 8;
					outp[j] = (float)v * 1.5259022E-05f;
				}
				result = outp;
			}
			return result;
		}

		// Token: 0x0400011C RID: 284
		private readonly Dictionary<ulong, IndexEntry> index = new Dictionary<ulong, IndexEntry>();

		// Token: 0x0400011D RID: 285
		private readonly List<string> blobByLevel = new List<string>();

		// Token: 0x0400011E RID: 286
		private readonly WebTileArchive web;
	}
}
