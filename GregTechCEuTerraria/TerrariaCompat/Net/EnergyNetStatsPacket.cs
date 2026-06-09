#nullable enable
using System.IO;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Cable;
using Terraria;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Net;

public static class EnergyNetStatsPacket
{
	public static void Broadcast()
	{
		if (Main.netMode != NetmodeID.Server) return;
		var nets = EnergyNetSystem.Nets;

		static bool Active(EnergyNet net) =>
			net.LastTickExtracted != 0 || net.LastTickDelivered != 0 || net.SmoothedLoad > 0f;

		int n = 0;
		for (int i = 0; i < nets.Count; i++)
			if (Active(nets[i])) n++;

		var p = NetRouter.NewPacket(PacketType.EnergyNetStats);
		p.Write(n);
		for (int i = 0; i < nets.Count; i++)
		{
			var net = nets[i];
			if (!Active(net)) continue;
			var anchor = net.AnchorCell;
			p.Write((short)anchor.x);
			p.Write((short)anchor.y);
			p.Write(net.LastTickExtracted);
			p.Write(net.LastTickDelivered);
			p.Write((byte)(net.SmoothedLoad * 255f));
		}
		p.Send();
	}

	public static void HandleOnClient(BinaryReader r)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient) return;
		int n = r.ReadInt32();
		var cache = EnergyNetSystem.ClientStats;
		var load  = EnergyNetSystem.ClientLoad;
		cache.Clear();
		load.Clear();
		for (int i = 0; i < n; i++)
		{
			int x = r.ReadInt16();
			int y = r.ReadInt16();
			long ex = r.ReadInt64();
			long de = r.ReadInt64();
			float ld = r.ReadByte() / 255f;
			cache[(x, y)] = (ex, de);
			load[(x, y)]  = ld;
		}
	}
}
