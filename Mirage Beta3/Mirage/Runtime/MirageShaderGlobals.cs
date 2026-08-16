using System;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirage.Runtime
{
	/// <summary>Pushes Mirage's per-frame and per-camera shader globals in flight.</summary>
	// Token: 0x0200006E RID: 110
	internal static class MirageShaderGlobals
	{
		// Token: 0x06000347 RID: 839 RVA: 0x000191A0 File Offset: 0x000173A0
		public static void Install()
		{
			Shader.SetGlobalVector(MirageShaderGlobals.s_MirageSunDirID, new Vector4(0f, 1f, 0f, 0f));
			Shader.SetGlobalVector(MirageShaderGlobals.s_MirageSunColorID, Vector4.one);
			MirageSettings.PushShaderGlobals();
			Delegate onPreRender = Camera.onPreRender;
			Camera.CameraCallback b;
			if ((b = MirageShaderGlobals.<>O.<0>__OnCameraPreRender) == null)
			{
				b = (MirageShaderGlobals.<>O.<0>__OnCameraPreRender = new Camera.CameraCallback(MirageShaderGlobals.OnCameraPreRender));
			}
			Camera.onPreRender = (Camera.CameraCallback)Delegate.Combine(onPreRender, b);
		}

		// Token: 0x06000348 RID: 840 RVA: 0x00019217 File Offset: 0x00017417
		public static void Uninstall()
		{
			Delegate onPreRender = Camera.onPreRender;
			Camera.CameraCallback value;
			if ((value = MirageShaderGlobals.<>O.<0>__OnCameraPreRender) == null)
			{
				value = (MirageShaderGlobals.<>O.<0>__OnCameraPreRender = new Camera.CameraCallback(MirageShaderGlobals.OnCameraPreRender));
			}
			Camera.onPreRender = (Camera.CameraCallback)Delegate.Remove(onPreRender, value);
		}

		// Token: 0x06000349 RID: 841 RVA: 0x00019248 File Offset: 0x00017448
		public static void PushPerFrame(CelestialBody body)
		{
			Shader.SetGlobalFloat(MirageShaderGlobals.s_ShadowCascadeFarID, QualitySettings.shadowDistance);
			bool flag = body == null;
			if (!flag)
			{
				Vector3 origin = body.transform.position;
				Shader.SetGlobalVector(MirageShaderGlobals.s_PlanetOriginID, origin);
				Shader.SetGlobalFloat(MirageShaderGlobals.s_PlanetRadiusID, (float)body.Radius);
				Shader.SetGlobalMatrix(MirageShaderGlobals.s_PlanetRotationID, Matrix4x4.Rotate(Quaternion.Inverse(body.transform.rotation)));
				Shader.SetGlobalVector(MirageShaderGlobals.s_TerrainShaderOffsetID, FloatingOrigin.TerrainShaderOffset);
				double originAltitude = origin.magnitude - body.Radius;
				Shader.SetGlobalFloat(MirageShaderGlobals.s_OriginAltitudeID, (float)originAltitude);
			}
		}

		// Token: 0x0600034A RID: 842 RVA: 0x00019304 File Offset: 0x00017504
		private static void OnCameraPreRender(Camera cam)
		{
			bool flag = MirageShaderGlobals.s_SunLight == null && MirageShaderGlobals.s_LastSunSearchFrame != Time.frameCount;
			if (flag)
			{
				MirageShaderGlobals.s_LastSunSearchFrame = Time.frameCount;
				MirageShaderGlobals.s_SunLight = MirageShaderGlobals.FindSunLight();
			}
			bool flag2 = MirageShaderGlobals.s_SunLight != null;
			if (flag2)
			{
				Shader.SetGlobalVector(MirageShaderGlobals.s_MirageSunDirID, -MirageShaderGlobals.s_SunLight.transform.forward);
				Shader.SetGlobalVector(MirageShaderGlobals.s_MirageSunColorID, MirageShaderGlobals.s_SunLight.color);
			}
			CelestialBody body = FlightGlobals.currentMainBody;
			bool flag3 = body != null;
			if (flag3)
			{
				double camAltitude = (cam.transform.position - body.transform.position).magnitude - body.Radius;
				Shader.SetGlobalFloat(MirageShaderGlobals.s_CameraAltitudeID, (float)camAltitude);
			}
		}

		// Token: 0x0600034B RID: 843 RVA: 0x000193F4 File Offset: 0x000175F4
		private static Light FindSunLight()
		{
			Light[] lights = Object.FindObjectsOfType<Light>();
			Light result;
			if ((result = lights.FirstOrDefault((Light l) => l.name == "SunLight")) == null)
			{
				result = (from l in lights
				where l.type == 1
				orderby l.intensity descending
				select l).FirstOrDefault<Light>();
			}
			return result;
		}

		// Token: 0x0400031E RID: 798
		private static readonly int s_PlanetOriginID = Shader.PropertyToID("_PlanetOrigin");

		// Token: 0x0400031F RID: 799
		private static readonly int s_PlanetRadiusID = Shader.PropertyToID("_PlanetRadius");

		// Token: 0x04000320 RID: 800
		private static readonly int s_PlanetRotationID = Shader.PropertyToID("_PlanetRotation");

		// Token: 0x04000321 RID: 801
		private static readonly int s_OriginAltitudeID = Shader.PropertyToID("_OriginAltitude");

		// Token: 0x04000322 RID: 802
		private static readonly int s_TerrainShaderOffsetID = Shader.PropertyToID("_TerrainShaderOffset");

		// Token: 0x04000323 RID: 803
		private static readonly int s_ShadowCascadeFarID = Shader.PropertyToID("_ShadowCascadeFar");

		// Token: 0x04000324 RID: 804
		private static readonly int s_MirageSunDirID = Shader.PropertyToID("_MirageSunDir");

		// Token: 0x04000325 RID: 805
		private static readonly int s_MirageSunColorID = Shader.PropertyToID("_MirageSunColor");

		// Token: 0x04000326 RID: 806
		private static readonly int s_CameraAltitudeID = Shader.PropertyToID("_CameraAltitude");

		// Token: 0x04000327 RID: 807
		private static Light s_SunLight;

		// Token: 0x04000328 RID: 808
		private static int s_LastSunSearchFrame = -1;

		// Token: 0x020000DF RID: 223
		[CompilerGenerated]
		private static class <>O
		{
			// Token: 0x040005BF RID: 1471
			public static Camera.CameraCallback <0>__OnCameraPreRender;
		}
	}
}
