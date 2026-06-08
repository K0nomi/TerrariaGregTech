#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.TerrariaCompat.Utils;
using Terraria;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Steam;

public class SteamSolarBoiler : SteamBoilerMachine
{
	public SteamSolarBoiler() : base() { }

	protected override string Label => Definition?.Label ?? "Solar Boiler";
	public override GTRecipeType GetRecipeType() => Definition?.RecipeType!;

	public override bool ShowsInRecipeBrowser(GTRecipe recipe) => false;

	protected override long GetBaseSteamOutput() => IsHighPressure ? 360 : 120;

	protected override int GetCooldownInterval() => IsHighPressure ? 50 : 45;
	protected override int GetCoolDownRate()     => 3;

	protected override bool IsHeating() => SkyStatusCached == SunlightAccess.SkyStatus.Clear;

	public override bool IsActive => CurrentTemperature > 0;

	private SunlightAccess.SkyStatus _skyCache;
	private uint _skyCacheTick;
	private bool _skyCacheValid;

	public SunlightAccess.SkyStatus SkyStatusCached
	{
		get
		{
			uint now = (uint)Main.GameUpdateCount;
			if (!_skyCacheValid || now - _skyCacheTick >= 20)
			{
				_skyCacheTick  = now;
				_skyCacheValid = true;
				_skyCache      = SunlightAccess.Check(Position);
			}
			return _skyCache;
		}
	}

	public override void AppendTooltip(List<string> lines)
	{
		base.AppendTooltip(lines);
		if (SkyStatusCached == SunlightAccess.SkyStatus.Obstructed)
			lines.Add("Can't see sky");
	}
}
