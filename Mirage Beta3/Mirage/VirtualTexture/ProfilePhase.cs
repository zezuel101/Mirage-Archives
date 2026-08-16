using System;

namespace Mirage.VirtualTexture
{
	/// <summary>One measured phase of the streamer, in report order.</summary>
	// Token: 0x0200004E RID: 78
	public enum ProfilePhase
	{
		// Token: 0x04000184 RID: 388
		Leaves,
		// Token: 0x04000185 RID: 389
		LevelCtx,
		// Token: 0x04000186 RID: 390
		Collect,
		// Token: 0x04000187 RID: 391
		Lru,
		// Token: 0x04000188 RID: 392
		Queues,
		// Token: 0x04000189 RID: 393
		StartLoads,
		// Token: 0x0400018A RID: 394
		Drain,
		// Token: 0x0400018B RID: 395
		GetTex,
		// Token: 0x0400018C RID: 396
		Upload,
		// Token: 0x0400018D RID: 397
		GpuSync,
		// Token: 0x0400018E RID: 398
		Blit,
		// Token: 0x0400018F RID: 399
		Paint,
		// Token: 0x04000190 RID: 400
		Dispose,
		// Token: 0x04000191 RID: 401
		ApplyPage,
		// Token: 0x04000192 RID: 402
		Pump,
		// Token: 0x04000193 RID: 403
		Ingest,
		// Token: 0x04000194 RID: 404
		Commit,
		// Token: 0x04000195 RID: 405
		Metrics,
		// Token: 0x04000196 RID: 406
		TileLoad
	}
}
