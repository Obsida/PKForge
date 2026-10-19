namespace PKForge.Chrome;

/// <summary>
/// The semantic icon vocabulary: every menu icon name, the bundled asset it resolves to
/// under ui/pksm/, and the single meaning it stands for. One meaning per icon — menus pick
/// icons by name from here, never by look. px_* assets are PKForge's original pixel icons
/// (generated from tools/ChromePreview/PixelIcons.cs); the rest are the PKSM / game-icons
/// assets credited in Resources/UI/ATTRIBUTION.md.
/// </summary>
public static class PksmIconCatalog
{
    public sealed record Entry(string Name, string File, string Meaning);

    public static readonly IReadOnlyList<Entry> Entries =
    [
        // Places and hubs
        new("storage", "icon_storage.png", "Save storage (the box system)"),
        new("box", "px_box.png", "A single box"),
        new("bank", "pkf_bank.png", "PKForge Bank / PKSM bank"),
        new("party", "icon_party.png", "Команда"),
        new("bag", "icon_bag.png", "Bag and pouches"),
        new("item", "px_item.png", "Предмет"),
        new("pokedex", "px_pokedex.png", "Pokédex / living dex"),
        new("trainer", "gi_trainer.png", "Trainer card and records of the save's trainer"),
        new("events", "icon_events.png", "Wonder cards / Mystery Gift events"),
        new("settings", "icon_settings.png", "Настройки"),
        new("palette", "px_palette.png", "Цветовая тема"),
        new("folder", "icon_folder.png", "Pick a folder / files on device"),
        new("park", "px_park.png", "Покепарк"),
        // Pokémon actions
        new("editor", "icon_script.png", "Edit / view a Pokémon"),
        new("evolve", "px_evolve.png", "Эволюционировать"),
        new("move", "px_move.png", "Move / carry within storage"),
        new("copy", "px_copy.png", "Копировать / дублировать"),
        new("send", "px_send.png", "Send to another game"),
        new("export", "px_export.png", "Export / save out"),
        new("import", "px_import.png", "Импортировать файлы"),
        new("qr", "px_qr.png", "Показать QR-код"),
        new("scan", "px_scan.png", "Сканировать QR-код"),
        new("release", "gi_release.png", "Release a Pokémon"),
        new("padlock", "gi_padlock-white.png", "Включить / снять защиту"),
        new("script", "px_showdown.png", "Showdown set text"),
        new("create", "px_create.png", "Create / generate new"),
        new("egg", "px_egg.png", "Яйца"),
        new("daycare", "px_daycare.png", "Пансион / Питомник"),
        new("tree", "px_tree.png", "Медовые деревья"),
        new("heal", "px_heal.png", "Heal / healing supplies"),
        new("train", "px_train.png", "Training (Hyper Training, AVs, GVs)"),
        // Pokémon data fields
        new("stats", "px_stats.png", "IVs / EVs / stat spreads"),
        new("level", "px_level.png", "Уровень"),
        new("moves", "px_moves.png", "Moves / PP"),
        new("ability", "px_ability.png", "Способность"),
        new("nature", "px_nature.png", "Характер"),
        new("ball", "px_ball.png", "Poké Ball"),
        new("type", "px_type.png", "Type / Tera type"),
        new("rarity", "px_rarity.png", "Legendary / mythical rarity"),
        new("gender", "px_gender.png", "Gender (any)"),
        new("male", "gi_male.png", "Самец"),
        new("female", "gi_female.png", "Самка"),
        new("genderless", "gi_genderless.png", "Бесполый"),
        new("shiny", "icon_shiny.png", "Шайни"),
        new("heart", "gi_heart-white.png", "Friendship / affection / compatibility"),
        new("ribbons", "ribbon_award.png", "Ribbons and marks"),
        new("profile", "px_profile.png", "Trainer profile (OT presets)"),
        new("rename", "px_rename.png", "Rename / nickname"),
        new("fashion", "px_fashion.png", "Trainer fashion"),
        new("records", "px_records.png", "Рекорды тренера"),
        // Legality and tools
        new("check", "px_check.png", "Проверка легальности"),
        new("fix", "px_fix.png", "Legalize / fix"),
        new("info", "px_info.png", "Explain / about"),
        new("report", "px_report.png", "Reports (audit, scan)"),
        new("skull", "gi_skull-white.png", "Nuzlocke"),
        new("battle", "px_battle.png", "Подготовка к бою / правила"),
        new("dice", "gi_dice-white.png", "Random / RNG"),
        new("batch", "px_batch.png", "Массовое изменение"),
        new("code", "px_code.png", "Expert batch instructions"),
        new("preset", "px_preset.png", "Наборы"),
        new("gears", "gi_gears-white.png", "Misc tools"),
        new("hex", "icon_hex.png", "Raw bytes / hex editor"),
        new("blocks", "px_blocks.png", "Блоки сохранения"),
        new("inbox", "px_inbox.png", "In-save gift inbox"),
        new("key", "px_key.png", "Key items / key item events"),
        new("underground", "px_underground.png", "Великое подземелье"),
        new("hax", "px_hax.png", "HaX mode"),
        new("hardcore", "px_hardcore.png", "Режим Hardcore"),
        // Selection, ordering, filtering
        new("select", "px_select.png", "Mark / multi-select"),
        new("selectall", "px_selectall.png", "Отметить всё"),
        new("deselect", "px_deselect.png", "Снять отметки / выделение"),
        new("range", "px_range.png", "Отметить диапазон"),
        new("invert", "px_invert.png", "Инвертировать отметки"),
        new("compact", "px_compact.png", "Compact / merge into gaps"),
        new("sort", "px_sort.png", "Sort (natural order)"),
        new("reverse", "px_reverse.png", "Reverse order"),
        new("alpha", "px_alpha.png", "По алфавиту"),
        new("calendar", "px_calendar.png", "Date / year / release order"),
        new("generation", "px_generation.png", "Generation / era"),
        new("filter", "px_filter.png", "Фильтр"),
        new("search", "icon_search.png", "Find / jump to"),
        new("map", "px_map.png", "Как получить (места)"),
        new("all", "px_all.png", "Everything / all games"),
        // Saves, files, devices
        new("game", "px_game.png", "Игра / игра-источник"),
        new("patch", "px_patch.png", "ROM hack"),
        new("file", "px_file.png", "Открыть сохранение"),
        new("link", "px_link.png", "Подключить эмулятор"),
        new("unlink", "px_unlink.png", "Unlink storage"),
        new("sdcard", "px_sdcard.png", "Linked storage units"),
        new("download", "px_download.png", "Download"),
        new("update", "px_update.png", "Проверить обновления"),
        new("refresh", "px_refresh.png", "Rescan / refresh from current"),
        new("history", "px_history.png", "Restore points / backups"),
        new("community", "px_community.png", "Боксы сообщества"),
        new("show", "px_show.png", "Показать скрытые"),
        new("hide", "px_hide.png", "Скрыть"),
        // Generic verbs
        new("confirm", "px_confirm.png", "Done / apply"),
        new("close", "px_close.png", "Close / cancel / not now"),
        new("back", "px_back.png", "Back / previous"),
        new("next", "px_next.png", "Далее"),
        new("restore", "gi_restore.png", "Reset / undo"),
        new("clear", "px_clear.png", "Clear values / filters"),
        new("fill", "px_fill.png", "Fill / max out"),
        new("delete", "px_delete.png", "Удалить"),
        new("warning", "px_warning.png", "Предупреждение"),
        new("quit", "gi_quit.png", "Quit the app"),
        // Media
        new("music", "gi_music.png", "Музыка"),
        new("play", "gi_play.png", "Play / open"),
        new("pause", "gi_pause.png", "Пауза"),
        new("skip", "gi_skip.png", "Skip track"),
        new("shuffle", "gi_shuffle.png", "Play order"),
        new("power", "gi_power.png", "Сила"),
        new("credits", "icon_credits.png", "Авторы"),
        new("scripts", "icon_scripts.png", "Scripts"),
        // Emulators and platforms (illustrations, not logos)
        new("retroarch", "emu_retroarch.png", "RetroArch"),
        new("melonds", "emu_melonds.png", "melonDS"),
        new("azahar", "emu_azahar.png", "Azahar / Nintendo 3DS"),
        new("eden", "emu_eden.png", "Eden / Nintendo Switch"),
        new("linkboy", "emu_linkboy.png", "Linkboy"),
        new("dolphin", "gi_dolphin.png", "Dolphin"),
        new("pizzaboy", "gi_pizza.png", "Pizza Boy"),
        new("drastic", "gi_gamepad.png", "DraStic"),
        new("platform-gb", "emu_linkboy.png", "Game Boy platform"),
        new("platform-gba", "gi_gamepad.png", "Game Boy Advance platform"),
        new("platform-ds", "emu_melonds.png", "Nintendo DS platform"),
        new("platform-gc", "gi_cube.png", "GameCube platform"),
    ];

    private static readonly Dictionary<string, string> Files =
        Entries.ToDictionary(e => e.Name, e => e.File, StringComparer.Ordinal);

    /// <summary>True when the name is part of the vocabulary.</summary>
    public static bool Contains(string name) => Files.ContainsKey(name);

    /// <summary>The bundled asset for a semantic name; unknown names get the warning icon, never a misleading one.</summary>
    public static string Asset(string name) => Files.TryGetValue(name, out var file) ? file : "px_warning.png";
}
