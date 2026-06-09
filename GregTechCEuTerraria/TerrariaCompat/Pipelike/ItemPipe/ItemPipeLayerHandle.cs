#nullable enable
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Pipenet;
using GregTechCEuTerraria.TerrariaCompat.Items.Pipes;
using GregTechCEuTerraria.TerrariaCompat.Net;
using Terraria;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.ItemPipe;

public sealed class ItemPipeLayerHandle : IGridLayerHandle
{
	public static readonly ItemPipeLayerHandle Instance = new();
	private ItemPipeLayerHandle() { }

	public bool Has(int x, int y) => ItemPipeLayerSystem.Pipes.Has(x, y);

	public bool TryPlace(ItemPipeCell cell, int x, int y, Player placer)
	{
		if (PipeIntersection.BlocksPipeAt(x, y)) return false;
		var existing = ItemPipeLayerSystem.Pipes.CellAt(x, y);
		if (existing.HasValue && existing.Value.Equals(cell)) return false;
		if (existing.HasValue) RefundAt(placer, existing.Value);
		ItemPipeLayerSystem.Pipes.Set(x, y, cell);
		ItemPipeLayerSystem.EnsureSides(x, y);
		ItemPipeNetSystem.OnPipeAdded(x, y, cell);
		PipePackets.SendPlacedItem(x, y, cell);
		NotifyAdjacentCoversNeighborChanged(x, y);
		return true;
	}

	public bool CutAt(int x, int y, Player remover)
	{
		var existing = ItemPipeLayerSystem.Pipes.CellAt(x, y);
		if (existing is null) return false;
		ItemPipeLayerSystem.Pipes.Remove(x, y);
		ItemPipeLayerSystem.DropSides(x, y);
		ItemPipeNetSystem.OnPipeRemoved(x, y);
		PipePackets.SendRemove(x, y, PipeKind.Item);
		RefundAt(remover, existing.Value);
		NotifyAdjacentCoversNeighborChanged(x, y);
		return true;
	}

	internal static void NotifyAdjacentCoversNeighborChanged(int x, int y)
	{
		foreach (var (_, dx, dy) in IODirectionExtensions.Cardinal4)
		{
			int nx = x + dx, ny = y + dy;
			var pcv = ItemPipeLayerSystem.GetSides(nx, ny);
			if (pcv is null) continue;
			((Api.Cover.ICoverable)pcv).OnCoversNeighborChanged();
		}
	}

	private static void RefundAt(Player player, ItemPipeCell cell)
	{
		int type;
		if (cell.IsSimple)
		{
			if (!ModContent.GetInstance<GregTechCEuTerraria>().TryFind<ModItem>("simple_item_pipe", out var mi))
				return;
			type = mi.Type;
		}
		else
		{
			string prefix = cell.Restrictive ? "_restrictive_item_pipe" : "_item_pipe";
			string id = cell.MaterialId + "_" + PipeSizes.Word(cell.Size) + prefix;
			int? t = PipeItemRegistry.Get(id);
			if (t is null) return;
			type = t.Value;
		}
		global::GregTechCEuTerraria.TerrariaCompat.Utils.PlayerGive.Give(player, player.GetSource_Misc("PipeRemove"), type, 1);
	}
}
