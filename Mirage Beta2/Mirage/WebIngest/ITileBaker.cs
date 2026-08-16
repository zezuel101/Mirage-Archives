using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mirage.WebIngest
{
	/// <summary>Bakes one cube tile from web sources. The seam that keeps <see cref="T:Mirage.WebIngest.TileIngestQueue" /> free of
	/// Unity, HTTP and image codecs — so the state machine can be driven by a fake baker offline, where its
	/// invariants are actually testable, rather than only inside a live KSP session.</summary>
	// Token: 0x0200002E RID: 46
	public interface ITileBaker
	{
		// Token: 0x060000F4 RID: 244
		Task<BakedTile> BakeAsync(int face, int level, int tx, int ty, CancellationToken ct);
	}
}
