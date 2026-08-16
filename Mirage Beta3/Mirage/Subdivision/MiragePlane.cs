using System;
using Unity.Mathematics;
using UnityEngine;

namespace Mirage.Subdivision
{
	// Token: 0x0200005E RID: 94
	public struct MiragePlane
	{
		// Token: 0x060002C6 RID: 710 RVA: 0x00015C28 File Offset: 0x00013E28
		public MiragePlane(float3 normal, float distance)
		{
			this.normal = normal;
			this.distance = distance;
		}

		// Token: 0x060002C7 RID: 711 RVA: 0x00015C39 File Offset: 0x00013E39
		public static implicit operator MiragePlane(Plane p)
		{
			return new MiragePlane(p.normal, p.distance);
		}

		// Token: 0x060002C8 RID: 712 RVA: 0x00015C53 File Offset: 0x00013E53
		public bool GetSide(in float3 pos)
		{
			return math.dot(pos, this.normal) + this.distance > 0f;
		}

		// Token: 0x0400028F RID: 655
		private float3 normal;

		// Token: 0x04000290 RID: 656
		private float distance;
	}
}
