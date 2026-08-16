using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Mirage.VirtualTexture;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Mirage.Debugging
{
	/// <summary>
	/// Live stats panel for Mirage's virtual texture caches + the colour-by-level / desired-level
	/// shader debug toggles. Mounted on the inactive prefab GameObject; KSP clones the prefab when
	/// the user opens the panel, so all live references are <see cref="T:UnityEngine.SerializeField" />'d to
	/// survive cloning and the toggle listeners are re-wired in Awake.
	/// </summary>
	// Token: 0x0200006D RID: 109
	public class VTDebugScreenContent : MonoBehaviour
	{
		// Token: 0x0600030D RID: 781 RVA: 0x00018FB0 File Offset: 0x000171B0
		private void Awake()
		{
			bool flag = this.debugColorToggle != null;
			if (flag)
			{
				UnityEvent<bool> onValueChanged = this.debugColorToggle.onValueChanged;
				UnityAction<bool> unityAction;
				if ((unityAction = VTDebugScreenContent.<>O.<0>__OnDebugColorToggled) == null)
				{
					unityAction = (VTDebugScreenContent.<>O.<0>__OnDebugColorToggled = new UnityAction<bool>(VTDebugScreenContent.OnDebugColorToggled));
				}
				onValueChanged.RemoveListener(unityAction);
				this.debugColorToggle.isOn = Shader.IsKeywordEnabled("MIRAGE_VT_DEBUG");
				UnityEvent<bool> onValueChanged2 = this.debugColorToggle.onValueChanged;
				UnityAction<bool> unityAction2;
				if ((unityAction2 = VTDebugScreenContent.<>O.<0>__OnDebugColorToggled) == null)
				{
					unityAction2 = (VTDebugScreenContent.<>O.<0>__OnDebugColorToggled = new UnityAction<bool>(VTDebugScreenContent.OnDebugColorToggled));
				}
				onValueChanged2.AddListener(unityAction2);
			}
			bool flag2 = this.desiredLevelToggle != null;
			if (flag2)
			{
				UnityEvent<bool> onValueChanged3 = this.desiredLevelToggle.onValueChanged;
				UnityAction<bool> unityAction3;
				if ((unityAction3 = VTDebugScreenContent.<>O.<1>__OnDesiredLevelToggled) == null)
				{
					unityAction3 = (VTDebugScreenContent.<>O.<1>__OnDesiredLevelToggled = new UnityAction<bool>(VTDebugScreenContent.OnDesiredLevelToggled));
				}
				onValueChanged3.RemoveListener(unityAction3);
				this.desiredLevelToggle.isOn = (Shader.GetGlobalFloat("_VTDebugMode") > 0.5f);
				UnityEvent<bool> onValueChanged4 = this.desiredLevelToggle.onValueChanged;
				UnityAction<bool> unityAction4;
				if ((unityAction4 = VTDebugScreenContent.<>O.<1>__OnDesiredLevelToggled) == null)
				{
					unityAction4 = (VTDebugScreenContent.<>O.<1>__OnDesiredLevelToggled = new UnityAction<bool>(VTDebugScreenContent.OnDesiredLevelToggled));
				}
				onValueChanged4.AddListener(unityAction4);
			}
		}

		// Token: 0x0600030E RID: 782 RVA: 0x000190C8 File Offset: 0x000172C8
		public void BuildUI()
		{
			Transform t = base.transform;
			VTDebugScreenContent.AddLabel(t, "Mirage Virtual Texture", true, 16f);
			VTDebugScreenContent.AddSpacer(t, 6f);
			this.debugColorToggle = VTDebugScreenContent.BuildToggle(t, "Debug colour-by-VT-level", Shader.IsKeywordEnabled("MIRAGE_VT_DEBUG"));
			this.desiredLevelToggle = VTDebugScreenContent.BuildToggle(t, "Show desired level (instead of resident)", Shader.GetGlobalFloat("_VTDebugMode") > 0.5f);
			UnityEvent<bool> onValueChanged = this.debugColorToggle.onValueChanged;
			UnityAction<bool> unityAction;
			if ((unityAction = VTDebugScreenContent.<>O.<0>__OnDebugColorToggled) == null)
			{
				unityAction = (VTDebugScreenContent.<>O.<0>__OnDebugColorToggled = new UnityAction<bool>(VTDebugScreenContent.OnDebugColorToggled));
			}
			onValueChanged.AddListener(unityAction);
			UnityEvent<bool> onValueChanged2 = this.desiredLevelToggle.onValueChanged;
			UnityAction<bool> unityAction2;
			if ((unityAction2 = VTDebugScreenContent.<>O.<1>__OnDesiredLevelToggled) == null)
			{
				unityAction2 = (VTDebugScreenContent.<>O.<1>__OnDesiredLevelToggled = new UnityAction<bool>(VTDebugScreenContent.OnDesiredLevelToggled));
			}
			onValueChanged2.AddListener(unityAction2);
			VTDebugScreenContent.AddSpacer(t, 10f);
			VTDebugScreenContent.AddLabel(t, "Color legend: red=L0 orange=L1 yellow=L2 lime=L3 green=L4 cyan=L5 blue=L6\nviolet=L7 purple=L8 pink=L9 white=L10 grey=L11 dark=L12  magenta=missing", false, 11f);
			VTDebugScreenContent.AddSpacer(t, 10f);
			VTDebugScreenContent.AddLabel(t, "Streaming Stats", true, 14f);
			VTDebugScreenContent.AddSpacer(t, 2f);
			GameObject statsGo = new GameObject("Stats", new Type[]
			{
				typeof(RectTransform)
			});
			statsGo.transform.SetParent(t, false);
			this.statsText = statsGo.AddComponent<TextMeshProUGUI>();
			this.statsText.fontSize = 12f;
			this.statsText.text = "(no VT bodies registered)";
			this.statsText.enableWordWrapping = false;
			this.statsText.overflowMode = 0;
			this.statsText.alignment = 257;
			LayoutElement statsLayout = statsGo.AddComponent<LayoutElement>();
			statsLayout.flexibleHeight = 1f;
		}

		// Token: 0x0600030F RID: 783 RVA: 0x00019270 File Offset: 0x00017470
		private static Toggle BuildToggle(Transform parent, string label, bool initialState)
		{
			GameObject row = new GameObject("ToggleRow", new Type[]
			{
				typeof(RectTransform)
			});
			row.transform.SetParent(parent, false);
			HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
			hlg.spacing = 8f;
			hlg.childAlignment = 3;
			hlg.childControlWidth = false;
			hlg.childControlHeight = true;
			hlg.childForceExpandWidth = false;
			hlg.childForceExpandHeight = false;
			row.AddComponent<LayoutElement>().minHeight = 24f;
			GameObject bgGo = new GameObject("Checkbox", new Type[]
			{
				typeof(RectTransform)
			});
			bgGo.transform.SetParent(row.transform, false);
			Image bgImg = bgGo.AddComponent<Image>();
			bgImg.color = new Color(0.25f, 0.25f, 0.25f, 1f);
			RectTransform bgRect = bgGo.GetComponent<RectTransform>();
			bgRect.sizeDelta = new Vector2(20f, 20f);
			LayoutElement bgLayout = bgGo.AddComponent<LayoutElement>();
			bgLayout.minWidth = 20f;
			bgLayout.preferredWidth = 20f;
			bgLayout.minHeight = 20f;
			bgLayout.preferredHeight = 20f;
			GameObject checkGo = new GameObject("Check", new Type[]
			{
				typeof(RectTransform)
			});
			checkGo.transform.SetParent(bgGo.transform, false);
			Image checkImg = checkGo.AddComponent<Image>();
			checkImg.color = new Color(0.95f, 0.85f, 0.2f, 1f);
			RectTransform checkRect = checkGo.GetComponent<RectTransform>();
			checkRect.anchorMin = new Vector2(0.15f, 0.15f);
			checkRect.anchorMax = new Vector2(0.85f, 0.85f);
			checkRect.offsetMin = Vector2.zero;
			checkRect.offsetMax = Vector2.zero;
			Toggle toggle = bgGo.AddComponent<Toggle>();
			toggle.targetGraphic = bgImg;
			toggle.graphic = checkImg;
			toggle.isOn = initialState;
			VTDebugScreenContent.AddLabel(row.transform, label, false, 13f);
			return toggle;
		}

		// Token: 0x06000310 RID: 784 RVA: 0x00019495 File Offset: 0x00017695
		private static void OnDesiredLevelToggled(bool isOn)
		{
			Shader.SetGlobalFloat("_VTDebugMode", isOn ? 1f : 0f);
		}

		// Token: 0x06000311 RID: 785 RVA: 0x000194B4 File Offset: 0x000176B4
		private static void OnDebugColorToggled(bool isOn)
		{
			if (isOn)
			{
				Shader.EnableKeyword("MIRAGE_VT_DEBUG");
			}
			else
			{
				Shader.DisableKeyword("MIRAGE_VT_DEBUG");
			}
		}

		// Token: 0x06000312 RID: 786 RVA: 0x000194E0 File Offset: 0x000176E0
		private void Update()
		{
			bool flag = this.statsText == null;
			if (!flag)
			{
				List<TileStreamingManager.BodyDebugInfo> bodies = TileStreamingManager.GetAllBodyDebugInfo();
				bool flag2 = bodies.Count == 0;
				if (flag2)
				{
					this.statsText.text = "(no VT bodies registered)";
				}
				else
				{
					this.sb.Length = 0;
					for (int b = 0; b < bodies.Count; b++)
					{
						TileStreamingManager.BodyDebugInfo info = bodies[b];
						this.sb.Append(info.sphereName).Append('\n');
						this.sb.Append("  slots:").Append(info.slots).Append('/').Append(info.total).Append("  Q:").Append(info.queue).Append("  F:").Append(info.flight).Append("  load:").Append(info.loading).Append("  done:").Append(info.completed).Append("  miss:").Append(info.missing).Append("  desync:").Append(info.desync);
						bool flag3 = info.badIndirection > 0;
						if (flag3)
						{
							this.sb.Append("  *** BAD INDIRECTION:").Append(info.badIndirection).Append(" (see log)");
						}
						this.sb.Append("\n  L_dir:").Append(info.dirLevel).Append("/L").Append(info.maxLevel);
						bool flag4 = info.totalBlocks > 0;
						if (flag4)
						{
							this.sb.Append("  blocks:").Append(info.blocks).Append('/').Append(info.totalBlocks);
						}
						else
						{
							this.sb.Append("  blocks: (no fine tier)");
						}
						bool flag5 = info.levelCounts != null && info.levelCounts.Length != 0;
						if (flag5)
						{
							this.sb.Append("  [");
							for (int i = 0; i < info.levelCounts.Length; i++)
							{
								bool flag6 = i > 0;
								if (flag6)
								{
									this.sb.Append(' ');
								}
								this.sb.Append('L').Append(i).Append(':').Append(info.levelCounts[i]);
							}
							this.sb.Append(']');
						}
						this.sb.Append('\n');
						this.sb.Append("  req:").Append(info.tilesRequested).Append("  loaded:").Append(info.tilesLoaded).Append('\n');
						bool hasIngest = info.hasIngest;
						if (hasIngest)
						{
							this.sb.Append("  ingest want:").Append(info.ingestPending).Append("  active:").Append(info.ingestActive).Append("  baked:").Append(info.ingestBaked);
							bool flag7 = info.ingestNoCoverage > 0;
							if (flag7)
							{
								this.sb.Append("  nocov:").Append(info.ingestNoCoverage);
							}
							bool flag8 = info.ingestFailed > 0;
							if (flag8)
							{
								this.sb.Append("  fail:").Append(info.ingestFailed);
							}
							this.sb.Append('\n');
						}
						bool flag9 = b < bodies.Count - 1;
						if (flag9)
						{
							this.sb.Append('\n');
						}
					}
					this.statsText.text = this.sb.ToString();
				}
			}
		}

		// Token: 0x06000313 RID: 787 RVA: 0x000198D4 File Offset: 0x00017AD4
		private static void AddLabel(Transform parent, string text, bool bold, float size)
		{
			GameObject go = new GameObject("Label", new Type[]
			{
				typeof(RectTransform)
			});
			go.transform.SetParent(parent, false);
			TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
			tmp.text = text;
			tmp.fontSize = size;
			tmp.fontStyle = (bold ? 1 : 0);
			tmp.alignment = 257;
			tmp.enableWordWrapping = false;
			tmp.overflowMode = 0;
			LayoutElement le = go.AddComponent<LayoutElement>();
			le.minHeight = size + 4f;
			le.preferredHeight = size + 4f;
		}

		// Token: 0x06000314 RID: 788 RVA: 0x00019974 File Offset: 0x00017B74
		private static void AddSpacer(Transform parent, float height)
		{
			GameObject go = new GameObject("Spacer", new Type[]
			{
				typeof(RectTransform)
			});
			go.transform.SetParent(parent, false);
			LayoutElement le = go.AddComponent<LayoutElement>();
			le.minHeight = height;
			le.preferredHeight = height;
		}

		// Token: 0x040002C3 RID: 707
		public const string DebugKeyword = "MIRAGE_VT_DEBUG";

		// Token: 0x040002C4 RID: 708
		public const string DebugModeUniform = "_VTDebugMode";

		// Token: 0x040002C5 RID: 709
		[SerializeField]
		private TextMeshProUGUI statsText;

		// Token: 0x040002C6 RID: 710
		[SerializeField]
		private Toggle debugColorToggle;

		// Token: 0x040002C7 RID: 711
		[SerializeField]
		private Toggle desiredLevelToggle;

		// Token: 0x040002C8 RID: 712
		private readonly StringBuilder sb = new StringBuilder(1024);

		// Token: 0x020000CB RID: 203
		[CompilerGenerated]
		private static class <>O
		{
			// Token: 0x0400054B RID: 1355
			public static UnityAction<bool> <0>__OnDebugColorToggled;

			// Token: 0x0400054C RID: 1356
			public static UnityAction<bool> <1>__OnDesiredLevelToggled;
		}
	}
}
