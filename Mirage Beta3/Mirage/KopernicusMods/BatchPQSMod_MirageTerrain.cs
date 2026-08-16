using System;
using BurstPQS;
using Mirage.VirtualTexture;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Mirage.KopernicusMods
{
	/// <summary>BurstPQS fast path for <see cref="T:Mirage.KopernicusMods.PQSMod_MirageTerrain" />.</summary>
	// Token: 0x02000074 RID: 116
	[BurstCompile]
	[BatchPQSMod(typeof(PQSMod_MirageTerrain))]
	public class BatchPQSMod_MirageTerrain : BatchPQSMod<PQSMod_MirageTerrain>
	{
		/// <summary>BurstPQS fast path for <see cref="T:Mirage.KopernicusMods.PQSMod_MirageTerrain" />.</summary>
		// Token: 0x0600036A RID: 874 RVA: 0x0001A08C File Offset: 0x0001828C
		public BatchPQSMod_MirageTerrain(PQSMod_MirageTerrain mod) : base(mod)
		{
		}

		// Token: 0x0600036B RID: 875 RVA: 0x0001A098 File Offset: 0x00018298
		public override void OnQuadPreBuild(PQ quad, BatchPQSJobSet jobSet)
		{
			base.OnQuadPreBuild(quad, jobSet);
			bool flag = !base.Mod.EnsureResolved();
			if (!flag)
			{
				HeightTileLayer layer = base.Mod.HeightLayer;
				bool flag2 = layer == null;
				if (!flag2)
				{
					float2 uvSW;
					uvSW..ctor(quad.uvSW.x, quad.uvSW.y);
					int level = Mathf.Min(quad.subdivision, layer.MaxLevel);
					NativeArray<float> tile;
					int resolvedLevel;
					int tx;
					int ty;
					bool resolved = layer.TryLoadNativeWalkUp(quad.plane, uvSW.x, uvSW.y, level, out tile, out resolvedLevel, out tx, out ty);
					bool flag3 = !resolved;
					if (!flag3)
					{
						BatchPQSMod_MirageTerrain.HeightJob heightJob = default(BatchPQSMod_MirageTerrain.HeightJob);
						heightJob.tile = tile;
						heightJob.slotSize = layer.SlotSize;
						heightJob.borderPx = layer.BorderPx;
						heightJob.tileSize = layer.TileSize;
						heightJob.tilesPerSide = 1 << resolvedLevel;
						heightJob.tx = tx;
						heightJob.ty = ty;
						heightJob.face = quad.plane;
						heightJob.uvSW = uvSW;
						heightJob.uvDelta = new float2(quad.uvDelta.x, quad.uvDelta.y);
						heightJob.deformity = base.Mod.deformity;
						heightJob.offset = base.Mod.offset;
						jobSet.Add<BatchPQSMod_MirageTerrain.HeightJob>(ref heightJob);
					}
				}
			}
		}

		// Token: 0x020000E3 RID: 227
		[BurstCompile(FloatMode = 3)]
		private struct HeightJob : IBatchPQSHeightJob, IDisposable
		{
			// Token: 0x060004EC RID: 1260 RVA: 0x00022A10 File Offset: 0x00020C10
			public unsafe void BuildHeights(in BuildHeightsData data)
			{
				MitchellNetravali kernel = new MitchellNetravali(0.3333333333333333, 0.3333333333333333);
				int sideVerts = MirageTileMath.GridSide(data.VertexCount);
				float step = 1f / (float)(sideVerts - 1);
				for (int i = 0; i < data.VertexCount; i++)
				{
					float localU = (float)(i % sideVerts) * step;
					float localV = (float)(i / sideVerts) * step;
					double rawU = (double)(this.uvSW.x + localU * this.uvDelta.x);
					double rawV = (double)(this.uvSW.y + localV * this.uvDelta.y);
					double cu;
					double cv;
					MirageTileMath.CorrectFaceUV(this.face, rawU, rawV, out cu, out cv);
					double px = (double)this.borderPx + (cu * (double)this.tilesPerSide - (double)this.tx) * (double)this.tileSize;
					double py = (double)this.borderPx + (cv * (double)this.tilesPerSide - (double)this.ty) * (double)this.tileSize;
					int x0 = (int)math.floor(px);
					int y0 = (int)math.floor(py);
					double dx = px - (double)x0;
					double dy = py - (double)y0;
					double r0 = this.Row(kernel, x0, y0 - 1, dx);
					double r = this.Row(kernel, x0, y0, dx);
					double r2 = this.Row(kernel, x0, y0 + 1, dx);
					double r3 = this.Row(kernel, x0, y0 + 2, dx);
					*data.vertHeight[i] += this.offset + this.deformity * kernel.Evaluate(r0, r, r2, r3, dy);
				}
			}

			// Token: 0x060004ED RID: 1261 RVA: 0x00022BB0 File Offset: 0x00020DB0
			private double Row(in MitchellNetravali kernel, int x0, int y, double dx)
			{
				int row = math.clamp(y, 0, this.slotSize - 1) * this.slotSize;
				double p0 = (double)this.tile[row + math.clamp(x0 - 1, 0, this.slotSize - 1)];
				double p = (double)this.tile[row + math.clamp(x0, 0, this.slotSize - 1)];
				double p2 = (double)this.tile[row + math.clamp(x0 + 1, 0, this.slotSize - 1)];
				double p3 = (double)this.tile[row + math.clamp(x0 + 2, 0, this.slotSize - 1)];
				return kernel.Evaluate(p0, p, p2, p3, dx);
			}

			// Token: 0x060004EE RID: 1262 RVA: 0x00022C68 File Offset: 0x00020E68
			public void Dispose()
			{
				bool isCreated = this.tile.IsCreated;
				if (isCreated)
				{
					this.tile.Dispose();
				}
			}

			// Token: 0x060004EF RID: 1263 RVA: 0x00022C91 File Offset: 0x00020E91
			void IBatchPQSHeightJob.BuildHeights(in BuildHeightsData data)
			{
				this.BuildHeights(data);
			}

			// Token: 0x040005C8 RID: 1480
			public NativeArray<float> tile;

			// Token: 0x040005C9 RID: 1481
			public int slotSize;

			// Token: 0x040005CA RID: 1482
			public int borderPx;

			// Token: 0x040005CB RID: 1483
			public int tileSize;

			// Token: 0x040005CC RID: 1484
			public int tilesPerSide;

			// Token: 0x040005CD RID: 1485
			public int tx;

			// Token: 0x040005CE RID: 1486
			public int ty;

			// Token: 0x040005CF RID: 1487
			public int face;

			// Token: 0x040005D0 RID: 1488
			public float2 uvSW;

			// Token: 0x040005D1 RID: 1489
			public float2 uvDelta;

			// Token: 0x040005D2 RID: 1490
			public double deformity;

			// Token: 0x040005D3 RID: 1491
			public double offset;
		}
	}
}
