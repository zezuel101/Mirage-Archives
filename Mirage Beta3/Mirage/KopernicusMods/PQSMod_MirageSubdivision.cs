using System;
using Mirage.Subdivision;
using UnityEngine;

namespace Mirage.KopernicusMods
{
	/// <summary>Registers max-level quads with Mirage's CPU subdivision system.</summary>
	// Token: 0x02000076 RID: 118
	[AddComponentMenu("PQuadSphere/Mods/Misc/Mirage Subdivision")]
	public class PQSMod_MirageSubdivision : PQSMod
	{
		// Token: 0x0600036F RID: 879 RVA: 0x0001A288 File Offset: 0x00018488
		public override void OnSetup()
		{
			this.requirements = 32;
		}

		// Token: 0x06000370 RID: 880 RVA: 0x0001A294 File Offset: 0x00018494
		public override void OnQuadBuilt(PQ quad)
		{
			bool flag = quad.subdivision != this.sphere.maxLevel;
			if (!flag)
			{
				SubdivisionRuntime.RegisterQuad(quad, new SubdivisionQuad(quad, this.subdivisionLevel, this.subdivisionRange, true));
			}
		}

		// Token: 0x06000371 RID: 881 RVA: 0x0001A2D8 File Offset: 0x000184D8
		public override void OnQuadDestroy(PQ quad)
		{
			SubdivisionRuntime.UnregisterQuad(quad);
		}

		// Token: 0x06000372 RID: 882 RVA: 0x0001A2E2 File Offset: 0x000184E2
		public override void OnQuadUpdateNormals(PQ quad)
		{
			SubdivisionQuad quad2 = SubdivisionRuntime.GetQuad(quad);
			if (quad2 != null)
			{
				quad2.OnNormalUpdate();
			}
		}

		// Token: 0x0400033E RID: 830
		public int subdivisionLevel = 7;

		// Token: 0x0400033F RID: 831
		public float subdivisionRange = 50f;
	}
}
