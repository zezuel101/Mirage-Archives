using System;
using System.IO;
using Shabby;
using UnityEngine;

namespace Mirage.Runtime
{
	/// <summary>
	/// Loads Mirage's shader asset bundles on KSP startup and hands the contained
	/// shaders to Shabby so they resolve through <see cref="M:UnityEngine.Shader.Find(System.String)" />
	/// (Shabby Harmony-patches that call site).
	///
	/// <para><b>Ship layout</b> — one bundle per graphics API, all under
	/// <c>GameData/Mirage/Shaders/</c>:</para>
	///
	/// <code>
	/// GameData/Mirage/Shaders/MirageTerrain-windows.unity3d
	/// GameData/Mirage/Shaders/MirageTerrain-linux.unity3d
	/// GameData/Mirage/Shaders/MirageTerrain-macosx.unity3d
	/// </code>
	///
	/// <para>Only the bundle matching the current platform is loaded; the other
	/// files sit on disk untouched. Bundle prefix (<c>MirageTerrain</c> here) is
	/// free-form — any file in the shaders folder whose name ends in the current
	/// platform suffix is picked up.</para>
	///
	/// <para><b>Platform selection</b> mirrors Parallax-Continued's convention:
	/// Linux player → <c>-linux</c>; Windows with the OpenGL graphics device →
	/// also <c>-linux</c> (same Unity build target); Windows DX → <c>-windows</c>;
	/// macOS → <c>-macosx</c>. Unknown platforms fall back to the Linux bundle and
	/// log a warning.</para>
	///
	/// <para><b>Why <c>.unity3d</c> instead of <c>.shab</c></b> — Shabby's
	/// <c>[DatabaseLoaderAttrib(["shab"])]</c> would unconditionally try to load
	/// every <c>.shab</c> file in GameData; if we shipped three platform bundles
	/// with that extension Shabby would attempt all three, and the two wrong-
	/// platform loads would each emit a warning. <c>.unity3d</c> isn't registered
	/// as a KSP database extension so the per-platform pick happens here, in one
	/// place, with no spurious warnings.</para>
	/// </summary>
	// Token: 0x02000062 RID: 98
	[KSPAddon(-2, true)]
	public class MirageShaderLoader : MonoBehaviour
	{
		// Token: 0x060002E3 RID: 739 RVA: 0x0001843C File Offset: 0x0001663C
		private void Awake()
		{
			MirageDebug.Init();
			string suffix = MirageShaderLoader.CurrentPlatformSuffix();
			string root = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "Mirage/Shaders");
			bool flag = !Directory.Exists(root);
			if (flag)
			{
				MirageDebug.LogWarning("MirageShaderLoader: shaders directory not found at '" + root + "'. No Mirage shaders will be loaded — Material loaders that need Mirage/Parallax will fall back to InternalErrorShader.");
			}
			else
			{
				int bundles = 0;
				int shaders = 0;
				foreach (string path in Directory.GetFiles(root, "*" + suffix))
				{
					AssetBundle bundle = AssetBundle.LoadFromFile(path);
					bool flag2 = bundle == null;
					if (flag2)
					{
						MirageDebug.LogError("MirageShaderLoader: AssetBundle.LoadFromFile failed for '" + path + "'. File likely built for a different graphics API or corrupted.");
					}
					else
					{
						bundles++;
						foreach (Shader shader in bundle.LoadAllAssets<Shader>())
						{
							Shabby.AddShader(shader);
							MirageDebug.Log("MirageShaderLoader: loaded '" + shader.name + "' from " + Path.GetFileName(path));
							shaders++;
						}
					}
				}
				MirageDebug.Log(string.Format("MirageShaderLoader: registered {0} shader(s) from {1} bundle(s) ", shaders, bundles) + "(platform suffix '" + suffix + "').");
			}
		}

		// Token: 0x060002E4 RID: 740 RVA: 0x00018588 File Offset: 0x00016788
		private static string CurrentPlatformSuffix()
		{
			bool flag = Application.platform == 13 || (Application.platform == 2 && SystemInfo.graphicsDeviceVersion.StartsWith("OpenGL"));
			string result;
			if (flag)
			{
				result = "-linux.unity3d";
			}
			else
			{
				bool flag2 = Application.platform == 2;
				if (flag2)
				{
					result = "-windows.unity3d";
				}
				else
				{
					bool flag3 = Application.platform == 1;
					if (flag3)
					{
						result = "-macosx.unity3d";
					}
					else
					{
						MirageDebug.LogWarning(string.Format("MirageShaderLoader: unknown platform '{0}', falling back to Linux bundle.", Application.platform));
						result = "-linux.unity3d";
					}
				}
			}
			return result;
		}

		// Token: 0x040002B7 RID: 695
		private const string SuffixWindows = "-windows.unity3d";

		// Token: 0x040002B8 RID: 696
		private const string SuffixLinux = "-linux.unity3d";

		// Token: 0x040002B9 RID: 697
		private const string SuffixMacOSX = "-macosx.unity3d";

		// Token: 0x040002BA RID: 698
		private const string ShadersSubdir = "Mirage/Shaders";
	}
}
