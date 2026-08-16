using System;
using System.Diagnostics;
using Unity.Profiling;

namespace Mirage.VirtualTexture
{
	/// <summary>
	/// A phase of work measured two ways at once: a ProfilerMarker for a profiler timeline, and a
	/// <see cref="T:Mirage.VirtualTexture.FrameProfile" /> counter for the periodic log line. Wrap the work in
	/// <c>using (phase.Measure())</c>.
	/// </summary>
	// Token: 0x02000050 RID: 80
	public readonly struct ProfiledPhase
	{
		// Token: 0x060001EF RID: 495 RVA: 0x0000E524 File Offset: 0x0000C724
		public ProfiledPhase(ProfilePhase phase, string markerName)
		{
			this.phase = phase;
			this.marker = new ProfilerMarker(markerName);
		}

		/// <summary>Start measuring; disposing the returned scope records the elapsed time.</summary>
		// Token: 0x060001F0 RID: 496 RVA: 0x0000E53C File Offset: 0x0000C73C
		public ProfiledPhase.Scope Measure()
		{
			this.marker.Begin();
			return new ProfiledPhase.Scope(this.marker, this.phase);
		}

		// Token: 0x040001A4 RID: 420
		private readonly ProfilerMarker marker;

		// Token: 0x040001A5 RID: 421
		private readonly ProfilePhase phase;

		// Token: 0x020000CD RID: 205
		public readonly struct Scope : IDisposable
		{
			// Token: 0x060004B3 RID: 1203 RVA: 0x00021DE4 File Offset: 0x0001FFE4
			internal Scope(ProfilerMarker marker, ProfilePhase phase)
			{
				this.marker = marker;
				this.phase = phase;
				this.start = Stopwatch.GetTimestamp();
			}

			// Token: 0x060004B4 RID: 1204 RVA: 0x00021E00 File Offset: 0x00020000
			public void Dispose()
			{
				long ticks = Stopwatch.GetTimestamp() - this.start;
				this.marker.End();
				FrameProfile.Add(this.phase, ticks);
			}

			// Token: 0x0400056B RID: 1387
			private readonly ProfilerMarker marker;

			// Token: 0x0400056C RID: 1388
			private readonly ProfilePhase phase;

			// Token: 0x0400056D RID: 1389
			private readonly long start;
		}
	}
}
