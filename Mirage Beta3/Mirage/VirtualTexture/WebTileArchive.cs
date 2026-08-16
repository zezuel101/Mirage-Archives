using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Mirage.VirtualTexture
{
	/// <summary>Runtime read/write tier for one layer: an append blob + index of tiles baked while flying.</summary>
	// Token: 0x02000044 RID: 68
	public sealed class WebTileArchive : IDisposable
	{
		// Token: 0x17000046 RID: 70
		// (get) Token: 0x0600019C RID: 412 RVA: 0x0000C941 File Offset: 0x0000AB41
		public ArchiveLayer Layer
		{
			get
			{
				return this.layer;
			}
		}

		// Token: 0x17000047 RID: 71
		// (get) Token: 0x0600019D RID: 413 RVA: 0x0000C949 File Offset: 0x0000AB49
		public string BlobPath
		{
			get
			{
				return this.blobPath;
			}
		}

		// Token: 0x17000048 RID: 72
		// (get) Token: 0x0600019E RID: 414 RVA: 0x0000C951 File Offset: 0x0000AB51
		public int Count
		{
			get
			{
				return this.index.Count;
			}
		}

		/// <summary>Bytes on disk now, including tombstoned tiles Compact has not reclaimed.</summary>
		// Token: 0x17000049 RID: 73
		// (get) Token: 0x0600019F RID: 415 RVA: 0x0000C95E File Offset: 0x0000AB5E
		public long PhysicalBytes
		{
			get
			{
				return this.blobLength;
			}
		}

		/// <summary>Bytes live tiles would occupy after Compact (Physical − Live = reclaimable).</summary>
		// Token: 0x1700004A RID: 74
		// (get) Token: 0x060001A0 RID: 416 RVA: 0x0000C966 File Offset: 0x0000AB66
		public long LiveBytes
		{
			get
			{
				return this.liveBytes;
			}
		}

		// Token: 0x060001A1 RID: 417 RVA: 0x0000C970 File Offset: 0x0000AB70
		public WebTileArchive(string dir, ArchiveLayer layer, int tileSize, int borderPx)
		{
			Directory.CreateDirectory(dir);
			this.layer = layer;
			this.blobPath = TileArchivePaths.WebBlob(dir, layer);
			this.idxPath = TileArchivePaths.WebIndex(dir, layer);
			this.tileSize = (ushort)tileSize;
			this.borderPx = (ushort)borderPx;
			this.Open();
		}

		// Token: 0x060001A2 RID: 418 RVA: 0x0000C9D0 File Offset: 0x0000ABD0
		public bool Contains(ulong key)
		{
			return this.index.ContainsKey(key);
		}

		// Token: 0x060001A3 RID: 419 RVA: 0x0000C9DE File Offset: 0x0000ABDE
		public bool TryResolve(ulong key, out IndexEntry entry)
		{
			return this.index.TryGetValue(key, out entry);
		}

		/// <summary>Snapshot of live keys (safe to evict while iterating).</summary>
		// Token: 0x060001A4 RID: 420 RVA: 0x0000C9ED File Offset: 0x0000ABED
		public List<ulong> Keys()
		{
			return this.index.Keys.ToList<ulong>();
		}

		/// <summary>Append one baked tile. No-op if the key already exists.</summary>
		// Token: 0x060001A5 RID: 421 RVA: 0x0000C9FF File Offset: 0x0000ABFF
		public void Append(ulong key, byte[] payload, int format, TileCodec codec = TileCodec.None)
		{
			this.Append(key, payload, format, codec, MirageArchiveFormat.Crc32(payload));
		}

		/// <summary>Append with caller-supplied CRC (computed on the bake worker, not the main thread).</summary>
		// Token: 0x060001A6 RID: 422 RVA: 0x0000CA14 File Offset: 0x0000AC14
		public void Append(ulong key, byte[] payload, int format, TileCodec codec, uint crc32)
		{
			bool flag = this.index.ContainsKey(key);
			if (!flag)
			{
				long start = MirageArchiveFormat.AlignUp(this.blobLength, 16);
				this.blob.Position = this.blobLength;
				for (long p = this.blobLength; p < start; p += 1L)
				{
					this.blob.WriteByte(0);
				}
				using (BinaryWriter bw = new BinaryWriter(this.blob, Encoding.UTF8, true))
				{
					TileHeader tileHeader = default(TileHeader);
					tileHeader.key = key;
					tileHeader.payloadLen = (uint)payload.Length;
					tileHeader.codec = codec;
					tileHeader.format = (byte)format;
					tileHeader.crc32 = crc32;
					tileHeader.Write(bw);
					bw.Write(payload);
					bw.Flush();
				}
				this.index[key] = new IndexEntry
				{
					key = key,
					offset = (ulong)start,
					length = (uint)payload.Length,
					codec = codec,
					format = (byte)format
				};
				this.blobLength = this.blob.Position;
				this.liveBytes += WebTileArchive.TileLiveCost((uint)payload.Length);
				this.indexDirty = true;
			}
		}

		/// <summary>Tombstone a tile; bytes reclaimed by the next Compact.</summary>
		// Token: 0x060001A7 RID: 423 RVA: 0x0000CB70 File Offset: 0x0000AD70
		public bool Evict(ulong key)
		{
			IndexEntry e;
			bool flag = !this.index.TryGetValue(key, out e);
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				this.index.Remove(key);
				this.liveBytes -= WebTileArchive.TileLiveCost(e.length);
				this.indexDirty = true;
				result = true;
			}
			return result;
		}

		/// <summary>Persist the in-RAM index.</summary>
		// Token: 0x060001A8 RID: 424 RVA: 0x0000CBC8 File Offset: 0x0000ADC8
		public void Flush()
		{
			bool flag = !this.indexDirty || this.blob == null;
			if (!flag)
			{
				this.blob.Flush(true);
				this.WriteIndexFile();
				this.indexDirty = false;
			}
		}

		/// <summary>Rewrite the blob with only live tiles via atomic temp-file swap.</summary>
		// Token: 0x060001A9 RID: 425 RVA: 0x0000CC0C File Offset: 0x0000AE0C
		public void Compact()
		{
			string tmpPath = this.blobPath + ".tmp";
			List<IndexEntry> compacted = this.WriteCompactedBlob(tmpPath);
			this.blob.Dispose();
			this.SwapIn(tmpPath);
			this.index.Clear();
			foreach (IndexEntry e in compacted)
			{
				this.index[e.key] = e;
			}
			this.RecomputeLiveBytes();
			this.indexDirty = true;
			this.Flush();
		}

		// Token: 0x060001AA RID: 426 RVA: 0x0000CCB8 File Offset: 0x0000AEB8
		public void Dispose()
		{
			this.Flush();
			FileStream fileStream = this.blob;
			if (fileStream != null)
			{
				fileStream.Dispose();
			}
			this.blob = null;
		}

		// Token: 0x060001AB RID: 427 RVA: 0x0000CCDC File Offset: 0x0000AEDC
		private void Open()
		{
			bool flag = !File.Exists(this.blobPath);
			if (flag)
			{
				this.Create();
			}
			else
			{
				this.blob = new FileStream(this.blobPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
				this.blobLength = this.blob.Length;
				this.ReadHeader();
				bool flag2 = !this.TryLoadIndex();
				if (flag2)
				{
					this.RebuildIndexFromBlob();
				}
				this.RecomputeLiveBytes();
			}
		}

		// Token: 0x060001AC RID: 428 RVA: 0x0000CD50 File Offset: 0x0000AF50
		private void Create()
		{
			this.blob = new FileStream(this.blobPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite);
			using (BinaryWriter bw = new BinaryWriter(this.blob, Encoding.UTF8, true))
			{
				this.WriteBlobHeader(bw);
			}
			this.blob.Flush(true);
			this.tilesStart = (this.blobLength = this.blob.Position);
			this.liveBytes = this.tilesStart;
		}

		/// <summary>Read the blob header and set <see cref="F:Mirage.VirtualTexture.WebTileArchive.tilesStart" />.</summary>
		// Token: 0x060001AD RID: 429 RVA: 0x0000CDDC File Offset: 0x0000AFDC
		private void ReadHeader()
		{
			using (BinaryReader br = new BinaryReader(this.blob, Encoding.UTF8, true))
			{
				this.blob.Position = 0L;
				BlobHeader h = BlobHeader.Read(br);
				bool flag = h.version != 1;
				if (flag)
				{
					throw new InvalidDataException(string.Format("WebTileArchive: {0} is format version {1}, ", Path.GetFileName(this.blobPath), h.version) + string.Format("expected {0}. Delete the web cache to ", 1) + "rebuild it.");
				}
				bool flag2 = h.layer != this.layer;
				if (flag2)
				{
					throw new InvalidDataException(string.Format("WebTileArchive: {0} declares layer {1}, ", Path.GetFileName(this.blobPath), h.layer) + string.Format("expected {0}.", this.layer));
				}
				this.tilesStart = this.blob.Position;
			}
		}

		// Token: 0x060001AE RID: 430 RVA: 0x0000CEE4 File Offset: 0x0000B0E4
		private void WriteBlobHeader(BinaryWriter bw)
		{
			BlobHeader blobHeader = default(BlobHeader);
			blobHeader.version = 1;
			blobHeader.layer = this.layer;
			blobHeader.format = 0;
			blobHeader.tileSize = this.tileSize;
			blobHeader.borderPx = this.borderPx;
			blobHeader.faceCount = 6;
			blobHeader.flags = 0U;
			blobHeader.Write(bw);
		}

		/// <summary>Write live tiles to a temp blob in Morton order, returning new index entries.</summary>
		// Token: 0x060001AF RID: 431 RVA: 0x0000CF4C File Offset: 0x0000B14C
		private List<IndexEntry> WriteCompactedBlob(string tmpPath)
		{
			List<IndexEntry> live = (from e in this.index.Values
			orderby e.key
			select e).ToList<IndexEntry>();
			List<IndexEntry> compacted = new List<IndexEntry>(live.Count);
			List<IndexEntry> result;
			using (FileStream tmp = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				using (BinaryWriter bw = new BinaryWriter(tmp, Encoding.UTF8, true))
				{
					this.WriteBlobHeader(bw);
					foreach (IndexEntry e2 in live)
					{
						bw.Flush();
						long start = MirageArchiveFormat.AlignUp(tmp.Position, 16);
						for (long p = tmp.Position; p < start; p += 1L)
						{
							tmp.WriteByte(0);
						}
						bw.Write(this.ReadFramed(e2));
						compacted.Add(new IndexEntry
						{
							key = e2.key,
							offset = (ulong)start,
							length = e2.length,
							codec = e2.codec,
							format = e2.format
						});
					}
					bw.Flush();
					tmp.Flush(true);
					result = compacted;
				}
			}
			return result;
		}

		// Token: 0x060001B0 RID: 432 RVA: 0x0000D104 File Offset: 0x0000B304
		private void SwapIn(string tmpPath)
		{
			bool flag = File.Exists(this.blobPath);
			if (flag)
			{
				File.Replace(tmpPath, this.blobPath, null);
			}
			else
			{
				File.Move(tmpPath, this.blobPath);
			}
			this.blob = new FileStream(this.blobPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
			this.blobLength = this.blob.Length;
			this.ReadHeader();
		}

		// Token: 0x060001B1 RID: 433 RVA: 0x0000D16C File Offset: 0x0000B36C
		private void WriteIndexFile()
		{
			List<IndexEntry> entries = (from e in this.index.Values
			orderby e.key
			select e).ToList<IndexEntry>();
			using (FileStream fs = new FileStream(this.idxPath, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				using (BinaryWriter bw = new BinaryWriter(fs))
				{
					IndexHeader indexHeader = default(IndexHeader);
					indexHeader.version = 1;
					indexHeader.layer = this.layer;
					indexHeader.level = -1;
					indexHeader.entryCount = entries.Count;
					indexHeader.blobLength = this.blobLength;
					indexHeader.Write(bw);
					foreach (IndexEntry e2 in entries)
					{
						e2.Write(bw);
					}
					bw.Flush();
					fs.Flush(true);
				}
			}
		}

		/// <summary>Load the persisted index. False when missing, stale, or unreadable.</summary>
		// Token: 0x060001B2 RID: 434 RVA: 0x0000D294 File Offset: 0x0000B494
		private bool TryLoadIndex()
		{
			bool flag = !File.Exists(this.idxPath);
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				try
				{
					using (FileStream fs = new FileStream(this.idxPath, FileMode.Open, FileAccess.Read))
					{
						using (BinaryReader br = new BinaryReader(fs))
						{
							IndexHeader h = IndexHeader.Read(br);
							bool flag2 = h.blobLength != this.blobLength;
							if (flag2)
							{
								MirageDebug.Log(string.Format("WebTileArchive[{0}]: index blobLength {1} != blob ", this.layer, h.blobLength) + string.Format("{0} — rescanning.", this.blobLength));
								result = false;
							}
							else
							{
								this.index.Clear();
								for (int i = 0; i < h.entryCount; i++)
								{
									IndexEntry e = IndexEntry.Read(br);
									this.index[e.key] = e;
								}
								result = true;
							}
						}
					}
				}
				catch (Exception e2)
				{
					MirageDebug.LogError(string.Format("WebTileArchive[{0}]: index read failed ({1}) — rescanning blob.", this.layer, e2.Message));
					result = false;
				}
			}
			return result;
		}

		/// <summary>Rebuild the index by walking blob tile headers; drops any orphan tail.</summary>
		// Token: 0x060001B3 RID: 435 RVA: 0x0000D3F0 File Offset: 0x0000B5F0
		private void RebuildIndexFromBlob()
		{
			this.index.Clear();
			using (BinaryReader br = new BinaryReader(this.blob, Encoding.UTF8, true))
			{
				byte[] scratch = Array.Empty<byte>();
				long pos = this.tilesStart;
				while (pos + 24L <= this.blobLength)
				{
					long aligned = MirageArchiveFormat.AlignUp(pos, 16);
					bool flag = aligned + 24L > this.blobLength;
					if (flag)
					{
						break;
					}
					this.blob.Position = aligned;
					TileHeader th;
					try
					{
						th = TileHeader.Read(br);
					}
					catch
					{
						break;
					}
					long payloadStart = aligned + 24L;
					bool flag2 = th.payloadLen > 2147483647U || payloadStart + (long)((ulong)th.payloadLen) > this.blobLength;
					if (flag2)
					{
						break;
					}
					int len = (int)th.payloadLen;
					bool flag3 = scratch.Length < len;
					if (flag3)
					{
						scratch = new byte[len];
					}
					bool flag4 = !this.TryReadExactly(scratch, len);
					if (flag4)
					{
						break;
					}
					bool flag5 = MirageArchiveFormat.Crc32(scratch, 0, len) == th.crc32;
					if (flag5)
					{
						this.index[th.key] = new IndexEntry
						{
							key = th.key,
							offset = (ulong)aligned,
							length = th.payloadLen,
							codec = th.codec,
							format = th.format
						};
					}
					pos = payloadStart + (long)((ulong)th.payloadLen);
				}
				this.TruncateOrphanTail();
				this.indexDirty = true;
				MirageDebug.Log(string.Format("WebTileArchive[{0}]: rebuilt index from blob — {1} live tiles.", this.layer, this.index.Count));
			}
		}

		/// <summary>Cut the blob back to the end of the last accepted tile.</summary>
		// Token: 0x060001B4 RID: 436 RVA: 0x0000D5F0 File Offset: 0x0000B7F0
		private void TruncateOrphanTail()
		{
			long clean = this.tilesStart;
			foreach (IndexEntry e in this.index.Values)
			{
				long end = (long)(e.offset + 24UL + (ulong)e.length);
				bool flag = end > clean;
				if (flag)
				{
					clean = end;
				}
			}
			bool flag2 = clean >= this.blobLength;
			if (!flag2)
			{
				this.blob.SetLength(clean);
				this.blobLength = clean;
			}
		}

		/// <summary>Read one tile's header and payload verbatim.</summary>
		// Token: 0x060001B5 RID: 437 RVA: 0x0000D694 File Offset: 0x0000B894
		private byte[] ReadFramed(IndexEntry e)
		{
			int total = (int)(24U + e.length);
			byte[] buf = new byte[total];
			this.blob.Position = (long)e.offset;
			bool flag = !this.TryReadExactly(buf, total);
			if (flag)
			{
				throw new EndOfStreamException("WebTileArchive: unexpected EOF reading framed tile.");
			}
			return buf;
		}

		// Token: 0x060001B6 RID: 438 RVA: 0x0000D6E4 File Offset: 0x0000B8E4
		private bool TryReadExactly(byte[] buffer, int count)
		{
			int i;
			for (int read = 0; read < count; read += i)
			{
				i = this.blob.Read(buffer, read, count - read);
				bool flag = i <= 0;
				if (flag)
				{
					return false;
				}
			}
			return true;
		}

		// Token: 0x060001B7 RID: 439 RVA: 0x0000D72B File Offset: 0x0000B92B
		private static long TileLiveCost(uint length)
		{
			return (long)((ulong)(length + 24U));
		}

		// Token: 0x060001B8 RID: 440 RVA: 0x0000D734 File Offset: 0x0000B934
		private void RecomputeLiveBytes()
		{
			long i = this.tilesStart;
			foreach (IndexEntry e in this.index.Values)
			{
				i += WebTileArchive.TileLiveCost(e.length);
			}
			this.liveBytes = i;
		}

		// Token: 0x04000153 RID: 339
		private readonly ArchiveLayer layer;

		// Token: 0x04000154 RID: 340
		private readonly string blobPath;

		// Token: 0x04000155 RID: 341
		private readonly string idxPath;

		// Token: 0x04000156 RID: 342
		private readonly ushort tileSize;

		// Token: 0x04000157 RID: 343
		private readonly ushort borderPx;

		// Token: 0x04000158 RID: 344
		private FileStream blob;

		// Token: 0x04000159 RID: 345
		private long tilesStart;

		// Token: 0x0400015A RID: 346
		private long blobLength;

		// Token: 0x0400015B RID: 347
		private readonly Dictionary<ulong, IndexEntry> index = new Dictionary<ulong, IndexEntry>();

		// Token: 0x0400015C RID: 348
		private bool indexDirty;

		// Token: 0x0400015D RID: 349
		private long liveBytes;
	}
}
