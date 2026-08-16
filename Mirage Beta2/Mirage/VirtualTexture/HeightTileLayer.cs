using System;
using KSPTextureLoader;
using Unity.Collections;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>Height layer: stores the tile's R channel as <c>float</c>, samples Mitchell-Netravali bicubic.</summary>
	// Token: 0x02000039 RID: 57
	public sealed class HeightTileLayer : CpuTileLayer<float>
	{
		// Token: 0x06000161 RID: 353 RVA: 0x0000B6FF File Offset: 0x000098FF
		public HeightTileLayer(string rootPath, int tileSize, int borderPx, int maxLevel) : base(rootPath, tileSize, borderPx, maxLevel, true)
		{
		}

		// Token: 0x06000162 RID: 354 RVA: 0x0000B70F File Offset: 0x0000990F
		public HeightTileLayer(CpuHeightArchive archive, int tileSize, int borderPx, int maxLevel) : base(null, tileSize, borderPx, maxLevel, true)
		{
			this.archive = archive;
		}

		// Token: 0x06000163 RID: 355 RVA: 0x0000B725 File Offset: 0x00009925
		protected override float[] LoadTile(int face, int level, int tx, int ty)
		{
			return (this.archive != null) ? this.archive.LoadHeightTile(face, level, tx, ty, base.SlotSize) : base.LoadTile(face, level, tx, ty);
		}

		// Token: 0x06000164 RID: 356 RVA: 0x0000B752 File Offset: 0x00009952
		protected override bool CacheMissing(int level)
		{
			return this.archive == null || level <= this.archive.MaxResidentLevel;
		}

		// Token: 0x06000165 RID: 357 RVA: 0x0000B770 File Offset: 0x00009970
		protected override float[] Extract(CPUTexture2D tex)
		{
			float[] outp;
			using (NativeArray<Color> px = tex.GetPixels(0, 2))
			{
				outp = new float[px.Length];
				for (int i = 0; i < px.Length; i++)
				{
					outp[i] = px[i].r;
				}
			}
			bool flag = !HeightTileLayer.loggedFormat;
			if (flag)
			{
				HeightTileLayer.loggedFormat = true;
				float min = float.MaxValue;
				float max = float.MinValue;
				for (int j = 0; j < outp.Length; j++)
				{
					bool flag2 = outp[j] < min;
					if (flag2)
					{
						min = outp[j];
					}
					bool flag3 = outp[j] > max;
					if (flag3)
					{
						max = outp[j];
					}
				}
				MirageDebug.Log(string.Format("HeightTileLayer: first tile format={0} value range [{1:0.0000}..{2:0.0000}]", tex.Format, min, max));
			}
			return outp;
		}

		/// <summary>Sampled height fraction in [0,1], or NaN when the tile is missing.</summary>
		// Token: 0x06000166 RID: 358 RVA: 0x0000B874 File Offset: 0x00009A74
		public double Sample(int face, double rawU, double rawV, int level)
		{
			float[] tile;
			double px;
			double py;
			bool flag = !base.Resolve(face, rawU, rawV, level, out tile, out px, out py);
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
				double p0;
				double p;
				double p2;
				double p3;
				double r0 = this.Row(tile, x0, y0 - 1, out p0, out p, out p2, out p3) ? HeightTileLayer.Kernel.Evaluate(p0, p, p2, p3, dx) : 0.0;
				double r = this.Row(tile, x0, y0, out p0, out p, out p2, out p3) ? HeightTileLayer.Kernel.Evaluate(p0, p, p2, p3, dx) : 0.0;
				double r2 = this.Row(tile, x0, y0 + 1, out p0, out p, out p2, out p3) ? HeightTileLayer.Kernel.Evaluate(p0, p, p2, p3, dx) : 0.0;
				double r3 = this.Row(tile, x0, y0 + 2, out p0, out p, out p2, out p3) ? HeightTileLayer.Kernel.Evaluate(p0, p, p2, p3, dx) : 0.0;
				result = HeightTileLayer.Kernel.Evaluate(r0, r, r2, r3, dy);
			}
			return result;
		}

		// Token: 0x06000167 RID: 359 RVA: 0x0000B9C0 File Offset: 0x00009BC0
		private bool Row(float[] tile, int x0, int y, out double p0, out double p1, out double p2, out double p3)
		{
			int yc = Mathf.Clamp(y, 0, this.slotSize - 1);
			int stride = yc * this.slotSize;
			p0 = (double)tile[stride + Mathf.Clamp(x0 - 1, 0, this.slotSize - 1)];
			p1 = (double)tile[stride + Mathf.Clamp(x0, 0, this.slotSize - 1)];
			p2 = (double)tile[stride + Mathf.Clamp(x0 + 1, 0, this.slotSize - 1)];
			p3 = (double)tile[stride + Mathf.Clamp(x0 + 2, 0, this.slotSize - 1)];
			return true;
		}

		/// <summary>
		/// Copy the finest resident tile covering a quad's SW CORNER into a freshly-allocated NativeArray for a
		/// Burst job, walking up from <paramref name="level" /> until one resolves. Reports the level actually
		/// resolved so the job can address it (<c>g = 1 &lt;&lt; resolvedLevel</c>).
		///
		/// Addressing goes through <see cref="M:Mirage.VirtualTexture.TileStreamingManager.GetCorrectedTileCoord(System.Int32,System.Single,System.Single,System.Int32,System.Int32@,System.Int32@)" /> — flooring the RAW
		/// UV and rotating the index — rather than <c>ResolveWalkUp</c>'s floor-of-corrected-UV, because a quad
		/// corner sits exactly on a tile boundary where the two differ by one.
		///
		/// Walking up is safe for the job's one-tile-per-quad assumption: a coarser tile strictly CONTAINS the
		/// quad, so every vertex still lands inside the buffer. The quad just samples the ancestor's detail —
		/// the same thing the shader's page-table fallback does — instead of dropping to sea level.
		/// </summary>
		// Token: 0x06000168 RID: 360 RVA: 0x0000BA50 File Offset: 0x00009C50
		public bool TryLoadNativeWalkUp(int face, float uvSwX, float uvSwY, int level, out NativeArray<float> array, out int resolvedLevel, out int tx, out int ty)
		{
			for (int i = Mathf.Min(level, base.MaxLevel); i >= 0; i--)
			{
				int x;
				int y;
				TileStreamingManager.GetCorrectedTileCoord(face, uvSwX, uvSwY, i, out x, out y);
				float[] tile = base.GetTile(face, i, x, y);
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

		// Token: 0x0400012C RID: 300
		private static readonly MitchellNetravali Kernel = new MitchellNetravali(0.3333333333333333, 0.3333333333333333);

		// Token: 0x0400012D RID: 301
		private static bool loggedFormat;

		// Token: 0x0400012E RID: 302
		private readonly CpuHeightArchive archive;
	}
}
