using System;
using System.Collections.Generic;
using System.IO;

namespace Mirage.VirtualTexture
{
	/// <summary>File-naming + detection helpers shared by the runtime reader and the offline packer's on-disk
	/// convention. A body's archive is a directory of per-level subfolders:
	/// <c>&lt;dir&gt;/Level_&lt;N&gt;/canonical.&lt;layer&gt;.L&lt;N&gt;.{bin,idx}</c>. There is no manifest —
	/// installed layers and each layer's finest level are discovered by probing which files are present
	/// (contiguous from 0), so a user installs a subset just by copying the <c>Level_&lt;N&gt;</c> folders they want.</summary>
	// Token: 0x0200004B RID: 75
	public static class TileArchivePaths
	{
		// Token: 0x060001C2 RID: 450 RVA: 0x0000D6D1 File Offset: 0x0000B8D1
		public static string LayerName(ArchiveLayer layer)
		{
			return layer.ToString().ToLowerInvariant();
		}

		// Token: 0x060001C3 RID: 451 RVA: 0x0000D6E5 File Offset: 0x0000B8E5
		public static string LevelDir(string dir, int level)
		{
			return Path.Combine(dir, string.Format("Level_{0}", level));
		}

		// Token: 0x060001C4 RID: 452 RVA: 0x0000D6FD File Offset: 0x0000B8FD
		public static string Blob(string dir, ArchiveLayer layer, int level)
		{
			return Path.Combine(TileArchivePaths.LevelDir(dir, level), string.Format("canonical.{0}.L{1}.bin", TileArchivePaths.LayerName(layer), level));
		}

		// Token: 0x060001C5 RID: 453 RVA: 0x0000D721 File Offset: 0x0000B921
		public static string Index(string dir, ArchiveLayer layer, int level)
		{
			return Path.Combine(TileArchivePaths.LevelDir(dir, level), string.Format("canonical.{0}.L{1}.idx", TileArchivePaths.LayerName(layer), level));
		}

		// Token: 0x060001C6 RID: 454 RVA: 0x0000D745 File Offset: 0x0000B945
		public static string WebBlob(string dir, ArchiveLayer layer)
		{
			return Path.Combine(dir, "web." + TileArchivePaths.LayerName(layer) + ".bin");
		}

		// Token: 0x060001C7 RID: 455 RVA: 0x0000D762 File Offset: 0x0000B962
		public static string WebIndex(string dir, ArchiveLayer layer)
		{
			return Path.Combine(dir, "web." + TileArchivePaths.LayerName(layer) + ".idx");
		}

		/// <summary>True if the directory holds an archive — i.e. any layer has a Level_0 index present.</summary>
		// Token: 0x060001C8 RID: 456 RVA: 0x0000D77F File Offset: 0x0000B97F
		public static bool HasArchive(string dir)
		{
			return !string.IsNullOrEmpty(dir) && (File.Exists(TileArchivePaths.Index(dir, ArchiveLayer.Color, 0)) || File.Exists(TileArchivePaths.Index(dir, ArchiveLayer.Height, 0)) || File.Exists(TileArchivePaths.Index(dir, ArchiveLayer.Normal, 0)));
		}

		/// <summary>Finest contiguous level K present on disk for a layer (0..K with no gap), or -1 if the layer
		/// isn't installed. Replaces the manifest: "presence of a file is the config".</summary>
		// Token: 0x060001C9 RID: 457 RVA: 0x0000D7BC File Offset: 0x0000B9BC
		public static int DetectMaxLevel(string dir, ArchiveLayer layer)
		{
			bool flag = string.IsNullOrEmpty(dir);
			int result;
			if (flag)
			{
				result = -1;
			}
			else
			{
				int i = -1;
				int level = 0;
				for (;;)
				{
					bool flag2 = !File.Exists(TileArchivePaths.Index(dir, layer, level)) || !File.Exists(TileArchivePaths.Blob(dir, layer, level));
					if (flag2)
					{
						break;
					}
					i = level;
					level++;
				}
				result = i;
			}
			return result;
		}

		/// <summary>
		/// Merge one layer's per-level canonical <c>.idx</c> files into <paramref name="index" /> and populate
		/// <paramref name="blobByLevel" /> (index by level → <c>.bin</c> path). Enumerates levels 0..K
		/// contiguously (a gap ends the installed chain, design §6.2), and staleness-checks each blob's size
		/// against the index's <c>blobLength</c> sentinel — a half-copied blob drops that level and finer.
		/// Returns K (finest resident level, -1 if none). Shared by the GPU source and the CPU height reader so
		/// both see the exact same residency.
		/// </summary>
		// Token: 0x060001CA RID: 458 RVA: 0x0000D818 File Offset: 0x0000BA18
		public static int MergeCanonical(string dir, ArchiveLayer layer, Dictionary<ulong, IndexEntry> index, List<string> blobByLevel)
		{
			int i = -1;
			int level = 0;
			for (;;)
			{
				string idxPath = TileArchivePaths.Index(dir, layer, level);
				string binPath = TileArchivePaths.Blob(dir, layer, level);
				bool flag = !File.Exists(idxPath) || !File.Exists(binPath);
				if (flag)
				{
					break;
				}
				try
				{
					long fileLen = new FileInfo(binPath).Length;
					using (FileStream fs = new FileStream(idxPath, FileMode.Open, FileAccess.Read))
					{
						using (BinaryReader br = new BinaryReader(fs))
						{
							IndexHeader header = IndexHeader.Read(br);
							bool flag2 = header.blobLength != fileLen;
							if (flag2)
							{
								MirageDebug.LogError(string.Format("TileArchive: {0} size {1} != index blobLength ", Path.GetFileName(binPath), fileLen) + string.Format("{0} — dropping this level and finer (staleness).", header.blobLength));
								break;
							}
							blobByLevel.Add(binPath);
							for (int j = 0; j < header.entryCount; j++)
							{
								IndexEntry e = IndexEntry.Read(br);
								index[e.key] = e;
							}
						}
					}
				}
				catch (Exception ex)
				{
					MirageDebug.LogError("TileArchive: failed to merge " + idxPath + ": " + ex.Message);
					break;
				}
				i = level;
				level++;
			}
			return i;
		}
	}
}
