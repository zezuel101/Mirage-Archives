using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Mirage.VirtualTexture
{
	/// <summary>
	/// The runtime-generated, read/write tier for ONE layer (design §7). A single append blob
	/// (<c>web.&lt;layer&gt;.bin</c>) holding baked-as-you-fly tiles that canonical does NOT ship, plus an in-RAM
	/// index that is a rebuildable *cache* of the self-describing blob. These are the only writable handles in
	/// the tile system; canonical is opened read-only, so an offset bug here can cost at most a re-fetch.
	///
	/// One instance per layer: color, height and normal each get their own blob/index pair rather than sharing
	/// one. The Morton key carries no layer bits (§4), so a shared blob would need a layer field in the key just
	/// to keep the three sets apart; per-layer files also mean a corrupt or deleted height cache can't take color
	/// down with it, and each layer can be disk-capped and compacted on its own schedule.
	///
	/// Mutation discipline:
	///   - Steady state is append-only: the write position is definitionally EOF, computed by the container,
	///     never by tile arithmetic — which is what makes a wrong-offset write nearly impossible.
	///   - <see cref="M:Mirage.VirtualTexture.WebTileArchive.Append(System.UInt64,System.Byte[],System.Int32,Mirage.VirtualTexture.TileCodec)" /> writes the payload and does NOT fsync — it runs on the main thread, and a sync
	///     per layer per tile measured 3-6 ms typical / 41 ms worst, i.e. a visible frame hitch bought for a
	///     cache that can simply be re-baked. <see cref="M:Mirage.VirtualTexture.WebTileArchive.Flush" /> fsyncs the blob and then writes the index, so
	///     a persisted index still never references bytes that never reached the disk. On load, a
	///     <c>blobLength</c> mismatch triggers a rescan of the tile headers to rebuild the index from the blob,
	///     dropping anything that fails CRC and truncating the torn tail — so a crash (with or without the
	///     sync) recovers the tiles that landed rather than losing the file (the blob is authoritative).
	///   - <see cref="M:Mirage.VirtualTexture.WebTileArchive.Evict(System.UInt64)" /> tombstones (removes the in-RAM entry); the bytes stay until <see cref="M:Mirage.VirtualTexture.WebTileArchive.Compact" />.
	///   - <see cref="M:Mirage.VirtualTexture.WebTileArchive.Compact" /> is the only rewrite: it writes a fresh Morton-ordered tmp of the live tiles,
	///     fsyncs, atomically swaps it in, and rebuilds the index — all-or-nothing at the filesystem level.
	/// </summary>
	// Token: 0x02000050 RID: 80
	public sealed class WebTileArchive : IDisposable
	{
		// Token: 0x17000066 RID: 102
		// (get) Token: 0x0600023B RID: 571 RVA: 0x00013154 File Offset: 0x00011354
		public ArchiveLayer Layer
		{
			get
			{
				return this.layer;
			}
		}

		// Token: 0x17000067 RID: 103
		// (get) Token: 0x0600023C RID: 572 RVA: 0x0001315C File Offset: 0x0001135C
		public string BlobPath
		{
			get
			{
				return this.blobPath;
			}
		}

		// Token: 0x17000068 RID: 104
		// (get) Token: 0x0600023D RID: 573 RVA: 0x00013164 File Offset: 0x00011364
		public int Count
		{
			get
			{
				return this.index.Count;
			}
		}

		/// <summary>Bytes the blob occupies on disk right now, INCLUDING tombstoned tiles that
		/// <see cref="M:Mirage.VirtualTexture.WebTileArchive.Compact" /> has not reclaimed yet. This is the number a disk cap must be enforced against —
		/// it is what the user's drive actually gives up.</summary>
		// Token: 0x17000069 RID: 105
		// (get) Token: 0x0600023E RID: 574 RVA: 0x00013171 File Offset: 0x00011371
		public long PhysicalBytes
		{
			get
			{
				return this.blobLength;
			}
		}

		/// <summary>Bytes the live tiles would occupy after a <see cref="M:Mirage.VirtualTexture.WebTileArchive.Compact" /> — payloads plus their
		/// headers, ignoring inter-tile alignment padding (≤15 B/tile, under 0.02% at a 70 KB tile).
		/// <c>PhysicalBytes − LiveBytes</c> is the reclaimable garbage, i.e. what compacting would buy. O(1): a
		/// maintained counter, not a scan — see <see cref="F:Mirage.VirtualTexture.WebTileArchive.liveBytes" />.</summary>
		// Token: 0x1700006A RID: 106
		// (get) Token: 0x0600023F RID: 575 RVA: 0x00013179 File Offset: 0x00011379
		public long LiveBytes
		{
			get
			{
				return this.liveBytes;
			}
		}

		/// <summary>One live tile's contribution to <see cref="F:Mirage.VirtualTexture.WebTileArchive.liveBytes" />: its payload plus its fixed header.</summary>
		// Token: 0x06000240 RID: 576 RVA: 0x00013181 File Offset: 0x00011381
		private static long TileLiveCost(uint length)
		{
			return (long)((ulong)(length + 24U));
		}

		/// <summary>Recompute <see cref="F:Mirage.VirtualTexture.WebTileArchive.liveBytes" /> from the current index. Only for the paths that replace the
		/// whole index wholesale (load, rebuild, compact); the per-tile paths keep it in step incrementally.</summary>
		// Token: 0x06000241 RID: 577 RVA: 0x00013188 File Offset: 0x00011388
		private void RecomputeLiveBytes()
		{
			long i = this.tilesStart;
			foreach (IndexEntry e in this.index.Values)
			{
				i += WebTileArchive.TileLiveCost(e.length);
			}
			this.liveBytes = i;
		}

		/// <summary>Every key currently live in this tier. Snapshot — the caller may evict while iterating.</summary>
		// Token: 0x06000242 RID: 578 RVA: 0x000131F8 File Offset: 0x000113F8
		public List<ulong> Keys()
		{
			return new List<ulong>(this.index.Keys);
		}

		// Token: 0x06000243 RID: 579 RVA: 0x0001320C File Offset: 0x0001140C
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

		// Token: 0x06000244 RID: 580 RVA: 0x0001326C File Offset: 0x0001146C
		private void Open()
		{
			bool flag = !File.Exists(this.blobPath);
			if (flag)
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
			else
			{
				this.blob = new FileStream(this.blobPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
				this.blobLength = this.blob.Length;
				using (BinaryReader br = new BinaryReader(this.blob, Encoding.UTF8, true))
				{
					this.blob.Position = 0L;
					BlobHeader h = BlobHeader.Read(br);
					bool flag2 = h.layer != this.layer;
					if (flag2)
					{
						throw new InvalidDataException(string.Format("WebTileArchive: {0} declares layer {1}, expected {2}.", Path.GetFileName(this.blobPath), h.layer, this.layer));
					}
					this.tilesStart = this.blob.Position;
				}
				bool flag3 = !this.TryLoadIndex();
				if (flag3)
				{
					this.RebuildIndexFromBlob();
				}
				this.RecomputeLiveBytes();
			}
		}

		// Token: 0x06000245 RID: 581 RVA: 0x000133F0 File Offset: 0x000115F0
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

		// Token: 0x06000246 RID: 582 RVA: 0x00013455 File Offset: 0x00011655
		public bool Contains(ulong key)
		{
			return this.index.ContainsKey(key);
		}

		// Token: 0x06000247 RID: 583 RVA: 0x00013463 File Offset: 0x00011663
		public bool TryResolve(ulong key, out IndexEntry entry)
		{
			return this.index.TryGetValue(key, out entry);
		}

		/// <summary>Append one baked tile. No-op if the key is already present (append-only; callers must ensure
		/// canonical doesn't already own the key — the disjoint-set precedence rule, §5). The payload is stored
		/// verbatim and labelled <paramref name="codec" />; use <see cref="M:Mirage.VirtualTexture.MirageArchiveFormat.EncodeForWeb(System.Byte[],System.Int32,System.Int32,System.Int32,Mirage.VirtualTexture.TileCodec@)" /> to
		/// produce that pair from raw texels.</summary>
		// Token: 0x06000248 RID: 584 RVA: 0x00013472 File Offset: 0x00011672
		public void Append(ulong key, byte[] payload, int format, TileCodec codec = TileCodec.None)
		{
			this.Append(key, payload, format, codec, MirageArchiveFormat.Crc32(payload));
		}

		/// <summary>As <see cref="M:Mirage.VirtualTexture.WebTileArchive.Append(System.UInt64,System.Byte[],System.Int32,Mirage.VirtualTexture.TileCodec)" />, but with the payload CRC supplied by the
		/// caller. Web ingest computes it on the bake worker (see <c>CubeTileBaker.EncodePayloadsForCommit</c>) so
		/// the main-thread commit does no per-byte work.</summary>
		// Token: 0x06000249 RID: 585 RVA: 0x00013488 File Offset: 0x00011688
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

		/// <summary>Tombstone a tile (disk-cap eviction). Bytes are reclaimed by the next <see cref="M:Mirage.VirtualTexture.WebTileArchive.Compact" />.</summary>
		// Token: 0x0600024A RID: 586 RVA: 0x000135E4 File Offset: 0x000117E4
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

		/// <summary>Persist the in-RAM index to <c>web.&lt;layer&gt;.idx</c> (a cache of the blob).</summary>
		// Token: 0x0600024B RID: 587 RVA: 0x0001363C File Offset: 0x0001183C
		public void Flush()
		{
			bool flag = !this.indexDirty;
			if (!flag)
			{
				this.blob.Flush(true);
				this.WriteIndexFile();
				this.indexDirty = false;
			}
		}

		/// <summary>Rewrite the blob with only the live tiles, Morton-ordered (promoting read locality), via a
		/// tmp + atomic swap. An interrupted compaction leaves the original intact (the tmp is discarded).</summary>
		// Token: 0x0600024C RID: 588 RVA: 0x00013674 File Offset: 0x00011874
		public void Compact()
		{
			string tmpPath = this.blobPath + ".tmp";
			List<IndexEntry> live = (from e in this.index.Values
			orderby e.key
			select e).ToList<IndexEntry>();
			List<IndexEntry> newEntries = new List<IndexEntry>(live.Count);
			using (FileStream tmp = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				using (BinaryWriter bw = new BinaryWriter(tmp, Encoding.UTF8, true))
				{
					this.WriteBlobHeader(bw);
					foreach (IndexEntry e3 in live)
					{
						bw.Flush();
						long aligned = MirageArchiveFormat.AlignUp(tmp.Position, 16);
						for (long p = tmp.Position; p < aligned; p += 1L)
						{
							tmp.WriteByte(0);
						}
						long start = tmp.Position;
						byte[] framed = this.ReadFramed(e3);
						bw.Write(framed);
						newEntries.Add(new IndexEntry
						{
							key = e3.key,
							offset = (ulong)start,
							length = e3.length,
							codec = e3.codec,
							format = e3.format
						});
					}
					bw.Flush();
					tmp.Flush(true);
				}
			}
			this.blob.Dispose();
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
			this.blob.Position = 0L;
			using (BinaryReader br = new BinaryReader(this.blob, Encoding.UTF8, true))
			{
				BlobHeader.Read(br);
				this.tilesStart = this.blob.Position;
			}
			this.index.Clear();
			foreach (IndexEntry e2 in newEntries)
			{
				this.index[e2.key] = e2;
			}
			this.RecomputeLiveBytes();
			this.indexDirty = true;
			this.Flush();
		}

		// Token: 0x0600024D RID: 589 RVA: 0x00013990 File Offset: 0x00011B90
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

		// Token: 0x0600024E RID: 590 RVA: 0x00013AB8 File Offset: 0x00011CB8
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
								MirageDebug.Log(string.Format("WebTileArchive[{0}]: index blobLength {1} != blob {2} — rescanning.", this.layer, h.blobLength, this.blobLength));
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

		/// <summary>Rebuild the index by walking the self-describing tile headers in the blob. Recovers from a
		/// missing/stale index and reclaims a crash's orphan tail (a torn final tile fails CRC and is dropped).</summary>
		// Token: 0x0600024F RID: 591 RVA: 0x00013C04 File Offset: 0x00011E04
		private void RebuildIndexFromBlob()
		{
			this.index.Clear();
			long pos = this.tilesStart;
			using (BinaryReader br = new BinaryReader(this.blob, Encoding.UTF8, true))
			{
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
					bool flag2 = payloadStart + (long)((ulong)th.payloadLen) > this.blobLength;
					if (flag2)
					{
						break;
					}
					byte[] payload = br.ReadBytes((int)th.payloadLen);
					bool flag3 = MirageArchiveFormat.Crc32(payload) == th.crc32;
					if (flag3)
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
				long num;
				if (this.index.Count != 0)
				{
					num = this.index.Values.Max((IndexEntry e) => (long)(e.offset + 24UL + (ulong)e.length));
				}
				else
				{
					num = this.tilesStart;
				}
				long clean = num;
				bool flag4 = clean < this.blobLength;
				if (flag4)
				{
					this.blob.SetLength(clean);
					this.blobLength = clean;
				}
				this.indexDirty = true;
				MirageDebug.Log(string.Format("WebTileArchive[{0}]: rebuilt index from blob — {1} live tiles.", this.layer, this.index.Count));
			}
		}

		// Token: 0x06000250 RID: 592 RVA: 0x00013E28 File Offset: 0x00012028
		private byte[] ReadFramed(IndexEntry e)
		{
			int total = (int)(24U + e.length);
			byte[] buf = new byte[total];
			this.blob.Position = (long)e.offset;
			int i;
			for (int read = 0; read < total; read += i)
			{
				i = this.blob.Read(buf, read, total - read);
				bool flag = i <= 0;
				if (flag)
				{
					throw new EndOfStreamException("WebTileArchive: unexpected EOF reading framed tile.");
				}
			}
			return buf;
		}

		// Token: 0x06000251 RID: 593 RVA: 0x00013E9D File Offset: 0x0001209D
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

		// Token: 0x0400020B RID: 523
		private readonly ArchiveLayer layer;

		// Token: 0x0400020C RID: 524
		private readonly string blobPath;

		// Token: 0x0400020D RID: 525
		private readonly string idxPath;

		// Token: 0x0400020E RID: 526
		private readonly ushort tileSize;

		// Token: 0x0400020F RID: 527
		private readonly ushort borderPx;

		// Token: 0x04000210 RID: 528
		private FileStream blob;

		// Token: 0x04000211 RID: 529
		private long tilesStart;

		// Token: 0x04000212 RID: 530
		private long blobLength;

		// Token: 0x04000213 RID: 531
		private readonly Dictionary<ulong, IndexEntry> index = new Dictionary<ulong, IndexEntry>();

		// Token: 0x04000214 RID: 532
		private bool indexDirty;

		// Token: 0x04000215 RID: 533
		private long liveBytes;
	}
}
