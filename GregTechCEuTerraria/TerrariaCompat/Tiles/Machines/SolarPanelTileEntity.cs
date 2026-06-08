#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Utils;
using Terraria;

namespace GregTechCEuTerraria.TerrariaCompat.Tiles.Machines;

// Custom terraria-compat machine. Generates 1 amp of energy, checks sky using SunlightAccess
public sealed class SolarPanelTileEntity : TieredEnergyMachine
{
	public SolarPanelTileEntity() { }
	public SolarPanelTileEntity(VoltageTier tier) : base(tier) { }

	protected override string  Label       => "Solar Panel";
	public override long EnergyCapacity => VoltageTiers.Voltage(Tier) * 64;

	public override bool CanAccept  => false;
	public override bool CanExtract => true;

	protected override void OnTick()
	{
		if (!SunlightAccess.IsClear(Position)) return;
		long produced = VoltageTiers.Voltage(Tier);
		if (produced <= 0) return;
		EnergyContainer.SetEnergyStored(System.Math.Min(EnergyCapacity, EnergyContainer.EnergyStored + produced));
	}

	public override void AppendTooltip(List<string> lines)
	{
		base.AppendTooltip(lines);
		lines.Add(SunlightAccess.Check(Position) switch
		{
			SunlightAccess.SkyStatus.Clear      => $"Producing: {VoltageTiers.Voltage(Tier):N0} EU/t (1A at {VoltageTiers.ShortName(Tier)})",
			SunlightAccess.SkyStatus.Raining    => "Idle (rain)",
			SunlightAccess.SkyStatus.Obstructed => "Idle (can't see sky)",
			_                                   => "Idle (night)",
		});
	}
}
