using PKForge.Domain;

namespace PKForge.App.Services;

/// <summary>Display-only translations; the engine's tables and serialized game data stay canonical.</summary>
public sealed class RussianGameDataService(IGameDataService source) : IGameDataService
{
    public IReadOnlyList<string> SpeciesNames => source.SpeciesNames;
    public IReadOnlyList<string> MoveNames => source.MoveNames;
    public IReadOnlyList<string> ItemNames => source.ItemNames;
    public IReadOnlyList<string> AbilityNames { get; } = source.AbilityNames
        .Select((name, id) => RussianAbilities.Name(id, name)).ToArray();
    public IReadOnlyList<string> NatureNames { get; } = source.NatureNames
        .Select((name, id) => RussianGameText.NatureName(id, name)).ToArray();
    public IReadOnlyList<string> BallNames => source.BallNames;
    public IReadOnlyList<SpeciesFormFlags> FormFlags => source.FormFlags;
}
