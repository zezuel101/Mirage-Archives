using System;

namespace Mirage.WebIngest
{
	/// <summary>Why a bake ended. Mirrors <see cref="T:Mirage.WebIngest.TileFetchOutcome" />, and the distinction is the whole point:
	/// a tile past the mercator cut can NEVER bake, while a tile that lost a request probably can next time.
	/// Collapsing them either thrashes the network forever or blacklists the tropics on one dropped packet.</summary>
	// Token: 0x02000025 RID: 37
	public enum IngestOutcome
	{
		/// <summary>Every layer produced a payload. Commit it.</summary>
		// Token: 0x0400009D RID: 157
		Baked,
		/// <summary>No source exists here and none ever will (the ±85.05° mercator cut, §4.2/§4.5). Permanent:
		/// retrying is a request the provider will decline identically forever.</summary>
		// Token: 0x0400009E RID: 158
		NoCoverage,
		/// <summary>Transient — a dropped request, a 5xx, a decode error. Retried after a backoff.</summary>
		// Token: 0x0400009F RID: 159
		Failed
	}
}
