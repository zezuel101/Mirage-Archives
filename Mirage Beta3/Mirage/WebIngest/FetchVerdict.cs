using System;

namespace Mirage.WebIngest
{
	/// <summary>What to do with a response body that arrived without a transport error.</summary>
	// Token: 0x02000018 RID: 24
	public enum FetchVerdict
	{
		/// <summary>Real payload. Cache it and hand it to the baker.</summary>
		// Token: 0x04000079 RID: 121
		Success,
		/// <summary>The provider is saying it has nothing here — a NORMAL answer (§4.2). Never retried.</summary>
		// Token: 0x0400007A RID: 122
		NoCoverage,
		/// <summary>The body is not what this endpoint should serve. Retryable; must never reach the cache.</summary>
		// Token: 0x0400007B RID: 123
		Reject
	}
}
