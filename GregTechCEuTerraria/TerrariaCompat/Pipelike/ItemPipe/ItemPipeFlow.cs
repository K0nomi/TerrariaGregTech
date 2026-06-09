#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Cover;
using GregTechCEuTerraria.TerrariaCompat.Cover;
using Terraria;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.ItemPipe;

public static class ItemPipeFlow
{
	private static readonly Dictionary<(int x, int y), CoverSide> _outflow = new();
	private static long _lastBuilt = -1;
	private const int RebuildEveryTicks = 20;

	private static readonly (CoverSide side, int dx, int dy, int bit)[] Dirs =
	{
		(CoverSide.Up, 0, -1, 1), (CoverSide.Down, 0, 1, 2),
		(CoverSide.Left, -1, 0, 4), (CoverSide.Right, 1, 0, 8),
	};

	public static bool TryGetOutflow(int x, int y, out CoverSide outflow)
	{
		EnsureBuilt();
		return _outflow.TryGetValue((x, y), out outflow);
	}

	private static void EnsureBuilt()
	{
		long now = Main.GameUpdateCount;
		if (_lastBuilt >= 0 && now - _lastBuilt < RebuildEveryTicks) return;
		_lastBuilt = now;
		Build();
	}

	private static void Build()
	{
		_outflow.Clear();
		var layer = ItemPipeLayerSystem.Pipes;
		if (layer.Count == 0) return;

		var q = new Queue<(int x, int y)>();

		foreach (var kv in layer.All)
		{
			int x = kv.Key.x, y = kv.Key.y;
			var pcv = ItemPipeLayerSystem.GetSides(x, y);
			if (pcv is null) continue;
			foreach (var (side, _, _, _) in Dirs)
			{
				if (pcv.GetMode(side) != PipeSideMode.Active) continue;
				if (PipeCoverable.ActiveIoAt(pcv, side) != IO.OUT) continue;
				if (PipeNeighborProbe.ProbeAt(x, y, side, PipeKind.Item) != SideNeighbourKind.Inventory) continue;
				if (!_outflow.ContainsKey((x, y))) { _outflow[(x, y)] = side; q.Enqueue((x, y)); }
			}
		}

		while (q.Count > 0)
		{
			var (cx, cy) = q.Dequeue();
			int mask = layer.ConnectionMask(cx, cy);
			foreach (var (side, dx, dy, bit) in Dirs)
			{
				if ((mask & bit) == 0) continue;
				var np = Api.Pipenet.PipePassthrough.EffectiveNeighbor(cx, cy, dx, dy);
				if (!layer.CellAt(np.x, np.y).HasValue) continue;
				if (_outflow.ContainsKey(np)) continue;
				_outflow[np] = Opposite(side);
				q.Enqueue(np);
			}
		}
	}

	private static CoverSide Opposite(CoverSide s) => s switch
	{
		CoverSide.Up    => CoverSide.Down,
		CoverSide.Down  => CoverSide.Up,
		CoverSide.Left  => CoverSide.Right,
		CoverSide.Right => CoverSide.Left,
		_               => s,
	};
}
