using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>Coarse-level loading and pinning at body load.</summary>
	/// <summary>VT cache: shared indirection plus up to four payload atlases.</summary>
	/// <summary>Indirection consistency checks, off the rendering path.</summary>
	/// <summary>Coarse directory: page table for levels 0..DirectoryLevel with dirty-rect upload.</summary>
	/// <summary>Fine indirection tier: paged blocks in FineAtlas, free list + LRU.</summary>
	/// <summary>
	/// Bit layout of indirection texels and tile keys. Mirrored by MirageVTUniforms.cginc.
	/// <code>
	///   coarse: [0..5] slotX | [6..11] slotY | [12..14] fallbackLvl | [15] resident
	///           [16] hasBlock | [17..22] blockX | [23..28] blockY | [29..31] spare
	///   fine:   [0..5] slotX | [6..11] slotY | [12..15] fallbackLvl | [16] resident | [17..31] spare
	/// </code>
	/// </summary>
	// Token: 0x02000052 RID: 82
	public class TileCache : IDisposable
	{
		/// <summary>Load and pin levels 0..<paramref name="pinnedMaxLevel" />, blocking.</summary>
		// Token: 0x060001F2 RID: 498 RVA: 0x0000E728 File Offset: 0x0000C928
		public void BootstrapPinnedLevels(int pinnedMaxLevel)
		{
			int coarseMaxLevel = Mathf.Clamp(pinnedMaxLevel, 0, this.maxLevel);
			Texture2D[] perLayer = new Texture2D[this.atlases.Count];
			TileReadHandle[] handles = new TileReadHandle[this.atlases.Count];
			int loaded = 0;
			int skipped = 0;
			for (int level = 0; level <= coarseMaxLevel; level++)
			{
				int side = 1 << level;
				for (int face = 0; face < 6; face++)
				{
					for (int ty = 0; ty < side; ty++)
					{
						for (int tx = 0; tx < side; tx++)
						{
							bool flag = !this.atlases.AllHave(face, level, tx, ty);
							if (flag)
							{
								skipped++;
							}
							else
							{
								try
								{
									this.BeginLoadAll(face, level, tx, ty, handles);
									bool ok = this.CollectAll(handles, perLayer, face, level, tx, ty) && this.UploadPinned(face, level, tx, ty, perLayer);
									bool flag2 = ok;
									if (flag2)
									{
										loaded++;
									}
									else
									{
										skipped++;
									}
								}
								finally
								{
									this.DisposeAll(handles);
								}
							}
						}
					}
				}
			}
			this.ApplyPageTable();
			MirageDebug.Log(string.Format("TileCache: bootstrapped {0} coarse tiles ({1} missing or failed) across ", loaded, skipped) + string.Format("{0} layer(s)", this.atlases.Count));
		}

		/// <summary>Async variant, spread over frames. Fires <paramref name="onLevelComplete" /> per level.</summary>
		// Token: 0x060001F3 RID: 499 RVA: 0x0000E8C0 File Offset: 0x0000CAC0
		public IEnumerator BootstrapPinnedLevelsAsync(int pinnedMaxLevel, int uploadsPerFrame = 8, Action<int> onLevelComplete = null)
		{
			TileCache.<BootstrapPinnedLevelsAsync>d__1 <BootstrapPinnedLevelsAsync>d__ = new TileCache.<BootstrapPinnedLevelsAsync>d__1(0);
			<BootstrapPinnedLevelsAsync>d__.<>4__this = this;
			<BootstrapPinnedLevelsAsync>d__.pinnedMaxLevel = pinnedMaxLevel;
			<BootstrapPinnedLevelsAsync>d__.uploadsPerFrame = uploadsPerFrame;
			<BootstrapPinnedLevelsAsync>d__.onLevelComplete = onLevelComplete;
			return <BootstrapPinnedLevelsAsync>d__;
		}

		// Token: 0x060001F4 RID: 500 RVA: 0x0000E8E4 File Offset: 0x0000CAE4
		private void BeginLoadAll(int face, int level, int tx, int ty, TileReadHandle[] handles)
		{
			for (int li = 0; li < this.atlases.Count; li++)
			{
				handles[li] = (this.atlases.CoversLevel(li, level) ? this.atlases[li].source.BeginLoad(face, level, tx, ty) : SkippedReadHandle.Instance);
			}
		}

		// Token: 0x060001F5 RID: 501 RVA: 0x0000E944 File Offset: 0x0000CB44
		private bool CollectAll(TileReadHandle[] handles, Texture2D[] perLayer, int face, int level, int tx, int ty)
		{
			bool ok = true;
			for (int li = 0; li < this.atlases.Count; li++)
			{
				try
				{
					perLayer[li] = handles[li].GetTexture();
				}
				catch (Exception e)
				{
					MirageDebug.LogError(string.Format("TileCache: bootstrap {0} L{1} f{2} {3},{4} failed: ", new object[]
					{
						this.atlases[li].id,
						level,
						face,
						tx,
						ty
					}) + e.Message);
					ok = false;
				}
			}
			return ok;
		}

		// Token: 0x060001F6 RID: 502 RVA: 0x0000EA04 File Offset: 0x0000CC04
		private void DisposeAll(TileReadHandle[] handles)
		{
			for (int li = 0; li < this.atlases.Count; li++)
			{
				TileReadHandle tileReadHandle = handles[li];
				if (tileReadHandle != null)
				{
					tileReadHandle.Dispose();
				}
				handles[li] = null;
			}
		}

		// Token: 0x060001F7 RID: 503 RVA: 0x0000EA44 File Offset: 0x0000CC44
		private bool UploadPinned(int face, int level, int tx, int ty, Texture2D[] perLayer)
		{
			bool flag = !this.atlases.Accept(perLayer, face, level, tx, ty);
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				int slot = this.slots.TakeFree();
				bool flag2 = slot < 0;
				if (flag2)
				{
					MirageDebug.LogError(string.Format("TileCache: atlas full during bootstrap at L{0} f{1} {2},{3}", new object[]
					{
						level,
						face,
						tx,
						ty
					}));
					result = false;
				}
				else
				{
					int slotX = slot % this.SlotsPerRow;
					int slotY = slot / this.SlotsPerRow;
					this.atlases.CopyToSlot(perLayer, slotX, slotY);
					this.slotMap[TileCache.PackKey(face, level, tx, ty)] = slot;
					this.slots.Pin(slot);
					this.PaintResident(face, level, tx, ty, slotX, slotY);
					result = true;
				}
			}
			return result;
		}

		// Token: 0x17000058 RID: 88
		// (get) Token: 0x060001F8 RID: 504 RVA: 0x0000EB25 File Offset: 0x0000CD25
		// (set) Token: 0x060001F9 RID: 505 RVA: 0x0000EB2D File Offset: 0x0000CD2D
		public Texture2D PageTable { get; private set; }

		// Token: 0x17000059 RID: 89
		// (get) Token: 0x060001FA RID: 506 RVA: 0x0000EB36 File Offset: 0x0000CD36
		public int SlotSize
		{
			get
			{
				return this.tileSize + 2 * this.borderPx;
			}
		}

		// Token: 0x1700005A RID: 90
		// (get) Token: 0x060001FB RID: 507 RVA: 0x0000EB47 File Offset: 0x0000CD47
		public int SlotsPerRow
		{
			get
			{
				return this.atlasSize / this.SlotSize;
			}
		}

		// Token: 0x1700005B RID: 91
		// (get) Token: 0x060001FC RID: 508 RVA: 0x0000EB56 File Offset: 0x0000CD56
		public int TotalSlots
		{
			get
			{
				return this.SlotsPerRow * this.SlotsPerRow;
			}
		}

		/// <summary>Structural coarse/fine split level in the indirection.</summary>
		// Token: 0x1700005C RID: 92
		// (get) Token: 0x060001FD RID: 509 RVA: 0x0000EB65 File Offset: 0x0000CD65
		public int DirectoryLevel { get; }

		/// <summary>Levels served by fine blocks, DirectoryLevel+1 .. maxLevel. 0 = none.</summary>
		// Token: 0x1700005D RID: 93
		// (get) Token: 0x060001FE RID: 510 RVA: 0x0000EB6D File Offset: 0x0000CD6D
		public int BlockDepth
		{
			get
			{
				return this.maxLevel - this.DirectoryLevel;
			}
		}

		/// <summary>Texel columns per cube face in the directory (6 faces laid side by side).</summary>
		// Token: 0x1700005E RID: 94
		// (get) Token: 0x060001FF RID: 511 RVA: 0x0000EB7C File Offset: 0x0000CD7C
		private int DirFaceStride
		{
			get
			{
				return 1 << this.DirectoryLevel;
			}
		}

		// Token: 0x1700005F RID: 95
		// (get) Token: 0x06000200 RID: 512 RVA: 0x0000EB89 File Offset: 0x0000CD89
		public IReadOnlyList<TileLayerAtlases.Layer> Layers
		{
			get
			{
				return this.atlases.All;
			}
		}

		// Token: 0x17000060 RID: 96
		// (get) Token: 0x06000201 RID: 513 RVA: 0x0000EB96 File Offset: 0x0000CD96
		public int LayerCount
		{
			get
			{
				return this.atlases.Count;
			}
		}

		// Token: 0x06000202 RID: 514 RVA: 0x0000EBA4 File Offset: 0x0000CDA4
		public TileCache(int atlasSize, int tileSize, int borderPx, int maxLevel, int directoryLevel = 7)
		{
			bool flag = atlasSize <= 0 || tileSize <= 0 || maxLevel < 0;
			if (flag)
			{
				throw new ArgumentException(string.Format("TileCache: invalid args atlasSize={0} tileSize={1} ", atlasSize, tileSize) + string.Format("maxLevel={0}", maxLevel));
			}
			this.tileSize = tileSize;
			this.borderPx = borderPx;
			this.maxLevel = maxLevel;
			this.atlasSize = TileCache.ClampAtlasSize(atlasSize, tileSize + 2 * borderPx);
			this.DirectoryLevel = TileCache.ResolveDirectoryLevel(directoryLevel, maxLevel);
			bool flag2 = maxLevel > 15;
			if (flag2)
			{
				MirageDebug.LogError(string.Format("TileCache: maxLevel={0} exceeds the {1} the fine ", maxLevel, 15) + "texel's fallback-level field can hold.");
			}
			this.atlases = new TileLayerAtlases(this.atlasSize, this.SlotSize);
			this.slots = new TileSlotAllocator(this.TotalSlots);
			this.CreateDirectory();
			this.CreateFineAtlas();
			this.ApplyPageTable();
		}

		// Token: 0x06000203 RID: 515 RVA: 0x0000ECE8 File Offset: 0x0000CEE8
		private static int ResolveDirectoryLevel(int requested, int maxLevel)
		{
			int ceiling = Mathf.Min(maxLevel, 7);
			int level = Mathf.Clamp(requested, 0, ceiling);
			int asked = level;
			while (level < ceiling && TileCache.FineAtlasBytesForDepth(maxLevel - level) > 134217728L)
			{
				level++;
			}
			bool flag = level != asked;
			if (flag)
			{
				MirageDebug.LogError(string.Concat(new string[]
				{
					string.Format("TileCache: canonicalMaxLevel {0} with maxLevel {1} needs a ", asked, maxLevel),
					string.Format("{0} MB fine indirection atlas ", TileCache.FineAtlasBytesForDepth(maxLevel - asked) / 1048576L),
					string.Format("(depth {0}), over the {1} MB cap. ", maxLevel - asked, 128L),
					string.Format("Raised the coarse/fine split to {0} ", level),
					string.Format("({0} MB). Lowering ", TileCache.FineAtlasBytesForDepth(maxLevel - level) / 1048576L),
					"canonicalMaxLevel trades a small flat directory for a quadratically larger fine tier; keep it near the canonical archive's depth."
				}));
			}
			else
			{
				bool flag2 = TileCache.FineAtlasBytesForDepth(maxLevel - level) > 134217728L;
				if (flag2)
				{
					MirageDebug.LogError(string.Format("TileCache: even at the deepest addressable directory ({0}), maxLevel ", level) + string.Format("{0} needs a {1} MB fine ", maxLevel, TileCache.FineAtlasBytesForDepth(maxLevel - level) / 1048576L) + "indirection atlas — the pyramid is too deep for the current fine-tier format. Expect heavy GPU upload cost.");
				}
			}
			return level;
		}

		// Token: 0x06000204 RID: 516 RVA: 0x0000EE44 File Offset: 0x0000D044
		private static int ClampAtlasSize(int atlasSize, int slotSize)
		{
			int slotsPerRow = atlasSize / slotSize;
			bool flag = slotsPerRow - 1 <= 63;
			int result;
			if (flag)
			{
				result = atlasSize;
			}
			else
			{
				int usable = 64 * slotSize;
				bool flag2 = usable > 16384;
				if (flag2)
				{
					usable = 16384;
					MirageDebug.LogError(string.Format("TileCache: atlasSize={0} is greater than Unity's maximum texture ", atlasSize) + "size (16384). Lower atlasSize to hide this.");
				}
				else
				{
					MirageDebug.LogError(string.Concat(new string[]
					{
						string.Format("TileCache: atlasSize={0} gives {1} slots per axis, but the ", atlasSize, slotsPerRow),
						string.Format("page texel's slot field stops at {0}. Clamping to {1} ", 63, usable),
						string.Format("({0} slots/axis = ", 64),
						string.Format("{0} tiles). Lower atlasSize to hide ", 4096),
						"this, or widen slotX/slotY here and in MirageVTUniforms.cginc."
					}));
				}
				result = usable;
			}
			return result;
		}

		/// <summary>Register a payload layer. <paramref name="maxLevel" /> bounds the levels it holds
		/// tiles for; the default reaches everywhere. Call before <see cref="M:Mirage.VirtualTexture.TileCache.BootstrapPinnedLevels(System.Int32)" />.
		/// </summary>
		// Token: 0x06000205 RID: 517 RVA: 0x0000EF26 File Offset: 0x0000D126
		public void AddLayer(VTLayer id, string uniformPrefix, ITileLayerSource source, int maxLevel = 2147483647)
		{
			this.atlases.Add(id, uniformPrefix, source, maxLevel);
		}

		/// <summary>Does the layer reach this level, or does it drop out of the group here?</summary>
		// Token: 0x06000206 RID: 518 RVA: 0x0000EF39 File Offset: 0x0000D139
		public bool LayerCoversLevel(int layerIndex, int level)
		{
			return this.atlases.CoversLevel(layerIndex, level);
		}

		/// <summary>Does every layer that reaches this level hold the tile on disk? Layers move in
		/// lockstep, so one missing layer disqualifies the tile.</summary>
		// Token: 0x06000207 RID: 519 RVA: 0x0000EF48 File Offset: 0x0000D148
		public bool AllLayersHave(int face, int level, int tx, int ty)
		{
			return this.atlases.AllHave(face, level, tx, ty);
		}

		/// <summary><paramref name="blits" /> reports the copies actually issued — one per layer that
		/// reaches this tile's level — so the caller's per-frame budget counts real work.</summary>
		// Token: 0x06000208 RID: 520 RVA: 0x0000EF5C File Offset: 0x0000D15C
		public TileCache.TileUploadResult TryUploadTile(int face, int level, int tx, int ty, Texture2D[] tilesByLayer, int frame, out int blits)
		{
			blits = 0;
			bool flag = !this.atlases.Accept(tilesByLayer, face, level, tx, ty);
			TileCache.TileUploadResult result;
			if (flag)
			{
				result = TileCache.TileUploadResult.Rejected;
			}
			else
			{
				long evictedKey;
				int slot = this.slots.TakeOrEvict(frame, out evictedKey);
				bool flag2 = slot < 0;
				if (flag2)
				{
					result = TileCache.TileUploadResult.NoSlot;
				}
				else
				{
					long repointTicks = this.ReleaseEvicted(evictedKey);
					this.ForceGpuResources(tilesByLayer);
					blits = this.BlitToSlot(tilesByLayer, slot);
					long key = TileCache.PackKey(face, level, tx, ty);
					this.slotMap[key] = slot;
					this.slots.Assign(slot, key, frame);
					FrameProfile.Add(ProfilePhase.Paint, repointTicks + this.PaintUploaded(face, level, tx, ty, slot));
					result = TileCache.TileUploadResult.Uploaded;
				}
			}
			return result;
		}

		// Token: 0x06000209 RID: 521 RVA: 0x0000F014 File Offset: 0x0000D214
		private long ReleaseEvicted(long evictedKey)
		{
			bool flag = !TileSlotAllocator.HoldsTile(evictedKey);
			long result;
			if (flag)
			{
				result = 0L;
			}
			else
			{
				FrameProfile.Timer sw = FrameProfile.Start();
				TileCache.s_UpRepointMarker.Begin();
				this.slotMap.Remove(evictedKey);
				this.RepointEvicted(evictedKey);
				TileCache.s_UpRepointMarker.End();
				result = sw.ElapsedTicks;
			}
			return result;
		}

		// Token: 0x0600020A RID: 522 RVA: 0x0000F078 File Offset: 0x0000D278
		private void ForceGpuResources(Texture2D[] tilesByLayer)
		{
			using (TileCache.s_GpuSyncPhase.Measure())
			{
				this.atlases.ForceGpuResources(tilesByLayer);
			}
		}

		// Token: 0x0600020B RID: 523 RVA: 0x0000F0C0 File Offset: 0x0000D2C0
		private int BlitToSlot(Texture2D[] tilesByLayer, int slot)
		{
			int result;
			using (TileCache.s_BlitPhase.Measure())
			{
				result = this.atlases.CopyToSlot(tilesByLayer, slot % this.SlotsPerRow, slot / this.SlotsPerRow);
			}
			return result;
		}

		// Token: 0x0600020C RID: 524 RVA: 0x0000F118 File Offset: 0x0000D318
		private long PaintUploaded(int face, int level, int tx, int ty, int slot)
		{
			FrameProfile.Timer sw = FrameProfile.Start();
			TileCache.s_UpPaintMarker.Begin();
			this.PaintResident(face, level, tx, ty, slot % this.SlotsPerRow, slot / this.SlotsPerRow);
			TileCache.s_UpPaintMarker.End();
			return sw.ElapsedTicks;
		}

		// Token: 0x0600020D RID: 525 RVA: 0x0000F171 File Offset: 0x0000D371
		public bool IsTileResident(long key)
		{
			return this.slotMap.ContainsKey(key);
		}

		// Token: 0x0600020E RID: 526 RVA: 0x0000F17F File Offset: 0x0000D37F
		public bool IsTileResident(int face, int level, int tx, int ty)
		{
			return this.slotMap.ContainsKey(TileCache.PackKey(face, level, tx, ty));
		}

		// Token: 0x0600020F RID: 527 RVA: 0x0000F196 File Offset: 0x0000D396
		public void MarkTileUsed(long key, int frame)
		{
			this.TryMarkTileUsed(key, frame);
		}

		// Token: 0x06000210 RID: 528 RVA: 0x0000F1A4 File Offset: 0x0000D3A4
		public bool TryMarkTileUsed(long key, int frame)
		{
			int slot;
			bool flag = !this.slotMap.TryGetValue(key, out slot);
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				this.slots.Touch(slot, frame);
				result = true;
			}
			return result;
		}

		// Token: 0x17000061 RID: 97
		// (get) Token: 0x06000211 RID: 529 RVA: 0x0000F1DE File Offset: 0x0000D3DE
		public int OccupiedSlots
		{
			get
			{
				return this.slotMap.Count;
			}
		}

		// Token: 0x06000212 RID: 530 RVA: 0x0000F1EC File Offset: 0x0000D3EC
		public int[] GetLevelCounts()
		{
			int[] counts = new int[this.maxLevel + 1];
			foreach (KeyValuePair<long, int> kvp in this.slotMap)
			{
				int num;
				int level;
				int num2;
				int num3;
				TileCache.UnpackKey(kvp.Key, out num, out level, out num2, out num3);
				bool flag = level <= this.maxLevel;
				if (flag)
				{
					counts[level]++;
				}
			}
			return counts;
		}

		// Token: 0x06000213 RID: 531 RVA: 0x0000F288 File Offset: 0x0000D488
		public void BindToMaterial(Material mat)
		{
			mat.SetTexture(TileCache.s_PageTableId, this.PageTable);
			mat.SetTexture(TileCache.s_FineAtlasId, this.FineAtlas);
			mat.SetFloat(TileCache.s_AtlasSizeId, (float)this.atlasSize);
			mat.SetFloat(TileCache.s_TileSizeId, (float)this.tileSize);
			mat.SetFloat(TileCache.s_TileBorderId, (float)this.borderPx);
			mat.SetFloat(TileCache.s_MaxTileLevelId, (float)this.maxLevel);
			mat.SetFloat(TileCache.s_DirLevelId, (float)this.DirectoryLevel);
			mat.SetFloat(TileCache.s_BlockWId, (float)this.blockW);
			mat.SetFloat(TileCache.s_BlockHId, (float)this.blockH);
			this.atlases.BindTo(mat);
		}

		// Token: 0x06000214 RID: 532 RVA: 0x0000F34C File Offset: 0x0000D54C
		public void Dispose()
		{
			this.atlases.Dispose();
			bool flag = this.PageTable != null;
			if (flag)
			{
				Object.Destroy(this.PageTable);
				this.PageTable = null;
			}
			bool flag2 = this.FineAtlas != null;
			if (flag2)
			{
				Object.Destroy(this.FineAtlas);
				this.FineAtlas = null;
			}
			bool flag3 = this.fineStaging != null;
			if (flag3)
			{
				Object.Destroy(this.fineStaging);
				this.fineStaging = null;
			}
			this.slotMap.Clear();
			this.blockMap.Clear();
			this.dirtyBlocks.Clear();
		}

		// Token: 0x06000215 RID: 533 RVA: 0x0000F3FB File Offset: 0x0000D5FB
		public bool HasUploadCapacity(int frame)
		{
			return this.slots.HasCapacity(frame);
		}

		/// <summary>Count resident tiles whose page-table texel doesn't resolve to them.</summary>
		// Token: 0x06000216 RID: 534 RVA: 0x0000F40C File Offset: 0x0000D60C
		public int CountPageTableDesync()
		{
			int desync = 0;
			foreach (KeyValuePair<long, int> kv in this.slotMap)
			{
				int face;
				int level;
				int tx;
				int ty;
				TileCache.UnpackKey(kv.Key, out face, out level, out tx, out ty);
				int slot;
				int flvl;
				bool resident;
				bool flag = !this.TryReadOwnTexel(face, level, tx, ty, out slot, out flvl, out resident);
				if (!flag)
				{
					bool flag2 = !resident || slot != kv.Value || flvl != level;
					if (flag2)
					{
						desync++;
					}
				}
			}
			return desync;
		}

		// Token: 0x06000217 RID: 535 RVA: 0x0000F4C0 File Offset: 0x0000D6C0
		private bool TryReadOwnTexel(int face, int level, int tx, int ty, out int slot, out int fallbackLevel, out bool resident)
		{
			bool flag = level <= this.DirectoryLevel;
			int sx;
			int sy;
			if (flag)
			{
				int idx = ((1 << level) - 1 + ty) * this.ptW + face * this.DirFaceStride + tx;
				TileCache.UnpackPageWord(this.pageWords[idx], out sx, out sy, out fallbackLevel, out resident);
			}
			else
			{
				int fineIdx;
				bool flag2 = !this.TryGetFineTexelIndex(face, level, tx, ty, out fineIdx);
				if (flag2)
				{
					slot = -1;
					fallbackLevel = -1;
					resident = false;
					return false;
				}
				TileCache.UnpackFineWord(this.fineWords[fineIdx], out sx, out sy, out fallbackLevel, out resident);
			}
			slot = sy * this.SlotsPerRow + sx;
			return true;
		}

		/// <summary>Validate indirection against residency. <paramref name="deep" /> adds full texel walk.</summary>
		// Token: 0x06000218 RID: 536 RVA: 0x0000F56C File Offset: 0x0000D76C
		public int ValidateIndirection(List<string> report, int maxReports = 8, bool deep = false)
		{
			TileCache.Violations log = new TileCache.Violations(report, maxReports);
			this.CheckSlotBookkeeping(log);
			bool flag = this.BlockDepth > 0;
			if (flag)
			{
				this.CheckBlockReferences(log);
			}
			if (deep)
			{
				this.CheckDirectoryResolve(log);
				bool flag2 = this.BlockDepth > 0;
				if (flag2)
				{
					this.CheckFineResolve(log);
				}
			}
			return log.Count;
		}

		// Token: 0x06000219 RID: 537 RVA: 0x0000F5D0 File Offset: 0x0000D7D0
		private void CheckSlotBookkeeping(TileCache.Violations log)
		{
			for (int s = 0; s < this.slots.Count; s++)
			{
				long owner = this.slots.OwnerOf(s);
				bool flag = !TileSlotAllocator.HoldsTile(owner);
				if (!flag)
				{
					int mapped;
					bool flag2 = !this.slotMap.TryGetValue(owner, out mapped);
					if (flag2)
					{
						log.Add(string.Format("A: slot {0} owns {1} but slotMap has no entry (orphaned)", s, TileCache.KeyStr(owner)));
					}
					else
					{
						bool flag3 = mapped != s;
						if (flag3)
						{
							log.Add(string.Format("A: slot {0} owns {1} but slotMap points it at slot {2}", s, TileCache.KeyStr(owner), mapped));
						}
					}
				}
			}
			foreach (KeyValuePair<long, int> kv in this.slotMap)
			{
				long owner2 = this.slots.OwnerOf(kv.Value);
				bool flag4 = owner2 != kv.Key && owner2 != -9223372036854775807L;
				if (flag4)
				{
					log.Add(string.Format("A: slotMap says {0} -> slot {1}, but that slot is owned ", TileCache.KeyStr(kv.Key), kv.Value) + "by " + TileCache.DescribeOwner(owner2));
				}
			}
		}

		// Token: 0x0600021A RID: 538 RVA: 0x0000F740 File Offset: 0x0000D940
		private void CheckBlockReferences(TileCache.Violations log)
		{
			foreach (KeyValuePair<long, int> kv in this.blockMap)
			{
				bool flag = this.blockOwner[kv.Value] != kv.Key;
				if (flag)
				{
					log.Add(string.Format("B: blockMap says {0} -> block {1}, but that block is ", TileCache.KeyStr(kv.Key), kv.Value) + "owned by " + TileCache.DescribeBlockOwner(this.blockOwner[kv.Value]));
				}
				int bf;
				int num;
				int bx;
				int by;
				TileCache.UnpackKey(kv.Key, out bf, out num, out bx, out by);
				int idx = ((1 << this.DirectoryLevel) - 1 + by) * this.ptW + bf * this.DirFaceStride + bx;
				bool hasBlock;
				int blkX;
				int blkY;
				TileCache.UnpackBlockRef(this.pageWords[idx], out hasBlock, out blkX, out blkY);
				int referenced = blkY * this.blockGridW + blkX;
				bool flag2 = !hasBlock;
				if (flag2)
				{
					log.Add(string.Format("B: block {0} is owned by {1}, whose texel has no hasBlock", kv.Value, TileCache.KeyStr(kv.Key)));
				}
				else
				{
					bool flag3 = referenced != kv.Value;
					if (flag3)
					{
						log.Add(string.Format("B: {0} owns block {1} but its texel references block ", TileCache.KeyStr(kv.Key), kv.Value) + string.Format("{0}", referenced));
					}
				}
			}
			for (int s = 0; s < this.TotalBlocks; s++)
			{
				bool flag4 = this.blockOwner[s] != long.MinValue && !this.blockMap.ContainsKey(this.blockOwner[s]);
				if (flag4)
				{
					log.Add(string.Format("B: block {0} is owned by {1}, which blockMap doesn't list", s, TileCache.KeyStr(this.blockOwner[s])));
				}
			}
		}

		// Token: 0x0600021B RID: 539 RVA: 0x0000F964 File Offset: 0x0000DB64
		private void CheckDirectoryResolve(TileCache.Violations log)
		{
			for (int face = 0; face < 6; face++)
			{
				for (int level = 0; level <= this.DirectoryLevel; level++)
				{
					int side = 1 << level;
					for (int ty = 0; ty < side; ty++)
					{
						for (int tx = 0; tx < side; tx++)
						{
							int idx = ((1 << level) - 1 + ty) * this.ptW + face * this.DirFaceStride + tx;
							int sx;
							int sy;
							int flvl;
							bool res;
							TileCache.UnpackPageWord(this.pageWords[idx], out sx, out sy, out flvl, out res);
							bool flag = res;
							if (flag)
							{
								this.CheckResolve(log, face, level, tx, ty, sy * this.SlotsPerRow + sx, flvl, "C");
							}
							bool flooded = log.Flooded;
							if (flooded)
							{
								return;
							}
						}
					}
				}
			}
		}

		// Token: 0x0600021C RID: 540 RVA: 0x0000FA50 File Offset: 0x0000DC50
		private void CheckFineResolve(TileCache.Violations log)
		{
			foreach (KeyValuePair<long, int> kv in this.blockMap)
			{
				int face;
				int num;
				int dirTx;
				int dirTy;
				TileCache.UnpackKey(kv.Key, out face, out num, out dirTx, out dirTy);
				int ox = kv.Value % this.blockGridW * this.blockW;
				int oy = kv.Value / this.blockGridW * this.blockH;
				for (int level = this.DirectoryLevel + 1; level <= this.maxLevel; level++)
				{
					int i = level - this.DirectoryLevel;
					int span = 1 << i;
					int row = this.BlockRowStart(level);
					for (int y = 0; y < span; y++)
					{
						for (int x = 0; x < span; x++)
						{
							int idx = (oy + row + y) * this.fineW + ox + x;
							int fx;
							int fy;
							int flvl;
							bool r;
							TileCache.UnpackFineWord(this.fineWords[idx], out fx, out fy, out flvl, out r);
							bool flag = r;
							if (flag)
							{
								this.CheckResolve(log, face, level, (dirTx << i) + x, (dirTy << i) + y, fy * this.SlotsPerRow + fx, flvl, "D");
							}
							bool flooded = log.Flooded;
							if (flooded)
							{
								return;
							}
						}
					}
				}
			}
		}

		// Token: 0x0600021D RID: 541 RVA: 0x0000FBF4 File Offset: 0x0000DDF4
		private void CheckResolve(TileCache.Violations log, int face, int level, int tx, int ty, int slot, int fallbackLevel, string tag)
		{
			string self = TileCache.KeyStr(TileCache.PackKey(face, level, tx, ty));
			bool flag = fallbackLevel > level;
			if (flag)
			{
				log.Add(string.Format("{0}: {1} resolves to level {2}, FINER than itself", tag, self, fallbackLevel));
			}
			else
			{
				int shift = level - fallbackLevel;
				long ancestor = TileCache.PackKey(face, fallbackLevel, tx >> shift, ty >> shift);
				int expected;
				bool flag2 = !this.slotMap.TryGetValue(ancestor, out expected);
				if (flag2)
				{
					log.Add(string.Format("{0}: {1} -> slot {2} as {3}, but that tile is NOT ", new object[]
					{
						tag,
						self,
						slot,
						TileCache.KeyStr(ancestor)
					}) + "resident (stale entry — an eviction failed to repaint)");
				}
				else
				{
					bool flag3 = expected != slot;
					if (flag3)
					{
						log.Add(string.Format("{0}: {1} -> slot {2}, but {3} lives in slot {4}; ", new object[]
						{
							tag,
							self,
							slot,
							TileCache.KeyStr(ancestor),
							expected
						}) + string.Format("slot {0} actually holds {1}", slot, TileCache.DescribeOwner(this.slots.OwnerOf(slot))));
					}
				}
			}
		}

		// Token: 0x0600021E RID: 542 RVA: 0x0000FD24 File Offset: 0x0000DF24
		private static string KeyStr(long key)
		{
			int f;
			int i;
			int x;
			int y;
			TileCache.UnpackKey(key, out f, out i, out x, out y);
			return string.Format("[f{0} L{1} {2},{3}]", new object[]
			{
				f,
				i,
				x,
				y
			});
		}

		// Token: 0x0600021F RID: 543 RVA: 0x0000FD7B File Offset: 0x0000DF7B
		private static string DescribeOwner(long owner)
		{
			return (owner == long.MinValue) ? "NOTHING (freed)" : ((owner == -9223372036854775807L) ? "a pinned tile" : TileCache.KeyStr(owner));
		}

		// Token: 0x06000220 RID: 544 RVA: 0x0000FDA9 File Offset: 0x0000DFA9
		private static string DescribeBlockOwner(long owner)
		{
			return (owner == long.MinValue) ? "NOTHING (freed)" : TileCache.KeyStr(owner);
		}

		// Token: 0x06000221 RID: 545 RVA: 0x0000FDC4 File Offset: 0x0000DFC4
		private void VerifyBlockResolve(in TileCache.FineBlock block, List<long> residents)
		{
			if (this.blockVerifyScratch == null)
			{
				this.blockVerifyScratch = new uint[this.blockW * this.blockH];
			}
			int ox = block.OriginX;
			int oy = block.OriginY;
			this.ResolveBlockReference(block);
			for (int ry = 0; ry < this.blockH; ry++)
			{
				Array.Copy(this.fineWords, (oy + ry) * this.fineW + ox, this.blockVerifyScratch, ry * this.blockW, this.blockW);
			}
			this.ResolveBlock(block, residents);
			int bad = 0;
			int firstLevel = -1;
			int firstX = -1;
			int firstY = -1;
			for (int level = this.DirectoryLevel + 1; level <= this.maxLevel; level++)
			{
				int span = 1 << level - this.DirectoryLevel;
				int row = this.BlockRowStart(level);
				for (int y = 0; y < span; y++)
				{
					for (int x = 0; x < span; x++)
					{
						uint expected = this.blockVerifyScratch[(row + y) * this.blockW + x];
						uint actual = this.fineWords[(oy + row + y) * this.fineW + ox + x];
						bool flag = expected == actual;
						if (!flag)
						{
							bool flag2 = bad++ == 0;
							if (flag2)
							{
								firstLevel = level;
								firstX = x;
								firstY = y;
							}
						}
					}
				}
			}
			bool flag3 = bad == 0;
			if (!flag3)
			{
				MirageDebug.LogError(string.Format("[VT Validate] ResolveBlock diverged from the reference walk for block {0} ", block.Slot) + string.Format("(dir tile [f{0} L{1} {2},{3}], ", new object[]
				{
					block.Face,
					this.DirectoryLevel,
					block.DirTx,
					block.DirTy
				}) + string.Format("{0} residents): {1} texel(s) differ, first at level ", residents.Count, bad) + string.Format("{0} sub {1},{2}.", firstLevel, firstX, firstY));
			}
		}

		// Token: 0x06000222 RID: 546 RVA: 0x0000FFEC File Offset: 0x0000E1EC
		private void CreateDirectory()
		{
			this.ptW = 6 * (1 << this.DirectoryLevel);
			this.ptH = (1 << this.DirectoryLevel + 1) - 1;
			this.pageWords = new uint[this.ptW * this.ptH];
			this.pageColorsScratch = new Color32[this.ptW * this.ptH];
			this.pageRegionScratch = new Color32[this.ptW * this.ptH];
			this.PageTable = new Texture2D(this.ptW, this.ptH, 4, false, true)
			{
				name = "VTPageTable",
				wrapMode = 1,
				filterMode = 0
			};
			this.pageTableDirty = true;
		}

		/// <summary>Flush pending indirection changes to the GPU.</summary>
		// Token: 0x06000223 RID: 547 RVA: 0x000100A8 File Offset: 0x0000E2A8
		public void ApplyPageTable()
		{
			this.FlushFineAtlas();
			bool flag = !this.pageTableDirty;
			if (!flag)
			{
				TileCache.s_DirUploadMarker.Begin();
				this.UploadDirectory();
				TileCache.s_DirUploadMarker.End();
			}
		}

		// Token: 0x06000224 RID: 548 RVA: 0x000100F0 File Offset: 0x0000E2F0
		private void UploadDirectory()
		{
			int x = this.hasDirtyRect ? this.dirtyMinX : 0;
			int y = this.hasDirtyRect ? this.dirtyMinY : 0;
			int w = this.hasDirtyRect ? (this.dirtyMaxX - this.dirtyMinX + 1) : this.ptW;
			int h = this.hasDirtyRect ? (this.dirtyMaxY - this.dirtyMinY + 1) : this.ptH;
			bool flag = w == this.ptW;
			if (flag)
			{
				Array.Copy(this.pageColorsScratch, y * this.ptW, this.pageRegionScratch, 0, w * h);
			}
			else
			{
				for (int ry = 0; ry < h; ry++)
				{
					Array.Copy(this.pageColorsScratch, (y + ry) * this.ptW + x, this.pageRegionScratch, ry * w, w);
				}
			}
			this.PageTable.SetPixels32(x, y, w, h, this.pageRegionScratch);
			this.PageTable.Apply(false, false);
			this.pageTableDirty = false;
			this.hasDirtyRect = false;
		}

		// Token: 0x06000225 RID: 549 RVA: 0x00010204 File Offset: 0x0000E404
		private void WritePageTexel(int face, int level, int tx, int ty, int slotX, int slotY, int residentLevel, bool valid)
		{
			bool flag = slotX > 63 || slotY > 63;
			if (flag)
			{
				MirageDebug.LogError(string.Format("TileCache: slot {0},{1} exceeds the page texel's {2} — widen ", slotX, slotY, 63) + "slotX/slotY here and in MirageVTUniforms.cginc");
			}
			else
			{
				int idx = ((1 << level) - 1 + ty) * this.ptW + face * this.DirFaceStride + tx;
				bool hasBlock;
				int blockX;
				int blockY;
				TileCache.UnpackBlockRef(this.pageWords[idx], out hasBlock, out blockX, out blockY);
				this.StorePageWord(idx, TileCache.PackPageWord(slotX, slotY, residentLevel, valid, hasBlock, blockX, blockY));
			}
		}

		// Token: 0x06000226 RID: 550 RVA: 0x000102A4 File Offset: 0x0000E4A4
		private void StorePageWord(int idx, uint word)
		{
			this.pageWords[idx] = word;
			this.pageColorsScratch[idx] = new Color32((byte)(word & 255U), (byte)(word >> 8 & 255U), (byte)(word >> 16 & 255U), (byte)(word >> 24 & 255U));
			this.GrowDirtyRect(idx % this.ptW, idx / this.ptW);
			this.pageTableDirty = true;
		}

		// Token: 0x06000227 RID: 551 RVA: 0x00010314 File Offset: 0x0000E514
		private void GrowDirtyRect(int texelX, int texelY)
		{
			bool flag = !this.hasDirtyRect;
			if (flag)
			{
				this.dirtyMaxX = texelX;
				this.dirtyMinX = texelX;
				this.dirtyMaxY = texelY;
				this.dirtyMinY = texelY;
				this.hasDirtyRect = true;
			}
			else
			{
				this.dirtyMinX = Mathf.Min(this.dirtyMinX, texelX);
				this.dirtyMaxX = Mathf.Max(this.dirtyMaxX, texelX);
				this.dirtyMinY = Mathf.Min(this.dirtyMinY, texelY);
				this.dirtyMaxY = Mathf.Max(this.dirtyMaxY, texelY);
			}
		}

		// Token: 0x06000228 RID: 552 RVA: 0x000103A4 File Offset: 0x0000E5A4
		private void PaintSubtree(int face, int level, int tx, int ty, int slotX, int slotY, int residentLevel)
		{
			this.WritePageTexel(face, level, tx, ty, slotX, slotY, residentLevel, true);
			bool flag = level >= this.DirectoryLevel;
			if (!flag)
			{
				int childLevel = level + 1;
				for (int dy = 0; dy <= 1; dy++)
				{
					for (int dx = 0; dx <= 1; dx++)
					{
						int cx = tx * 2 + dx;
						int cy = ty * 2 + dy;
						bool flag2 = !this.slotMap.ContainsKey(TileCache.PackKey(face, childLevel, cx, cy));
						if (flag2)
						{
							this.PaintSubtree(face, childLevel, cx, cy, slotX, slotY, residentLevel);
						}
					}
				}
			}
		}

		// Token: 0x06000229 RID: 553 RVA: 0x00010448 File Offset: 0x0000E648
		private void PaintResident(int face, int level, int tx, int ty, int slotX, int slotY)
		{
			bool flag = level <= this.DirectoryLevel;
			if (flag)
			{
				this.PaintSubtree(face, level, tx, ty, slotX, slotY, level);
			}
			this.MarkBlocksUnder(face, level, tx, ty);
		}

		// Token: 0x0600022A RID: 554 RVA: 0x00010484 File Offset: 0x0000E684
		private void RepointEvicted(long evictedKey)
		{
			int face;
			int level;
			int tx;
			int ty;
			TileCache.UnpackKey(evictedKey, out face, out level, out tx, out ty);
			bool flag = level <= this.DirectoryLevel;
			if (flag)
			{
				this.RepointToAncestor(face, level, tx, ty);
			}
			this.MarkBlocksUnder(face, level, tx, ty);
		}

		// Token: 0x0600022B RID: 555 RVA: 0x000104CC File Offset: 0x0000E6CC
		private void RepointToAncestor(int face, int level, int tx, int ty)
		{
			for (int ancestor = level - 1; ancestor >= 0; ancestor--)
			{
				int shift = level - ancestor;
				long key = TileCache.PackKey(face, ancestor, tx >> shift, ty >> shift);
				int slot;
				bool flag = this.slotMap.TryGetValue(key, out slot);
				if (flag)
				{
					this.PaintSubtree(face, level, tx, ty, slot % this.SlotsPerRow, slot / this.SlotsPerRow, ancestor);
					return;
				}
			}
			this.WritePageTexel(face, level, tx, ty, 0, 0, 0, false);
		}

		// Token: 0x0600022C RID: 556 RVA: 0x00010550 File Offset: 0x0000E750
		private static List<long>[] NewResidentBuckets()
		{
			List<long>[] buckets = new List<long>[16];
			for (int i = 0; i < buckets.Length; i++)
			{
				buckets[i] = new List<long>();
			}
			return buckets;
		}

		// Token: 0x17000062 RID: 98
		// (get) Token: 0x0600022D RID: 557 RVA: 0x00010585 File Offset: 0x0000E785
		// (set) Token: 0x0600022E RID: 558 RVA: 0x0001058D File Offset: 0x0000E78D
		public Texture2D FineAtlas { get; private set; }

		// Token: 0x17000063 RID: 99
		// (get) Token: 0x0600022F RID: 559 RVA: 0x00010596 File Offset: 0x0000E796
		public int TotalBlocks
		{
			get
			{
				return this.blockGridW * this.blockGridH;
			}
		}

		// Token: 0x17000064 RID: 100
		// (get) Token: 0x06000230 RID: 560 RVA: 0x000105A5 File Offset: 0x0000E7A5
		public int OccupiedBlocks
		{
			get
			{
				return this.blockMap.Count;
			}
		}

		// Token: 0x17000065 RID: 101
		// (get) Token: 0x06000231 RID: 561 RVA: 0x000105B2 File Offset: 0x0000E7B2
		public bool HasFineTier
		{
			get
			{
				return this.BlockDepth > 0;
			}
		}

		// Token: 0x06000232 RID: 562 RVA: 0x000105C0 File Offset: 0x0000E7C0
		private static int NextPow2(int v)
		{
			int p;
			for (p = 1; p < v; p <<= 1)
			{
			}
			return p;
		}

		// Token: 0x06000233 RID: 563 RVA: 0x000105E4 File Offset: 0x0000E7E4
		private static void ComputeFineDims(int blockDepth, out int blockW, out int blockH, out int gridW, out int gridH)
		{
			blockW = 1 << blockDepth;
			blockH = TileCache.NextPow2((1 << blockDepth + 1) - 2);
			gridW = Math.Min(64, 2048);
			gridH = Math.Min(64, Math.Max(1, (2048 + gridW - 1) / gridW));
		}

		// Token: 0x06000234 RID: 564 RVA: 0x00010638 File Offset: 0x0000E838
		private static long FineAtlasBytesForDepth(int blockDepth)
		{
			bool flag = blockDepth <= 0;
			long result;
			if (flag)
			{
				result = 4L;
			}
			else
			{
				int bw;
				int bh;
				int gw;
				int gh;
				TileCache.ComputeFineDims(blockDepth, out bw, out bh, out gw, out gh);
				result = (long)(gw * bw) * (long)(gh * bh) * 4L;
			}
			return result;
		}

		// Token: 0x06000235 RID: 565 RVA: 0x0001067A File Offset: 0x0000E87A
		private int BlockRowStart(int level)
		{
			return (1 << level - this.DirectoryLevel) - 2;
		}

		// Token: 0x06000236 RID: 566 RVA: 0x0001068C File Offset: 0x0000E88C
		private TileCache.FineBlock BlockAt(int slot)
		{
			int face;
			int num;
			int dirTx;
			int dirTy;
			TileCache.UnpackKey(this.blockOwner[slot], out face, out num, out dirTx, out dirTy);
			return this.DirectoryTile(face, dirTx, dirTy, slot);
		}

		// Token: 0x06000237 RID: 567 RVA: 0x000106C0 File Offset: 0x0000E8C0
		private TileCache.FineBlock DirectoryTile(int face, int dirTx, int dirTy, int slot = -1)
		{
			return new TileCache.FineBlock(slot, face, dirTx, dirTy, ((1 << this.DirectoryLevel) - 1 + dirTy) * this.ptW + face * this.DirFaceStride + dirTx, (slot < 0) ? 0 : (slot % this.blockGridW * this.blockW), (slot < 0) ? 0 : (slot / this.blockGridW * this.blockH));
		}

		/// <summary>Ensure a block exists for (dirTx, dirTy) and refresh its LRU stamp.</summary>
		// Token: 0x06000238 RID: 568 RVA: 0x00010728 File Offset: 0x0000E928
		public bool TouchBlock(int face, int dirTx, int dirTy, int frame)
		{
			bool flag = this.BlockDepth <= 0;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				long key = TileCache.PackKey(face, this.DirectoryLevel, dirTx, dirTy);
				int slot;
				bool flag2 = this.blockMap.TryGetValue(key, out slot);
				if (flag2)
				{
					this.blockFrame[slot] = frame;
					result = true;
				}
				else
				{
					slot = this.AllocateBlock(frame);
					bool flag3 = slot < 0;
					if (flag3)
					{
						result = false;
					}
					else
					{
						this.blockMap[key] = slot;
						this.blockOwner[slot] = key;
						this.blockFrame[slot] = frame;
						TileCache.FineBlock fineBlock = this.DirectoryTile(face, dirTx, dirTy, -1);
						this.SetBlockRef(fineBlock, slot);
						this.dirtyBlocks.Add(slot);
						result = true;
					}
				}
			}
			return result;
		}

		// Token: 0x06000239 RID: 569 RVA: 0x000107E0 File Offset: 0x0000E9E0
		private int AllocateBlock(int frame)
		{
			for (int i = 0; i < this.TotalBlocks; i++)
			{
				bool flag = this.blockOwner[i] == long.MinValue;
				if (flag)
				{
					return i;
				}
			}
			int oldest = int.MaxValue;
			int lru = -1;
			for (int j = 0; j < this.TotalBlocks; j++)
			{
				bool flag2 = this.blockFrame[j] == frame;
				if (!flag2)
				{
					bool flag3 = this.blockFrame[j] < oldest;
					if (flag3)
					{
						oldest = this.blockFrame[j];
						lru = j;
					}
				}
			}
			bool flag4 = lru < 0;
			if (flag4)
			{
				return -1;
			}
			TileCache.FineBlock evicted = this.BlockAt(lru);
			this.blockMap.Remove(this.blockOwner[lru]);
			this.SetBlockRef(evicted, -1);
			this.dirtyBlocks.Remove(lru);
			this.blockOwner[lru] = long.MinValue;
			this.blockFrame[lru] = 0;
			return lru;
		}

		/// <summary>Set or clear a directory texel's block reference, preserving the resolve bits.</summary>
		// Token: 0x0600023A RID: 570 RVA: 0x000108E4 File Offset: 0x0000EAE4
		private void SetBlockRef(in TileCache.FineBlock tile, int blockSlot)
		{
			int sx;
			int sy;
			int flvl;
			bool res;
			TileCache.UnpackPageWord(this.pageWords[tile.DirTexel], out sx, out sy, out flvl, out res);
			bool has = blockSlot >= 0;
			this.StorePageWord(tile.DirTexel, TileCache.PackPageWord(sx, sy, flvl, res, has, has ? (blockSlot % this.blockGridW) : 0, has ? (blockSlot / this.blockGridW) : 0));
		}

		/// <summary>Dirty every block beneath (face, level, tx, ty) after a residency change.</summary>
		// Token: 0x0600023B RID: 571 RVA: 0x0001094C File Offset: 0x0000EB4C
		private void MarkBlocksUnder(int face, int level, int tx, int ty)
		{
			bool flag = this.BlockDepth <= 0 || this.blockMap.Count == 0;
			if (!flag)
			{
				bool flag2 = level > this.DirectoryLevel;
				if (flag2)
				{
					int i = level - this.DirectoryLevel;
					int s;
					bool flag3 = this.blockMap.TryGetValue(TileCache.PackKey(face, this.DirectoryLevel, tx >> i, ty >> i), out s);
					if (flag3)
					{
						this.dirtyBlocks.Add(s);
					}
				}
				else
				{
					int shift = this.DirectoryLevel - level;
					foreach (KeyValuePair<long, int> kv in this.blockMap)
					{
						int bf;
						int num;
						int bx;
						int by;
						TileCache.UnpackKey(kv.Key, out bf, out num, out bx, out by);
						bool flag4 = bf == face && bx >> shift == tx && by >> shift == ty;
						if (flag4)
						{
							this.dirtyBlocks.Add(kv.Value);
						}
					}
				}
			}
		}

		// Token: 0x0600023C RID: 572 RVA: 0x00010A6C File Offset: 0x0000EC6C
		private void ResolveBlock(in TileCache.FineBlock block, List<long> residents)
		{
			int sx;
			int sy;
			int flvl;
			bool res;
			TileCache.UnpackPageWord(this.pageWords[block.DirTexel], out sx, out sy, out flvl, out res);
			this.FillBlock(block, TileCache.PackFineWord(sx, sy, flvl, res));
			for (int i = 0; i < residents.Count; i++)
			{
				long key = residents[i];
				int num;
				int level;
				int tx;
				int ty;
				TileCache.UnpackKey(key, out num, out level, out tx, out ty);
				int slot = this.slotMap[key];
				uint word = TileCache.PackFineWord(slot % this.SlotsPerRow, slot / this.SlotsPerRow, level, true);
				this.OverlayResidentSubtree(block, level, tx, ty, word);
			}
		}

		/// <summary>Write <paramref name="word" /> to every used texel of a block.</summary>
		// Token: 0x0600023D RID: 573 RVA: 0x00010B18 File Offset: 0x0000ED18
		private void FillBlock(in TileCache.FineBlock block, uint word)
		{
			int ox = block.OriginX;
			int oy = block.OriginY;
			for (int level = this.DirectoryLevel + 1; level <= this.maxLevel; level++)
			{
				int span = 1 << level - this.DirectoryLevel;
				int row0 = oy + this.BlockRowStart(level);
				for (int y = 0; y < span; y++)
				{
					int rowBase = (row0 + y) * this.fineW + ox;
					for (int x = 0; x < span; x++)
					{
						this.fineWords[rowBase + x] = word;
					}
				}
			}
		}

		/// <summary>Paint <paramref name="word" /> onto a resident tile's texel and all descendants.</summary>
		// Token: 0x0600023E RID: 574 RVA: 0x00010BBC File Offset: 0x0000EDBC
		private void OverlayResidentSubtree(in TileCache.FineBlock block, int level, int tx, int ty, uint word)
		{
			int ox = block.OriginX;
			int oy = block.OriginY;
			for (int i = level; i <= this.maxLevel; i++)
			{
				int shift = i - level;
				int span = 1 << shift;
				int km = i - this.DirectoryLevel;
				int x0 = (tx << shift) - (block.DirTx << km);
				int y0 = (ty << shift) - (block.DirTy << km);
				int row0 = oy + this.BlockRowStart(i) + y0;
				for (int y = 0; y < span; y++)
				{
					int rowBase = (row0 + y) * this.fineW + ox + x0;
					for (int x = 0; x < span; x++)
					{
						this.fineWords[rowBase + x] = word;
					}
				}
			}
		}

		/// <summary>Index of a tile's fine texel, if its block exists.</summary>
		// Token: 0x0600023F RID: 575 RVA: 0x00010C9C File Offset: 0x0000EE9C
		private bool TryGetFineTexelIndex(int face, int level, int tx, int ty, out int idx)
		{
			idx = 0;
			bool flag = this.BlockDepth <= 0 || level <= this.DirectoryLevel || level > this.maxLevel;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				int i = level - this.DirectoryLevel;
				int slot;
				bool flag2 = !this.blockMap.TryGetValue(TileCache.PackKey(face, this.DirectoryLevel, tx >> i, ty >> i), out slot);
				if (flag2)
				{
					result = false;
				}
				else
				{
					int px = slot % this.blockGridW * this.blockW + (tx - (tx >> i << i));
					int py = slot / this.blockGridW * this.blockH + this.BlockRowStart(level) + (ty - (ty >> i << i));
					idx = py * this.fineW + px;
					result = true;
				}
			}
			return result;
		}

		/// <summary>Re-resolve dirty blocks and push them to the GPU.</summary>
		// Token: 0x06000240 RID: 576 RVA: 0x00010D74 File Offset: 0x0000EF74
		private void FlushFineAtlas()
		{
			bool flag = this.dirtyBlocks.Count == 0;
			if (!flag)
			{
				TileCache.s_FineSelectMarker.Begin();
				int count = this.SelectBlocksToFlush();
				bool flag2 = count > 0;
				if (flag2)
				{
					this.BucketResidentsByBlock(count);
				}
				TileCache.s_FineSelectMarker.End();
				bool flag3 = count == 0;
				if (!flag3)
				{
					this.ResolveAndStage(count);
					this.UploadStagedBands(count);
				}
			}
		}

		/// <summary>Gather dirty blocks, order freshest first, defer past the budget.</summary>
		// Token: 0x06000241 RID: 577 RVA: 0x00010DE8 File Offset: 0x0000EFE8
		private int SelectBlocksToFlush()
		{
			this.resolvedBlocks.Clear();
			foreach (int slot in this.dirtyBlocks)
			{
				bool flag = this.blockOwner[slot] != long.MinValue;
				if (flag)
				{
					this.resolvedBlocks.Add(slot);
				}
			}
			this.dirtyBlocks.Clear();
			bool flag2 = this.resolvedBlocks.Count == 0;
			int result;
			if (flag2)
			{
				result = 0;
			}
			else
			{
				bool flag3 = this.resolvedBlocks.Count > 16;
				if (flag3)
				{
					this.resolvedBlocks.Sort(this.freshestBlockFirst);
				}
				int count = Math.Min(this.resolvedBlocks.Count, 16);
				this.DeferRemaining(count);
				result = count;
			}
			return result;
		}

		// Token: 0x06000242 RID: 578 RVA: 0x00010ED8 File Offset: 0x0000F0D8
		private void DeferRemaining(int count)
		{
			for (int i = count; i < this.resolvedBlocks.Count; i++)
			{
				int slot = this.resolvedBlocks[i];
				TileCache.FineBlock fineBlock = this.BlockAt(slot);
				this.EnsureBlockUnpublished(fineBlock);
				this.dirtyBlocks.Add(slot);
			}
		}

		/// <summary>Group resident fine tiles by block. Buckets come out ascending for ResolveBlock.</summary>
		// Token: 0x06000243 RID: 579 RVA: 0x00010F30 File Offset: 0x0000F130
		private void BucketResidentsByBlock(int count)
		{
			for (int i = 0; i < count; i++)
			{
				this.blockResidentKeys[i].Clear();
				this.blockResidentDirKey[i] = this.blockOwner[this.resolvedBlocks[i]];
			}
			foreach (KeyValuePair<long, int> kv in this.slotMap)
			{
				int face;
				int level;
				int tx;
				int ty;
				TileCache.UnpackKey(kv.Key, out face, out level, out tx, out ty);
				bool flag = level <= this.DirectoryLevel;
				if (!flag)
				{
					int j = level - this.DirectoryLevel;
					long dirKey = TileCache.PackKey(face, this.DirectoryLevel, tx >> j, ty >> j);
					for (int k = 0; k < count; k++)
					{
						bool flag2 = this.blockResidentDirKey[k] == dirKey;
						if (flag2)
						{
							this.blockResidentKeys[k].Add(kv.Key);
							break;
						}
					}
				}
			}
			for (int l = 0; l < count; l++)
			{
				this.blockResidentKeys[l].Sort();
			}
		}

		// Token: 0x06000244 RID: 580 RVA: 0x00011080 File Offset: 0x0000F280
		private void ResolveAndStage(int count)
		{
			TileCache.s_FineResolveMarker.Begin();
			NativeArray<uint> staging = this.fineStaging.GetRawTextureData<uint>();
			for (int i = 0; i < count; i++)
			{
				TileCache.FineBlock block = this.BlockAt(this.resolvedBlocks[i]);
				bool validateIndirection = MirageSettings.ValidateIndirection;
				if (validateIndirection)
				{
					this.VerifyBlockResolve(block, this.blockResidentKeys[i]);
				}
				else
				{
					this.ResolveBlock(block, this.blockResidentKeys[i]);
				}
				int ox = block.OriginX;
				int oy = block.OriginY;
				int band = i * this.blockW * this.blockH;
				for (int ry = 0; ry < this.blockH; ry++)
				{
					int src = (oy + ry) * this.fineW + ox;
					int dst = band + ry * this.blockW;
					for (int cx = 0; cx < this.blockW; cx++)
					{
						staging[dst + cx] = this.fineWords[src + cx];
					}
				}
				this.EnsureBlockPublished(block);
			}
			TileCache.s_FineResolveMarker.End();
		}

		// Token: 0x06000245 RID: 581 RVA: 0x000111AC File Offset: 0x0000F3AC
		private void UploadStagedBands(int count)
		{
			TileCache.s_FineUploadMarker.Begin();
			this.fineStaging.Apply(false, false);
			for (int i = 0; i < count; i++)
			{
				TileCache.FineBlock block = this.BlockAt(this.resolvedBlocks[i]);
				Graphics.CopyTexture(this.fineStaging, 0, 0, 0, i * this.blockH, this.blockW, this.blockH, this.FineAtlas, 0, 0, block.OriginX, block.OriginY);
			}
			TileCache.s_FineUploadMarker.End();
		}

		// Token: 0x06000246 RID: 582 RVA: 0x00011240 File Offset: 0x0000F440
		private void EnsureBlockUnpublished(in TileCache.FineBlock tile)
		{
			bool hasBlock;
			int num;
			int num2;
			TileCache.UnpackBlockRef(this.pageWords[tile.DirTexel], out hasBlock, out num, out num2);
			bool flag = hasBlock;
			if (flag)
			{
				this.SetBlockRef(tile, -1);
			}
		}

		// Token: 0x06000247 RID: 583 RVA: 0x00011278 File Offset: 0x0000F478
		private void EnsureBlockPublished(in TileCache.FineBlock block)
		{
			bool hasBlock;
			int bx;
			int by;
			TileCache.UnpackBlockRef(this.pageWords[block.DirTexel], out hasBlock, out bx, out by);
			bool flag = hasBlock && by * this.blockGridW + bx == block.Slot;
			if (!flag)
			{
				this.SetBlockRef(block, block.Slot);
			}
		}

		/// <summary>Tree-walking reference resolve, kept as the oracle for validateIndirection.</summary>
		// Token: 0x06000248 RID: 584 RVA: 0x000112CC File Offset: 0x0000F4CC
		private void ResolveBlockReference(in TileCache.FineBlock block)
		{
			int sx;
			int sy;
			int flvl;
			bool res;
			TileCache.UnpackPageWord(this.pageWords[block.DirTexel], out sx, out sy, out flvl, out res);
			this.PaintBlockChildren(block, this.DirectoryLevel, block.DirTx, block.DirTy, TileCache.PackFineWord(sx, sy, flvl, res));
		}

		// Token: 0x06000249 RID: 585 RVA: 0x00011318 File Offset: 0x0000F518
		private void PaintBlockChildren(in TileCache.FineBlock block, int level, int tx, int ty, uint inherited)
		{
			bool flag = level >= this.maxLevel;
			if (!flag)
			{
				int childLevel = level + 1;
				int i = childLevel - this.DirectoryLevel;
				for (int dy = 0; dy <= 1; dy++)
				{
					for (int dx = 0; dx <= 1; dx++)
					{
						int cx = tx * 2 + dx;
						int cy = ty * 2 + dy;
						uint word = inherited;
						int s;
						bool flag2 = this.slotMap.TryGetValue(TileCache.PackKey(block.Face, childLevel, cx, cy), out s);
						if (flag2)
						{
							word = TileCache.PackFineWord(s % this.SlotsPerRow, s / this.SlotsPerRow, childLevel, true);
						}
						this.WriteFineTexel(block, childLevel, cx - (block.DirTx << i), cy - (block.DirTy << i), word);
						this.PaintBlockChildren(block, childLevel, cx, cy, word);
					}
				}
			}
		}

		// Token: 0x0600024A RID: 586 RVA: 0x0001140C File Offset: 0x0000F60C
		private void WriteFineTexel(in TileCache.FineBlock block, int level, int subX, int subY, uint word)
		{
			int px = block.OriginX + subX;
			int py = block.OriginY + this.BlockRowStart(level) + subY;
			this.fineWords[py * this.fineW + px] = word;
		}

		/// <summary>Allocate the fine indirection atlas and staging strip.</summary>
		// Token: 0x0600024B RID: 587 RVA: 0x00011448 File Offset: 0x0000F648
		private void CreateFineAtlas()
		{
			bool flag = this.BlockDepth > 0;
			if (flag)
			{
				TileCache.ComputeFineDims(this.BlockDepth, out this.blockW, out this.blockH, out this.blockGridW, out this.blockGridH);
				this.fineW = this.blockGridW * this.blockW;
				this.fineH = this.blockGridH * this.blockH;
			}
			else
			{
				this.blockW = (this.blockH = (this.blockGridW = (this.blockGridH = 1)));
				this.fineW = (this.fineH = 1);
			}
			this.fineWords = new uint[this.fineW * this.fineH];
			this.blockOwner = new long[this.TotalBlocks];
			this.blockFrame = new int[this.TotalBlocks];
			this.freshestBlockFirst = new TileCache.FreshestBlockComparer(this);
			for (int i = 0; i < this.TotalBlocks; i++)
			{
				this.blockOwner[i] = long.MinValue;
			}
			this.FineAtlas = new Texture2D(this.fineW, this.fineH, 4, false, true)
			{
				name = "VTFineIndirection",
				wrapMode = 1,
				filterMode = 0
			};
			this.fineStaging = new Texture2D(this.blockW, this.blockH * 16, 4, false, true)
			{
				name = "VTFineStaging",
				filterMode = 0
			};
			this.FineAtlas.SetPixelData<uint>(this.fineWords, 0, 0);
			this.FineAtlas.Apply(false, false);
			bool flag2 = this.BlockDepth > 0;
			if (flag2)
			{
				MirageDebug.Log(string.Format("TileCache: split={0}, maxLevel={1} -> fine tier depth ", this.DirectoryLevel, this.maxLevel) + string.Format("{0}: {1} blocks of {2}x{3} in a ", new object[]
				{
					this.BlockDepth,
					this.TotalBlocks,
					this.blockW,
					this.blockH
				}) + string.Format("{0}x{1} atlas ({2:0.0} MB).", this.fineW, this.fineH, (double)((long)(this.fineW * this.fineH) * 4L) / 1048576.0));
			}
		}

		// Token: 0x0600024C RID: 588 RVA: 0x000116A1 File Offset: 0x0000F8A1
		public static uint PackPageWord(int slotX, int slotY, int fallbackLevel, bool resident, bool hasBlock = false, int blockX = 0, int blockY = 0)
		{
			return (uint)((slotX & 63) | (slotY & 63) << 6 | (fallbackLevel & 7) << 12 | (resident ? 32768 : 0) | (hasBlock ? 65536 : 0) | (blockX & 63) << 17 | (blockY & 63) << 23);
		}

		// Token: 0x0600024D RID: 589 RVA: 0x000116E0 File Offset: 0x0000F8E0
		public static void UnpackPageWord(uint w, out int slotX, out int slotY, out int fallbackLevel, out bool resident)
		{
			slotX = (int)(w & 63U);
			slotY = (int)(w >> 6 & 63U);
			fallbackLevel = (int)(w >> 12 & 7U);
			resident = ((w >> 15 & 1U) > 0U);
		}

		// Token: 0x0600024E RID: 590 RVA: 0x00011705 File Offset: 0x0000F905
		public static void UnpackBlockRef(uint w, out bool hasBlock, out int blockX, out int blockY)
		{
			hasBlock = ((w >> 16 & 1U) > 0U);
			blockX = (int)(w >> 17 & 63U);
			blockY = (int)(w >> 23 & 63U);
		}

		// Token: 0x0600024F RID: 591 RVA: 0x00011725 File Offset: 0x0000F925
		public static uint PackFineWord(int slotX, int slotY, int fallbackLevel, bool resident)
		{
			return (uint)((slotX & 63) | (slotY & 63) << 6 | (fallbackLevel & 15) << 12 | (resident ? 65536 : 0));
		}

		// Token: 0x06000250 RID: 592 RVA: 0x00011746 File Offset: 0x0000F946
		public static void UnpackFineWord(uint w, out int slotX, out int slotY, out int fallbackLevel, out bool resident)
		{
			slotX = (int)(w & 63U);
			slotY = (int)(w >> 6 & 63U);
			fallbackLevel = (int)(w >> 12 & 15U);
			resident = ((w >> 16 & 1U) > 0U);
		}

		// Token: 0x06000251 RID: 593 RVA: 0x0001176C File Offset: 0x0000F96C
		public static long PackKey(int face, int level, int tx, int ty)
		{
			return (long)face << 40 | (long)level << 32 | (long)(ty & 65535) << 16 | (long)(tx & 65535);
		}

		// Token: 0x06000252 RID: 594 RVA: 0x0001178E File Offset: 0x0000F98E
		public static void UnpackKey(long key, out int face, out int level, out int tx, out int ty)
		{
			tx = (int)(key & 65535L);
			ty = (int)(key >> 16 & 65535L);
			level = (int)(key >> 32 & 255L);
			face = (int)(key >> 40 & 255L);
		}

		// Token: 0x040001C2 RID: 450
		public readonly int atlasSize;

		// Token: 0x040001C3 RID: 451
		public readonly int tileSize;

		// Token: 0x040001C4 RID: 452
		public readonly int borderPx;

		// Token: 0x040001C5 RID: 453
		public readonly int maxLevel;

		/// <summary>Default coarse/fine split level.</summary>
		// Token: 0x040001C6 RID: 454
		public const int DefaultDirectoryLevel = 7;

		// Token: 0x040001C8 RID: 456
		private TileLayerAtlases atlases;

		// Token: 0x040001C9 RID: 457
		private readonly Dictionary<long, int> slotMap = new Dictionary<long, int>();

		// Token: 0x040001CA RID: 458
		private TileSlotAllocator slots;

		// Token: 0x040001CB RID: 459
		private const long MB = 1048576L;

		// Token: 0x040001CC RID: 460
		private static readonly ProfiledPhase s_GpuSyncPhase = new ProfiledPhase(ProfilePhase.GpuSync, "Mirage.VT.Upload.GpuSync");

		// Token: 0x040001CD RID: 461
		private static readonly ProfiledPhase s_BlitPhase = new ProfiledPhase(ProfilePhase.Blit, "Mirage.VT.Upload.Blit");

		// Token: 0x040001CE RID: 462
		private static readonly ProfilerMarker s_UpRepointMarker = new ProfilerMarker("Mirage.VT.Upload.RepointEvicted");

		// Token: 0x040001CF RID: 463
		private static readonly ProfilerMarker s_UpPaintMarker = new ProfilerMarker("Mirage.VT.Upload.Paint");

		// Token: 0x040001D0 RID: 464
		private static readonly int s_PageTableId = Shader.PropertyToID("_VTPageTable");

		// Token: 0x040001D1 RID: 465
		private static readonly int s_FineAtlasId = Shader.PropertyToID("_VTFineAtlas");

		// Token: 0x040001D2 RID: 466
		private static readonly int s_AtlasSizeId = Shader.PropertyToID("_VTAtlasSize");

		// Token: 0x040001D3 RID: 467
		private static readonly int s_TileSizeId = Shader.PropertyToID("_VTTileSize");

		// Token: 0x040001D4 RID: 468
		private static readonly int s_TileBorderId = Shader.PropertyToID("_VTTileBorder");

		// Token: 0x040001D5 RID: 469
		private static readonly int s_MaxTileLevelId = Shader.PropertyToID("_VTMaxTileLevel");

		// Token: 0x040001D6 RID: 470
		private static readonly int s_DirLevelId = Shader.PropertyToID("_VTDirLevel");

		// Token: 0x040001D7 RID: 471
		private static readonly int s_BlockWId = Shader.PropertyToID("_VTBlockW");

		// Token: 0x040001D8 RID: 472
		private static readonly int s_BlockHId = Shader.PropertyToID("_VTBlockH");

		// Token: 0x040001D9 RID: 473
		private uint[] blockVerifyScratch;

		// Token: 0x040001DA RID: 474
		private int ptW;

		// Token: 0x040001DB RID: 475
		private int ptH;

		// Token: 0x040001DC RID: 476
		private uint[] pageWords;

		// Token: 0x040001DD RID: 477
		private Color32[] pageColorsScratch;

		// Token: 0x040001DE RID: 478
		private Color32[] pageRegionScratch;

		// Token: 0x040001DF RID: 479
		private bool pageTableDirty;

		// Token: 0x040001E0 RID: 480
		private bool hasDirtyRect;

		// Token: 0x040001E1 RID: 481
		private int dirtyMinX;

		// Token: 0x040001E2 RID: 482
		private int dirtyMinY;

		// Token: 0x040001E3 RID: 483
		private int dirtyMaxX;

		// Token: 0x040001E4 RID: 484
		private int dirtyMaxY;

		// Token: 0x040001E5 RID: 485
		private static readonly ProfilerMarker s_DirUploadMarker = new ProfilerMarker("Mirage.VT.ApplyPage.Directory");

		// Token: 0x040001E6 RID: 486
		private const int TargetBlockCount = 2048;

		// Token: 0x040001E7 RID: 487
		private const long MaxFineAtlasBytes = 134217728L;

		// Token: 0x040001E8 RID: 488
		private const int MaxBlockFlushesPerFrame = 16;

		// Token: 0x040001E9 RID: 489
		private const long BLOCK_FREE = -9223372036854775808L;

		// Token: 0x040001EA RID: 490
		private int blockW;

		// Token: 0x040001EB RID: 491
		private int blockH;

		// Token: 0x040001EC RID: 492
		private int blockGridW;

		// Token: 0x040001ED RID: 493
		private int blockGridH;

		// Token: 0x040001EE RID: 494
		private int fineW;

		// Token: 0x040001EF RID: 495
		private int fineH;

		// Token: 0x040001F0 RID: 496
		private uint[] fineWords;

		// Token: 0x040001F1 RID: 497
		private readonly Dictionary<long, int> blockMap = new Dictionary<long, int>();

		// Token: 0x040001F2 RID: 498
		private long[] blockOwner;

		// Token: 0x040001F3 RID: 499
		private int[] blockFrame;

		// Token: 0x040001F4 RID: 500
		private readonly HashSet<int> dirtyBlocks = new HashSet<int>();

		// Token: 0x040001F5 RID: 501
		private Texture2D fineStaging;

		// Token: 0x040001F6 RID: 502
		private readonly List<int> resolvedBlocks = new List<int>();

		// Token: 0x040001F7 RID: 503
		private readonly List<long>[] blockResidentKeys = TileCache.NewResidentBuckets();

		// Token: 0x040001F8 RID: 504
		private readonly long[] blockResidentDirKey = new long[16];

		// Token: 0x040001F9 RID: 505
		private TileCache.FreshestBlockComparer freshestBlockFirst;

		// Token: 0x040001FA RID: 506
		private static readonly ProfilerMarker s_FineSelectMarker = new ProfilerMarker("Mirage.VT.ApplyPage.FineSelect");

		// Token: 0x040001FB RID: 507
		private static readonly ProfilerMarker s_FineResolveMarker = new ProfilerMarker("Mirage.VT.ApplyPage.FineResolve");

		// Token: 0x040001FC RID: 508
		private static readonly ProfilerMarker s_FineUploadMarker = new ProfilerMarker("Mirage.VT.ApplyPage.FineUpload");

		// Token: 0x040001FE RID: 510
		public const int MaxSlotCoord = 63;

		// Token: 0x040001FF RID: 511
		public const int MaxFallbackLevel = 7;

		// Token: 0x04000200 RID: 512
		public const int MaxFineFallbackLevel = 15;

		// Token: 0x04000201 RID: 513
		public const int MaxBlockCoord = 63;

		// Token: 0x020000CE RID: 206
		public enum TileUploadResult
		{
			// Token: 0x0400056F RID: 1391
			Uploaded,
			// Token: 0x04000570 RID: 1392
			Rejected,
			// Token: 0x04000571 RID: 1393
			NoSlot
		}

		// Token: 0x020000CF RID: 207
		private sealed class Violations
		{
			// Token: 0x060004B5 RID: 1205 RVA: 0x00021E37 File Offset: 0x00020037
			public Violations(List<string> report, int maxReports)
			{
			}

			// Token: 0x170000BC RID: 188
			// (get) Token: 0x060004B6 RID: 1206 RVA: 0x00021E4E File Offset: 0x0002004E
			// (set) Token: 0x060004B7 RID: 1207 RVA: 0x00021E56 File Offset: 0x00020056
			public int Count { get; private set; }

			// Token: 0x170000BD RID: 189
			// (get) Token: 0x060004B8 RID: 1208 RVA: 0x00021E5F File Offset: 0x0002005F
			public bool Flooded
			{
				get
				{
					return this.Count > this.<maxReports>P * 4;
				}
			}

			// Token: 0x060004B9 RID: 1209 RVA: 0x00021E74 File Offset: 0x00020074
			public void Add(string message)
			{
				int count = this.Count;
				this.Count = count + 1;
				bool flag = this.<report>P != null && this.<report>P.Count < this.<maxReports>P;
				if (flag)
				{
					this.<report>P.Add(message);
				}
			}

			// Token: 0x04000572 RID: 1394
			[CompilerGenerated]
			[DebuggerBrowsable(DebuggerBrowsableState.Never)]
			private List<string> <report>P = report;

			// Token: 0x04000573 RID: 1395
			[CompilerGenerated]
			[DebuggerBrowsable(DebuggerBrowsableState.Never)]
			private int <maxReports>P = maxReports;
		}

		// Token: 0x020000D0 RID: 208
		private sealed class FreshestBlockComparer : IComparer<int>
		{
			// Token: 0x060004BA RID: 1210 RVA: 0x00021EC2 File Offset: 0x000200C2
			public FreshestBlockComparer(TileCache cache)
			{
			}

			// Token: 0x060004BB RID: 1211 RVA: 0x00021ED2 File Offset: 0x000200D2
			public int Compare(int a, int b)
			{
				return this.<cache>P.blockFrame[b].CompareTo(this.<cache>P.blockFrame[a]);
			}

			// Token: 0x04000575 RID: 1397
			[CompilerGenerated]
			[DebuggerBrowsable(DebuggerBrowsableState.Never)]
			private TileCache <cache>P = cache;
		}

		// Token: 0x020000D1 RID: 209
		private readonly struct FineBlock
		{
			// Token: 0x060004BC RID: 1212 RVA: 0x00021EF7 File Offset: 0x000200F7
			public FineBlock(int slot, int face, int dirTx, int dirTy, int dirTexel, int originX, int originY)
			{
				this.Slot = slot;
				this.Face = face;
				this.DirTx = dirTx;
				this.DirTy = dirTy;
				this.DirTexel = dirTexel;
				this.OriginX = originX;
				this.OriginY = originY;
			}

			// Token: 0x04000576 RID: 1398
			public readonly int Slot;

			// Token: 0x04000577 RID: 1399
			public readonly int Face;

			// Token: 0x04000578 RID: 1400
			public readonly int DirTx;

			// Token: 0x04000579 RID: 1401
			public readonly int DirTy;

			// Token: 0x0400057A RID: 1402
			public readonly int DirTexel;

			// Token: 0x0400057B RID: 1403
			public readonly int OriginX;

			// Token: 0x0400057C RID: 1404
			public readonly int OriginY;
		}
	}
}
