#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Fluids;

namespace GregTechCEuTerraria.Api.Recipe.Ingredient;

// Resolver bridge - translates upstream item / tag / fluid IDs to Terraria-
// side types at Ingredient construction time
public interface IIngredientResolver
{
	static IIngredientResolver? Default { get; set; }

	int ResolveItemType(string upstreamId);

	IReadOnlyList<int> ResolveItemTag(string tagName);

	FluidType? ResolveFluidType(string upstreamId);

	IReadOnlyList<FluidType> ResolveFluidTag(string tagName);
}
