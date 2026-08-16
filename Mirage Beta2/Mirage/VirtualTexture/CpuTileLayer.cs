using System;
using System.Collections.Generic;
using System.Diagnostics;
using KSPTextureLoader;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>
	/// CPU-side, on-demand, LRU-bounded cache of readable Mirage VT tiles for a single layer
	/// (height or colour). Tiles are <c>SlotSize = tileSize + 2*borderPx</c> px square on disk — the
	/// border padding is baked in, so an edge texel's bicubic/bilinear footprint stays inside one tile.
	///
	/// Loads are synchronous (blocking <c>handle.GetTexture()</c>, the same path
	/// <c>TileCache.BootstrapCoarseLevels</c> uses) because PQS quad building needs the data
	/// immediately. Missing tiles are cached negatively so we don't re-hit the disk every vertex.
	/// </summary>
	/// <typeparam name="TPixel">Per-texel payload: <c>float</c> (height R channel) or <c>Color32</c>.</typeparam>
	// Token: 0x02000038 RID: 56
	public abstract class CpuTileLayer<TPixel> where TPixel : struct
	{
		// Token: 0x06000153 RID: 339 RVA: 0x0000B224 File Offset: 0x00009424
		protected CpuTileLayer(string rootPath, int tileSize, int borderPx, int maxLevel, bool linear)
		{
			this.rootPath = rootPath;
			this.tileSize = tileSize;
			this.borderPx = borderPx;
			this.maxLevel = maxLevel;
			this.linear = linear;
			this.slotSize = tileSize + 2 * borderPx;
		}

		// Token: 0x17000040 RID: 64
		// (get) Token: 0x06000154 RID: 340 RVA: 0x0000B28A File Offset: 0x0000948A
		public int MaxLevel
		{
			get
			{
				return this.maxLevel;
			}
		}

		// Token: 0x17000041 RID: 65
		// (get) Token: 0x06000155 RID: 341 RVA: 0x0000B292 File Offset: 0x00009492
		public int TileSize
		{
			get
			{
				return this.tileSize;
			}
		}

		// Token: 0x17000042 RID: 66
		// (get) Token: 0x06000156 RID: 342 RVA: 0x0000B29A File Offset: 0x0000949A
		public int BorderPx
		{
			get
			{
				return this.borderPx;
			}
		}

		// Token: 0x17000043 RID: 67
		// (get) Token: 0x06000157 RID: 343 RVA: 0x0000B2A2 File Offset: 0x000094A2
		public int SlotSize
		{
			get
			{
				return this.slotSize;
			}
		}

		// Token: 0x06000158 RID: 344
		protected abstract TPixel[] Extract(CPUTexture2D tex);

		/// <summary>Returns the cached/loaded tile payload, or null if the tile is missing on disk.</summary>
		// Token: 0x06000159 RID: 345 RVA: 0x0000B2AC File Offset: 0x000094AC
		protected TPixel[] GetTile(int face, int level, int tx, int ty)
		{
			long key = TileCache.PackKey(face, level, tx, ty);
			TPixel[] cached;
			bool flag = this.cache.TryGetValue(key, out cached);
			TPixel[] result;
			if (flag)
			{
				this.Touch(key);
				result = cached;
			}
			else
			{
				Stopwatch sw = FrameProfile.Start();
				TPixel[] payload = this.LoadTile(face, level, tx, ty);
				FrameProfile.AddTileLoad(sw.ElapsedTicks);
				bool flag2 = payload != null || this.CacheMissing(level);
				if (flag2)
				{
					this.Insert(key, payload);
				}
				result = payload;
			}
			return result;
		}

		/// <summary>
		/// May a MISS at <paramref name="level" /> be cached as known-missing? Only true where residency is
		/// immutable. A level the web tier can bake into is not: negative-caching it would pin the walk-up to a
		/// coarse ancestor forever, and the CPU mesh would never refine to the surface the GPU is already
		/// displacing — the exact GPU/CPU divergence design §11 exists to close.
		///
		/// Re-probing such a level is cheap by construction: an absent tile is an in-memory dictionary miss in
		/// the archive index plus one in the web index — no file I/O — and the instant it IS baked the next
		/// probe finds it and the positive entry caches normally.
		/// </summary>
		// Token: 0x0600015A RID: 346 RVA: 0x0000B328 File Offset: 0x00009528
		protected virtual bool CacheMissing(int level)
		{
			return true;
		}

		/// <summary>
		/// Finest AVAILABLE tile at or below <paramref name="level" />, walking toward level 0 — the CPU's
		/// counterpart to the shader's page-table walk-up.
		///
		/// Without this a missing tile means NO height contribution at all (the caller falls back to sea level),
		/// which is not a degradation but a cliff: with maxLevel past canonical's K, every quad near the craft
		/// asks for a web level that usually isn't baked yet and the mesh drops flat while the GPU — which does
		/// walk up — renders correct terrain over it.
		///
		/// <paramref name="cu" />/<paramref name="cv" /> are CORRECTED face UVs of an INTERIOR point. Callers
		/// addressing a quad CORNER must not use this: a corner lies exactly on a tile boundary, where flooring
		/// the corrected UV and rotating the floored raw index differ by one (see
		/// <c>TileStreamingManager.GetCorrectedTileCoord</c>).
		/// </summary>
		// Token: 0x0600015B RID: 347 RVA: 0x0000B32C File Offset: 0x0000952C
		protected int ResolveWalkUp(int face, double cu, double cv, int level, out TPixel[] tile, out int tx, out int ty)
		{
			for (int i = Math.Min(level, this.maxLevel); i >= 0; i--)
			{
				int g = 1 << i;
				int x = Mathf.Clamp((int)Math.Floor(cu * (double)g), 0, g - 1);
				int y = Mathf.Clamp((int)Math.Floor(cv * (double)g), 0, g - 1);
				TPixel[] t = this.GetTile(face, i, x, y);
				bool flag = t != null;
				if (flag)
				{
					tile = t;
					tx = x;
					ty = y;
					return i;
				}
			}
			tile = null;
			tx = (ty = 0);
			return -1;
		}

		/// <summary>Load one tile's payload, or null if missing. Virtual so an archive-backed subclass can
		/// read the tile from the binary archive instead of a loose <c>.dds</c> file.</summary>
		// Token: 0x0600015C RID: 348 RVA: 0x0000B3D0 File Offset: 0x000095D0
		protected virtual TPixel[] LoadTile(int face, int level, int tx, int ty)
		{
			string path = MirageTileMath.TilePath(this.rootPath, face, level, tx, ty);
			bool flag = !TextureLoader.TextureExists(path);
			TPixel[] result;
			if (flag)
			{
				result = null;
			}
			else
			{
				TextureLoadOptions textureLoadOptions;
				textureLoadOptions..ctor();
				textureLoadOptions.Linear = new bool?(this.linear);
				TextureLoadOptions options = textureLoadOptions;
				CPUTextureHandle handle = TextureLoader.LoadCPUTexture(path, options);
				CPUTexture2D tex;
				try
				{
					tex = handle.GetTexture();
				}
				catch (Exception e)
				{
					MirageDebug.LogError("CpuTileLayer: load failed for " + path + ": " + e.Message);
					handle.Dispose();
					return null;
				}
				bool flag2 = tex == null;
				if (flag2)
				{
					handle.Dispose();
					result = null;
				}
				else
				{
					bool flag3 = tex.Width != this.slotSize || tex.Height != this.slotSize;
					if (flag3)
					{
						MirageDebug.LogError(string.Format("CpuTileLayer: tile {0} is {1}x{2}, expected {3}x{4}", new object[]
						{
							path,
							tex.Width,
							tex.Height,
							this.slotSize,
							this.slotSize
						}));
						handle.Dispose();
						result = null;
					}
					else
					{
						TPixel[] payload;
						try
						{
							payload = this.Extract(tex);
						}
						catch (Exception e2)
						{
							MirageDebug.LogError("CpuTileLayer: extract failed for " + path + ": " + e2.Message);
							handle.Dispose();
							return null;
						}
						handle.Dispose();
						result = payload;
					}
				}
			}
			return result;
		}

		// Token: 0x0600015D RID: 349 RVA: 0x0000B56C File Offset: 0x0000976C
		private void Insert(long key, TPixel[] payload)
		{
			this.cache[key] = payload;
			LinkedListNode<long> node = this.lru.AddFirst(key);
			this.lruNodes[key] = node;
			while (this.lru.Count > 192)
			{
				LinkedListNode<long> oldest = this.lru.Last;
				this.lru.RemoveLast();
				this.lruNodes.Remove(oldest.Value);
				this.cache.Remove(oldest.Value);
			}
		}

		// Token: 0x0600015E RID: 350 RVA: 0x0000B5FC File Offset: 0x000097FC
		private void Touch(long key)
		{
			LinkedListNode<long> node;
			bool flag = this.lruNodes.TryGetValue(key, out node);
			if (flag)
			{
				this.lru.Remove(node);
				this.lru.AddFirst(node);
			}
		}

		// Token: 0x0600015F RID: 351 RVA: 0x0000B638 File Offset: 0x00009838
		public void Clear()
		{
			this.cache.Clear();
			this.lru.Clear();
			this.lruNodes.Clear();
		}

		/// <summary>
		/// Resolve a raw face UV to the finest resident tile at or below <paramref name="level" /> (see
		/// <see cref="M:Mirage.VirtualTexture.CpuTileLayer`1.ResolveWalkUp(System.Int32,System.Double,System.Double,System.Int32,`0[]@,System.Int32@,System.Int32@)" />) + the continuous pixel position within that tile's loaded SlotSize
		/// buffer (border included). Returns false only when NOTHING resolves down to level 0 — i.e. the
		/// pyramid is genuinely empty for this face.
		/// </summary>
		// Token: 0x06000160 RID: 352 RVA: 0x0000B660 File Offset: 0x00009860
		protected bool Resolve(int face, double rawU, double rawV, int level, out TPixel[] tile, out double px, out double py)
		{
			double cu;
			double cv;
			MirageTileMath.CorrectFaceUV(face, rawU, rawV, out cu, out cv);
			int tx;
			int ty;
			int i = this.ResolveWalkUp(face, cu, cv, level, out tile, out tx, out ty);
			bool flag = i < 0;
			bool result;
			if (flag)
			{
				px = (py = 0.0);
				result = false;
			}
			else
			{
				int g = 1 << i;
				double withinU = cu * (double)g - (double)tx;
				double withinV = cv * (double)g - (double)ty;
				px = (double)this.borderPx + withinU * (double)this.tileSize;
				py = (double)this.borderPx + withinV * (double)this.tileSize;
				result = true;
			}
			return result;
		}

		// Token: 0x04000122 RID: 290
		private readonly string rootPath;

		// Token: 0x04000123 RID: 291
		private readonly bool linear;

		// Token: 0x04000124 RID: 292
		protected readonly int tileSize;

		// Token: 0x04000125 RID: 293
		protected readonly int borderPx;

		// Token: 0x04000126 RID: 294
		protected readonly int maxLevel;

		// Token: 0x04000127 RID: 295
		protected readonly int slotSize;

		// Token: 0x04000128 RID: 296
		private const int CacheCapacity = 192;

		// Token: 0x04000129 RID: 297
		private readonly Dictionary<long, TPixel[]> cache = new Dictionary<long, TPixel[]>();

		// Token: 0x0400012A RID: 298
		private readonly LinkedList<long> lru = new LinkedList<long>();

		// Token: 0x0400012B RID: 299
		private readonly Dictionary<long, LinkedListNode<long>> lruNodes = new Dictionary<long, LinkedListNode<long>>();
	}
}
