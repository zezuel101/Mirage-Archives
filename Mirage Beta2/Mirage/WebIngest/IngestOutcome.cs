using System;

namespace Mirage.WebIngest
{
	/// <summary>Why a bake ended. Mirrors <see cref="T:Mirage.WebIngest.TileFetchOutcome" />, and the distinction is the whole point:
	/// a tile past the mercator cut can NEVER bake, while a tile that lost a request probably can next time.
	/// Collapsing them either thrashes the network forever or blacklists the tropics on one dropped packet.</summary>
	// Token: 0x0200002C RID: 44
	public enum IngestOutcome
	{
		/// <summary>Every layer produced a payload. Commit it.</summary>
		// Token: 0x040000CA RID: 202
		Baked,
		/// <summary>No source exists here and none ever will (the ±85.05° mercator cut, §4.2/§4.5). Permanent:
		/// retrying is a request the provider will decline identically forever.</summary>
		// Token: 0x040000CB RID: 203
		NoCoverage,
		/// <summary>Transient — a dropped request, a 5xx, a decode error. Retried after a backoff.</summary>
		// Token: 0x040000CC RID: 204
		Failed
	}
}
