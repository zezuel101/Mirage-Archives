using System;
using BurstPQS;

namespace Mirage.KopernicusMods
{
	/// <summary>BurstPQS adapter for <see cref="T:Mirage.KopernicusMods.PQSMod_MirageSubdivision" />; forwarding only.</summary>
	// Token: 0x02000073 RID: 115
	[BatchPQSMod(typeof(PQSMod_MirageSubdivision))]
	public class BatchPQSMod_MirageSubdivision : BatchPQSMod<PQSMod_MirageSubdivision>
	{
		/// <summary>BurstPQS adapter for <see cref="T:Mirage.KopernicusMods.PQSMod_MirageSubdivision" />; forwarding only.</summary>
		// Token: 0x06000369 RID: 873 RVA: 0x0001A082 File Offset: 0x00018282
		public BatchPQSMod_MirageSubdivision(PQSMod_MirageSubdivision mod) : base(mod)
		{
		}
	}
}
