#nullable enable
using System;

namespace GregTechCEuTerraria.Api.Pipenet;

// lets two perpendicular pipe runs cross without joining
public static class PipePassthrough
{
	public static Func<int, int, bool> IsCrossover = static (_, _) => false;

	public static (int x, int y) EffectiveNeighbor(int x, int y, int dx, int dy)
	{
		int nx = x + dx, ny = y + dy;
		int guard = 0;
		while (IsCrossover(nx, ny) && guard++ < 4096) { nx += dx; ny += dy; }
		return (nx, ny);
	}
}
