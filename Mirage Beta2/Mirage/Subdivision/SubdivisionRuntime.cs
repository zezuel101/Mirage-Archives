using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Mirage.Subdivision
{
	// Token: 0x0200005A RID: 90
	public static class SubdivisionRuntime
	{
		// Token: 0x1700006B RID: 107
		// (get) Token: 0x06000280 RID: 640 RVA: 0x00015F84 File Offset: 0x00014184
		// (set) Token: 0x06000281 RID: 641 RVA: 0x00015F8B File Offset: 0x0001418B
		public static float3 CameraPosition { get; private set; }

		// Token: 0x1700006C RID: 108
		// (get) Token: 0x06000282 RID: 642 RVA: 0x00015F93 File Offset: 0x00014193
		// (set) Token: 0x06000283 RID: 643 RVA: 0x00015F9A File Offset: 0x0001419A
		public static MiragePlane[] FrustumPlanes { get; private set; } = new MiragePlane[6];

		// Token: 0x1700006D RID: 109
		// (get) Token: 0x06000284 RID: 644 RVA: 0x00015FA2 File Offset: 0x000141A2
		// (set) Token: 0x06000285 RID: 645 RVA: 0x00015FA9 File Offset: 0x000141A9
		public static Vector3 CameraPositionV3 { get; private set; }

		// Token: 0x06000286 RID: 646 RVA: 0x00015FB4 File Offset: 0x000141B4
		public static void Update()
		{
			SubdivisionRuntime.RefreshCamera();
			foreach (KeyValuePair<PQ, SubdivisionQuad> kv in SubdivisionRuntime.quads)
			{
				kv.Value.RangeCheck();
			}
		}

		// Token: 0x06000287 RID: 647 RVA: 0x00016018 File Offset: 0x00014218
		private static void RefreshCamera()
		{
			bool flag = SubdivisionRuntime.terrainCamera == null || !SubdivisionRuntime.terrainCamera.isActiveAndEnabled;
			if (flag)
			{
				SubdivisionRuntime.terrainCamera = null;
				foreach (Camera cam in Camera.allCameras)
				{
					bool flag2 = cam.name == "Camera 00";
					if (flag2)
					{
						SubdivisionRuntime.terrainCamera = cam;
						break;
					}
				}
				bool flag3 = SubdivisionRuntime.terrainCamera == null;
				if (flag3)
				{
					SubdivisionRuntime.terrainCamera = Camera.main;
				}
			}
			bool flag4 = SubdivisionRuntime.terrainCamera == null;
			if (!flag4)
			{
				Vector3 pos = SubdivisionRuntime.terrainCamera.transform.position;
				SubdivisionRuntime.CameraPositionV3 = pos;
				SubdivisionRuntime.CameraPosition = pos;
				GeometryUtility.CalculateFrustumPlanes(SubdivisionRuntime.terrainCamera, SubdivisionRuntime.stagingPlanes);
				for (int i = 0; i < 6; i++)
				{
					SubdivisionRuntime.FrustumPlanes[i] = SubdivisionRuntime.stagingPlanes[i];
				}
			}
		}

		// Token: 0x06000288 RID: 648 RVA: 0x00016120 File Offset: 0x00014320
		public static void RegisterQuad(PQ quad, SubdivisionQuad data)
		{
			bool flag = SubdivisionRuntime.quads.ContainsKey(quad);
			if (flag)
			{
				SubdivisionRuntime.quads[quad].Cleanup();
				SubdivisionRuntime.quads[quad] = data;
			}
			else
			{
				SubdivisionRuntime.quads.Add(quad, data);
			}
		}

		// Token: 0x06000289 RID: 649 RVA: 0x00016170 File Offset: 0x00014370
		public static void UnregisterQuad(PQ quad)
		{
			SubdivisionQuad data;
			bool flag = SubdivisionRuntime.quads.TryGetValue(quad, out data);
			if (flag)
			{
				data.Cleanup();
				SubdivisionRuntime.quads.Remove(quad);
			}
		}

		// Token: 0x0600028A RID: 650 RVA: 0x000161A4 File Offset: 0x000143A4
		public static SubdivisionQuad GetQuad(PQ quad)
		{
			SubdivisionQuad data;
			return SubdivisionRuntime.quads.TryGetValue(quad, out data) ? data : null;
		}

		// Token: 0x0600028B RID: 651 RVA: 0x000161C4 File Offset: 0x000143C4
		public static void Clear()
		{
			foreach (KeyValuePair<PQ, SubdivisionQuad> kv in SubdivisionRuntime.quads)
			{
				kv.Value.Cleanup();
			}
			SubdivisionRuntime.quads.Clear();
			SubdivisionRuntime.terrainCamera = null;
		}

		// Token: 0x0400026D RID: 621
		private static Camera terrainCamera;

		// Token: 0x0400026E RID: 622
		private static readonly Plane[] stagingPlanes = new Plane[6];

		// Token: 0x0400026F RID: 623
		private static readonly Dictionary<PQ, SubdivisionQuad> quads = new Dictionary<PQ, SubdivisionQuad>();
	}
}
