using System;
using Unity.Mathematics;
using UnityEngine;

namespace Mirage.Subdivision
{
	// Token: 0x02000053 RID: 83
	public struct MiragePlane
	{
		// Token: 0x06000261 RID: 609 RVA: 0x00014A08 File Offset: 0x00012C08
		public MiragePlane(float3 normal, float distance)
		{
			this.normal = normal;
			this.distance = distance;
		}

		// Token: 0x06000262 RID: 610 RVA: 0x00014A19 File Offset: 0x00012C19
		public static implicit operator MiragePlane(Plane p)
		{
			return new MiragePlane(p.normal, p.distance);
		}

		// Token: 0x06000263 RID: 611 RVA: 0x00014A33 File Offset: 0x00012C33
		public bool GetSide(in float3 pos)
		{
			return math.dot(pos, this.normal) + this.distance > 0f;
		}

		// Token: 0x04000224 RID: 548
		private float3 normal;

		// Token: 0x04000225 RID: 549
		private float distance;
	}
}
