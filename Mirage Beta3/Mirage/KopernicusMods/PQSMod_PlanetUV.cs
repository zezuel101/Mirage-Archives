using System;
using Mirage.VirtualTexture;
using UnityEngine;

namespace Mirage.KopernicusMods
{
	/// <summary>Bakes global cube-face UVs into UV3 (<c>x = face + faceU, y = faceV</c>).</summary>
	// Token: 0x0200007A RID: 122
	[AddComponentMenu("PQuadSphere/Mods/Misc/UV planet face coords")]
	public class PQSMod_PlanetUV : PQSMod
	{
		// Token: 0x06000388 RID: 904 RVA: 0x0001A750 File Offset: 0x00018950
		public override void OnQuadBuilt(PQ quad)
		{
			int face = quad.plane;
			double uvSwX = (double)quad.uvSW.x;
			double uvSwY = (double)quad.uvSW.y;
			double uvDeltaX = (double)quad.uvDelta.x;
			double uvDeltaY = (double)quad.uvDelta.y;
			int vertCount = PQS.cacheVertCount;
			int sideVerts = MirageTileMath.GridSide(vertCount);
			float step = 1f / (float)(sideVerts - 1);
			for (int i = 0; i < vertCount; i++)
			{
				float localU = (float)(i % sideVerts) * step;
				float localV = (float)(i / sideVerts) * step;
				PQS.cacheUV3s[i].x = (float)face + Mathf.Min((float)(uvSwX + (double)localU * uvDeltaX), 0.999999f);
				PQS.cacheUV3s[i].y = (float)(uvSwY + (double)localV * uvDeltaY);
			}
			quad.mesh.uv3 = PQS.cacheUV3s;
		}

		/// <summary>Largest faceU that cannot carry into the face index — see <see cref="M:Mirage.KopernicusMods.PQSMod_PlanetUV.OnQuadBuilt(PQ)" />.</summary>
		// Token: 0x04000346 RID: 838
		internal const float MaxFaceU = 0.999999f;
	}
}
