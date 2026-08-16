using System;
using System.Collections.Generic;
using Mirage.WebIngest;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>Registry of bodies being streamed.</summary>
	// Token: 0x0200005A RID: 90
	public static class TileStreamingManager
	{
		// Token: 0x0600029C RID: 668 RVA: 0x000149D0 File Offset: 0x00012BD0
		public static void RegisterBody(string sphereName, IMirageBody body)
		{
			bool flag = TileStreamingManager.s_Streamers.ContainsKey(sphereName);
			if (!flag)
			{
				TileStreamingManager.s_Streamers[sphereName] = new BodyStreamer(sphereName, body);
				MirageDebug.Log("TileStreamingManager: registered '" + sphereName + "'");
			}
		}

		// Token: 0x0600029D RID: 669 RVA: 0x00014A18 File Offset: 0x00012C18
		public static void UnregisterBody(string sphereName)
		{
			BodyStreamer streamer;
			bool flag = !TileStreamingManager.s_Streamers.TryGetValue(sphereName, out streamer);
			if (!flag)
			{
				streamer.Shutdown();
				TileStreamingManager.s_Streamers.Remove(sphereName);
				MirageDebug.Log("TileStreamingManager: unregistered '" + sphereName + "'");
			}
		}

		/// <summary>Enable web ingest for an already-registered body.</summary>
		// Token: 0x0600029E RID: 670 RVA: 0x00014A68 File Offset: 0x00012C68
		public static void EnableIngest(string sphereName, ITileBaker baker, long diskCapBytes, int maxConcurrent = 2)
		{
			BodyStreamer streamer;
			bool flag = !TileStreamingManager.s_Streamers.TryGetValue(sphereName, out streamer);
			if (flag)
			{
				MirageDebug.LogError("EnableIngest: '" + sphereName + "' is not registered.");
			}
			else
			{
				streamer.EnableIngest(baker, diskCapBytes, maxConcurrent);
			}
		}

		// Token: 0x0600029F RID: 671 RVA: 0x00014AB0 File Offset: 0x00012CB0
		public static void Update(int frame)
		{
			FrameProfile.AddFrameTime((double)Time.unscaledDeltaTime * 1000.0);
			ArchiveLoadReaper.Reap();
			foreach (KeyValuePair<string, BodyStreamer> kvp in TileStreamingManager.s_Streamers)
			{
				kvp.Value.Update(frame);
			}
		}

		// Token: 0x060002A0 RID: 672 RVA: 0x00014B28 File Offset: 0x00012D28
		public static List<BodyDebugInfo> GetAllBodyDebugInfo()
		{
			List<BodyDebugInfo> result = new List<BodyDebugInfo>(TileStreamingManager.s_Streamers.Count);
			foreach (KeyValuePair<string, BodyStreamer> kvp in TileStreamingManager.s_Streamers)
			{
				result.Add(kvp.Value.Snapshot());
			}
			return result;
		}

		// Token: 0x04000261 RID: 609
		public const int PinnedMaxLevel = 0;

		// Token: 0x04000262 RID: 610
		public static bool ValidateEveryFrame;

		// Token: 0x04000263 RID: 611
		private static readonly Dictionary<string, BodyStreamer> s_Streamers = new Dictionary<string, BodyStreamer>();
	}
}
