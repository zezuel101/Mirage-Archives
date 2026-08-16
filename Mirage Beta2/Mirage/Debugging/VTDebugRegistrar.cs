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
	// Token: 0x0200006C RID: 108
	[KSPAddon(2, true)]
	public class VTDebugRegistrar : MonoBehaviour
	{
		// Token: 0x0600030B RID: 779 RVA: 0x00018E04 File Offset: 0x00017004
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

		// Token: 0x040002C1 RID: 705
		private const string ScreenName = "MirageVT";

		// Token: 0x040002C2 RID: 706
		private const string ScreenText = "Mirage VT";

		// Token: 0x020000CA RID: 202
		private class VTScreenWrapper : AddDebugScreens.ScreenWrapper
		{
			// Token: 0x06000506 RID: 1286 RVA: 0x00021F80 File Offset: 0x00020180
			public override string ToString()
			{
				return this.name;
			}
		}
	}
}
