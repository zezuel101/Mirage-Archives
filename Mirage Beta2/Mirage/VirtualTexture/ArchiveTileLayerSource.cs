using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using KSPTextureLoader;
using Unity.Collections;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>
	/// Reads one layer's tiles from the canonical binary archive via KSPTextureLoader's
	/// <c>LoadOwnedTexture2D(config, path, offset, length)</c> seek-read-decode-upload primitive (true seeking
	/// read when <c>UseAsyncReadManager</c> is on — required for the multi-GB blobs). At construction it merges
	/// the per-level <c>.idx</c> files into one in-RAM residency map (design §6.3): enumerate levels 0..K
	/// contiguously, staleness-check each blob, fold entries in. The map IS the residency set, so
	/// <see cref="M:Mirage.VirtualTexture.ArchiveTileLayerSource.Exists(System.Int32,System.Int32,System.Int32,System.Int32)" /> is an O(1) dictionary lookup. The archive is opened read-only.
	/// </summary>
	// Token: 0x02000035 RID: 53
	public sealed class ArchiveTileLayerSource : ITileLayerSource, IDisposable
	{
		// Token: 0x1700003D RID: 61
		// (get) Token: 0x06000142 RID: 322 RVA: 0x0000A9DA File Offset: 0x00008BDA
		public bool Linear { get; }

		// Token: 0x1700003E RID: 62
		// (get) Token: 0x06000143 RID: 323 RVA: 0x0000A9E2 File Offset: 0x00008BE2
		// (set) Token: 0x06000144 RID: 324 RVA: 0x0000A9EA File Offset: 0x00008BEA
		public int MaxResidentLevel { get; private set; } = -1;

		/// <summary>Attach this layer's writable web tier. Resolve then checks canonical first, web second — the
		/// two key sets are disjoint by construction (web only ever holds keys canonical lacks). The archive's
		/// layer must match ours, or a color tile would be handed to the height atlas.</summary>
		// Token: 0x06000145 RID: 325 RVA: 0x0000A9F4 File Offset: 0x00008BF4
		public void AttachWebArchive(WebTileArchive webArchive)
		{
			bool flag = webArchive != null && webArchive.Layer != this.layer;
			if (flag)
			{
				throw new ArgumentException(string.Format("AttachWebArchive: {0} archive attached to a {1} source.", webArchive.Layer, this.layer));
			}
			this.web = webArchive;
		}

		// Token: 0x06000146 RID: 326 RVA: 0x0000AA4C File Offset: 0x00008C4C
		public ArchiveTileLayerSource(string archiveDir, ArchiveLayer layer, bool linear, int slotDim)
		{
			this.layer = layer;
			this.slotDim = slotDim;
			this.Linear = linear;
			ArchiveTileLayerSource.ForceSeekingReadBackend();
			this.MaxResidentLevel = TileArchivePaths.MergeCanonical(archiveDir, layer, this.index, this.blobByLevel);
			MirageDebug.Log(string.Format("TileArchive: {0} merged K={1}, {2} tiles resident.", layer, this.MaxResidentLevel, this.index.Count));
		}

		/// <summary>Force KSPTextureLoader onto its seeking read backend (AsyncReadManager). Its managed fallback
		/// reaches a byte offset by reading from the START of the file — so on a multi-GB archive blob every tile
		/// read scans gigabytes (a tile 2 GB in reads 2 GB to fetch ~70 KB). The archive is unusable without the
		/// seeking path, so we set <c>Config.UseAsyncReadManager = true</c> reflectively at first archive use
		/// (the field is internal). <c>true</c> is also KSPTextureLoader's own default; the managed path is a
		/// niche HDD option that the archive must not run under. Best-effort + once.</summary>
		// Token: 0x06000147 RID: 327 RVA: 0x0000AAE8 File Offset: 0x00008CE8
		private static void ForceSeekingReadBackend()
		{
			bool flag = ArchiveTileLayerSource.s_ReadBackendForced;
			if (!flag)
			{
				ArchiveTileLayerSource.s_ReadBackendForced = true;
				try
				{
					Type cfgType = Type.GetType("KSPTextureLoader.Config, KSPTextureLoader");
					object obj;
					if (cfgType == null)
					{
						obj = null;
					}
					else
					{
						PropertyInfo property = cfgType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
						obj = ((property != null) ? property.GetValue(null) : null);
					}
					object obj2;
					if ((obj2 = obj) == null)
					{
						if (cfgType == null)
						{
							obj2 = null;
						}
						else
						{
							FieldInfo field2 = cfgType.GetField("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
							obj2 = ((field2 != null) ? field2.GetValue(null) : null);
						}
					}
					object instance = obj2;
					FieldInfo field = (cfgType != null) ? cfgType.GetField("UseAsyncReadManager", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : null;
					bool flag2 = instance == null || field == null;
					if (flag2)
					{
						MirageDebug.LogError("TileArchive: couldn't reach Config.UseAsyncReadManager — if archive loads are extremely slow, set UseAsyncReadManager = true in KSPTextureLoader.cfg manually.");
					}
					else
					{
						object value = field.GetValue(instance);
						bool b;
						bool flag3;
						if (value is bool)
						{
							b = (bool)value;
							flag3 = true;
						}
						else
						{
							flag3 = false;
						}
						bool current = flag3 && b;
						bool flag4 = !current;
						if (flag4)
						{
							field.SetValue(instance, true);
							MirageDebug.Log("TileArchive: forced KSPTextureLoader UseAsyncReadManager = true (the managed read fallback scans the blob from byte 0 per tile — unusable for multi-GB archives).");
						}
					}
				}
				catch (Exception e)
				{
					MirageDebug.LogError("TileArchive: could not force the seeking read backend: " + e.Message);
				}
			}
		}

		// Token: 0x06000148 RID: 328 RVA: 0x0000AC0C File Offset: 0x00008E0C
		public bool Exists(int face, int level, int tx, int ty)
		{
			ulong key = MirageArchiveFormat.PackKey(face, level, tx, ty);
			return this.index.ContainsKey(key) || (this.web != null && this.web.Contains(key));
		}

		// Token: 0x06000149 RID: 329 RVA: 0x0000AC54 File Offset: 0x00008E54
		public TileReadHandle BeginLoad(int face, int level, int tx, int ty)
		{
			ulong key = MirageArchiveFormat.PackKey(face, level, tx, ty);
			IndexEntry e;
			bool flag = this.index.TryGetValue(key, out e) && level < this.blobByLevel.Count;
			TileReadHandle result;
			if (flag)
			{
				result = this.MakeHandle(this.blobByLevel[level], e);
			}
			else
			{
				IndexEntry we;
				bool flag2 = this.web != null && this.web.TryResolve(key, out we);
				if (flag2)
				{
					result = this.MakeHandle(this.web.BlobPath, we);
				}
				else
				{
					result = ArchiveTileLayerSource.ArchiveReadHandle.Missing;
				}
			}
			return result;
		}

		// Token: 0x0600014A RID: 330 RVA: 0x0000ACEC File Offset: 0x00008EEC
		private TileReadHandle MakeHandle(string blobPath, IndexEntry e)
		{
			Texture2DConfig texture2DConfig = default(Texture2DConfig);
			texture2DConfig.Width = this.slotDim;
			texture2DConfig.Height = this.slotDim;
			texture2DConfig.MipCount = 1;
			texture2DConfig.Format = e.format;
			texture2DConfig.Readable = false;
			texture2DConfig.Linear = this.Linear;
			Texture2DConfig config = texture2DConfig;
			long payloadOffset = (long)(e.offset + 24UL);
			bool flag = e.codec == TileCodec.None;
			TileReadHandle result;
			if (flag)
			{
				result = new ArchiveTileLayerSource.ArchiveReadHandle(TextureLoader.LoadOwnedTexture2D(config, blobPath, payloadOffset, (long)((ulong)e.length)));
			}
			else
			{
				int rawLen = MirageArchiveFormat.RawPayloadBytes((int)e.format, this.slotDim, this.slotDim);
				result = new ArchiveTileLayerSource.CompressedReadHandle(config, blobPath, payloadOffset, (int)e.length, e.codec, rawLen);
			}
			return result;
		}

		// Token: 0x0600014B RID: 331 RVA: 0x0000ADB3 File Offset: 0x00008FB3
		public void Dispose()
		{
		}

		// Token: 0x04000114 RID: 276
		private readonly ArchiveLayer layer;

		// Token: 0x04000115 RID: 277
		private readonly int slotDim;

		// Token: 0x04000116 RID: 278
		private readonly Dictionary<ulong, IndexEntry> index = new Dictionary<ulong, IndexEntry>();

		// Token: 0x04000117 RID: 279
		private readonly List<string> blobByLevel = new List<string>();

		// Token: 0x04000118 RID: 280
		private WebTileArchive web;

		// Token: 0x0400011B RID: 283
		private static bool s_ReadBackendForced;

		// Token: 0x020000B3 RID: 179
		private sealed class ArchiveReadHandle : TileReadHandle
		{
			// Token: 0x060004C4 RID: 1220 RVA: 0x00020D8A File Offset: 0x0001EF8A
			public ArchiveReadHandle(TextureLoadTask<Texture2D> task)
			{
				this.task = task;
			}

			// Token: 0x17000107 RID: 263
			// (get) Token: 0x060004C5 RID: 1221 RVA: 0x00020D9A File Offset: 0x0001EF9A
			public override bool IsComplete
			{
				get
				{
					return this.task == null || this.task.IsComplete;
				}
			}

			// Token: 0x17000108 RID: 264
			// (get) Token: 0x060004C6 RID: 1222 RVA: 0x00020DB2 File Offset: 0x0001EFB2
			public override bool IsFaulted
			{
				get
				{
					return this.task == null;
				}
			}

			// Token: 0x060004C7 RID: 1223 RVA: 0x00020DC0 File Offset: 0x0001EFC0
			public override Texture2D GetTexture()
			{
				bool flag = this.task == null;
				if (flag)
				{
					throw new InvalidOperationException("ArchiveReadHandle: tile not present in archive.");
				}
				this.result = this.task.GetTexture();
				return this.result;
			}

			// Token: 0x060004C8 RID: 1224 RVA: 0x00020E04 File Offset: 0x0001F004
			public override void Dispose()
			{
				bool flag = this.result != null;
				if (flag)
				{
					Object.Destroy(this.result);
					this.result = null;
				}
			}

			/// <summary>Shared sentinel for a tile the archive does not contain: complete + faulted.</summary>
			// Token: 0x040004B5 RID: 1205
			public static readonly ArchiveTileLayerSource.ArchiveReadHandle Missing = new ArchiveTileLayerSource.ArchiveReadHandle(null);

			// Token: 0x040004B6 RID: 1206
			private readonly TextureLoadTask<Texture2D> task;

			// Token: 0x040004B7 RID: 1207
			private Texture2D result;
		}

		/// <summary>
		/// Read + decompress a compressed tile off-thread, then upload the raw bytes via the NativeArray owned-
		/// texture overload. Two-phase, polled: phase 1 (decodeTask) reads the stored bytes with a real seeking
		/// FileStream and LZ4-decodes (+ un-plane-split for height) into a raw buffer; phase 2 (loadTask) is
		/// KSPTextureLoader's native upload of those bytes. Started lazily from the poll so nothing blocks.
		/// </summary>
		// Token: 0x020000B4 RID: 180
		private sealed class CompressedReadHandle : TileReadHandle
		{
			// Token: 0x060004CA RID: 1226 RVA: 0x00020E44 File Offset: 0x0001F044
			public CompressedReadHandle(Texture2DConfig config, string blobPath, long payloadOffset, int storedLen, TileCodec codec, int rawLen)
			{
				this.config = config;
				this.decodeTask = Task.Run<byte[]>(() => ArchiveTileLayerSource.CompressedReadHandle.ReadAndDecode(blobPath, payloadOffset, storedLen, codec, rawLen));
			}

			// Token: 0x060004CB RID: 1227 RVA: 0x00020EA4 File Offset: 0x0001F0A4
			private static byte[] ReadAndDecode(string blobPath, long payloadOffset, int storedLen, TileCodec codec, int rawLen)
			{
				byte[] stored = new byte[storedLen];
				using (FileStream fs = new FileStream(blobPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536))
				{
					fs.Position = payloadOffset;
					int i;
					for (int read = 0; read < storedLen; read += i)
					{
						i = fs.Read(stored, read, storedLen - read);
						bool flag = i <= 0;
						if (flag)
						{
							throw new EndOfStreamException("archive: short read for compressed tile");
						}
					}
				}
				byte[] raw = new byte[rawLen];
				MirageArchiveFormat.DecodeTilePayload(codec, stored, storedLen, raw, rawLen);
				return raw;
			}

			/// <summary>Kick off phase 2 — the native upload of the decoded bytes. We own <c>data</c> and must keep
			/// it alive until the upload completes (disposed in Dispose, after GetTexture).</summary>
			// Token: 0x060004CC RID: 1228 RVA: 0x00020F48 File Offset: 0x0001F148
			private void StartUpload(byte[] raw)
			{
				this.data = new NativeArray<byte>(raw, 4);
				this.loadTask = TextureLoader.LoadOwnedTexture2D<byte>(this.config, this.data);
			}

			// Token: 0x17000109 RID: 265
			// (get) Token: 0x060004CD RID: 1229 RVA: 0x00020F70 File Offset: 0x0001F170
			public override bool IsComplete
			{
				get
				{
					bool flag = this.loadTask != null;
					bool flag2;
					if (flag)
					{
						flag2 = this.loadTask.IsComplete;
					}
					else
					{
						bool flag3 = !this.decodeTask.IsCompleted;
						if (flag3)
						{
							flag2 = false;
						}
						else
						{
							bool isFaulted = this.decodeTask.IsFaulted;
							if (isFaulted)
							{
								this.faulted = true;
								flag2 = true;
							}
							else
							{
								this.StartUpload(this.decodeTask.Result);
								flag2 = this.loadTask.IsComplete;
							}
						}
					}
					return flag2;
				}
			}

			// Token: 0x1700010A RID: 266
			// (get) Token: 0x060004CE RID: 1230 RVA: 0x00020FEB File Offset: 0x0001F1EB
			public override bool IsFaulted
			{
				get
				{
					return this.faulted || (this.loadTask == null && this.decodeTask.IsFaulted);
				}
			}

			// Token: 0x060004CF RID: 1231 RVA: 0x00021010 File Offset: 0x0001F210
			public override Texture2D GetTexture()
			{
				bool flag = this.loadTask == null;
				if (flag)
				{
					byte[] raw;
					try
					{
						raw = this.decodeTask.Result;
					}
					catch (AggregateException ae)
					{
						this.faulted = true;
						Exception inner = ae.GetBaseException();
						throw new InvalidOperationException("archive: tile decode failed: " + inner.GetType().Name + ": " + inner.Message, inner);
					}
					this.StartUpload(raw);
				}
				this.result = this.loadTask.GetTexture();
				return this.result;
			}

			// Token: 0x060004D0 RID: 1232 RVA: 0x000210AC File Offset: 0x0001F2AC
			public override void Dispose()
			{
				bool isCreated = this.data.IsCreated;
				if (isCreated)
				{
					this.data.Dispose();
				}
				bool flag = this.result != null;
				if (flag)
				{
					Object.Destroy(this.result);
					this.result = null;
				}
			}

			// Token: 0x040004B8 RID: 1208
			private readonly Texture2DConfig config;

			// Token: 0x040004B9 RID: 1209
			private readonly Task<byte[]> decodeTask;

			// Token: 0x040004BA RID: 1210
			private TextureLoadTask<Texture2D> loadTask;

			// Token: 0x040004BB RID: 1211
			private NativeArray<byte> data;

			// Token: 0x040004BC RID: 1212
			private Texture2D result;

			// Token: 0x040004BD RID: 1213
			private bool faulted;
		}
	}
}
