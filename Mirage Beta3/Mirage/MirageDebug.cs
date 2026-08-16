using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace Mirage
{
	/// <summary>
	/// Thread-safe "[Mirage]"-prefixed logging. Off-main-thread messages arrive a frame late.
	/// </summary>
	// Token: 0x02000004 RID: 4
	public static class MirageDebug
	{
		/// <summary>Call once from the main thread before any background work logs.</summary>
		// Token: 0x06000003 RID: 3 RVA: 0x00002064 File Offset: 0x00000264
		public static void Init()
		{
			bool flag = MirageDebug.s_Pump != null;
			if (!flag)
			{
				MirageDebug.s_MainThreadId = Thread.CurrentThread.ManagedThreadId;
				GameObject host = new GameObject("MirageDebugPump");
				Object.DontDestroyOnLoad(host);
				MirageDebug.s_Pump = host.AddComponent<MirageDebug.MainThreadLogPump>();
			}
		}

		// Token: 0x06000004 RID: 4 RVA: 0x000020AF File Offset: 0x000002AF
		public static void Log(string message)
		{
			MirageDebug.Emit(MirageDebug.Level.Info, message);
		}

		// Token: 0x06000005 RID: 5 RVA: 0x000020B9 File Offset: 0x000002B9
		public static void LogWarning(string message)
		{
			MirageDebug.Emit(MirageDebug.Level.Warning, message);
		}

		// Token: 0x06000006 RID: 6 RVA: 0x000020C3 File Offset: 0x000002C3
		public static void LogError(string message)
		{
			MirageDebug.Emit(MirageDebug.Level.Error, message);
		}

		// Token: 0x06000007 RID: 7 RVA: 0x000020D0 File Offset: 0x000002D0
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

		// Token: 0x06000008 RID: 8 RVA: 0x00002120 File Offset: 0x00000320
		private static void Write(MirageDebug.Level level, string message)
		{
			string line = "[Mirage] " + message;
			if (level != MirageDebug.Level.Warning)
			{
				if (level != MirageDebug.Level.Error)
				{
					Debug.Log(line);
				}
				else
				{
					Debug.LogError(line);
				}
			}
			else
			{
				Debug.LogWarning(line);
			}
		}

		// Token: 0x04000002 RID: 2
		[TupleElementNames(new string[]
		{
			"Level",
			"Message"
		})]
		private static readonly ConcurrentQueue<ValueTuple<MirageDebug.Level, string>> s_Deferred = new ConcurrentQueue<ValueTuple<MirageDebug.Level, string>>();

		// Token: 0x04000003 RID: 3
		private static int s_MainThreadId = -1;

		// Token: 0x04000004 RID: 4
		private static MirageDebug.MainThreadLogPump s_Pump;

		// Token: 0x0200008B RID: 139
		private enum Level
		{
			// Token: 0x04000375 RID: 885
			Info,
			// Token: 0x04000376 RID: 886
			Warning,
			// Token: 0x04000377 RID: 887
			Error
		}

		/// <summary>Drains the background-thread log queue each frame.</summary>
		// Token: 0x0200008C RID: 140
		private sealed class MainThreadLogPump : MonoBehaviour
		{
			// Token: 0x06000405 RID: 1029 RVA: 0x0001C8E0 File Offset: 0x0001AAE0
			private void Update()
			{
				for (;;)
				{
					ValueTuple<MirageDebug.Level, string> entry;
					bool flag = MirageDebug.s_Deferred.TryDequeue(out entry);
					if (!flag)
					{
						break;
					}
					MirageDebug.Write(entry.Item1, entry.Item2);
				}
			}
		}
	}
}
