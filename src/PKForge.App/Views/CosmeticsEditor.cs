using PKForge.Domain;

namespace PKForge.App.Views;

/// <summary>
/// PKHeX-style cosmetic data that belongs to an individual Pokémon rather than its
/// battle build. The engine gates every group by the entity format, so a control is
/// never shown merely because the currently-open save is from a newer generation.
/// </summary>
public static class CosmeticsEditor
{
    private static readonly string[] ContestNames = ["Крутость", "Красота", "Миловидность", "Ум", "Крепость", "Блеск"];

    public static async Task<bool> ShowAsync(Grid host, ISaveEngineSession session, int box, int slot)
    {
        var dirty = false;
        while (true)
        {
            var c = session.GetCosmetics(box, slot);
            var options = new List<PadOption>();
            if (c.Markings.Count != 0) options.Add(new PadOption($"Отметки в боксе · {c.Markings.Count(m => m.Value != 0)}/{c.Markings.Count}",
                Detail: "Круг, сердце и другие символы в боксе."));
            if (c.ContestStats.Count != 0) options.Add(new PadOption($"Конкурсные показатели · {c.ContestStats.Sum()}/1530",
                Detail: "Cool, Beauty, Cute, Smart, Tough and Sheen, used in contests."));
            if (c.SupportsSize || c.SupportsScale) options.Add(new PadOption("Размер", Detail: "Внутренние значения роста, веса и размера."));
            if (c.SupportsAffection || c.SupportsFullnessEnjoyment) options.Add(new PadOption("Уход", Detail: "Привязанность, сытость и удовольствие."));
            if (HasSpecial(c)) options.Add(new PadOption("Special", Detail: "Favorite, Dynamax, Alpha and sociability, where the game has them."));

            if (options.Count == 0)
            {
                await EditorMenu.ShowAsync(host, "Внешность", "В этом формате нет данных внешности.", "OK");
                return dirty;
            }

            var choice = await EditorMenu.ShowAsync(host, "Внешность", null, options.ToArray());
            if (choice is null) return dirty;
            if (choice.StartsWith("Отметки в боксе", StringComparison.Ordinal)) dirty |= await EditMarkingsAsync(host, session, box, slot);
            else if (choice.StartsWith("Конкурсные показатели", StringComparison.Ordinal)) dirty |= await EditContestAsync(host, session, box, slot);
            else if (choice == "Размер") dirty |= await EditSizeAsync(host, session, box, slot);
            else if (choice == "Уход") dirty |= await EditCareAsync(host, session, box, slot);
            else if (choice == "Special") dirty |= await EditSpecialAsync(host, session, box, slot);
        }
    }

    private static bool HasSpecial(CosmeticInfo c) => c.SupportsFavorite || c.SupportsDynamax || c.SupportsAlpha || c.SupportsSociability;

    private static async Task<bool> EditMarkingsAsync(Grid host, ISaveEngineSession session, int box, int slot)
    {
        var dirty = false;
        while (true)
        {
            var c = session.GetCosmetics(box, slot);
            var items = c.Markings.Select((m, i) => new PickItem(i, $"{m.Name} · {MarkingValueName(m.Value, m.MaxValue)}")).ToList();
            var picked = await PickerMenu.ShowAsync(host, "Отметки в боксе", items);
            if (picked is null) return dirty;

            var marking = c.Markings[picked.Id];
            var next = await PickMarkingValueAsync(host, marking);
            if (next is not { } value || value == marking.Value) continue;
            var values = c.Markings.Select(m => m.Value).ToArray();
            values[picked.Id] = value;
            session.ApplyCosmeticEdit(box, slot, new CosmeticEdit(Markings: values));
            dirty = true;
        }
    }

    private static async Task<int?> PickMarkingValueAsync(Grid host, CosmeticMarking marking)
    {
        if (marking.MaxValue == 1)
        {
            var choice = await EditorMenu.ShowAsync(host, marking.Name, null,
                new PadOption("Выкл"), new PadOption("On"));
            return choice switch { "Выкл" => 0, "On" => 1, _ => null };
        }
        var color = await EditorMenu.ShowAsync(host, marking.Name, null,
            new PadOption("Выкл"), new PadOption("Blue"), new PadOption("Pink"));
        return color switch { "Выкл" => 0, "Blue" => 1, "Pink" => 2, _ => null };
    }

    private static async Task<bool> EditContestAsync(Grid host, ISaveEngineSession session, int box, int slot)
    {
        var dirty = false;
        while (true)
        {
            var c = session.GetCosmetics(box, slot);
            var options = ContestNames.Select((name, i) => new PadOption($"{name} · {c.ContestStats[i]}/255")).ToList();
            options.Add(new PadOption("Максимум всем", IconPath: "fill"));
            options.Add(new PadOption("Clear all", IconPath: "clear"));
            var choice = await EditorMenu.ShowAsync(host, "Конкурсные показатели", null, options.ToArray());
            if (choice is null) return dirty;

            var values = c.ContestStats.ToArray();
            if (choice == "Максимум всем") values = [255, 255, 255, 255, 255, 255];
            else if (choice == "Clear all") values = [0, 0, 0, 0, 0, 0];
            else
            {
                var index = Array.FindIndex(ContestNames, name => choice.StartsWith(name, StringComparison.Ordinal));
                if (index < 0) continue;
                var value = await StatsPopup.ShowSingleAsync(host, ContestNames[index], values[index], 255);
                if (value is not { } next || next == values[index]) continue;
                values[index] = next;
            }
            if (values.SequenceEqual(c.ContestStats)) continue;
            session.ApplyCosmeticEdit(box, slot, new CosmeticEdit(ContestStats: values));
            dirty = true;
        }
    }

    private static async Task<bool> EditSizeAsync(Grid host, ISaveEngineSession session, int box, int slot)
    {
        var c = session.GetCosmetics(box, slot);
        var fields = new List<(PadOption Option, int Value, Func<int, CosmeticEdit> Edit)>();
        if (c.SupportsSize)
        {
            fields.Add((new PadOption($"Масштаб роста · {c.HeightScalar}/255", Detail: "0 — самый низкий, 255 — самый высокий."), c.HeightScalar, n => new CosmeticEdit(HeightScalar: n)));
            fields.Add((new PadOption($"Масштаб веса · {c.WeightScalar}/255", Detail: "0 — самый лёгкий, 255 — самый тяжёлый."), c.WeightScalar, n => new CosmeticEdit(WeightScalar: n)));
        }
        if (c.SupportsScale) fields.Add((new PadOption($"Масштаб размера · {c.Scale}/255", Detail: "0 — самый маленький, 255 — самый большой."), c.Scale, n => new CosmeticEdit(Scale: n)));
        var choice = await EditorMenu.ShowAsync(host, "Размер", null, fields.Select(f => f.Option).ToArray());
        var index = fields.FindIndex(field => field.Option.Label == choice);
        if (index < 0) return false;
        var selected = fields[index];
        var value = await StatsPopup.ShowSingleAsync(host, selected.Option.Label.Split('·')[0].Trim(), selected.Value, 255);
        if (value is not { } next || next == selected.Value) return false;
        session.ApplyCosmeticEdit(box, slot, selected.Edit(next));
        return true;
    }

    private static async Task<bool> EditCareAsync(Grid host, ISaveEngineSession session, int box, int slot)
    {
        var c = session.GetCosmetics(box, slot);
        var fields = new List<(PadOption Option, int Value, Func<int, CosmeticEdit> Edit)>();
        if (c.SupportsAffection)
        {
            fields.Add((new PadOption($"Привязанность к исходному тренеру · {c.OriginalTrainerAffection}/255"), c.OriginalTrainerAffection, n => new CosmeticEdit(OriginalTrainerAffection: n)));
            fields.Add((new PadOption($"Привязанность к текущему тренеру · {c.HandlingTrainerAffection}/255"), c.HandlingTrainerAffection, n => new CosmeticEdit(HandlingTrainerAffection: n)));
        }
        if (c.SupportsFullnessEnjoyment)
        {
            fields.Add((new PadOption($"Сытость · {c.Fullness}/255"), c.Fullness, n => new CosmeticEdit(Fullness: n)));
            fields.Add((new PadOption($"Удовольствие · {c.Enjoyment}/255"), c.Enjoyment, n => new CosmeticEdit(Enjoyment: n)));
        }
        var choice = await EditorMenu.ShowAsync(host, "Уход", null, fields.Select(f => f.Option).ToArray());
        var index = fields.FindIndex(field => field.Option.Label == choice);
        if (index < 0) return false;
        var selected = fields[index];
        var value = await StatsPopup.ShowSingleAsync(host, selected.Option.Label.Split('·')[0].Trim(), selected.Value, 255);
        if (value is not { } next || next == selected.Value) return false;
        session.ApplyCosmeticEdit(box, slot, selected.Edit(next));
        return true;
    }

    private static async Task<bool> EditSpecialAsync(Grid host, ISaveEngineSession session, int box, int slot)
    {
        var c = session.GetCosmetics(box, slot);
        var options = new List<PadOption>();
        var keys = new Dictionary<string, string>();
        void Add(string key, PadOption option) { options.Add(option); keys[option.Label] = key; }
        if (c.SupportsFavorite) Add("favorite", new PadOption($"Избранный · {(c.IsFavorite ? "вкл" : "выкл")}"));
        if (c.SupportsDynamax)
        {
            Add("dynamax", new PadOption($"Уровень динамакса · {c.DynamaxLevel}/10", Detail: "Каждый уровень увеличивает HP в состоянии динамакса."));
            Add("gmax", new PadOption($"Фактор гигантамакса · {(c.CanGigantamax ? "вкл" : "выкл")}", Detail: "Разрешает гигантамакс, если у вида есть такая форма."));
        }
        if (c.SupportsAlpha) Add("alpha", new PadOption($"Альфа · {(c.IsAlpha ? "вкл" : "выкл")}", Detail: "Отметить покемона как крупную альфа-особь из игр Legends."));
        if (c.SupportsSociability) Add("social", new PadOption($"Общительность · {c.Sociability}"));
        var choice = await EditorMenu.ShowAsync(host, "Особые параметры", null, options.ToArray());
        if (choice is null || !keys.TryGetValue(choice, out var key)) return false;

        if (key == "dynamax")
        {
            var value = await StatsPopup.ShowSingleAsync(host, "Уровень динамакса", c.DynamaxLevel, 10);
            if (value is not { } next || next == c.DynamaxLevel) return false;
            session.ApplyCosmeticEdit(box, slot, new CosmeticEdit(DynamaxLevel: next));
            return true;
        }
        if (key == "social")
        {
            var value = await StatsPopup.ShowSingleAsync(host, "Общительность", (int)Math.Min(c.Sociability, int.MaxValue), int.MaxValue);
            if (value is not { } next || (uint)next == c.Sociability) return false;
            session.ApplyCosmeticEdit(box, slot, new CosmeticEdit(Sociability: (uint)next));
            return true;
        }
        if (key == "favorite") session.ApplyCosmeticEdit(box, slot, new CosmeticEdit(IsFavorite: !c.IsFavorite));
        else if (key == "gmax") session.ApplyCosmeticEdit(box, slot, new CosmeticEdit(CanGigantamax: !c.CanGigantamax));
        else if (key == "alpha") session.ApplyCosmeticEdit(box, slot, new CosmeticEdit(IsAlpha: !c.IsAlpha));
        else return false;
        return true;
    }

    private static string MarkingValueName(int value, int max) => max == 1
        ? (value == 0 ? "off" : "on")
        : value switch { 1 => "blue", 2 => "pink", _ => "off" };
}
