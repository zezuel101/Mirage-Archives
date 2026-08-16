using System;
using Kopernicus.Configuration.ModLoader;

namespace Mirage.KopernicusMods
{
	/// <summary>Kopernicus loader for <see cref="T:Mirage.KopernicusMods.PQSMod_PlanetUV" />.</summary>
	// Token: 0x0200007B RID: 123
	public class PlanetUV : ModLoader<PQSMod_PlanetUV>
	{
		// Token: 0x0600038A RID: 906 RVA: 0x0001A840 File Offset: 0x00018A40
		public override void Create(PQS pqsVersion)
		{
			base.Create(pqsVersion);
			base.Mod.order = 2147483645;
		}
	}
}
