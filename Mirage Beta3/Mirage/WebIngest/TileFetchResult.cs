using System;

namespace Mirage.WebIngest
{
	/// <summary>Outcome + payload of one tile fetch.</summary>
	// Token: 0x02000017 RID: 23
	public readonly struct TileFetchResult
	{
		// Token: 0x06000090 RID: 144 RVA: 0x000056AA File Offset: 0x000038AA
		public TileFetchResult(TileFetchOutcome outcome, byte[] bytes, JpegInfo info)
		{
			this.Outcome = outcome;
			this.Bytes = bytes;
			this.Info = info;
		}

		// Token: 0x17000020 RID: 32
		// (get) Token: 0x06000091 RID: 145 RVA: 0x000056C2 File Offset: 0x000038C2
		public bool IsSuccess
		{
			get
			{
				return this.Outcome == TileFetchOutcome.Success;
			}
		}

		// Token: 0x04000075 RID: 117
		public readonly TileFetchOutcome Outcome;

		// Token: 0x04000076 RID: 118
		public readonly byte[] Bytes;

		// Token: 0x04000077 RID: 119
		public readonly JpegInfo Info;
	}
}
