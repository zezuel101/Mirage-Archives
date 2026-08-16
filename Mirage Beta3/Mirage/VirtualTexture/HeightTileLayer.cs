using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>LRU-bounded cache of height tiles, sampled CPU-side with Mitchell-Netravali bicubic.</summary>
	// Token: 0x02000046 RID: 70
	public sealed class HeightTileLayer
	{
		// Token: 0x060001BB RID: 443 RVA: 0x0000D814 File Offset: 0x0000BA14
		public HeightTileLayer(CpuHeightArchive archive, int tileSize, int borderPx, int maxLevel)
		{
			this.archive = archive;
			this.tileSize = tileSize;
			this.borderPx = borderPx;
			this.maxLevel = maxLevel;
			this.slotSize = tileSize + 2 * borderPx;
		}

		// Token: 0x1700004B RID: 75
		// (get) Token: 0x060001BC RID: 444 RVA: 0x0000D872 File Offset: 0x0000BA72
		public int MaxLevel
		{
			get
			{
				return this.maxLevel;
			}
		}

		// Token: 0x1700004C RID: 76
		// (get) Token: 0x060001BD RID: 445 RVA: 0x0000D87A File Offset: 0x0000BA7A
		public int TileSize
		{
			get
			{
				return this.tileSize;
			}
		}

		// Token: 0x1700004D RID: 77
		// (get) Token: 0x060001BE RID: 446 RVA: 0x0000D882 File Offset: 0x0000BA82
		public int BorderPx
		{
			get
			{
				return this.borderPx;
			}
		}

		// Token: 0x1700004E RID: 78
		// (get) Token: 0x060001BF RID: 447 RVA: 0x0000D88A File Offset: 0x0000BA8A
		public int SlotSize
		{
			get
			{
				return this.slotSize;
			}
		}

		/// <summary>Sampled height fraction in [0,1], or NaN when nothing resolves.</summary>
		// Token: 0x060001C0 RID: 448 RVA: 0x0000D894 File Offset: 0x0000BA94
		public double Sample(int face, double rawU, double rawV, int level)
		{
			float[] tile;
			double px;
			double py;
			bool flag = !this.Resolve(face, rawU, rawV, level, out tile, out px, out py);
			double result;
			if (flag)
			{
				result = double.NaN;
			}
			else
			{
				int x0 = (int)Math.Floor(px);
				int y0 = (int)Math.Floor(py);
				double dx = px - (double)x0;
				double dy = py - (double)y0;
				double r0 = this.SampleRow(tile, x0, y0 - 1, dx);
				double r = this.SampleRow(tile, x0, y0, dx);
				double r2 = this.SampleRow(tile, x0, y0 + 1, dx);
				double r3 = this.SampleRow(tile, x0, y0 + 2, dx);
				result = HeightTileLayer.Kernel.Evaluate(r0, r, r2, r3, dy);
			}
			return result;
		}

		/// <summary>Copy the finest resident tile into a NativeArray for a Burst job, walking up until one resolves.</summary>
		// Token: 0x060001C1 RID: 449 RVA: 0x0000D940 File Offset: 0x0000BB40
		public bool TryLoadNativeWalkUp(int face, float uvSwX, float uvSwY, int level, out NativeArray<float> array, out int resolvedLevel, out int tx, out int ty)
		{
			for (int i = Mathf.Min(level, this.maxLevel); i >= 0; i--)
			{
				int x;
				int y;
				TileGeometry.GetCorrectedTileCoord(face, uvSwX, uvSwY, i, out x, out y);
				float[] tile = this.GetTile(face, i, x, y);
				bool flag = tile != null;
				if (flag)
				{
					array = new NativeArray<float>(tile, 4);
					resolvedLevel = i;
					tx = x;
					ty = y;
					return true;
				}
			}
			array = default(NativeArray<float>);
			resolvedLevel = -1;
			tx = (ty = 0);
			return false;
		}

		// Token: 0x060001C2 RID: 450 RVA: 0x0000D9D4 File Offset: 0x0000BBD4
		private double SampleRow(float[] tile, int x0, int y, double dx)
		{
			int stride = Mathf.Clamp(y, 0, this.slotSize - 1) * this.slotSize;
			return HeightTileLayer.Kernel.Evaluate((double)tile[stride + Mathf.Clamp(x0 - 1, 0, this.slotSize - 1)], (double)tile[stride + Mathf.Clamp(x0, 0, this.slotSize - 1)], (double)tile[stride + Mathf.Clamp(x0 + 1, 0, this.slotSize - 1)], (double)tile[stride + Mathf.Clamp(x0 + 2, 0, this.slotSize - 1)], dx);
		}

		/// <summary>Resolve a raw face UV to the finest resident tile and continuous pixel position.</summary>
		// Token: 0x060001C3 RID: 451 RVA: 0x0000DA60 File Offset: 0x0000BC60
		private bool Resolve(int face, double rawU, double rawV, int level, out float[] tile, out double px, out double py)
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
				px = (double)this.borderPx + (cu * (double)g - (double)tx) * (double)this.tileSize;
				py = (double)this.borderPx + (cv * (double)g - (double)ty) * (double)this.tileSize;
				result = true;
			}
			return result;
		}

		/// <summary>Walk up from <paramref name="level" /> toward 0 until a tile resolves; returns level or -1.</summary>
		// Token: 0x060001C4 RID: 452 RVA: 0x0000DAF8 File Offset: 0x0000BCF8
		private int ResolveWalkUp(int face, double cu, double cv, int level, out float[] tile, out int tx, out int ty)
		{
			for (int i = Math.Min(level, this.maxLevel); i >= 0; i--)
			{
				int g = 1 << i;
				int x = Mathf.Clamp((int)Math.Floor(cu * (double)g), 0, g - 1);
				int y = Mathf.Clamp((int)Math.Floor(cv * (double)g), 0, g - 1);
				float[] t = this.GetTile(face, i, x, y);
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

		// Token: 0x060001C5 RID: 453 RVA: 0x0000DB9C File Offset: 0x0000BD9C
		private float[] GetTile(int face, int level, int tx, int ty)
		{
			long key = TileCache.PackKey(face, level, tx, ty);
			float[] cached;
			bool flag = this.cache.TryGetValue(key, out cached);
			float[] result;
			if (flag)
			{
				this.Touch(key);
				result = cached;
			}
			else
			{
				FrameProfile.Timer sw = FrameProfile.Start();
				float[] payload = this.archive.LoadHeightTile(face, level, tx, ty, this.slotSize);
				FrameProfile.Add(ProfilePhase.TileLoad, sw.ElapsedTicks);
				bool flag2 = payload != null || this.CacheMissing(level);
				if (flag2)
				{
					this.Insert(key, payload);
				}
				result = payload;
			}
			return result;
		}

		// Token: 0x060001C6 RID: 454 RVA: 0x0000DC26 File Offset: 0x0000BE26
		private bool CacheMissing(int level)
		{
			return level <= this.archive.MaxResidentLevel;
		}

		// Token: 0x060001C7 RID: 455 RVA: 0x0000DC3C File Offset: 0x0000BE3C
		private void Insert(long key, float[] payload)
		{
			this.cache[key] = payload;
			this.lruNodes[key] = this.lru.AddFirst(key);
			while (this.lru.Count > 192)
			{
				long oldest = this.lru.Last.Value;
				this.lru.RemoveLast();
				this.lruNodes.Remove(oldest);
				this.cache.Remove(oldest);
			}
		}

		// Token: 0x060001C8 RID: 456 RVA: 0x0000DCC4 File Offset: 0x0000BEC4
		private void Touch(long key)
		{
			LinkedListNode<long> node;
			bool flag = !this.lruNodes.TryGetValue(key, out node);
			if (!flag)
			{
				this.lru.Remove(node);
				this.lru.AddFirst(node);
			}
		}

		// Token: 0x0400015E RID: 350
		private const int CacheCapacity = 192;

		// Token: 0x0400015F RID: 351
		private static readonly MitchellNetravali Kernel = new MitchellNetravali(0.3333333333333333, 0.3333333333333333);

		// Token: 0x04000160 RID: 352
		private readonly CpuHeightArchive archive;

		// Token: 0x04000161 RID: 353
		private readonly int tileSize;

		// Token: 0x04000162 RID: 354
		private readonly int borderPx;

		// Token: 0x04000163 RID: 355
		private readonly int maxLevel;

		// Token: 0x04000164 RID: 356
		private readonly int slotSize;

		// Token: 0x04000165 RID: 357
		private readonly Dictionary<long, float[]> cache = new Dictionary<long, float[]>();

		// Token: 0x04000166 RID: 358
		private readonly LinkedList<long> lru = new LinkedList<long>();

		// Token: 0x04000167 RID: 359
		private readonly Dictionary<long, LinkedListNode<long>> lruNodes = new Dictionary<long, LinkedListNode<long>>();
	}
}
