using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>
	/// Unified virtual-texture cache: ONE slot map + ONE indirection (page table) shared by up to three
	/// parallel payload atlases (color / height / normal). A slot index maps to the SAME (slotX,slotY) region
	/// in every atlas, so one page-table texel resolves all layers — the "lockstep residency" invariant: a
	/// tile is resident only when every present layer has landed at the shared slot.
	///
	/// TWO-LEVEL INDIRECTION (Stage 4, doc §2). A fixed DIRECTORY covers levels 0..<see cref="P:Mirage.VirtualTexture.TileCache.DirectoryLevel" />
	/// and is sized by IT, not by maxLevel — 6·2^L_dir × (2^(L_dir+1)−1), a constant 768×255 at L_dir=7. Levels
	/// past it live in fine BLOCKS, one per directory tile with resident fine descendants, paged into
	/// <see cref="P:Mirage.VirtualTexture.TileCache.FineAtlas" /> by a free-list + LRU. So indirection memory scales with RESIDENCY, not virtual
	/// extent: a flat table at L12 would be 24,576 px wide (over the 16k texture-dim cap) and 805 MB.
	///
	/// §5 frozen texels (see MirageVTUniforms.cginc), one 32-bit word each:
	///   coarse: [0..4] slotX(5) | [5..9] slotY(5) | [10..12] fallbackLvl(3) | [13] resident(1)
	///           [14] hasBlock(1) | [15..20] blockX(6) | [21..26] blockY(6) | [27..31] reserved
	///   fine:   [0..4] slotX(5) | [5..9] slotY(5) | [10..13] fallbackLvl(4) | [14] resident(1)
	/// The coarse texel is MERGED: its resolve half is owned by painting (<see cref="M:Mirage.VirtualTexture.TileCache.PaintSubtree(System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32)" />) and its
	/// block half by the allocator (<see cref="M:Mirage.VirtualTexture.TileCache.SetBlockRef(System.Int32,System.Int32,System.Int32,System.Int32)" />) — each preserves the other's bits. Fine texels
	/// index the SAME slot map / atlases, so a fine texel can fall back straight to a coarse ancestor's slot
	/// (hence its wider 4-bit level) and the shader never needs a second fetch to find that fallback.
	///
	/// Both are R32_UInt (read via Texture2D.Load — zero samplers) or, on GPUs that refuse integer Load, RGBA32
	/// with the same word little-endian across the four bytes. Every texel is CPU-resolved to the nearest
	/// resident tile at level ≤ its own, so the shader reads one texel per tier and never walks.
	/// Directory layout: texelX = face·2^L_dir + tileX_corrected; texelY = (1&lt;&lt;level)−1 + tileY_corrected.
	/// Tiles are in CORRECTED UV space.
	///
	/// Caller contract:
	///   1. Construct, <see cref="M:Mirage.VirtualTexture.TileCache.AddLayer(Mirage.VirtualTexture.VTLayer,System.String,Mirage.VirtualTexture.ITileLayerSource)" /> for each present layer, <see cref="M:Mirage.VirtualTexture.TileCache.BootstrapCoarseLevels(System.Int32)" />, then <see cref="M:Mirage.VirtualTexture.TileCache.BindToMaterial(UnityEngine.Material)" />.
	///   2. Each frame the streaming layer calls <see cref="M:Mirage.VirtualTexture.TileCache.TryUploadTile(System.Int32,System.Int32,System.Int32,System.Int32,UnityEngine.Texture2D[],System.Int32)" /> (all present layers together) then <see cref="M:Mirage.VirtualTexture.TileCache.ApplyPageTable" />.
	///   3. On body unload call <see cref="M:Mirage.VirtualTexture.TileCache.Dispose" />.
	/// </summary>
	// Token: 0x0200004D RID: 77
	public class TileCache : IDisposable
	{
		// Token: 0x1700004D RID: 77
		// (get) Token: 0x060001CB RID: 459 RVA: 0x0000D99C File Offset: 0x0000BB9C
		// (set) Token: 0x060001CC RID: 460 RVA: 0x0000D9A4 File Offset: 0x0000BBA4
		public Texture2D PageTable { get; private set; }

		// Token: 0x1700004E RID: 78
		// (get) Token: 0x060001CD RID: 461 RVA: 0x0000D9AD File Offset: 0x0000BBAD
		public int SlotSize
		{
			get
			{
				return this.tileSize + 2 * this.borderPx;
			}
		}

		// Token: 0x1700004F RID: 79
		// (get) Token: 0x060001CE RID: 462 RVA: 0x0000D9BE File Offset: 0x0000BBBE
		public int SlotsPerRow
		{
			get
			{
				return this.atlasSize / this.SlotSize;
			}
		}

		// Token: 0x17000050 RID: 80
		// (get) Token: 0x060001CF RID: 463 RVA: 0x0000D9CD File Offset: 0x0000BBCD
		public int TotalSlots
		{
			get
			{
				return this.SlotsPerRow * this.SlotsPerRow;
			}
		}

		/// <summary>Where the indirection changes representation (doc §2's <c>L_dir</c>). Orthogonal to
		/// <see cref="F:Mirage.VirtualTexture.TileStreamingManager.CoarseMaxLevel" />, which is a residency (pinning) policy —
		/// this is a structural split. Configurable so a body can exercise the fine path with shallow data.</summary>
		// Token: 0x17000051 RID: 81
		// (get) Token: 0x060001D0 RID: 464 RVA: 0x0000D9DC File Offset: 0x0000BBDC
		public int DirectoryLevel { get; }

		/// <summary>Levels served by fine blocks: <c>DirectoryLevel+1 .. maxLevel</c>. 0 = no fine tier.</summary>
		// Token: 0x17000052 RID: 82
		// (get) Token: 0x060001D1 RID: 465 RVA: 0x0000D9E4 File Offset: 0x0000BBE4
		public int BlockDepth
		{
			get
			{
				return this.maxLevel - this.DirectoryLevel;
			}
		}

		/// <summary>Texel columns per cube face in the directory (6 faces laid side by side).</summary>
		// Token: 0x17000053 RID: 83
		// (get) Token: 0x060001D2 RID: 466 RVA: 0x0000D9F3 File Offset: 0x0000BBF3
		private int DirFaceStride
		{
			get
			{
				return 1 << this.DirectoryLevel;
			}
		}

		// Token: 0x060001D3 RID: 467 RVA: 0x0000DA00 File Offset: 0x0000BC00
		public static uint PackPageWord(int slotX, int slotY, int fallbackLevel, bool resident, bool hasBlock = false, int blockX = 0, int blockY = 0)
		{
			return (uint)((slotX & 63) | (slotY & 63) << 6 | (fallbackLevel & 7) << 12 | (resident ? 32768 : 0) | (hasBlock ? 65536 : 0) | (blockX & 63) << 17 | (blockY & 63) << 23);
		}

		// Token: 0x060001D4 RID: 468 RVA: 0x0000DA3F File Offset: 0x0000BC3F
		public static void UnpackPageWord(uint w, out int slotX, out int slotY, out int fallbackLevel, out bool resident)
		{
			slotX = (int)(w & 63U);
			slotY = (int)(w >> 6 & 63U);
			fallbackLevel = (int)(w >> 12 & 7U);
			resident = ((w >> 15 & 1U) > 0U);
		}

		// Token: 0x060001D5 RID: 469 RVA: 0x0000DA64 File Offset: 0x0000BC64
		public static void UnpackBlockRef(uint w, out bool hasBlock, out int blockX, out int blockY)
		{
			hasBlock = ((w >> 16 & 1U) > 0U);
			blockX = (int)(w >> 17 & 63U);
			blockY = (int)(w >> 23 & 63U);
		}

		/// <summary>Fine-block texel: like the coarse word but with a 4-bit fallbackLvl (a fine texel must be
		/// able to point at a coarse ancestor at ANY level 0..maxLevel) and no block reference.</summary>
		// Token: 0x060001D6 RID: 470 RVA: 0x0000DA84 File Offset: 0x0000BC84
		public static uint PackFineWord(int slotX, int slotY, int fallbackLevel, bool resident)
		{
			return (uint)((slotX & 63) | (slotY & 63) << 6 | (fallbackLevel & 15) << 12 | (resident ? 65536 : 0));
		}

		// Token: 0x060001D7 RID: 471 RVA: 0x0000DAA5 File Offset: 0x0000BCA5
		public static void UnpackFineWord(uint w, out int slotX, out int slotY, out int fallbackLevel, out bool resident)
		{
			slotX = (int)(w & 63U);
			slotY = (int)(w >> 6 & 63U);
			fallbackLevel = (int)(w >> 12 & 15U);
			resident = ((w >> 16 & 1U) > 0U);
		}

		// Token: 0x17000054 RID: 84
		// (get) Token: 0x060001D8 RID: 472 RVA: 0x0000DACB File Offset: 0x0000BCCB
		public IReadOnlyList<TileCache.LayerState> Layers
		{
			get
			{
				return this.layers;
			}
		}

		// Token: 0x17000055 RID: 85
		// (get) Token: 0x060001D9 RID: 473 RVA: 0x0000DAD3 File Offset: 0x0000BCD3
		public int LayerCount
		{
			get
			{
				return this.layers.Count;
			}
		}

		// Token: 0x17000056 RID: 86
		// (get) Token: 0x060001DA RID: 474 RVA: 0x0000DAE0 File Offset: 0x0000BCE0
		// (set) Token: 0x060001DB RID: 475 RVA: 0x0000DAE8 File Offset: 0x0000BCE8
		public Texture2D FineAtlas { get; private set; }

		// Token: 0x17000057 RID: 87
		// (get) Token: 0x060001DC RID: 476 RVA: 0x0000DAF1 File Offset: 0x0000BCF1
		public int TotalBlocks
		{
			get
			{
				return this.blockGridW * this.blockGridH;
			}
		}

		// Token: 0x17000058 RID: 88
		// (get) Token: 0x060001DD RID: 477 RVA: 0x0000DB00 File Offset: 0x0000BD00
		public int OccupiedBlocks
		{
			get
			{
				return this.blockMap.Count;
			}
		}

		// Token: 0x17000059 RID: 89
		// (get) Token: 0x060001DE RID: 478 RVA: 0x0000DB0D File Offset: 0x0000BD0D
		public bool HasFineTier
		{
			get
			{
				return this.BlockDepth > 0;
			}
		}

		// Token: 0x060001DF RID: 479 RVA: 0x0000DB18 File Offset: 0x0000BD18
		public TileCache(int atlasSize, int tileSize, int borderPx, int maxLevel, bool useRgba32PageTable = false, int directoryLevel = 7)
		{
			bool flag = atlasSize <= 0 || tileSize <= 0 || maxLevel < 0;
			if (flag)
			{
				throw new ArgumentException(string.Format("TileCache: invalid args atlasSize={0} tileSize={1} maxLevel={2}", atlasSize, tileSize, maxLevel));
			}
			this.atlasSize = atlasSize;
			this.tileSize = tileSize;
			this.borderPx = borderPx;
			this.maxLevel = maxLevel;
			int dirCeil = Mathf.Min(maxLevel, 7);
			int dirLevel = Mathf.Clamp(directoryLevel, 0, dirCeil);
			int requestedDir = dirLevel;
			while (dirLevel < dirCeil && TileCache.FineAtlasBytesForDepth(maxLevel - dirLevel) > 134217728L)
			{
				dirLevel++;
			}
			this.DirectoryLevel = dirLevel;
			bool flag2 = dirLevel != requestedDir;
			if (flag2)
			{
				MirageDebug.LogError(string.Concat(new string[]
				{
					string.Format("TileCache: canonicalMaxLevel {0} with maxLevel {1} needs a ", requestedDir, maxLevel),
					string.Format("{0} MB fine indirection ", TileCache.FineAtlasBytesForDepth(maxLevel - requestedDir) / 1048576L),
					string.Format("atlas (fine-tier depth {0}) — over the {1} MB ", maxLevel - requestedDir, 128L),
					string.Format("cap. Raised the coarse/fine split to {0} ", dirLevel),
					string.Format("({0} MB). Lowering ", TileCache.FineAtlasBytesForDepth(maxLevel - dirLevel) / 1048576L),
					"canonicalMaxLevel trades a small flat directory for a quadratically larger fine tier; keep it near the canonical archive's depth."
				}));
			}
			else
			{
				bool flag3 = TileCache.FineAtlasBytesForDepth(maxLevel - dirLevel) > 134217728L;
				if (flag3)
				{
					MirageDebug.LogError(string.Format("TileCache: even at the deepest addressable directory ({0}), maxLevel {1} needs a ", dirLevel, maxLevel) + string.Format("{0} MB fine indirection atlas — ", TileCache.FineAtlasBytesForDepth(maxLevel - dirLevel) / 1048576L) + "the pyramid is too deep for the current fine-tier format. Expect heavy GPU upload cost.");
				}
			}
			this.useRgba32 = useRgba32PageTable;
			bool flag4 = this.SlotsPerRow - 1 > 63;
			if (flag4)
			{
				int usable = 64 * this.SlotSize;
				MirageDebug.LogError(string.Format("TileCache: atlasSize={0} gives {1} slots per axis, but the §5 texel's ", atlasSize, this.SlotsPerRow) + string.Format("{0}-slot field cannot address past {1}. Clamping the atlas to ", 64, 63) + string.Format("{0} ({1} slots/axis = {2} tiles). ", usable, 64, 4096) + "Lower atlasSize to silence this, or widen slotX/slotY (and MirageVTUniforms.cginc with it).");
				this.atlasSize = usable;
			}
			bool flag5 = maxLevel > 15;
			if (flag5)
			{
				MirageDebug.LogError(string.Format("TileCache: maxLevel={0} exceeds the {1} the 4-bit fine fallbackLvl field can hold.", maxLevel, 15));
			}
			int total = this.TotalSlots;
			this.slotOwner = new long[total];
			this.slotFrame = new int[total];
			for (int i = 0; i < total; i++)
			{
				this.slotOwner[i] = long.MinValue;
			}
			this.ptW = 6 * (1 << this.DirectoryLevel);
			this.ptH = (1 << this.DirectoryLevel + 1) - 1;
			this.pageWords = new uint[this.ptW * this.ptH];
			bool flag6 = this.useRgba32;
			if (flag6)
			{
				this.pageColorsScratch = new Color32[this.ptW * this.ptH];
				this.pageRegionScratch = new Color32[this.ptW * this.ptH];
				this.PageTable = new Texture2D(this.ptW, this.ptH, 4, false, true);
			}
			else
			{
				this.PageTable = new Texture2D(this.ptW, this.ptH, 37, 0);
			}
			this.PageTable.name = "VTPageTable";
			this.PageTable.wrapMode = 1;
			this.PageTable.filterMode = 0;
			this.pageTableDirty = true;
			bool flag7 = this.BlockDepth > 0;
			if (flag7)
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
			for (int j = 0; j < this.TotalBlocks; j++)
			{
				this.blockOwner[j] = long.MinValue;
			}
			int stagingH = this.blockH * 48;
			bool flag8 = this.useRgba32;
			if (flag8)
			{
				this.FineAtlas = new Texture2D(this.fineW, this.fineH, 4, false, true);
				this.fineStaging = new Texture2D(this.blockW, stagingH, 4, false, true);
			}
			else
			{
				this.FineAtlas = new Texture2D(this.fineW, this.fineH, 37, 0);
				this.fineStaging = new Texture2D(this.blockW, stagingH, 37, 0);
			}
			this.FineAtlas.name = "VTFineIndirection";
			this.FineAtlas.wrapMode = 1;
			this.FineAtlas.filterMode = 0;
			this.fineStaging.name = "VTFineStaging";
			this.fineStaging.filterMode = 0;
			bool flag9 = !this.useRgba32;
			if (flag9)
			{
				this.FineAtlas.SetPixelData<uint>(this.fineWords, 0, 0);
			}
			this.FineAtlas.Apply(false, false);
			bool flag10 = this.BlockDepth > 0;
			if (flag10)
			{
				MirageDebug.Log(string.Format("TileCache: L_dir={0}, maxLevel={1} -> fine tier depth {2}: ", this.DirectoryLevel, maxLevel, this.BlockDepth) + string.Format("{0} blocks of {1}x{2} in a {3}x{4} atlas ", new object[]
				{
					this.TotalBlocks,
					this.blockW,
					this.blockH,
					this.fineW,
					this.fineH
				}) + string.Format("({0:0.0} MB).", (double)((long)(this.fineW * this.fineH) * 4L / 1024L) / 1024.0));
			}
			this.ApplyPageTable();
		}

		/// <summary>Register a payload layer with the tile source it reads from (loose files or archive).
		/// Call before <see cref="M:Mirage.VirtualTexture.TileCache.BootstrapCoarseLevels(System.Int32)" />.</summary>
		// Token: 0x060001E0 RID: 480 RVA: 0x0000E1CF File Offset: 0x0000C3CF
		public void AddLayer(VTLayer id, string uniformPrefix, ITileLayerSource source)
		{
			this.layers.Add(new TileCache.LayerState
			{
				id = id,
				uniformPrefix = uniformPrefix,
				source = source,
				linear = source.Linear
			});
		}

		/// <summary>
		/// Synchronously loads levels 0..coarseMaxLevel for ALL present layers and pins them at a shared slot
		/// so they are never evicted — the coarse fallback floor the shader walks up to. A coarse tile is only
		/// pinned when every present layer has it (lockstep); a layer missing a coarse tile is logged and the
		/// tile falls back to its parent.
		/// </summary>
		// Token: 0x060001E1 RID: 481 RVA: 0x0000E204 File Offset: 0x0000C404
		public void BootstrapCoarseLevels(int coarseMaxLevel)
		{
			coarseMaxLevel = Mathf.Clamp(coarseMaxLevel, 0, this.maxLevel);
			int loaded = 0;
			int failed = 0;
			Texture2D[] perLayer = new Texture2D[this.layers.Count];
			TileReadHandle[] handles = new TileReadHandle[this.layers.Count];
			for (int face = 0; face < 6; face++)
			{
				for (int level = 0; level <= coarseMaxLevel; level++)
				{
					int g = 1 << level;
					for (int ty = 0; ty < g; ty++)
					{
						for (int tx = 0; tx < g; tx++)
						{
							bool flag = !this.AllLayersExist(face, level, tx, ty);
							if (flag)
							{
								failed++;
							}
							else
							{
								bool ok = true;
								for (int li = 0; li < this.layers.Count; li++)
								{
									handles[li] = this.layers[li].source.BeginLoad(face, level, tx, ty);
								}
								for (int li2 = 0; li2 < this.layers.Count; li2++)
								{
									try
									{
										perLayer[li2] = handles[li2].GetTexture();
									}
									catch (Exception e)
									{
										MirageDebug.LogError(string.Format("TileCache: bootstrap {0} L{1} f{2} {3},{4} failed: {5}", new object[]
										{
											this.layers[li2].id,
											level,
											face,
											tx,
											ty,
											e.Message
										}));
										ok = false;
									}
								}
								bool flag2 = ok && this.UploadPinned(face, level, tx, ty, perLayer);
								if (flag2)
								{
									loaded++;
								}
								else
								{
									failed++;
								}
								for (int li3 = 0; li3 < this.layers.Count; li3++)
								{
									handles[li3].Dispose();
								}
							}
						}
					}
				}
			}
			this.ApplyPageTable();
			MirageDebug.Log(string.Format("TileCache: bootstrapped {0} coarse tiles ({1} failed/missing) across {2} layer(s)", loaded, failed, this.layers.Count));
		}

		// Token: 0x060001E2 RID: 482 RVA: 0x0000E468 File Offset: 0x0000C668
		private bool AllLayersExist(int face, int level, int tx, int ty)
		{
			for (int li = 0; li < this.layers.Count; li++)
			{
				bool flag = !this.layers[li].source.Exists(face, level, tx, ty);
				if (flag)
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Coroutine bootstrap — same lockstep pinning, a few tiles per frame (scaled-body load).
		///
		/// Iterates LEVEL-major (all six faces of L0, then all of L1, ...) — NOT face-major. That ordering is
		/// load-bearing for progressive display: once level 0's six tiles are pinned, PaintSubtree has painted
		/// EVERY directory texel to fall back on them, so the whole globe resolves (blurry but correct) after
		/// ~6 tiles instead of after all 126. Face-major instead finishes one face completely before starting
		/// the next, so the globe isn't fully covered until the very end.
		///
		/// <paramref name="onLevelComplete" /> fires after each level is pinned and flushed, so the caller can
		/// bind the material as soon as level 0 lands rather than waiting for the whole coarse set.
		/// </summary>
		// Token: 0x060001E3 RID: 483 RVA: 0x0000E4BB File Offset: 0x0000C6BB
		public IEnumerator BootstrapCoarseLevelsAsync(int coarseMaxLevel, int uploadsPerFrame = 8, Action<int> onLevelComplete = null)
		{
			TileCache.<BootstrapCoarseLevelsAsync>d__85 <BootstrapCoarseLevelsAsync>d__ = new TileCache.<BootstrapCoarseLevelsAsync>d__85(0);
			<BootstrapCoarseLevelsAsync>d__.<>4__this = this;
			<BootstrapCoarseLevelsAsync>d__.coarseMaxLevel = coarseMaxLevel;
			<BootstrapCoarseLevelsAsync>d__.uploadsPerFrame = uploadsPerFrame;
			<BootstrapCoarseLevelsAsync>d__.onLevelComplete = onLevelComplete;
			return <BootstrapCoarseLevelsAsync>d__;
		}

		// Token: 0x060001E4 RID: 484 RVA: 0x0000E4E0 File Offset: 0x0000C6E0
		private bool UploadPinned(int face, int level, int tx, int ty, Texture2D[] perLayer)
		{
			for (int li = 0; li < this.layers.Count; li++)
			{
				string dbg = string.Format("bootstrap {0} L{1} f{2} {3},{4}", new object[]
				{
					this.layers[li].id,
					level,
					face,
					tx,
					ty
				});
				bool flag = perLayer[li] == null || !this.EnsureAtlasAllocated(this.layers[li], perLayer[li], dbg) || !this.ValidateTile(this.layers[li], perLayer[li], dbg);
				if (flag)
				{
					return false;
				}
			}
			int slot = this.AllocateAnyFreeSlot();
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
				return false;
			}
			for (int li2 = 0; li2 < this.layers.Count; li2++)
			{
				this.CopyTileToSlot(this.layers[li2], perLayer[li2], slot);
			}
			long key = TileCache.PackKey(face, level, tx, ty);
			this.slotMap[key] = slot;
			this.slotOwner[slot] = -9223372036854775807L;
			this.slotFrame[slot] = int.MaxValue;
			this.PaintResident(face, level, tx, ty, slot % this.SlotsPerRow, slot / this.SlotsPerRow, level);
			return true;
		}

		/// <summary>
		/// Upload a completed streaming tile — <paramref name="tilesByLayer" /> is aligned to <see cref="P:Mirage.VirtualTexture.TileCache.Layers" />
		/// and must contain every present layer's payload (lockstep). Allocates ONE slot, copies every layer into
		/// it, and paints the shared page table once. Evicts the LRU non-pinned slot if the atlas is full.
		/// </summary>
		// Token: 0x060001E5 RID: 485 RVA: 0x0000E69C File Offset: 0x0000C89C
		public TileCache.TileUploadResult TryUploadTile(int face, int level, int tx, int ty, Texture2D[] tilesByLayer, int frame)
		{
			for (int li = 0; li < this.layers.Count; li++)
			{
				string dbg = string.Format("streaming {0} L{1} f{2} {3},{4}", new object[]
				{
					this.layers[li].id,
					level,
					face,
					tx,
					ty
				});
				bool flag = tilesByLayer[li] == null || !this.EnsureAtlasAllocated(this.layers[li], tilesByLayer[li], dbg) || !this.ValidateTile(this.layers[li], tilesByLayer[li], dbg);
				if (flag)
				{
					return TileCache.TileUploadResult.Rejected;
				}
			}
			long evictedKey;
			int slot = this.AllocateStreamingSlot(frame, out evictedKey);
			bool flag2 = slot < 0;
			if (flag2)
			{
				return TileCache.TileUploadResult.NoSlot;
			}
			Stopwatch swPaint = FrameProfile.Start();
			bool flag3 = evictedKey != long.MinValue && evictedKey != -9223372036854775807L;
			if (flag3)
			{
				this.slotMap.Remove(evictedKey);
				this.RepointEvicted(evictedKey);
			}
			long paintTicks = swPaint.ElapsedTicks;
			Stopwatch swBlit = FrameProfile.Start();
			for (int li2 = 0; li2 < this.layers.Count; li2++)
			{
				this.CopyTileToSlot(this.layers[li2], tilesByLayer[li2], slot);
			}
			FrameProfile.AddBlit(swBlit.ElapsedTicks);
			long key = TileCache.PackKey(face, level, tx, ty);
			this.slotMap[key] = slot;
			this.slotOwner[slot] = key;
			this.slotFrame[slot] = frame;
			swPaint.Restart();
			this.PaintResident(face, level, tx, ty, slot % this.SlotsPerRow, slot / this.SlotsPerRow, level);
			FrameProfile.AddPaint(paintTicks + swPaint.ElapsedTicks);
			return TileCache.TileUploadResult.Uploaded;
		}

		/// <summary>
		/// Diagnostic: count resident tiles whose OWN page-table texel doesn't resolve to themselves —
		/// unresolved, wrong slot, or wrong resident level. Non-zero means the CPU residency map and the page
		/// table have desynced (streamer thinks a tile resident while the shader resolves a coarser ancestor).
		/// </summary>
		// Token: 0x060001E6 RID: 486 RVA: 0x0000E898 File Offset: 0x0000CA98
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
				bool flag = level <= this.DirectoryLevel;
				int sx;
				int sy;
				int flvl;
				bool resident;
				if (flag)
				{
					int texelX = face * this.DirFaceStride + tx;
					int texelY = (1 << level) - 1 + ty;
					TileCache.UnpackPageWord(this.pageWords[texelY * this.ptW + texelX], out sx, out sy, out flvl, out resident);
				}
				else
				{
					int fineIdx;
					int num;
					bool flag2 = !this.TryGetFineTexelIndex(face, level, tx, ty, out fineIdx, out num);
					if (flag2)
					{
						continue;
					}
					TileCache.UnpackFineWord(this.fineWords[fineIdx], out sx, out sy, out flvl, out resident);
				}
				bool flag3 = !resident;
				if (flag3)
				{
					desync++;
				}
				else
				{
					int slot = sy * this.SlotsPerRow + sx;
					bool flag4 = slot != kv.Value || flvl != level;
					if (flag4)
					{
						desync++;
					}
				}
			}
			return desync;
		}

		/// <summary>
		/// Check the indirection against the residency map and report every violation found, most structural
		/// first. This is the "terrain shows tiles from somewhere else" detector: that symptom means a page-table
		/// texel is pointing at an atlas slot whose CURRENT occupant isn't an ancestor of that texel, and each
		/// check below fails on a different cause, so a report names the bug instead of narrowing it.
		///
		/// <paramref name="deep" /> = false runs only the O(slots + blocks) bookkeeping checks (A, B), which are
		/// cheap enough to run every frame — worth doing, because catching the FRAME the invariant breaks is
		/// what separates a cause from a symptom. true adds the full O(directory + live block texels) resolve
		/// walk (C, D), which is millions of texels — on demand only.
		///
		/// Returns the number of violations; the first <paramref name="maxReports" /> are appended to
		/// <paramref name="report" />.
		/// </summary>
		// Token: 0x060001E7 RID: 487 RVA: 0x0000E9D8 File Offset: 0x0000CBD8
		public int ValidateIndirection(List<string> report, int maxReports = 8, bool deep = false)
		{
			TileCache.<>c__DisplayClass90_0 CS$<>8__locals1;
			CS$<>8__locals1.report = report;
			CS$<>8__locals1.maxReports = maxReports;
			CS$<>8__locals1.<>4__this = this;
			CS$<>8__locals1.bad = 0;
			for (int s = 0; s < this.TotalSlots; s++)
			{
				long owner = this.slotOwner[s];
				bool flag = owner == long.MinValue || owner == -9223372036854775807L;
				if (!flag)
				{
					int mapped;
					bool flag2 = !this.slotMap.TryGetValue(owner, out mapped);
					if (flag2)
					{
						this.<ValidateIndirection>g__Report|90_0(string.Format("A: slot {0} owns {1} but slotMap has no entry (orphaned slot)", s, TileCache.KeyStr(owner)), ref CS$<>8__locals1);
					}
					else
					{
						bool flag3 = mapped != s;
						if (flag3)
						{
							this.<ValidateIndirection>g__Report|90_0(string.Format("A: slot {0} owns {1} but slotMap points it at slot {2}", s, TileCache.KeyStr(owner), mapped), ref CS$<>8__locals1);
						}
					}
				}
			}
			foreach (KeyValuePair<long, int> kv in this.slotMap)
			{
				long owner2 = this.slotOwner[kv.Value];
				bool flag4 = owner2 != kv.Key && owner2 != -9223372036854775807L;
				if (flag4)
				{
					this.<ValidateIndirection>g__Report|90_0(string.Format("A: slotMap says {0} -> slot {1}, but that slot is owned by ", TileCache.KeyStr(kv.Key), kv.Value) + ((owner2 == long.MinValue) ? "NOTHING (freed)" : TileCache.KeyStr(owner2)), ref CS$<>8__locals1);
				}
			}
			bool flag5 = this.BlockDepth > 0;
			if (flag5)
			{
				foreach (KeyValuePair<long, int> kv2 in this.blockMap)
				{
					bool flag6 = this.blockOwner[kv2.Value] != kv2.Key;
					if (flag6)
					{
						this.<ValidateIndirection>g__Report|90_0(string.Format("B: blockMap says {0} -> block {1}, but that block is owned by ", TileCache.KeyStr(kv2.Key), kv2.Value) + ((this.blockOwner[kv2.Value] == long.MinValue) ? "NOTHING (freed)" : TileCache.KeyStr(this.blockOwner[kv2.Value])), ref CS$<>8__locals1);
					}
					int bf;
					int num;
					int bx;
					int by;
					TileCache.UnpackKey(kv2.Key, out bf, out num, out bx, out by);
					int idx = ((1 << this.DirectoryLevel) - 1 + by) * this.ptW + bf * this.DirFaceStride + bx;
					bool hasBlock;
					int blkX;
					int blkY;
					TileCache.UnpackBlockRef(this.pageWords[idx], out hasBlock, out blkX, out blkY);
					int referenced = blkY * this.blockGridW + blkX;
					bool flag7 = !hasBlock;
					if (flag7)
					{
						this.<ValidateIndirection>g__Report|90_0(string.Format("B: block {0} is owned by {1}, whose texel has no hasBlock", kv2.Value, TileCache.KeyStr(kv2.Key)), ref CS$<>8__locals1);
					}
					else
					{
						bool flag8 = referenced != kv2.Value;
						if (flag8)
						{
							this.<ValidateIndirection>g__Report|90_0(string.Format("B: {0} owns block {1} but its texel references block {2}", TileCache.KeyStr(kv2.Key), kv2.Value, referenced), ref CS$<>8__locals1);
						}
					}
				}
				for (int s2 = 0; s2 < this.TotalBlocks; s2++)
				{
					bool flag9 = this.blockOwner[s2] != long.MinValue && !this.blockMap.ContainsKey(this.blockOwner[s2]);
					if (flag9)
					{
						this.<ValidateIndirection>g__Report|90_0(string.Format("B: block {0} is owned by {1}, which blockMap doesn't list", s2, TileCache.KeyStr(this.blockOwner[s2])), ref CS$<>8__locals1);
					}
				}
			}
			bool flag10 = !deep;
			int bad;
			if (flag10)
			{
				bad = CS$<>8__locals1.bad;
			}
			else
			{
				for (int face = 0; face < 6; face++)
				{
					for (int level = 0; level <= this.DirectoryLevel; level++)
					{
						int g = 1 << level;
						for (int ty = 0; ty < g; ty++)
						{
							for (int tx = 0; tx < g; tx++)
							{
								int idx2 = ((1 << level) - 1 + ty) * this.ptW + face * this.DirFaceStride + tx;
								int sx;
								int sy;
								int flvl;
								bool res;
								TileCache.UnpackPageWord(this.pageWords[idx2], out sx, out sy, out flvl, out res);
								bool flag11 = !res;
								if (!flag11)
								{
									this.<ValidateIndirection>g__CheckResolve|90_1(face, level, tx, ty, sx, sy, flvl, "C", ref CS$<>8__locals1);
									bool flag12 = CS$<>8__locals1.bad > CS$<>8__locals1.maxReports * 4;
									if (flag12)
									{
										return CS$<>8__locals1.bad;
									}
								}
							}
						}
					}
				}
				bool flag13 = this.BlockDepth > 0;
				if (flag13)
				{
					foreach (KeyValuePair<long, int> kv3 in this.blockMap)
					{
						int num;
						int face2;
						int dirTx;
						int dirTy;
						TileCache.UnpackKey(kv3.Key, out face2, out num, out dirTx, out dirTy);
						for (int level2 = this.DirectoryLevel + 1; level2 <= this.maxLevel; level2++)
						{
							int i = level2 - this.DirectoryLevel;
							int span = 1 << i;
							for (int sy2 = 0; sy2 < span; sy2++)
							{
								for (int sx2 = 0; sx2 < span; sx2++)
								{
									int px = kv3.Value % this.blockGridW * this.blockW + sx2;
									int py = kv3.Value / this.blockGridW * this.blockH + this.BlockRowStart(level2) + sy2;
									int fx;
									int fy;
									int flvl2;
									bool res2;
									TileCache.UnpackFineWord(this.fineWords[py * this.fineW + px], out fx, out fy, out flvl2, out res2);
									bool flag14 = !res2;
									if (!flag14)
									{
										this.<ValidateIndirection>g__CheckResolve|90_1(face2, level2, (dirTx << i) + sx2, (dirTy << i) + sy2, fx, fy, flvl2, "D", ref CS$<>8__locals1);
										bool flag15 = CS$<>8__locals1.bad > CS$<>8__locals1.maxReports * 4;
										if (flag15)
										{
											return CS$<>8__locals1.bad;
										}
									}
								}
							}
						}
					}
				}
				bad = CS$<>8__locals1.bad;
			}
			return bad;
		}

		// Token: 0x060001E8 RID: 488 RVA: 0x0000F0B0 File Offset: 0x0000D2B0
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

		/// <summary>Flush incremental page-table changes to the GPU. Call once per frame. Uploads only the
		/// dirty sub-rectangle of texels changed since the last call (the RGBA32 mirror is kept current in
		/// WritePageTexel, so there is no whole-table rescan).</summary>
		// Token: 0x060001E9 RID: 489 RVA: 0x0000F108 File Offset: 0x0000D308
		public void ApplyPageTable()
		{
			this.FlushFineAtlas();
			bool flag = !this.pageTableDirty;
			if (!flag)
			{
				bool flag2 = !this.useRgba32;
				if (flag2)
				{
					this.PageTable.SetPixelData<uint>(this.pageWords, 0, 0);
					this.PageTable.Apply(false, false);
					this.pageTableDirty = false;
					this.hasDirtyRect = false;
				}
				else
				{
					int x = this.hasDirtyRect ? this.dirtyMinX : 0;
					int y = this.hasDirtyRect ? this.dirtyMinY : 0;
					int w = this.hasDirtyRect ? (this.dirtyMaxX - this.dirtyMinX + 1) : this.ptW;
					int h = this.hasDirtyRect ? (this.dirtyMaxY - this.dirtyMinY + 1) : this.ptH;
					bool flag3 = w == this.ptW;
					if (flag3)
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
			}
		}

		// Token: 0x060001EA RID: 490 RVA: 0x0000F279 File Offset: 0x0000D479
		public bool IsTileResident(long key)
		{
			return this.slotMap.ContainsKey(key);
		}

		// Token: 0x060001EB RID: 491 RVA: 0x0000F287 File Offset: 0x0000D487
		public bool IsTileResident(int face, int level, int tx, int ty)
		{
			return this.slotMap.ContainsKey(TileCache.PackKey(face, level, tx, ty));
		}

		/// <summary>Refresh the LRU timestamp so this slot won't be evicted while the tile is needed.</summary>
		// Token: 0x060001EC RID: 492 RVA: 0x0000F2A0 File Offset: 0x0000D4A0
		public void MarkTileUsed(long key, int frame)
		{
			int slot;
			bool flag = this.slotMap.TryGetValue(key, out slot);
			if (flag)
			{
				this.slotFrame[slot] = frame;
			}
		}

		// Token: 0x1700005A RID: 90
		// (get) Token: 0x060001ED RID: 493 RVA: 0x0000F2C9 File Offset: 0x0000D4C9
		public int OccupiedSlots
		{
			get
			{
				return this.slotMap.Count;
			}
		}

		/// <summary>Returns tile counts per level, indexed 0..maxLevel inclusive.</summary>
		// Token: 0x060001EE RID: 494 RVA: 0x0000F2D8 File Offset: 0x0000D4D8
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

		/// <summary>
		/// Bind the shared page table + sizes and every layer's atlas onto a material. Per-material (never a
		/// global) — scaled space renders multiple bodies at once, each with its own cache. Call after bootstrap.
		/// </summary>
		// Token: 0x060001EF RID: 495 RVA: 0x0000F374 File Offset: 0x0000D574
		public void BindToMaterial(Material mat)
		{
			mat.SetTexture("_VTPageTable", this.PageTable);
			mat.SetTexture("_VTFineAtlas", this.FineAtlas);
			mat.SetFloat("_VTAtlasSize", (float)this.atlasSize);
			mat.SetFloat("_VTTileSize", (float)this.tileSize);
			mat.SetFloat("_VTTileBorder", (float)this.borderPx);
			mat.SetFloat("_VTMaxTileLevel", (float)this.maxLevel);
			mat.SetFloat("_VTDirLevel", (float)this.DirectoryLevel);
			mat.SetFloat("_VTBlockW", (float)this.blockW);
			mat.SetFloat("_VTBlockH", (float)this.blockH);
			bool hasNormal = false;
			foreach (TileCache.LayerState i in this.layers)
			{
				bool flag = i.atlas != null;
				if (flag)
				{
					mat.SetTexture(i.uniformPrefix + "TileAtlas", i.atlas);
				}
				bool flag2 = i.id == VTLayer.Normal;
				if (flag2)
				{
					hasNormal = true;
				}
			}
			mat.SetFloat("_HasNormalVT", hasNormal ? 1f : 0f);
		}

		// Token: 0x060001F0 RID: 496 RVA: 0x0000F4CC File Offset: 0x0000D6CC
		public static long PackKey(int face, int level, int tx, int ty)
		{
			return (long)face << 40 | (long)level << 32 | (long)(ty & 65535) << 16 | (long)(tx & 65535);
		}

		// Token: 0x060001F1 RID: 497 RVA: 0x0000F4EE File Offset: 0x0000D6EE
		public static void UnpackKey(long key, out int face, out int level, out int tx, out int ty)
		{
			tx = (int)(key & 65535L);
			ty = (int)(key >> 16 & 65535L);
			level = (int)(key >> 32 & 255L);
			face = (int)(key >> 40 & 255L);
		}

		// Token: 0x060001F2 RID: 498 RVA: 0x0000F528 File Offset: 0x0000D728
		public void Dispose()
		{
			foreach (TileCache.LayerState i in this.layers)
			{
				bool flag = i.atlas != null;
				if (flag)
				{
					Object.Destroy(i.atlas);
					i.atlas = null;
				}
				ITileLayerSource source = i.source;
				if (source != null)
				{
					source.Dispose();
				}
			}
			this.layers.Clear();
			bool flag2 = this.PageTable != null;
			if (flag2)
			{
				Object.Destroy(this.PageTable);
				this.PageTable = null;
			}
			bool flag3 = this.FineAtlas != null;
			if (flag3)
			{
				Object.Destroy(this.FineAtlas);
				this.FineAtlas = null;
			}
			bool flag4 = this.fineStaging != null;
			if (flag4)
			{
				Object.Destroy(this.fineStaging);
				this.fineStaging = null;
			}
			this.slotMap.Clear();
			this.blockMap.Clear();
			this.dirtyBlocks.Clear();
		}

		// Token: 0x060001F3 RID: 499 RVA: 0x0000F658 File Offset: 0x0000D858
		private bool EnsureAtlasAllocated(TileCache.LayerState layer, Texture2D firstTile, string debugPath)
		{
			bool flag = layer.atlas != null;
			bool result;
			if (flag)
			{
				result = true;
			}
			else
			{
				int blockSize = TileCache.CompressedBlockSize(firstTile.format);
				bool flag2 = blockSize > 1 && this.SlotSize % blockSize != 0;
				if (flag2)
				{
					MirageDebug.LogError(string.Format("TileCache: {0} block size {1} doesn't divide slot size {2} — ", firstTile.format, blockSize, this.SlotSize) + "use aligned tile/border or uncompressed format. (" + debugPath + ")");
					result = false;
				}
				else
				{
					layer.atlas = new Texture2D(this.atlasSize, this.atlasSize, firstTile.format, false, layer.linear);
					layer.atlas.name = "VTAtlas_" + layer.id.ToString();
					layer.atlas.wrapMode = 1;
					layer.atlas.filterMode = 1;
					result = true;
				}
			}
			return result;
		}

		// Token: 0x060001F4 RID: 500 RVA: 0x0000F74C File Offset: 0x0000D94C
		private bool ValidateTile(TileCache.LayerState layer, Texture2D tile, string debugPath)
		{
			bool flag = tile.width != this.SlotSize || tile.height != this.SlotSize;
			bool result;
			if (flag)
			{
				MirageDebug.LogError(string.Format("TileCache: tile {0} is {1}x{2}, expected {3}x{4}", new object[]
				{
					debugPath,
					tile.width,
					tile.height,
					this.SlotSize,
					this.SlotSize
				}));
				result = false;
			}
			else
			{
				bool flag2 = tile.format != layer.atlas.format;
				if (flag2)
				{
					MirageDebug.LogError(string.Format("TileCache: tile {0} format {1} ≠ atlas {2}", debugPath, tile.format, layer.atlas.format));
					result = false;
				}
				else
				{
					result = true;
				}
			}
			return result;
		}

		// Token: 0x060001F5 RID: 501 RVA: 0x0000F828 File Offset: 0x0000DA28
		private void CopyTileToSlot(TileCache.LayerState layer, Texture2D tile, int slot)
		{
			tile.GetNativeTexturePtr();
			int slotX = slot % this.SlotsPerRow;
			int slotY = slot / this.SlotsPerRow;
			Graphics.CopyTexture(tile, 0, 0, 0, 0, this.SlotSize, this.SlotSize, layer.atlas, 0, 0, slotX * this.SlotSize, slotY * this.SlotSize);
		}

		// Token: 0x060001F6 RID: 502 RVA: 0x0000F880 File Offset: 0x0000DA80
		private int AllocateAnyFreeSlot()
		{
			int total = this.TotalSlots;
			for (int i = 0; i < total; i++)
			{
				bool flag = this.slotOwner[i] == long.MinValue;
				if (flag)
				{
					return i;
				}
			}
			return -1;
		}

		// Token: 0x060001F7 RID: 503 RVA: 0x0000F8C8 File Offset: 0x0000DAC8
		private int AllocateStreamingSlot(int frame, out long evictedKey)
		{
			evictedKey = long.MinValue;
			int slot = this.AllocateAnyFreeSlot();
			bool flag = slot >= 0;
			int result;
			if (flag)
			{
				result = slot;
			}
			else
			{
				int oldestFrame = int.MaxValue;
				int lruSlot = -1;
				int total = this.TotalSlots;
				for (int i = 0; i < total; i++)
				{
					bool flag2 = this.slotOwner[i] == long.MinValue || this.slotOwner[i] == -9223372036854775807L;
					if (!flag2)
					{
						bool flag3 = this.slotFrame[i] == frame;
						if (!flag3)
						{
							bool flag4 = this.slotFrame[i] < oldestFrame;
							if (flag4)
							{
								oldestFrame = this.slotFrame[i];
								lruSlot = i;
							}
						}
					}
				}
				bool flag5 = lruSlot < 0;
				if (flag5)
				{
					result = -1;
				}
				else
				{
					evictedKey = this.slotOwner[lruSlot];
					this.slotOwner[lruSlot] = long.MinValue;
					this.slotFrame[lruSlot] = 0;
					result = lruSlot;
				}
			}
			return result;
		}

		// Token: 0x060001F8 RID: 504 RVA: 0x0000F9C8 File Offset: 0x0000DBC8
		private void WritePageTexel(int face, int level, int tx, int ty, int slotX, int slotY, int residentLevel, bool valid)
		{
			bool flag = slotX > 63 || slotY > 63;
			if (flag)
			{
				MirageDebug.LogError(string.Format("TileCache: slot {0},{1} exceeds the {2} 5-bit page-table field — widen the texel format", slotX, slotY, 63));
			}
			else
			{
				int texelX = face * this.DirFaceStride + tx;
				int texelY = (1 << level) - 1 + ty;
				int idx = texelY * this.ptW + texelX;
				bool hasBlock;
				int blockX;
				int blockY;
				TileCache.UnpackBlockRef(this.pageWords[idx], out hasBlock, out blockX, out blockY);
				this.StorePageWord(idx, texelX, texelY, TileCache.PackPageWord(slotX, slotY, residentLevel, valid, hasBlock, blockX, blockY));
			}
		}

		// Token: 0x060001F9 RID: 505 RVA: 0x0000FA68 File Offset: 0x0000DC68
		private void StorePageWord(int idx, int texelX, int texelY, uint word)
		{
			this.pageWords[idx] = word;
			bool flag = this.useRgba32;
			if (flag)
			{
				this.pageColorsScratch[idx] = new Color32((byte)(word & 255U), (byte)(word >> 8 & 255U), (byte)(word >> 16 & 255U), (byte)(word >> 24 & 255U));
			}
			bool flag2 = !this.hasDirtyRect;
			if (flag2)
			{
				this.dirtyMaxX = texelX;
				this.dirtyMinX = texelX;
				this.dirtyMaxY = texelY;
				this.dirtyMinY = texelY;
				this.hasDirtyRect = true;
			}
			else
			{
				bool flag3 = texelX < this.dirtyMinX;
				if (flag3)
				{
					this.dirtyMinX = texelX;
				}
				bool flag4 = texelX > this.dirtyMaxX;
				if (flag4)
				{
					this.dirtyMaxX = texelX;
				}
				bool flag5 = texelY < this.dirtyMinY;
				if (flag5)
				{
					this.dirtyMinY = texelY;
				}
				bool flag6 = texelY > this.dirtyMaxY;
				if (flag6)
				{
					this.dirtyMaxY = texelY;
				}
			}
			this.pageTableDirty = true;
		}

		/// <summary>
		/// Paint (slotX, slotY, residentLevel) into (level, tx, ty) and every finer descendant that inherits
		/// from it, stopping at any descendant that is itself resident. Maintains the resolved-table invariant:
		/// every texel points at the nearest resident tile at level ≤ its own.
		/// </summary>
		// Token: 0x060001FA RID: 506 RVA: 0x0000FB60 File Offset: 0x0000DD60
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

		/// <summary>
		/// A tile became resident at <paramref name="residentLevel" />. Levels ≤ DirectoryLevel paint the
		/// directory directly; deeper levels exist only inside a fine block, so they're published by marking
		/// that block dirty. Either way, any block beneath this tile has a new fallback and must re-resolve.
		/// </summary>
		// Token: 0x060001FB RID: 507 RVA: 0x0000FC04 File Offset: 0x0000DE04
		private void PaintResident(int face, int level, int tx, int ty, int slotX, int slotY, int residentLevel)
		{
			bool flag = level <= this.DirectoryLevel;
			if (flag)
			{
				this.PaintSubtree(face, level, tx, ty, slotX, slotY, residentLevel);
			}
			this.MarkBlocksUnder(face, level, tx, ty);
		}

		/// <summary>
		/// A streaming tile was just evicted (already removed from slotMap). Fall its region back to the
		/// nearest resident ancestor (guaranteed — level 0 is pinned).
		/// </summary>
		// Token: 0x060001FC RID: 508 RVA: 0x0000FC40 File Offset: 0x0000DE40
		private void RepointEvicted(long evictedKey)
		{
			int face;
			int level;
			int tx;
			int ty;
			TileCache.UnpackKey(evictedKey, out face, out level, out tx, out ty);
			bool flag = level > this.DirectoryLevel;
			if (flag)
			{
				this.MarkBlocksUnder(face, level, tx, ty);
			}
			else
			{
				for (int a = level - 1; a >= 0; a--)
				{
					int shift = level - a;
					int aSlot;
					bool flag2 = this.slotMap.TryGetValue(TileCache.PackKey(face, a, tx >> shift, ty >> shift), out aSlot);
					if (flag2)
					{
						this.PaintSubtree(face, level, tx, ty, aSlot % this.SlotsPerRow, aSlot / this.SlotsPerRow, a);
						this.MarkBlocksUnder(face, level, tx, ty);
						return;
					}
				}
				this.WritePageTexel(face, level, tx, ty, 0, 0, 0, false);
				this.MarkBlocksUnder(face, level, tx, ty);
			}
		}

		// Token: 0x060001FD RID: 509 RVA: 0x0000FD10 File Offset: 0x0000DF10
		private static int NextPow2(int v)
		{
			int p;
			for (p = 1; p < v; p <<= 1)
			{
			}
			return p;
		}

		// Token: 0x060001FE RID: 510 RVA: 0x0000FD34 File Offset: 0x0000DF34
		private static void ComputeFineDims(int blockDepth, out int blockW, out int blockH, out int gridW, out int gridH)
		{
			blockW = 1 << blockDepth;
			blockH = TileCache.NextPow2((1 << blockDepth + 1) - 2);
			gridW = Math.Min(64, 2048);
			gridH = Math.Min(64, Math.Max(1, (2048 + gridW - 1) / gridW));
		}

		// Token: 0x060001FF RID: 511 RVA: 0x0000FD88 File Offset: 0x0000DF88
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

		/// <summary>Row offset of level <paramref name="level" /> inside a block's mip stack. The block's top
		/// row is DirectoryLevel+1 (2x2), so rowStart(dir+1) = 0.</summary>
		// Token: 0x06000200 RID: 512 RVA: 0x0000FDCA File Offset: 0x0000DFCA
		private int BlockRowStart(int level)
		{
			return (1 << level - this.DirectoryLevel) - 2;
		}

		/// <summary>
		/// Ensure a fine block exists for the directory tile covering <paramref name="face" />/(dirTx,dirTy)
		/// and refresh its LRU stamp. Returns false when there's no fine tier, or the atlas is full of blocks
		/// still needed this frame (transient — the shader just falls back to coarse, which is never a hole).
		/// </summary>
		// Token: 0x06000201 RID: 513 RVA: 0x0000FDDC File Offset: 0x0000DFDC
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
						this.SetBlockRef(face, dirTx, dirTy, slot);
						this.dirtyBlocks.Add(slot);
						result = true;
					}
				}
			}
			return result;
		}

		// Token: 0x06000202 RID: 514 RVA: 0x0000FE88 File Offset: 0x0000E088
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
			int ef;
			int num;
			int ex;
			int ey;
			TileCache.UnpackKey(this.blockOwner[lru], out ef, out num, out ex, out ey);
			this.blockMap.Remove(this.blockOwner[lru]);
			this.SetBlockRef(ef, ex, ey, -1);
			this.dirtyBlocks.Remove(lru);
			this.blockOwner[lru] = long.MinValue;
			this.blockFrame[lru] = 0;
			return lru;
		}

		/// <summary>Write (or clear, with blockSlot &lt; 0) the block reference in a directory texel, preserving
		/// its resolve bits — the two halves of the merged coarse texel have different owners.</summary>
		// Token: 0x06000203 RID: 515 RVA: 0x0000FFA4 File Offset: 0x0000E1A4
		private void SetBlockRef(int face, int dirTx, int dirTy, int blockSlot)
		{
			int texelX = face * this.DirFaceStride + dirTx;
			int texelY = (1 << this.DirectoryLevel) - 1 + dirTy;
			int idx = texelY * this.ptW + texelX;
			int sx;
			int sy;
			int flvl;
			bool res;
			TileCache.UnpackPageWord(this.pageWords[idx], out sx, out sy, out flvl, out res);
			bool has = blockSlot >= 0;
			this.StorePageWord(idx, texelX, texelY, TileCache.PackPageWord(sx, sy, flvl, res, has, has ? (blockSlot % this.blockGridW) : 0, has ? (blockSlot / this.blockGridW) : 0));
		}

		/// <summary>Mark every allocated block that lies under (face, level, tx, ty) dirty. Needed because a
		/// COARSE tile's residency change moves the fallback of the fine texels beneath it.</summary>
		// Token: 0x06000204 RID: 516 RVA: 0x00010030 File Offset: 0x0000E230
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

		/// <summary>
		/// Fully re-resolve one block: every texel gets the nearest resident tile at level ≤ its own. Seeded
		/// from the directory tile's ALREADY-resolved texel and painted top-down, so it costs O(block texels)
		/// with no per-texel ancestor walk. Doc §6: blocks are event-driven (dirty), not per-frame.
		/// </summary>
		// Token: 0x06000205 RID: 517 RVA: 0x00010150 File Offset: 0x0000E350
		private void ResolveBlock(int blockSlot)
		{
			int face;
			int num;
			int dirTx;
			int dirTy;
			TileCache.UnpackKey(this.blockOwner[blockSlot], out face, out num, out dirTx, out dirTy);
			int texelX = face * this.DirFaceStride + dirTx;
			int texelY = (1 << this.DirectoryLevel) - 1 + dirTy;
			int sx;
			int sy;
			int flvl;
			bool res;
			TileCache.UnpackPageWord(this.pageWords[texelY * this.ptW + texelX], out sx, out sy, out flvl, out res);
			this.PaintBlockChildren(face, this.DirectoryLevel, dirTx, dirTy, blockSlot, dirTx, dirTy, sx, sy, flvl, res);
		}

		// Token: 0x06000206 RID: 518 RVA: 0x000101CC File Offset: 0x0000E3CC
		private void PaintBlockChildren(int face, int level, int tx, int ty, int blockSlot, int dirTx, int dirTy, int slotX, int slotY, int residentLevel, bool resident)
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
						int csx = slotX;
						int csy = slotY;
						int crl = residentLevel;
						bool cres = resident;
						int s;
						bool flag2 = this.slotMap.TryGetValue(TileCache.PackKey(face, childLevel, cx, cy), out s);
						if (flag2)
						{
							csx = s % this.SlotsPerRow;
							csy = s / this.SlotsPerRow;
							crl = childLevel;
							cres = true;
						}
						this.WriteFineTexel(blockSlot, childLevel, cx - (dirTx << i), cy - (dirTy << i), csx, csy, crl, cres);
						this.PaintBlockChildren(face, childLevel, cx, cy, blockSlot, dirTx, dirTy, csx, csy, crl, cres);
					}
				}
			}
		}

		// Token: 0x06000207 RID: 519 RVA: 0x000102D8 File Offset: 0x0000E4D8
		private void WriteFineTexel(int blockSlot, int level, int subX, int subY, int slotX, int slotY, int residentLevel, bool valid)
		{
			int px = blockSlot % this.blockGridW * this.blockW + subX;
			int py = blockSlot / this.blockGridW * this.blockH + this.BlockRowStart(level) + subY;
			this.fineWords[py * this.fineW + px] = TileCache.PackFineWord(slotX, slotY, residentLevel, valid);
		}

		/// <summary>Index of the fine texel for a tile at level &gt; DirectoryLevel, if its block is allocated.</summary>
		// Token: 0x06000208 RID: 520 RVA: 0x00010334 File Offset: 0x0000E534
		private bool TryGetFineTexelIndex(int face, int level, int tx, int ty, out int idx, out int blockSlot)
		{
			idx = 0;
			blockSlot = -1;
			bool flag = this.BlockDepth <= 0 || level <= this.DirectoryLevel || level > this.maxLevel;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				int i = level - this.DirectoryLevel;
				bool flag2 = !this.blockMap.TryGetValue(TileCache.PackKey(face, this.DirectoryLevel, tx >> i, ty >> i), out blockSlot);
				if (flag2)
				{
					result = false;
				}
				else
				{
					int px = blockSlot % this.blockGridW * this.blockW + (tx - (tx >> i << i));
					int py = blockSlot / this.blockGridW * this.blockH + this.BlockRowStart(level) + (ty - (ty >> i << i));
					idx = py * this.fineW + px;
					result = true;
				}
			}
			return result;
		}

		/// <summary>
		/// Re-resolve dirty blocks and push them to the GPU. Called from ApplyPageTable so the whole indirection
		/// lands in one place, once per frame.
		///
		/// ONE Apply PER FRAME IS A CORRECTNESS REQUIREMENT, not an optimisation. This used to stage each block
		/// through a single block-sized texture — SetPixelData, Apply, CopyTexture, repeat — on the assumption
		/// that "Apply and CopyTexture run in submission order on the render thread, so a block's copy reads the
		/// staging before the next block's Apply overwrites it". Unity does not promise that: Texture2D.Apply
		/// queues a CPU-&gt;GPU upload against ONE resource, and N of them in a frame can coalesce or land out of
		/// order relative to the CopyTexture calls that read them, so a block's atlas rectangle can end up with
		/// a DIFFERENT block's texels. Every fine texel under that directory tile then resolves through a
		/// stranger — which reads on screen as a patch of terrain from somewhere else entirely, with the CPU-side
		/// fineWords perfectly correct (so ValidateIndirection, which only sees CPU state, stays silent).
		///
		/// Now: fill up to MaxBlockFlushesPerFrame bands of a strip-shaped staging texture, Apply the strip ONCE,
		/// then issue the CopyTexture calls. No band is rewritten until the next frame, so there is no aliasing
		/// window at all.
		/// </summary>
		// Token: 0x06000209 RID: 521 RVA: 0x00010410 File Offset: 0x0000E610
		private void FlushFineAtlas()
		{
			bool flag = this.dirtyBlocks.Count == 0;
			if (!flag)
			{
				this.resolvedBlocks.Clear();
				foreach (int slot in this.dirtyBlocks)
				{
					bool flag2 = this.blockOwner[slot] != long.MinValue;
					if (flag2)
					{
						this.resolvedBlocks.Add(slot);
					}
				}
				this.dirtyBlocks.Clear();
				bool flag3 = this.resolvedBlocks.Count == 0;
				if (!flag3)
				{
					bool flag4 = this.resolvedBlocks.Count > 48;
					if (flag4)
					{
						this.resolvedBlocks.Sort((int a, int b) => this.blockFrame[b].CompareTo(this.blockFrame[a]));
					}
					int i = Math.Min(this.resolvedBlocks.Count, 48);
					for (int j = i; j < this.resolvedBlocks.Count; j++)
					{
						int slot2 = this.resolvedBlocks[j];
						int f;
						int num;
						int bx;
						int by;
						TileCache.UnpackKey(this.blockOwner[slot2], out f, out num, out bx, out by);
						this.SetBlockRef(f, bx, by, -1);
						this.dirtyBlocks.Add(slot2);
					}
					NativeArray<uint> staging = this.fineStaging.GetRawTextureData<uint>();
					for (int k = 0; k < i; k++)
					{
						int slot3 = this.resolvedBlocks[k];
						this.ResolveBlock(slot3);
						int ox = slot3 % this.blockGridW * this.blockW;
						int oy = slot3 / this.blockGridW * this.blockH;
						int band = k * this.blockW * this.blockH;
						for (int ry = 0; ry < this.blockH; ry++)
						{
							int src = (oy + ry) * this.fineW + ox;
							int dst = band + ry * this.blockW;
							for (int cx = 0; cx < this.blockW; cx++)
							{
								staging[dst + cx] = this.fineWords[src + cx];
							}
						}
						int num;
						int f2;
						int bx2;
						int by2;
						TileCache.UnpackKey(this.blockOwner[slot3], out f2, out num, out bx2, out by2);
						this.EnsureBlockPublished(f2, bx2, by2, slot3);
					}
					this.fineStaging.Apply(false, false);
					for (int l = 0; l < i; l++)
					{
						int slot4 = this.resolvedBlocks[l];
						Graphics.CopyTexture(this.fineStaging, 0, 0, 0, l * this.blockH, this.blockW, this.blockH, this.FineAtlas, 0, 0, slot4 % this.blockGridW * this.blockW, slot4 / this.blockGridW * this.blockH);
					}
				}
			}
		}

		/// <summary>Point a directory texel at <paramref name="blockSlot" /> only if it doesn't already, so a
		/// republish costs nothing when nothing changed.</summary>
		// Token: 0x0600020A RID: 522 RVA: 0x00010708 File Offset: 0x0000E908
		private void EnsureBlockPublished(int face, int dirTx, int dirTy, int blockSlot)
		{
			int idx = ((1 << this.DirectoryLevel) - 1 + dirTy) * this.ptW + face * this.DirFaceStride + dirTx;
			bool hasBlock;
			int bx;
			int by;
			TileCache.UnpackBlockRef(this.pageWords[idx], out hasBlock, out bx, out by);
			bool flag = hasBlock && by * this.blockGridW + bx == blockSlot;
			if (!flag)
			{
				this.SetBlockRef(face, dirTx, dirTy, blockSlot);
			}
		}

		// Token: 0x0600020B RID: 523 RVA: 0x00010774 File Offset: 0x0000E974
		private static int CompressedBlockSize(TextureFormat fmt)
		{
			int result;
			if (fmt != 10 && fmt != 12 && fmt - 24 > 3)
			{
				result = 1;
			}
			else
			{
				result = 4;
			}
			return result;
		}

		// Token: 0x0600020C RID: 524 RVA: 0x000107A8 File Offset: 0x0000E9A8
		[CompilerGenerated]
		private void <ValidateIndirection>g__Report|90_0(string msg, ref TileCache.<>c__DisplayClass90_0 A_2)
		{
			int bad = A_2.bad;
			A_2.bad = bad + 1;
			bool flag = A_2.report != null && A_2.report.Count < A_2.maxReports;
			if (flag)
			{
				A_2.report.Add(msg);
			}
		}

		// Token: 0x0600020D RID: 525 RVA: 0x000107F8 File Offset: 0x0000E9F8
		[CompilerGenerated]
		private void <ValidateIndirection>g__CheckResolve|90_1(int face, int level, int tx, int ty, int sx, int sy, int fallbackLevel, string tag, ref TileCache.<>c__DisplayClass90_0 A_9)
		{
			bool flag = fallbackLevel > level;
			if (flag)
			{
				this.<ValidateIndirection>g__Report|90_0(string.Format("{0}: {1} resolves to level {2}, FINER than itself", tag, TileCache.KeyStr(TileCache.PackKey(face, level, tx, ty)), fallbackLevel), ref A_9);
			}
			else
			{
				int shift = level - fallbackLevel;
				long ancestor = TileCache.PackKey(face, fallbackLevel, tx >> shift, ty >> shift);
				int slot = sy * this.SlotsPerRow + sx;
				int expected;
				bool flag2 = !this.slotMap.TryGetValue(ancestor, out expected);
				if (flag2)
				{
					this.<ValidateIndirection>g__Report|90_0(string.Format("{0}: {1} -> slot {2} as {3}, ", new object[]
					{
						tag,
						TileCache.KeyStr(TileCache.PackKey(face, level, tx, ty)),
						slot,
						TileCache.KeyStr(ancestor)
					}) + "but that tile is NOT resident (stale entry — an eviction failed to repaint)", ref A_9);
				}
				else
				{
					bool flag3 = expected != slot;
					if (flag3)
					{
						long occupant = this.slotOwner[slot];
						this.<ValidateIndirection>g__Report|90_0(string.Format("{0}: {1} -> slot {2}, but {3} ", new object[]
						{
							tag,
							TileCache.KeyStr(TileCache.PackKey(face, level, tx, ty)),
							slot,
							TileCache.KeyStr(ancestor)
						}) + string.Format("lives in slot {0}; slot {1} actually holds ", expected, slot) + ((occupant == long.MinValue) ? "NOTHING" : ((occupant == -9223372036854775807L) ? "a pinned tile" : TileCache.KeyStr(occupant))), ref A_9);
					}
				}
			}
		}

		// Token: 0x040001A0 RID: 416
		public readonly int atlasSize;

		// Token: 0x040001A1 RID: 417
		public readonly int tileSize;

		// Token: 0x040001A2 RID: 418
		public readonly int borderPx;

		// Token: 0x040001A3 RID: 419
		public readonly int maxLevel;

		// Token: 0x040001A4 RID: 420
		public const int MaxSlotCoord = 63;

		// Token: 0x040001A5 RID: 421
		public const int MaxFallbackLevel = 7;

		// Token: 0x040001A6 RID: 422
		public const int MaxFineFallbackLevel = 15;

		// Token: 0x040001A7 RID: 423
		public const int MaxBlockCoord = 63;

		/// <summary>Default split between the coarse directory and fine blocks (doc §2's L_dir). Levels ≤ this
		/// resolve straight out of the fixed directory; deeper levels need a fine block.</summary>
		// Token: 0x040001A8 RID: 424
		public const int DefaultDirectoryLevel = 7;

		// Token: 0x040001AA RID: 426
		private readonly List<TileCache.LayerState> layers = new List<TileCache.LayerState>();

		// Token: 0x040001AB RID: 427
		private readonly Dictionary<long, int> slotMap = new Dictionary<long, int>();

		// Token: 0x040001AC RID: 428
		private long[] slotOwner;

		// Token: 0x040001AD RID: 429
		private int[] slotFrame;

		// Token: 0x040001AE RID: 430
		private const long SLOT_FREE = -9223372036854775808L;

		// Token: 0x040001AF RID: 431
		private const long SLOT_PINNED = -9223372036854775807L;

		// Token: 0x040001B0 RID: 432
		private readonly int ptW;

		// Token: 0x040001B1 RID: 433
		private readonly int ptH;

		// Token: 0x040001B2 RID: 434
		private readonly bool useRgba32;

		// Token: 0x040001B3 RID: 435
		private uint[] pageWords;

		// Token: 0x040001B4 RID: 436
		private Color32[] pageColorsScratch;

		// Token: 0x040001B5 RID: 437
		private Color32[] pageRegionScratch;

		// Token: 0x040001B6 RID: 438
		private bool pageTableDirty;

		// Token: 0x040001B7 RID: 439
		private int dirtyMinX;

		// Token: 0x040001B8 RID: 440
		private int dirtyMinY;

		// Token: 0x040001B9 RID: 441
		private int dirtyMaxX;

		// Token: 0x040001BA RID: 442
		private int dirtyMaxY;

		// Token: 0x040001BB RID: 443
		private bool hasDirtyRect;

		// Token: 0x040001BC RID: 444
		private const int TargetBlockCount = 2048;

		// Token: 0x040001BD RID: 445
		private const long MaxFineAtlasBytes = 134217728L;

		// Token: 0x040001BE RID: 446
		private readonly int blockW;

		// Token: 0x040001BF RID: 447
		private readonly int blockH;

		// Token: 0x040001C0 RID: 448
		private readonly int blockGridW;

		// Token: 0x040001C1 RID: 449
		private readonly int blockGridH;

		// Token: 0x040001C2 RID: 450
		private readonly int fineW;

		// Token: 0x040001C3 RID: 451
		private readonly int fineH;

		// Token: 0x040001C4 RID: 452
		private uint[] fineWords;

		// Token: 0x040001C5 RID: 453
		private Texture2D fineStaging;

		// Token: 0x040001C6 RID: 454
		private readonly List<int> resolvedBlocks = new List<int>();

		/// <summary>
		/// Blocks resolved + uploaded in one frame. Bounds BOTH costs of a flush storm: a coarse tile's
		/// residency change dirties every block beneath it (a level-3 tile covers 16x16 = 256 directory tiles),
		/// and each block is ~2000 texels to re-resolve plus its own GPU upload — so an uncapped flush was
		/// hundreds of thousands of texel writes and hundreds of texture uploads on a single frame.
		/// Blocks past the cap stay dirty and are UNPUBLISHED until their turn (see FlushFineAtlas).
		/// </summary>
		// Token: 0x040001C7 RID: 455
		private const int MaxBlockFlushesPerFrame = 48;

		// Token: 0x040001C8 RID: 456
		private const long BLOCK_FREE = -9223372036854775808L;

		// Token: 0x040001C9 RID: 457
		private readonly Dictionary<long, int> blockMap = new Dictionary<long, int>();

		// Token: 0x040001CA RID: 458
		private long[] blockOwner;

		// Token: 0x040001CB RID: 459
		private int[] blockFrame;

		// Token: 0x040001CC RID: 460
		private readonly HashSet<int> dirtyBlocks = new HashSet<int>();

		/// <summary>A payload atlas + its load parameters. Atlas allocation is deferred to the first tile.</summary>
		// Token: 0x020000B7 RID: 183
		public sealed class LayerState
		{
			// Token: 0x040004C2 RID: 1218
			public VTLayer id;

			// Token: 0x040004C3 RID: 1219
			public string uniformPrefix;

			// Token: 0x040004C4 RID: 1220
			public ITileLayerSource source;

			// Token: 0x040004C5 RID: 1221
			public bool linear;

			// Token: 0x040004C6 RID: 1222
			public Texture2D atlas;
		}

		/// <summary>Outcome of a <see cref="M:Mirage.VirtualTexture.TileCache.TryUploadTile(System.Int32,System.Int32,System.Int32,System.Int32,UnityEngine.Texture2D[],System.Int32)" /> attempt.</summary>
		// Token: 0x020000B8 RID: 184
		public enum TileUploadResult
		{
			// Token: 0x040004C8 RID: 1224
			Uploaded,
			// Token: 0x040004C9 RID: 1225
			Rejected,
			// Token: 0x040004CA RID: 1226
			NoSlot
		}
	}
}
