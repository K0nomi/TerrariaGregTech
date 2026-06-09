#nullable enable
using System.IO;
using GregTechCEuTerraria.TerrariaCompat.Net;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Cable;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.ItemPipe;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Fluid;
using Terraria;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike;

public static class PipeIntersection
{
	public static int TileType = -1;

	public static bool BlocksPipeAt(int x, int y)
	{
		if (TileType < 0) return false;
		if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY) return false;
		Tile t = Main.tile[x, y];
		return t.HasTile && t.TileType == TileType;
	}

	public static void InstallHook() => Api.Pipenet.PipePassthrough.IsCrossover = BlocksPipeAt;

	public static void UninstallHook() => Api.Pipenet.PipePassthrough.IsCrossover = static (_, _) => false;

	private static int _recheckTicks;

	public static void RequestRecheck() => _recheckTicks = 4;

	public static void TickRecheck()
	{
		if (_recheckTicks <= 0) return;
		_recheckTicks--;
		CableLayerSystem.Cables.MarkDirty();
		ItemPipeLayerSystem.Pipes.MarkDirty();
		FluidPipeLayerSystem.Pipes.MarkDirty();
	}

	public static void OnPlaced(int x, int y, Player placer)
	{
		if (CableLayerHandle.Instance.Has(x, y))     CableLayerHandle.Instance.CutAt(x, y, placer);
		if (ItemPipeLayerHandle.Instance.Has(x, y))  ItemPipeLayerHandle.Instance.CutAt(x, y, placer);
		if (FluidPipeLayerHandle.Instance.Has(x, y)) FluidPipeLayerHandle.Instance.CutAt(x, y, placer);
		RequestRecheck();
		SendChange(x, y);
	}

	public static void OnRemoved(int x, int y)
	{
		RequestRecheck();
		SendChange(x, y);
	}

	private static void SendChange(int x, int y)
	{
		if (Main.netMode == NetmodeID.SinglePlayer) return;
		var p = NetRouter.NewPacket(PacketType.CrossoverChange);
		p.Write((short)x);
		p.Write((short)y);
		p.Send();
	}

	public static void HandleChange(BinaryReader r, int whoAmI)
	{
		int x = r.ReadInt16();
		int y = r.ReadInt16();
		RequestRecheck();
		if (Main.netMode == NetmodeID.Server)
		{
			var p = NetRouter.NewPacket(PacketType.CrossoverChange);
			p.Write((short)x);
			p.Write((short)y);
			p.Send(ignoreClient: whoAmI);
		}
	}
}
