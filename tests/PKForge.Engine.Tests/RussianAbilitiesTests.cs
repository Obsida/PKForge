using PKForge.App.Services;
using Xunit;

namespace PKForge.Engine.Tests;

public sealed class RussianAbilitiesTests
{
    [Fact]
    public void TranslationsMatchCanonicalIdsAndDoNotChangeEngineNames()
    {
        var original = new GameDataService();
        var localized = new RussianGameDataService(original);
        Assert.Equal(original.AbilityNames.Count, localized.AbilityNames.Count);
        foreach (var (id, translation) in RussianAbilities.Entries)
        {
            Assert.Equal(translation.EnglishName, original.AbilityNames[id]);
            Assert.Equal(translation.Name, localized.AbilityNames[id]);
            Assert.Contains(translation.Name, c => c is >= 'А' and <= 'я');
            Assert.Contains(RussianAbilities.Effect(id, null)!, c => c is >= 'А' and <= 'я');
        }
        Assert.Same(original.SpeciesNames, localized.SpeciesNames);
        Assert.Same(original.MoveNames, localized.MoveNames);
        Assert.Same(original.ItemNames, localized.ItemNames);
    }

    [Fact]
    public void UntranslatedAbilitiesRetainNamesAndDescriptions()
    {
        var original = new GameDataService();
        var localized = new RussianGameDataService(original);
        var id = Enumerable.Range(1, original.AbilityNames.Count - 1).First(id => !RussianAbilities.Entries.ContainsKey(id));
        Assert.Equal(original.AbilityNames[id], localized.AbilityNames[id]);
        Assert.Equal("Original effect", RussianAbilities.Effect(id, "Original effect"));
        Assert.Null(RussianAbilities.Effect(-1, null));
    }
}
