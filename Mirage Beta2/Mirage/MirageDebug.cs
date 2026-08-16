using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace Mirage
{
	/// <summary>
	/// Tagged logging shim — every message lands in the KSP log with the
	/// "[Mirage]" prefix so the user can filter / grep cleanly.
	///
	/// <para><b>Thread safety.</b> <see cref="M:UnityEngine.Debug.Log(System.Object)" /> is not safe to call off the
	/// main thread here: KSP hooks the log callback and, when the Alt+F12 debug console is open, runs it
	/// <i>synchronously on the calling thread</i>, instantiating a <c>TextMeshPro</c> GameObject and building a
	/// <c>Mesh</c>. GameObject/Mesh creation off the main thread is a hard engine crash (observed from
	/// <c>CubeTileBaker.BakeInner</c> on a thread-pool thread). So a log raised on any other thread is queued
	/// and flushed on the next main-thread frame by <see cref="T:Mirage.MirageDebug.PumpHost" />. Deferred lines therefore appear a
	/// frame late and after same-frame main-thread lines — acceptable for diagnostics.</para>
	/// </summary>
	// Token: 0x02000005 RID: 5
	public static class MirageDebug
	{
		/// <summary>Capture the main thread and start the deferred-log pump. Call once, early, from the main
		/// thread (before any background bake can log). Idempotent.</summary>
		// Token: 0x06000005 RID: 5 RVA: 0x00002078 File Offset: 0x00000278
		public static void Init()
		{
			MirageDebug.s_MainThreadId = Thread.CurrentThread.ManagedThreadId;
			bool flag = MirageDebug.s_Host != null;
			if (!flag)
			{
				GameObject go = new GameObject("MirageDebugPump");
				Object.DontDestroyOnLoad(go);
				MirageDebug.s_Host = go.AddComponent<MirageDebug.PumpHost>();
			}
		}

		// Token: 0x06000006 RID: 6 RVA: 0x000020C3 File Offset: 0x000002C3
		public static void Log(string message)
		{
			MirageDebug.Emit(MirageDebug.Level.Info, message);
		}

		// Token: 0x06000007 RID: 7 RVA: 0x000020CD File Offset: 0x000002CD
		public static void LogWarning(string message)
		{
			MirageDebug.Emit(MirageDebug.Level.Warning, message);
		}

		// Token: 0x06000008 RID: 8 RVA: 0x000020D7 File Offset: 0x000002D7
		public static void LogError(string message)
		{
			MirageDebug.Emit(MirageDebug.Level.Error, message);
		}

		// Token: 0x06000009 RID: 9 RVA: 0x000020E4 File Offset: 0x000002E4
		private static void Emit(MirageDebug.Level level, string message)
		{
			bool flag = MirageDebug.s_MainThreadId != -1 && Thread.CurrentThread.ManagedThreadId != MirageDebug.s_MainThreadId;
			if (flag)
			{
				MirageDebug.s_Deferred.Enqueue(new ValueTuple<MirageDebug.Level, string>(level, message));
			}
			else
			{
				MirageDebug.Write(level, message);
			}
		}

		// Token: 0x0600000A RID: 10 RVA: 0x00002134 File Offset: 0x00000334
		private static void Write(MirageDebug.Level level, string message)
		{
			if (level != MirageDebug.Level.Warning)
			{
				if (level != MirageDebug.Level.Error)
				{
					Debug.Log("[Mirage] " + message);
				}
				else
				{
					Debug.LogError("[Mirage] " + message);
				}
			}
			else
			{
				Debug.LogWarning("[Mirage] " + message);
			}
		}

		// Token: 0x04000002 RID: 2
		private static int s_MainThreadId = -1;

		// Token: 0x04000003 RID: 3
		[TupleElementNames(new string[]
		{
			"level",
			"message"
		})]
		private static readonly ConcurrentQueue<ValueTuple<MirageDebug.Level, string>> s_Deferred = new ConcurrentQueue<ValueTuple<MirageDebug.Level, string>>();

		// Token: 0x04000004 RID: 4
		private static MirageDebug.PumpHost s_Host;

		// Token: 0x02000078 RID: 120
		private enum Level
		{
			// Token: 0x040002E8 RID: 744
			Info,
			// Token: 0x040002E9 RID: 745
			Warning,
			// Token: 0x040002EA RID: 746
			Error
		}

		// Token: 0x02000079 RID: 121
		private sealed class PumpHost : MonoBehaviour
		{
			// Token: 0x06000422 RID: 1058 RVA: 0x0001BB10 File Offset: 0x00019D10
			private void Update()
			{
				for (;;)
				{
					ValueTuple<MirageDebug.Level, string> e;
					bool flag = MirageDebug.s_Deferred.TryDequeue(out e);
					if (!flag)
					{
						break;
					}
					MirageDebug.Write(e.Item1, e.Item2);
				}
			}
		}
	}
}
