using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Mirage.Subdivision
{
	// Token: 0x02000065 RID: 101
	public static class SubdivisionRuntime
	{
		// Token: 0x17000072 RID: 114
		// (get) Token: 0x060002E5 RID: 741 RVA: 0x000171A4 File Offset: 0x000153A4
		// (set) Token: 0x060002E6 RID: 742 RVA: 0x000171AB File Offset: 0x000153AB
		public static float3 CameraPosition { get; private set; }

		// Token: 0x17000073 RID: 115
		// (get) Token: 0x060002E7 RID: 743 RVA: 0x000171B3 File Offset: 0x000153B3
		// (set) Token: 0x060002E8 RID: 744 RVA: 0x000171BA File Offset: 0x000153BA
		public static MiragePlane[] FrustumPlanes { get; private set; } = new MiragePlane[6];

		// Token: 0x17000074 RID: 116
		// (get) Token: 0x060002E9 RID: 745 RVA: 0x000171C2 File Offset: 0x000153C2
		// (set) Token: 0x060002EA RID: 746 RVA: 0x000171C9 File Offset: 0x000153C9
		public static Vector3 CameraPositionV3 { get; private set; }

		// Token: 0x060002EB RID: 747 RVA: 0x000171D4 File Offset: 0x000153D4
		public static void Update()
		{
			SubdivisionRuntime.RefreshCamera();
			foreach (KeyValuePair<PQ, SubdivisionQuad> kv in SubdivisionRuntime.quads)
			{
				kv.Value.RangeCheck();
			}
		}

		// Token: 0x060002EC RID: 748 RVA: 0x00017238 File Offset: 0x00015438
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

		// Token: 0x060002ED RID: 749 RVA: 0x00017340 File Offset: 0x00015540
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

		// Token: 0x060002EE RID: 750 RVA: 0x00017390 File Offset: 0x00015590
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

		// Token: 0x060002EF RID: 751 RVA: 0x000173C4 File Offset: 0x000155C4
		public static SubdivisionQuad GetQuad(PQ quad)
		{
			SubdivisionQuad data;
			return SubdivisionRuntime.quads.TryGetValue(quad, out data) ? data : null;
		}

		// Token: 0x060002F0 RID: 752 RVA: 0x000173E4 File Offset: 0x000155E4
		public static void Clear()
		{
			foreach (KeyValuePair<PQ, SubdivisionQuad> kv in SubdivisionRuntime.quads)
			{
				kv.Value.Cleanup();
			}
			SubdivisionRuntime.quads.Clear();
			SubdivisionRuntime.terrainCamera = null;
		}

		// Token: 0x040002D8 RID: 728
		private static Camera terrainCamera;

		// Token: 0x040002D9 RID: 729
		private static readonly Plane[] stagingPlanes = new Plane[6];

		// Token: 0x040002DA RID: 730
		private static readonly Dictionary<PQ, SubdivisionQuad> quads = new Dictionary<PQ, SubdivisionQuad>();
	}
}
