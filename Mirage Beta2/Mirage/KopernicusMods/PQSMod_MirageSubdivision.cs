using System;
using Mirage.Subdivision;
using UnityEngine;

namespace Mirage.KopernicusMods
{
	/// <summary>
	/// PQS mod that registers each quad for Mirage's CPU subdivision system.
	/// Only max-level quads trigger active subdivision; coarser quads are tracked
	/// but produce no overhead beyond a dictionary entry.
	///
	/// Kopernicus config (inside a PQS { Mods { } } block):
	/// <code>
	/// MirageSubdivision
	/// {
	///     order = 2147483645
	///     enabled = true
	///     subdivisionLevel = 7
	///     subdivisionRange = 50
	/// }
	/// </code>
	/// </summary>
	// Token: 0x02000066 RID: 102
	[AddComponentMenu("PQuadSphere/Mods/Misc/Mirage Subdivision")]
	public class PQSMod_MirageSubdivision : PQSMod
	{
		// Token: 0x060002EC RID: 748 RVA: 0x00018831 File Offset: 0x00016A31
		public override void OnSetup()
		{
			this.requirements = 32;
		}

		// Token: 0x060002ED RID: 749 RVA: 0x0001883C File Offset: 0x00016A3C
		public override void OnQuadBuilt(PQ quad)
		{
			bool isMax = quad.subdivision == this.sphere.maxLevel;
			SubdivisionRuntime.RegisterQuad(quad, new SubdivisionQuad(quad, this.subdivisionLevel, this.subdivisionRange, isMax));
		}

		// Token: 0x060002EE RID: 750 RVA: 0x00018878 File Offset: 0x00016A78
		public override void OnQuadDestroy(PQ quad)
		{
			SubdivisionRuntime.UnregisterQuad(quad);
		}

		// Token: 0x060002EF RID: 751 RVA: 0x00018882 File Offset: 0x00016A82
		public override void OnQuadUpdateNormals(PQ quad)
		{
			SubdivisionQuad quad2 = SubdivisionRuntime.GetQuad(quad);
			if (quad2 != null)
			{
				quad2.OnNormalUpdate();
			}
		}

		// Token: 0x040002BB RID: 699
		public int subdivisionLevel = 7;

		// Token: 0x040002BC RID: 700
		public float subdivisionRange = 50f;
	}
}
