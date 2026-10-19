using PKForge.App.Theme;
using PKForge.Domain;

namespace PKForge.App.Views;

/// <summary>
/// Cross-format Pokérus and ribbon editor. The engine reports exactly what the current
/// Pokémon structure can store; this surface never guesses from the save generation.
/// </summary>
public static class AwardsEditor
{
    public static async Task<bool> ShowAsync(Grid host, ISaveEngineSession session, int box, int slot)
    {
        var dirty = false;
        while (true)
        {
            var pokerus = session.GetPokerus(box, slot);
            var ribbons = session.GetRibbons(box, slot);
            var affixed = session.GetAffixedRibbon(box, slot);
            var selected = ribbons.Count(r => r.Value != 0);
            var options = new List<PadOption>();
            if (pokerus.Supported || pokerus.Status != PokerusStatus.Susceptible)
                options.Add(new PadOption($"Pokérus · {PokerusLabel(pokerus)}", IconPath: PokerusIcon(pokerus.Status),
                    Detail: "Редкий вирус, удваивающий получаемые в бою EV."));
            if (ribbons.Count != 0)
                options.Add(new PadOption($"Ленты · {selected}/{ribbons.Count}", IconPath: "ribbons", Detail: "Полученные ленты и метки."));
            if (affixed.Supported)
                options.Add(new PadOption($"Титул · {affixed.SelectedName}", IconPath: "ribbons", Detail: "Лента или метка, отображаемая как титул рядом с именем."));

            if (options.Count == 0)
            {
                await EditorMenu.ShowAsync(host, "Награды", "В этом формате нет данных покеруса и лент.", "OK");
                return dirty;
            }

            var choice = await EditorMenu.ShowAsync(host, "Награды", null, options.ToArray());
            if (choice is null) return dirty;
            if (choice.StartsWith("Pokérus", StringComparison.Ordinal))
            {
                if (await EditPokerusAsync(host, session, box, slot)) dirty = true;
            }
            else if (choice.StartsWith("Ленты", StringComparison.Ordinal))
            {
                if (await EditRibbonsAsync(host, session, box, slot)) dirty = true;
            }
            else if (choice.StartsWith("Титул", StringComparison.Ordinal))
            {
                if (await EditAffixedRibbonAsync(host, session, box, slot)) dirty = true;
            }
        }
    }

    private static async Task<bool> EditPokerusAsync(Grid host, ISaveEngineSession session, int box, int slot)
    {
        var current = session.GetPokerus(box, slot);
        var choice = await EditorMenu.ShowAsync(host, "POKéRUS",
            "Infectious spreads Pokérus. Cured keeps the immunity marker but no longer spreads it.",
            new PadOption("Заразить", IconPath: "pokerus-infected", Accent: UiTokens.GiftRed, Detail: "Носит покерус и может его распространять."),
            new PadOption("Вылечить", IconPath: "pokerus-cured", Accent: UiTokens.Green, Detail: "Больше не распространяет покерус и не может заразиться снова."),
            new PadOption("Не заражён / восприимчив", Glyph: "○", Accent: UiTokens.Ink1, Detail: "Покеруса нет; покемон может заразиться."));
        if (choice is null) return false;

        var status = choice switch
        {
            "Заразить" => PokerusStatus.Infectious,
            "Вылечить" => PokerusStatus.Cured,
            _ => PokerusStatus.Susceptible,
        };
        if (status == current.Status) return false;
        session.SetPokerus(box, slot, status);
        return true;
    }

    private static async Task<bool> EditRibbonsAsync(Grid host, ISaveEngineSession session, int box, int slot)
    {
        var dirty = false;
        while (true)
        {
            var ribbons = session.GetRibbons(box, slot);
            var items = ribbons.Select((r, i) => new PickItem(i,
                r.MaxValue == 1
                    ? $"{(r.Value != 0 ? "✓" : "□")} {r.Name}{(r.IsMark ? " · mark" : "")}"
                    : $"{r.Name} · {r.Value}/{r.MaxValue}",
                $"ribbons/{r.Id.ToLowerInvariant()}.png"))
                .ToList();
            var picked = await PickerMenu.ShowAsync(host, "Ленты и метки", items);
            if (picked is null) return dirty;

            var ribbon = ribbons[picked.Id];
            if (ribbon.MaxValue == 1)
            {
                session.SetRibbon(box, slot, ribbon.Id, ribbon.Value == 0 ? 1 : 0);
                dirty = true;
                continue;
            }

            var count = await StatsPopup.ShowSingleAsync(host, ribbon.Name, ribbon.Value, ribbon.MaxValue);
            if (count is not { } value || value == ribbon.Value) continue;
            session.SetRibbon(box, slot, ribbon.Id, value);
            dirty = true;
        }
    }

    private static async Task<bool> EditAffixedRibbonAsync(Grid host, ISaveEngineSession session, int box, int slot)
    {
        var info = session.GetAffixedRibbon(box, slot);
        var items = new List<PickItem> { new(-1, "□ No title", "ribbons/none.png") };
        items.AddRange(info.Choices.Select(r => new PickItem(r.Id, $"{(r.Id == info.SelectedIndex ? "✓" : "□")} {r.Name}", "ribbons/ribbon.png")));
        var picked = await PickerMenu.ShowAsync(host, "Выбрать титул", items);
        if (picked is null || picked.Id == info.SelectedIndex) return false;

        session.SetAffixedRibbon(box, slot, picked.Id);
        return true;
    }

    private static string PokerusLabel(PokerusInfo info) => info.Status switch
    {
        PokerusStatus.Infectious => $"infectious · {info.Days} day{(info.Days == 1 ? "" : "s")}",
        PokerusStatus.Cured => "cured",
        _ => "susceptible",
    };

    private static string PokerusIcon(PokerusStatus status) =>
        status == PokerusStatus.Cured ? "pokerus-cured" : "pokerus-infected";
}
