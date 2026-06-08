#nullable enable
using Terraria;
using Terraria.DataStructures;

namespace GregTechCEuTerraria.TerrariaCompat.Utils;

public static class SunlightAccess
{
	public enum SkyStatus { Clear, Night, Raining, Obstructed }

	public static SkyStatus Check(Point16 origin)
	{
		if (!Main.dayTime) return SkyStatus.Night;
		if (Main.raining)  return SkyStatus.Raining;

		if (origin.Y >= Main.worldSurface) return SkyStatus.Obstructed;

		for (int dx = 0; dx < 2; dx++)
		for (int dy = 1; dy <= 2; dy++)
		{
			int x = origin.X + dx;
			int y = origin.Y - dy;
			if (x < 0 || x >= Main.maxTilesX || y < 0) continue;
			var tile = Main.tile[x, y];
			if (tile.HasTile && Main.tileNoSunLight[tile.TileType]) return SkyStatus.Obstructed;
			if (!Main.wallLight[tile.WallType]) return SkyStatus.Obstructed;
		}
		return SkyStatus.Clear;
	}

	public static bool IsClear(Point16 origin) => Check(origin) == SkyStatus.Clear;
}
