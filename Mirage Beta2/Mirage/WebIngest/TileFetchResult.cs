using System;

namespace Mirage.WebIngest
{
	/// <summary>Outcome + payload of one tile fetch.</summary>
	// Token: 0x0200002B RID: 43
	public readonly struct TileFetchResult
	{
		// Token: 0x060000F1 RID: 241 RVA: 0x0000918D File Offset: 0x0000738D
		public TileFetchResult(TileFetchOutcome outcome, byte[] bytes, JpegInfo info)
		{
			this.Outcome = outcome;
			this.Bytes = bytes;
			this.Info = info;
		}

		// Token: 0x17000027 RID: 39
		// (get) Token: 0x060000F2 RID: 242 RVA: 0x000091A5 File Offset: 0x000073A5
		public bool IsSuccess
		{
			get
			{
				return this.Outcome == TileFetchOutcome.Success;
			}
		}

		// Token: 0x040000C6 RID: 198
		public readonly TileFetchOutcome Outcome;

		// Token: 0x040000C7 RID: 199
		public readonly byte[] Bytes;

		// Token: 0x040000C8 RID: 200
		public readonly JpegInfo Info;
	}
}
