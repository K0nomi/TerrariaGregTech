#nullable enable
using GregTechCEuTerraria.Api.Cover.Filter;
using Microsoft.Xna.Framework;

namespace GregTechCEuTerraria.TerrariaCompat.UI;

// warning for empty whitelist because its ambiguous
public static class FilterWarning
{
	public const string Text = "! EMPTY WHITELIST !";

	public static readonly Color Color = new(255, 70, 70);

	public static bool IsEmptyWhitelist(SimpleItemFilter? f)
	{
		if (f is null || f.IsBlackList) return false;
		foreach (var m in f.Matches) if (!m.IsAir) return false;
		return true;
	}

	public static bool IsEmptyWhitelist(SimpleFluidFilter? f)
	{
		if (f is null || f.IsBlackList) return false;
		foreach (var m in f.Matches) if (!m.IsEmpty) return false;
		return true;
	}
}
