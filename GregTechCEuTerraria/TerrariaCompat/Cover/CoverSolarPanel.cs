#nullable enable
using System;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Cover;
using GregTechCEuTerraria.Api.Machine;
using GregTechCEuTerraria.TerrariaCompat.Capabilities;
using GregTechCEuTerraria.TerrariaCompat.Utils;
using Terraria;

namespace GregTechCEuTerraria.TerrariaCompat.Cover;

// Port of common.cover.CoverSolarPanel
//
// Adaptations:
//  GTUtil.canSeeSunClearly -> SunlightAccess helper
//  getEnergyContainer -> cast CoverHolder
//  acceptEnergyFromNetwork(null,...) -> AcceptEnergyFromNetwork(side,...).
public sealed class CoverSolarPanel : CoverBehavior
{
	private readonly long _eut;
	private TickableSubscription? _subscription;

	public CoverSolarPanel(CoverDefinition definition, ICoverable coverHolder, CoverSide attachedSide, long eut)
		: base(definition, coverHolder, attachedSide)
	{
		_eut = eut;
	}

	public override void OnLoad()
	{
		base.OnLoad();
		_subscription = CoverHolder.SubscribeServerTick(Update);
	}

	public override void OnRemoved()
	{
		base.OnRemoved();
		_subscription?.Unsubscribe();
		_subscription = null;
	}

	public override bool CanAttach() => base.CanAttach() && AttachedSide == CoverSide.Up && CoverHolder is IEnergyContainer;

	private void Update()
	{
		if (SunlightAccess.IsClear(CoverHolder.GetBlockPos()) && CoverHolder is IEnergyContainer energyContainer)
			energyContainer.AcceptEnergyFromNetwork(
				WorldCapability.ToIODirection(AttachedSide), _eut, 1);
	}

	public override string? GetStatusText() => SunlightAccess.Check(CoverHolder.GetBlockPos()) switch
	{
		SunlightAccess.SkyStatus.Clear      => $"Producing {_eut} EU/t",
		SunlightAccess.SkyStatus.Night      => "Idle: nighttime",
		SunlightAccess.SkyStatus.Raining    => "Idle: raining",
		SunlightAccess.SkyStatus.Obstructed => "Idle: can't see sky",
		_                                   => null,
	};
}
