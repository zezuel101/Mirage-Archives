using System;
using UnityEngine;

namespace Mirage.KopernicusMods
{
	/// <summary>
	/// Writes global cube-face UVs into UV3 (mesh.uv3) for GPU-side virtual-texture sampling.
	/// UV3.x encodes the face index in the integer part and the face U coordinate in the fractional part.
	/// UV3.y is the face V coordinate.
	///
	/// Shader unpacking:
	///   float faceIndex = floor(texcoord2.x);
	///   float2 faceUV   = float2(frac(texcoord2.x), texcoord2.y);
	///
	/// </summary>
	// Token: 0x0200006A RID: 106
	[AddComponentMenu("PQuadSphere/Mods/Misc/UV planet face coords")]
	public class PQSMod_PlanetUV : PQSMod
	{
		// Token: 0x06000306 RID: 774 RVA: 0x00018CDC File Offset: 0x00016EDC
		public override void OnQuadBuilt(PQ quad)
		{
			int face = quad.plane;
			double uvSwX = (double)quad.uvSW.x;
			double uvSwY = (double)quad.uvSW.y;
			double uvDeltaX = (double)quad.uvDelta.x;
			double uvDeltaY = (double)quad.uvDelta.y;
			int vertCount = PQS.cacheVertCount;
			int sideVerts = (int)Mathf.Sqrt((float)vertCount);
			float step = 1f / (float)(sideVerts - 1);
			for (int i = 0; i < vertCount; i++)
			{
				float localU = (float)(i % sideVerts) * step;
				float localV = (float)(i / sideVerts) * step;
				PQS.cacheUV3s[i].x = (float)face + (float)(uvSwX + (double)localU * uvDeltaX);
				PQS.cacheUV3s[i].y = (float)(uvSwY + (double)localV * uvDeltaY);
			}
			quad.mesh.uv3 = PQS.cacheUV3s;
		}
	}
}
