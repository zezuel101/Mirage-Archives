using System;
using System.IO;

namespace Mirage.VirtualTexture
{
	/// <summary>CPU-side height archive reader — unpacks R16 to height fractions for HeightTileLayer.</summary>
	// Token: 0x02000038 RID: 56
	public sealed class CpuHeightArchive
	{
		// Token: 0x17000043 RID: 67
		// (get) Token: 0x0600015E RID: 350 RVA: 0x0000AF1A File Offset: 0x0000911A
		public int MaxResidentLevel
		{
			get
			{
				return this.canonical.MaxResidentLevel;
			}
		}

		// Token: 0x0600015F RID: 351 RVA: 0x0000AF28 File Offset: 0x00009128
		public CpuHeightArchive(string archiveDir, WebTileArchive webHeight = null)
		{
			bool flag = webHeight != null && webHeight.Layer != ArchiveLayer.Height;
			if (flag)
			{
				throw new ArgumentException(string.Format("CpuHeightArchive: web archive is {0}, expected Height.", webHeight.Layer));
			}
			this.canonical = new CanonicalIndex(archiveDir, ArchiveLayer.Height);
			this.web = webHeight;
			string webTier = (this.web != null) ? string.Format(" (+{0} web)", this.web.Count) : "";
			MirageDebug.Log(string.Concat(new string[]
			{
				string.Format("CpuHeightArchive: merged K={0}, {1} height tiles ", this.MaxResidentLevel, this.canonical.Count),
				"resident",
				webTier,
				" from ",
				TileArchivePaths.Label(archiveDir),
				"."
			}));
		}

		/// <summary>Row-major height fractions in [0,1], or null if absent or not R16.</summary>
		// Token: 0x06000160 RID: 352 RVA: 0x0000B00C File Offset: 0x0000920C
		public float[] LoadHeightTile(int face, int level, int tx, int ty, int slotDim)
		{
			ulong key = MirageArchiveFormat.PackKey(face, level, tx, ty);
			string blobPath;
			IndexEntry e;
			bool flag = !this.canonical.TryResolve(key, this.web, out blobPath, out e);
			float[] result;
			if (flag)
			{
				result = null;
			}
			else
			{
				bool flag2 = e.format != 9;
				if (flag2)
				{
					MirageDebug.LogError(string.Format("CpuHeightArchive: tile L{0} f{1} {2},{3} is format {4}, not the ", new object[]
					{
						level,
						face,
						tx,
						ty,
						e.format
					}) + "R16 that CPU height sampling expects.");
					result = null;
				}
				else
				{
					byte[] stored = CpuHeightArchive.ReadStoredPayload(blobPath, e);
					bool flag3 = stored == null;
					if (flag3)
					{
						MirageDebug.LogError(string.Format("CpuHeightArchive: short read for L{0} f{1} {2},{3}.", new object[]
						{
							level,
							face,
							tx,
							ty
						}));
						result = null;
					}
					else
					{
						int rawLen = slotDim * slotDim * 2;
						byte[] r16 = new byte[rawLen];
						try
						{
							MirageArchiveFormat.DecodeTilePayload(e.codec, stored, (int)e.length, r16, rawLen);
						}
						catch (Exception ex)
						{
							MirageDebug.LogError(string.Format("CpuHeightArchive: decode failed for L{0} f{1} {2},{3} ", new object[]
							{
								level,
								face,
								tx,
								ty
							}) + string.Format("(codec {0}): {1}", e.codec, ex.Message));
							return null;
						}
						result = CpuHeightArchive.ToHeightFractions(r16, slotDim * slotDim);
					}
				}
			}
			return result;
		}

		// Token: 0x06000161 RID: 353 RVA: 0x0000B1C4 File Offset: 0x000093C4
		private static byte[] ReadStoredPayload(string blobPath, IndexEntry e)
		{
			byte[] stored = new byte[e.length];
			byte[] result;
			using (FileStream fs = new FileStream(blobPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
				fs.Position = (long)(e.offset + 24UL);
				int i;
				for (int read = 0; read < stored.Length; read += i)
				{
					i = fs.Read(stored, read, stored.Length - read);
					bool flag = i <= 0;
					if (flag)
					{
						return null;
					}
				}
				result = stored;
			}
			return result;
		}

		// Token: 0x06000162 RID: 354 RVA: 0x0000B254 File Offset: 0x00009454
		private static float[] ToHeightFractions(byte[] r16, int count)
		{
			float[] heights = new float[count];
			for (int i = 0; i < count; i++)
			{
				heights[i] = (float)((int)r16[2 * i] | (int)r16[2 * i + 1] << 8) * 1.5259022E-05f;
			}
			return heights;
		}

		// Token: 0x04000117 RID: 279
		private readonly CanonicalIndex canonical;

		// Token: 0x04000118 RID: 280
		private readonly WebTileArchive web;
	}
}
