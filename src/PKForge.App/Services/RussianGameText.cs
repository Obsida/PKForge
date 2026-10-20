using PKForge.Domain;

namespace PKForge.App.Services;

/// <summary>Display vocabulary only; IDs and engine/export strings are unchanged.</summary>
public static class RussianGameText
{
    private static readonly string[] Natures =
    [
        "Выносливый", "Одинокий", "Храбрый", "Непреклонный", "Непослушный",
        "Смелый", "Покладистый", "Расслабленный", "Озорной", "Беспечный",
        "Робкий", "Торопливый", "Серьёзный", "Весёлый", "Наивный",
        "Скромный", "Мягкий", "Тихий", "Застенчивый", "Опрометчивый",
        "Спокойный", "Нежный", "Дерзкий", "Осторожный", "Чудаковатый",
    ];
    private static readonly string[] EnglishNatures =
    [
        "Hardy", "Lonely", "Brave", "Adamant", "Naughty",
        "Bold", "Docile", "Relaxed", "Impish", "Lax",
        "Timid", "Hasty", "Serious", "Jolly", "Naive",
        "Modest", "Mild", "Quiet", "Bashful", "Rash",
        "Calm", "Gentle", "Sassy", "Careful", "Quirky",
    ];
    private static readonly string[] Types =
    [
        "Обычный", "Боевой", "Летающий", "Ядовитый", "Земляной", "Каменный", "Насекомый", "Призрачный", "Стальной",
        "Огненный", "Водный", "Травяной", "Электрический", "Психический", "Ледяной", "Драконий", "Тёмный", "Волшебный",
    ];
    private static readonly string[] Flavors = ["острый", "кислый", "сладкий", "сухой", "горький"];

    public static string NatureName(int id, string fallback) => (uint)id < Natures.Length ? Natures[id] : fallback;
    public static string? EnglishNatureName(int id) => (uint)id < EnglishNatures.Length ? EnglishNatures[id] : null;
    public static string TypeName(int id) => id == TypeFacts.Stellar ? "Звёздный" : (uint)id < Types.Length ? Types[id] : $"#{id}";
    public static string NatureEffect(int id) => NatureFacts.IsNeutral(id) ? "Без изменений" : NatureFacts.EffectLabel(id);
    public static string NatureFlavor(int id) => !NatureFacts.IsValid(id) ? "—" : NatureFacts.IsNeutral(id)
        ? "Нет предпочтений во вкусах"
        : $"Любит {Flavors[NatureFacts.LikedFlavor(id)!.Value]} · не любит {Flavors[NatureFacts.DislikedFlavor(id)!.Value]}";
}
