using PKForge.App.Services;
using PKForge.Domain;
using PKHeX.Core;
using Xunit;

namespace PKForge.Engine.Tests;

public sealed class RussianAbilitiesTests
{
    [Fact]
    public void EveryAbilityInThePinnedEngineHasATranslation()
    {
        var count = new GameDataService().AbilityNames.Count - 1;
        Assert.Equal(count, RussianAbilities.Entries.Count);
        Assert.All(Enumerable.Range(1, count), id => Assert.True(RussianAbilities.Entries.ContainsKey(id), $"Missing ability {id}"));
    }

    [Fact]
    public void TranslationsMatchCanonicalIdsAndDoNotChangeEngineNames()
    {
        var original = new GameDataService();
        var localized = new RussianGameDataService(original);
        Assert.Equal(original.AbilityNames.Count, localized.AbilityNames.Count);
        Assert.Empty(RussianAbilities.Entries.Where(pair => pair.Value.EnglishName != original.AbilityNames[pair.Key])
            .Select(pair => $"{pair.Key}: expected '{original.AbilityNames[pair.Key]}', got '{pair.Value.EnglishName}'"));
        foreach (var (id, translation) in RussianAbilities.Entries)
        {
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
        var id = original.AbilityNames.Count + 10;
        Assert.Equal("Future ability", RussianAbilities.Name(id, "Future ability"));
        Assert.Equal(original.AbilityNames[0], localized.AbilityNames[0]);
        Assert.Equal("Original effect", RussianAbilities.Effect(id, "Original effect"));
        Assert.Null(RussianAbilities.Effect(-1, null));
    }

    [Fact]
    public void SameNamedAbilitiesKeepTheirDistinctFormsAndEffects()
    {
        Assert.Equal("As One (Glastrier)", RussianAbilities.EnglishName(266));
        Assert.Equal("As One (Spectrier)", RussianAbilities.EnglishName(267));
        Assert.NotEqual(RussianAbilities.Name(266, ""), RussianAbilities.Name(267, ""));
        Assert.NotEqual(RussianAbilities.Effect(266, null), RussianAbilities.Effect(267, null));
        Assert.Equal(4, Enumerable.Range(301, 4).Select(id => RussianAbilities.Name(id, "")).Distinct().Count());
        Assert.Contains("Скорость", RussianAbilities.Effect(301, null)!);
        Assert.Contains("Атаку", RussianAbilities.Effect(302, null)!);
        Assert.Contains("Спецзащиту", RussianAbilities.Effect(303, null)!);
        Assert.Contains("Защиту", RussianAbilities.Effect(304, null)!);
        // The personal table uses game IDs even where display-name suffixes differ.
        Assert.Equal(301, PersonalTable.SV.GetFormEntry(1017, 4).GetAbilityAtIndex(0));
        Assert.Equal(303, PersonalTable.SV.GetFormEntry(1017, 5).GetAbilityAtIndex(0));
        Assert.Equal(302, PersonalTable.SV.GetFormEntry(1017, 6).GetAbilityAtIndex(0));
        Assert.Equal(304, PersonalTable.SV.GetFormEntry(1017, 7).GetAbilityAtIndex(0));
    }

    [Fact]
    public void NewAbilitiesUseCanonicalDescriptionsInsteadOfShiftedSourceIds()
    {
        Assert.Contains("Скорость", RussianAbilities.Effect(238, null)!);
        Assert.Contains("восстанавливает союзнику", RussianAbilities.Effect(299, null)!);
        Assert.Contains("уклонение", RussianAbilities.Effect(300, null)!);
        Assert.Contains("отравление", RussianAbilities.Effect(305, null)!);
        Assert.Contains("уклонение", RussianAbilities.Effect(306, null)!);
        Assert.Contains("Терапагос", RussianAbilities.Effect(307, null)!);
        Assert.Contains("Печарантом", RussianAbilities.Effect(310, null)!);
    }

    [Theory]
    [InlineData(0, "Slot 1 · Static", "Ячейка 1 · Статика")]
    [InlineData(2, "Hidden · Static", "Скрытая · Статика")]
    [InlineData(2, "Hidden · Custom Surge", "Скрытая · Custom Surge")]
    public void AbilitySlotLabelsTranslateWithoutReinterpretingSlotIds(int id, string name, string expected)
    {
        Assert.Equal(expected, RussianAbilities.SlotName(new NamedChoice(id, name)));
    }
}
