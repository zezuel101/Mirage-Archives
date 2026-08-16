using System;
using KSP.UI.Screens.DebugToolbar;
using UnityEngine;
using UnityEngine.UI;

namespace Mirage.Debugging
{
	/// <summary>
	/// Registers a "Mirage VT" entry in the Alt+F12 debug menu at MainMenu, once per session.
	/// Built with plain Unity UI (KSPTextureLoader's DebugUIManager is internal so we can't
	/// reuse its styled prefabs).
	/// </summary>
	// Token: 0x0200007C RID: 124
	[KSPAddon(2, true)]
	public class VTDebugRegistrar : MonoBehaviour
	{
		// Token: 0x0600038C RID: 908 RVA: 0x0001A864 File Offset: 0x00018A64
		private void Start()
		{
			DebugScreenSpawner spawner = DebugScreenSpawner.Instance;
			bool flag = spawner == null || spawner.debugScreens == null;
			if (flag)
			{
				MirageDebug.Log("VTDebugRegistrar: DebugScreenSpawner not ready, skipping.");
			}
			else
			{
				foreach (AddDebugScreens.ScreenWrapper existing in spawner.debugScreens.screens)
				{
					bool flag2 = existing.name == "MirageVT";
					if (flag2)
					{
						return;
					}
				}
				GameObject root = new GameObject("Mirage_VTDebugScreen", new Type[]
				{
					typeof(RectTransform)
				});
				root.SetActive(false);
				Object.DontDestroyOnLoad(root);
				RectTransform rect = root.GetComponent<RectTransform>();
				rect.anchorMin = Vector2.zero;
				rect.anchorMax = Vector2.one;
				rect.offsetMin = Vector2.zero;
				rect.offsetMax = Vector2.zero;
				VerticalLayoutGroup vlg = root.AddComponent<VerticalLayoutGroup>();
				vlg.childAlignment = 0;
				vlg.childControlWidth = true;
				vlg.childControlHeight = true;
				vlg.childForceExpandWidth = true;
				vlg.childForceExpandHeight = false;
				vlg.spacing = 4f;
				vlg.padding = new RectOffset(8, 8, 8, 8);
				VTDebugScreenContent content = root.AddComponent<VTDebugScreenContent>();
				content.BuildUI();
				spawner.debugScreens.screens.Add(new VTDebugRegistrar.VTScreenWrapper
				{
					parentName = null,
					name = "MirageVT",
					text = "Mirage VT",
					screen = rect
				});
			}
		}

		// Token: 0x04000347 RID: 839
		private const string ScreenName = "MirageVT";

		// Token: 0x04000348 RID: 840
		private const string ScreenText = "Mirage VT";

		// Token: 0x020000E6 RID: 230
		private class VTScreenWrapper : AddDebugScreens.ScreenWrapper
		{
			// Token: 0x060004F3 RID: 1267 RVA: 0x00022D86 File Offset: 0x00020F86
			public override string ToString()
			{
				return this.name;
			}
		}
	}
}
