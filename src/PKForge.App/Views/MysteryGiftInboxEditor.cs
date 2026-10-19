using PKForge.App.Services;
using PKForge.Domain;

namespace PKForge.App.Views;

/// <summary>Read-only view of the Mystery Gift cards physically stored in a save.
/// Receiving, deleting, and received-flag mutation are intentionally not exposed.</summary>
public static class MysteryGiftInboxEditor
{
    public static async Task ShowAsync(Grid host, ISaveEngineSession session)
    {
        var inbox = session.GetMysteryGiftInbox();
        if (!inbox.Supported)
        {
            await EditorMenu.ShowAsync(host, "Входящие подарки",
                "This game does not expose a supported Mystery Gift card store.", "OK");
            return;
        }
        if (inbox.Cards.Count == 0)
        {
            await EditorMenu.ShowAsync(host, "Входящие подарки",
                "В сохранении нет карточек подарков или записей их получения.", "OK");
            return;
        }

        var data = IPlatformApplication.Current!.Services.GetRequiredService<IGameDataService>();
        string SpeciesName(int species) => (uint)species < (uint)data.SpeciesNames.Count && data.SpeciesNames[species].Length != 0
            ? data.SpeciesNames[species]
            : $"#{species}";

        while (true)
        {
            inbox = session.GetMysteryGiftInbox();
            var options = inbox.Cards.Select(card => new PickItem(card.Slot,
                $"{card.Title} · {CardNumber(card)}")).ToArray();
            var picked = await PickerMenu.ShowAsync(host, $"Входящие подарки · {inbox.Cards.Count}", options);
            if (picked is null)
                return;
            var card = inbox.Cards.FirstOrDefault(candidate => candidate.Slot == picked.Id);
            if (card is null)
                continue;

            var kind = card.IsReceivedRecord ? "Запись полученного подарка" : "Сохранённая карточка подарка";
            var contents = card.IsEntity && card.Species > 0
                ? $"Pokémon: {SpeciesName(card.Species)} · Lv. {card.Level}"
                : "Подарок без покемона или запись";
            var state = card.IsReceivedRecord ? "Запись истории" : card.GiftUsed ? "Подарок получен" : "Подарок не получен";
            await EditorMenu.ShowAsync(host, card.Title,
                $"{kind}\n{CardNumber(card)} · {card.Type}\n{contents}\n{state}\n\nRead-only: PKForge does not alter cards or received flags.", "Назад");
        }
    }

    private static string CardNumber(MysteryGiftCard card) => card.CardId > 0
        ? $"Карточка \u2116{card.CardId:0000}"
        : $"Ячейка {card.Slot + 1}";
}
