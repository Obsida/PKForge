using PKForge.App.Services;
using Xunit;

namespace PKForge.Engine.Tests;

public sealed class RussianGameTextTests
{
    [Fact]
    public void NaturesKeepCanonicalIdsAndEnglishSearchNames()
    {
        var original = new GameDataService();
        var localized = new RussianGameDataService(original);
        Assert.Equal(25, localized.NatureNames.Count);
        for (var id = 0; id < original.NatureNames.Count; id++)
        {
            Assert.Equal(original.NatureNames[id], RussianGameText.EnglishNatureName(id));
            Assert.Contains(localized.NatureNames[id], c => c is >= 'А' and <= 'я');
        }
        Assert.Equal("Непреклонный", localized.NatureNames[3]);
        Assert.Equal("Скромный", localized.NatureNames[15]);
        Assert.Equal("Без изменений", RussianGameText.NatureEffect(0));
        Assert.Contains("Нет предпочтений", RussianGameText.NatureFlavor(0));
        Assert.Contains("Любит острый", RussianGameText.NatureFlavor(3));
    }

    [Fact]
    public void FutureNamesAndStellarTypeHaveSafeDisplayLabels()
    {
        Assert.Equal("Future nature", RussianGameText.NatureName(25, "Future nature"));
        Assert.Null(RussianGameText.EnglishNatureName(25));
        Assert.Equal("Звёздный", RussianGameText.TypeName(99));
        Assert.Equal("#100", RussianGameText.TypeName(100));
        Assert.Equal("Future ability", RussianAbilities.TranslateName("Future ability"));
        Assert.Equal("Единство", RussianAbilities.TranslateName("As One"));
    }
}
