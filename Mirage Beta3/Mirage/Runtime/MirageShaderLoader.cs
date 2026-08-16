using System;
using System.IO;
using Shabby;
using UnityEngine;

namespace Mirage.Runtime
{
	/// <summary>Loads the platform-matching shader bundle and registers its shaders with Shabby.</summary>
	// Token: 0x0200006F RID: 111
	[KSPAddon(-2, true)]
	public class MirageShaderLoader : MonoBehaviour
	{
		// Token: 0x0600034D RID: 845 RVA: 0x00019520 File Offset: 0x00017720
		private void Awake()
		{
			MirageDebug.Init();
			string suffix = MirageShaderLoader.CurrentPlatformSuffix();
			string root = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "Mirage/Shaders");
			bool flag = !Directory.Exists(root);
			if (flag)
			{
				MirageShaderLoader.WarnNoShaders("shaders directory not found at '" + root + "'");
			}
			else
			{
				string[] paths = Directory.GetFiles(root, "*" + suffix);
				bool flag2 = paths.Length == 0;
				if (flag2)
				{
					MirageShaderLoader.WarnNoShaders(string.Concat(new string[]
					{
						"no '*",
						suffix,
						"' bundle in '",
						root,
						"'"
					}));
				}
				else
				{
					int bundles = 0;
					int shaders = 0;
					foreach (string path in paths)
					{
						AssetBundle bundle = AssetBundle.LoadFromFile(path);
						bool flag3 = bundle == null;
						if (flag3)
						{
							MirageDebug.LogError("MirageShaderLoader: AssetBundle.LoadFromFile failed for '" + path + "'. The file is corrupt, or was built for a different graphics API.");
						}
						else
						{
							bundles++;
							shaders += MirageShaderLoader.RegisterShaders(bundle, path);
						}
					}
					bool flag4 = shaders == 0;
					if (flag4)
					{
						MirageShaderLoader.WarnNoShaders("'*" + suffix + "' bundles held no shaders");
					}
					else
					{
						MirageDebug.Log(string.Format("MirageShaderLoader: registered {0} shader(s) from {1} bundle(s) ", shaders, bundles) + "(platform suffix '" + suffix + "').");
					}
				}
			}
		}

		// Token: 0x0600034E RID: 846 RVA: 0x00019688 File Offset: 0x00017888
		private static int RegisterShaders(AssetBundle bundle, string path)
		{
			int count = 0;
			foreach (Shader shader in bundle.LoadAllAssets<Shader>())
			{
				Shabby.AddShader(shader);
				MirageDebug.Log("MirageShaderLoader: loaded '" + shader.name + "' from " + Path.GetFileName(path));
				count++;
			}
			return count;
		}

		// Token: 0x0600034F RID: 847 RVA: 0x000196E8 File Offset: 0x000178E8
		private static string CurrentPlatformSuffix()
		{
			RuntimePlatform platform = Application.platform;
			RuntimePlatform runtimePlatform = platform;
			string result;
			if (runtimePlatform != 1)
			{
				if (runtimePlatform != 2)
				{
					if (runtimePlatform != 13)
					{
						MirageDebug.LogWarning(string.Format("MirageShaderLoader: unknown platform '{0}', ", Application.platform) + "falling back to the Linux bundle.");
						result = "-linux.unity3d";
					}
					else
					{
						result = "-linux.unity3d";
					}
				}
				else
				{
					result = (SystemInfo.graphicsDeviceVersion.StartsWith("OpenGL") ? "-linux.unity3d" : "-windows.unity3d");
				}
			}
			else
			{
				result = "-macosx.unity3d";
			}
			return result;
		}

		// Token: 0x06000350 RID: 848 RVA: 0x0001976E File Offset: 0x0001796E
		private static void WarnNoShaders(string reason)
		{
			MirageDebug.LogWarning("MirageShaderLoader: " + reason + ". No Mirage shaders were loaded!");
		}

		// Token: 0x04000329 RID: 809
		private const string SuffixWindows = "-windows.unity3d";

		// Token: 0x0400032A RID: 810
		private const string SuffixLinux = "-linux.unity3d";

		// Token: 0x0400032B RID: 811
		private const string SuffixMacOSX = "-macosx.unity3d";

		// Token: 0x0400032C RID: 812
		private const string ShadersSubdir = "Mirage/Shaders";
	}
}
