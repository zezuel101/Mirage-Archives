using System;

namespace Mirage.WebIngest
{
	/// <summary>What to do with a response body that arrived without a transport error.</summary>
	// Token: 0x02000028 RID: 40
	public enum FetchVerdict
	{
		/// <summary>Real payload. Cache it and hand it to the baker.</summary>
		// Token: 0x040000BF RID: 191
		Success,
		/// <summary>The provider is saying it has nothing here — a NORMAL answer (§4.2). Never retried.</summary>
		// Token: 0x040000C0 RID: 192
		NoCoverage,
		/// <summary>The body is not what this endpoint should serve. Retryable; must never reach the cache.</summary>
		// Token: 0x040000C1 RID: 193
		Reject
	}
}
