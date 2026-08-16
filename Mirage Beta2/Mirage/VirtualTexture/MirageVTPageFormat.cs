using System;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>
	/// Decides once per session whether the VT page table uses the frozen <c>R32_UInt</c> integer texel (read
	/// in-shader via <c>Texture2D&lt;uint&gt;.Load</c> — zero samplers) or falls back to an <c>RGBA32</c> texel
	/// carrying the same 32-bit word across its four bytes. Both decode to the identical word
	/// (<see cref="M:Mirage.VirtualTexture.TileCache.PackPageWord(System.Int32,System.Int32,System.Int32,System.Boolean,System.Boolean,System.Int32,System.Int32)" />); the fallback only exists for GPUs/drivers that refuse integer
	/// texture creation or <c>Load</c>/<c>texelFetch</c> translation (a portability risk under Unity 2019 on
	/// some GL/Mac targets).
	///
	/// The chosen path drives BOTH the C# page-table format (<see cref="T:Mirage.VirtualTexture.TileCache" /> ctor) and the shader
	/// variant (the global <see cref="F:Mirage.VirtualTexture.MirageVTPageFormat.Rgba32Keyword" /> keyword gating <c>MirageVT.cginc:VTReadPage</c>), so the
	/// two always agree. A user can force the fallback via <see cref="M:Mirage.VirtualTexture.MirageVTPageFormat.ForceRgba32" /> before the first body
	/// loads if they hit driver-specific corruption.
	/// </summary>
	// Token: 0x02000049 RID: 73
	public static class MirageVTPageFormat
	{
		/// <summary>Force the RGBA32 fallback (call before the first body loads). For broken integer-Load drivers.</summary>
		// Token: 0x060001BC RID: 444 RVA: 0x0000D3DF File Offset: 0x0000B5DF
		public static void ForceRgba32()
		{
			MirageVTPageFormat.s_useRgba32 = new bool?(true);
			MirageVTPageFormat.ApplyKeyword();
			MirageDebug.Log("MirageVTPageFormat: RGBA32 page-table fallback forced by request.");
		}

		/// <summary>True when the page table must use the RGBA32 fallback texel. Probes lazily on first read.</summary>
		// Token: 0x1700004C RID: 76
		// (get) Token: 0x060001BD RID: 445 RVA: 0x0000D400 File Offset: 0x0000B600
		public static bool UseRgba32
		{
			get
			{
				bool flag = MirageVTPageFormat.s_useRgba32 == null;
				if (flag)
				{
					MirageVTPageFormat.s_useRgba32 = new bool?(!MirageVTPageFormat.ProbeR32UIntSupported());
					MirageVTPageFormat.ApplyKeyword();
					MirageDebug.Log("MirageVTPageFormat: VT page table = " + (MirageVTPageFormat.s_useRgba32.Value ? "RGBA32 (fallback)" : "R32_UInt (Load)") + ".");
				}
				return MirageVTPageFormat.s_useRgba32.Value;
			}
		}

		// Token: 0x060001BE RID: 446 RVA: 0x0000D474 File Offset: 0x0000B674
		private static void ApplyKeyword()
		{
			bool valueOrDefault = MirageVTPageFormat.s_useRgba32.GetValueOrDefault();
			if (valueOrDefault)
			{
				Shader.EnableKeyword("MIRAGE_VT_PAGE_RGBA32");
			}
			else
			{
				Shader.DisableKeyword("MIRAGE_VT_PAGE_RGBA32");
			}
		}

		// Token: 0x060001BF RID: 447 RVA: 0x0000D4A8 File Offset: 0x0000B6A8
		private static bool ProbeR32UIntSupported()
		{
			bool flag = !SystemInfo.IsFormatSupported(37, 0);
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				try
				{
					Texture2D tex = new Texture2D(1, 1, 37, 0);
					tex.SetPixelData<uint>(new uint[]
					{
						195948557U
					}, 0, 0);
					tex.Apply(false, false);
					Object.Destroy(tex);
					result = true;
				}
				catch (Exception e)
				{
					MirageDebug.Log("MirageVTPageFormat: R32_UInt reported sampleable but creation failed; using RGBA32. " + e.Message);
					result = false;
				}
			}
			return result;
		}

		/// <summary>Global multi_compile keyword selecting the RGBA32 decode branch in the shader.</summary>
		// Token: 0x0400018D RID: 397
		public const string Rgba32Keyword = "MIRAGE_VT_PAGE_RGBA32";

		// Token: 0x0400018E RID: 398
		private static bool? s_useRgba32;
	}
}
