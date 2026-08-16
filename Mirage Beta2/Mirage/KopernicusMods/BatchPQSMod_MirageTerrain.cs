using System;
using BurstPQS;
using Mirage.VirtualTexture;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Mirage.KopernicusMods
{
	/// <summary>
	/// BurstPQS fast path for <see cref="T:Mirage.KopernicusMods.PQSMod_MirageTerrain" />. For each quad it resolves the single
	/// VT tile (level <c>min(quad.subdivision, maxLevel)</c>, corrected coord) and blocking-loads it
	/// into a NativeArray on the main thread, then schedules Burst jobs that sample it on the quad
	/// build threads. Addressing matches <see cref="T:Mirage.KopernicusMods.PQSMod_PlanetUV" /> / the stock
	/// <see cref="T:Mirage.KopernicusMods.PQSMod_MirageTerrain" /> exactly, so the Burst and stock paths produce identical terrain.
	/// </summary>
	// Token: 0x02000064 RID: 100
	[BurstCompile]
	[BatchPQSMod(typeof(PQSMod_MirageTerrain))]
	public class BatchPQSMod_MirageTerrain : BatchPQSMod<PQSMod_MirageTerrain>
	{
		/// <summary>
		/// BurstPQS fast path for <see cref="T:Mirage.KopernicusMods.PQSMod_MirageTerrain" />. For each quad it resolves the single
		/// VT tile (level <c>min(quad.subdivision, maxLevel)</c>, corrected coord) and blocking-loads it
		/// into a NativeArray on the main thread, then schedules Burst jobs that sample it on the quad
		/// build threads. Addressing matches <see cref="T:Mirage.KopernicusMods.PQSMod_PlanetUV" /> / the stock
		/// <see cref="T:Mirage.KopernicusMods.PQSMod_MirageTerrain" /> exactly, so the Burst and stock paths produce identical terrain.
		/// </summary>
		// Token: 0x060002E8 RID: 744 RVA: 0x00018629 File Offset: 0x00016829
		public BatchPQSMod_MirageTerrain(PQSMod_MirageTerrain mod) : base(mod)
		{
		}

		// Token: 0x060002E9 RID: 745 RVA: 0x00018634 File Offset: 0x00016834
		public override void OnQuadPreBuild(PQ quad, BatchPQSJobSet jobSet)
		{
			base.OnQuadPreBuild(quad, jobSet);
			bool flag = !base.Mod.EnsureResolved();
			if (!flag)
			{
				HeightTileLayer hl = base.Mod.HeightLayer;
				bool flag2 = hl == null;
				if (!flag2)
				{
					int face = quad.plane;
					float2 uvSW;
					uvSW..ctor(quad.uvSW.x, quad.uvSW.y);
					float2 uvDelta;
					uvDelta..ctor(quad.uvDelta.x, quad.uvDelta.y);
					int level = Mathf.Min(quad.subdivision, hl.MaxLevel);
					NativeArray<float> tile;
					int resolvedLevel;
					int tx;
					int ty;
					bool flag3 = hl.TryLoadNativeWalkUp(face, quad.uvSW.x, quad.uvSW.y, level, out tile, out resolvedLevel, out tx, out ty);
					if (flag3)
					{
						BatchPQSMod_MirageTerrain.HeightJob heightJob = default(BatchPQSMod_MirageTerrain.HeightJob);
						heightJob.tile = tile;
						heightJob.slotSize = hl.SlotSize;
						heightJob.borderPx = hl.BorderPx;
						heightJob.tileSize = hl.TileSize;
						heightJob.g = 1 << resolvedLevel;
						heightJob.tx = tx;
						heightJob.ty = ty;
						heightJob.face = face;
						heightJob.uvSW = uvSW;
						heightJob.uvDelta = uvDelta;
						heightJob.deformity = base.Mod.deformity;
						heightJob.offset = base.Mod.offset;
						jobSet.Add<BatchPQSMod_MirageTerrain.HeightJob>(ref heightJob);
					}
				}
			}
		}

		// Token: 0x020000C8 RID: 200
		[BurstCompile(FloatMode = 3)]
		private struct HeightJob : IBatchPQSHeightJob, IDisposable
		{
			// Token: 0x06000500 RID: 1280 RVA: 0x00021C2C File Offset: 0x0001FE2C
			public unsafe void BuildHeights(in BuildHeightsData data)
			{
				MitchellNetravali mn = new MitchellNetravali(0.3333333333333333, 0.3333333333333333);
				int sideVerts = (int)math.sqrt((float)data.VertexCount);
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
					double px = (double)this.borderPx + (cu * (double)this.g - (double)this.tx) * (double)this.tileSize;
					double py = (double)this.borderPx + (cv * (double)this.g - (double)this.ty) * (double)this.tileSize;
					int x0 = (int)math.floor(px);
					int y0 = (int)math.floor(py);
					double dx = px - (double)x0;
					double dy = py - (double)y0;
					double r0 = this.Row(mn, x0, y0 - 1, dx);
					double r = this.Row(mn, x0, y0, dx);
					double r2 = this.Row(mn, x0, y0 + 1, dx);
					double r3 = this.Row(mn, x0, y0 + 2, dx);
					double value = mn.Evaluate(r0, r, r2, r3, dy);
					*data.vertHeight[i] += this.offset + this.deformity * value;
				}
			}

			// Token: 0x06000501 RID: 1281 RVA: 0x00021DD4 File Offset: 0x0001FFD4
			private double Row(in MitchellNetravali mn, int x0, int y, double dx)
			{
				int s = math.clamp(y, 0, this.slotSize - 1) * this.slotSize;
				double p0 = (double)this.tile[s + math.clamp(x0 - 1, 0, this.slotSize - 1)];
				double p = (double)this.tile[s + math.clamp(x0, 0, this.slotSize - 1)];
				double p2 = (double)this.tile[s + math.clamp(x0 + 1, 0, this.slotSize - 1)];
				double p3 = (double)this.tile[s + math.clamp(x0 + 2, 0, this.slotSize - 1)];
				return mn.Evaluate(p0, p, p2, p3, dx);
			}

			// Token: 0x06000502 RID: 1282 RVA: 0x00021E8C File Offset: 0x0002008C
			public void Dispose()
			{
				bool isCreated = this.tile.IsCreated;
				if (isCreated)
				{
					this.tile.Dispose();
				}
			}

			// Token: 0x06000503 RID: 1283 RVA: 0x00021EB5 File Offset: 0x000200B5
			void IBatchPQSHeightJob.BuildHeights(in BuildHeightsData data)
			{
				this.BuildHeights(data);
			}

			// Token: 0x0400053C RID: 1340
			public NativeArray<float> tile;

			// Token: 0x0400053D RID: 1341
			public int slotSize;

			// Token: 0x0400053E RID: 1342
			public int borderPx;

			// Token: 0x0400053F RID: 1343
			public int tileSize;

			// Token: 0x04000540 RID: 1344
			public int g;

			// Token: 0x04000541 RID: 1345
			public int tx;

			// Token: 0x04000542 RID: 1346
			public int ty;

			// Token: 0x04000543 RID: 1347
			public int face;

			// Token: 0x04000544 RID: 1348
			public float2 uvSW;

			// Token: 0x04000545 RID: 1349
			public float2 uvDelta;

			// Token: 0x04000546 RID: 1350
			public double deformity;

			// Token: 0x04000547 RID: 1351
			public double offset;
		}
	}
}
