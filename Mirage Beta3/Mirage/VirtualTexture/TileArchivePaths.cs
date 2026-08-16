using System;
using System.Collections.Generic;
using System.IO;

namespace Mirage.VirtualTexture
{
	/// <summary>Archive file layout and installed-level discovery (no manifest — probe for files).</summary>
	// Token: 0x02000042 RID: 66
	public static class TileArchivePaths
	{
		// Token: 0x0600018D RID: 397 RVA: 0x0000C40F File Offset: 0x0000A60F
		public static string LayerName(ArchiveLayer layer)
		{
			return layer.ToString().ToLowerInvariant();
		}

		// Token: 0x0600018E RID: 398 RVA: 0x0000C423 File Offset: 0x0000A623
		public static string LevelDir(string dir, int level)
		{
			return Path.Combine(dir, string.Format("Level_{0}", level));
		}

		// Token: 0x0600018F RID: 399 RVA: 0x0000C43B File Offset: 0x0000A63B
		public static string Blob(string dir, ArchiveLayer layer, int level)
		{
			return Path.Combine(TileArchivePaths.LevelDir(dir, level), string.Format("canonical.{0}.L{1}.bin", TileArchivePaths.LayerName(layer), level));
		}

		// Token: 0x06000190 RID: 400 RVA: 0x0000C45F File Offset: 0x0000A65F
		public static string Index(string dir, ArchiveLayer layer, int level)
		{
			return Path.Combine(TileArchivePaths.LevelDir(dir, level), string.Format("canonical.{0}.L{1}.idx", TileArchivePaths.LayerName(layer), level));
		}

		// Token: 0x06000191 RID: 401 RVA: 0x0000C483 File Offset: 0x0000A683
		public static string WebBlob(string dir, ArchiveLayer layer)
		{
			return Path.Combine(dir, "web." + TileArchivePaths.LayerName(layer) + ".bin");
		}

		// Token: 0x06000192 RID: 402 RVA: 0x0000C4A0 File Offset: 0x0000A6A0
		public static string WebIndex(string dir, ArchiveLayer layer)
		{
			return Path.Combine(dir, "web." + TileArchivePaths.LayerName(layer) + ".idx");
		}

		/// <summary>Enough of an archive path to tell one body's log line from another's.</summary>
		// Token: 0x06000193 RID: 403 RVA: 0x0000C4C0 File Offset: 0x0000A6C0
		public static string Label(string dir)
		{
			bool flag = string.IsNullOrEmpty(dir);
			string result;
			if (flag)
			{
				result = "(none)";
			}
			else
			{
				string trimmed = dir.TrimEnd(new char[]
				{
					'/',
					'\\'
				});
				string leaf = Path.GetFileName(trimmed);
				string parent = Path.GetFileName(Path.GetDirectoryName(trimmed) ?? "");
				result = (string.IsNullOrEmpty(parent) ? leaf : (parent + "/" + leaf));
			}
			return result;
		}

		/// <summary>True if the directory holds an archive: any layer has a Level_0 index.</summary>
		// Token: 0x06000194 RID: 404 RVA: 0x0000C531 File Offset: 0x0000A731
		public static bool HasArchive(string dir)
		{
			return !string.IsNullOrEmpty(dir) && (File.Exists(TileArchivePaths.Index(dir, ArchiveLayer.Color, 0)) || File.Exists(TileArchivePaths.Index(dir, ArchiveLayer.Height, 0)) || File.Exists(TileArchivePaths.Index(dir, ArchiveLayer.Normal, 0)));
		}

		/// <summary>Finest contiguous level on disk (0..K), or -1 if not installed.</summary>
		// Token: 0x06000195 RID: 405 RVA: 0x0000C56C File Offset: 0x0000A76C
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

		/// <summary>Merge per-level canonical indexes into a single map. Returns finest level, or -1.</summary>
		// Token: 0x06000196 RID: 406 RVA: 0x0000C5C8 File Offset: 0x0000A7C8
		public static int MergeCanonical(string dir, ArchiveLayer layer, Dictionary<ulong, IndexEntry> index, List<string> blobByLevel)
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
					string idxPath = TileArchivePaths.Index(dir, layer, level);
					string binPath = TileArchivePaths.Blob(dir, layer, level);
					bool flag2 = !File.Exists(idxPath) || !File.Exists(binPath);
					if (flag2)
					{
						break;
					}
					List<IndexEntry> entries = TileArchivePaths.ReadLevelIndex(idxPath, binPath);
					bool flag3 = entries == null;
					if (flag3)
					{
						break;
					}
					blobByLevel.Add(binPath);
					foreach (IndexEntry e in entries)
					{
						index[e.key] = e;
					}
					i = level;
					level++;
				}
				result = i;
			}
			return result;
		}

		// Token: 0x06000197 RID: 407 RVA: 0x0000C6A4 File Offset: 0x0000A8A4
		private static List<IndexEntry> ReadLevelIndex(string idxPath, string binPath)
		{
			List<IndexEntry> result;
			try
			{
				long fileLen = new FileInfo(binPath).Length;
				using (FileStream fs = new FileStream(idxPath, FileMode.Open, FileAccess.Read))
				{
					using (BinaryReader br = new BinaryReader(fs))
					{
						IndexHeader header = IndexHeader.Read(br);
						bool flag = header.blobLength != fileLen;
						if (flag)
						{
							MirageDebug.LogError(string.Format("TileArchive: {0} size {1} != index blobLength ", Path.GetFileName(binPath), fileLen) + string.Format("{0} — dropping this level and finer (staleness).", header.blobLength));
							result = null;
						}
						else
						{
							long room = (fs.Length - fs.Position) / 22L;
							bool flag2 = header.entryCount < 0 || (long)header.entryCount > room;
							if (flag2)
							{
								MirageDebug.LogError(string.Concat(new string[]
								{
									"TileArchive: ",
									Path.GetFileName(idxPath),
									" declares ",
									string.Format("{0} entries but holds room for {1} — dropping this ", header.entryCount, room),
									"level and finer."
								}));
								result = null;
							}
							else
							{
								List<IndexEntry> entries = new List<IndexEntry>(header.entryCount);
								for (int i = 0; i < header.entryCount; i++)
								{
									entries.Add(IndexEntry.Read(br));
								}
								result = entries;
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				MirageDebug.LogError("TileArchive: failed to merge " + idxPath + ": " + ex.Message);
				result = null;
			}
			return result;
		}
	}
}
