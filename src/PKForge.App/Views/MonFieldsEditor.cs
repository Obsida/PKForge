using PKForge.App.Theme;
using PKForge.Domain;
using PKForge.Engine;

namespace PKForge.App.Views;

/// <summary>
/// Sub-editors for the per-format Pokémon fields: form, form argument, mint, shiny type and
/// raw PID / EC ("Form &amp; shiny"); OT gender, the handling trainer and memories ("Trainers");
/// and Technical Records. Each menu lists only what the format stores. Like the other
/// sub-editors they write to the session as they go, so callers gate them behind the
/// Hardcore edit door (box editor) or the entry's own Save (bank).
/// </summary>
public static class MonFieldsEditor
{
    private static IGameDataService Data => IPlatformApplication.Current!.Services.GetRequiredService<IGameDataService>();

    private static string NameOf(IReadOnlyList<string> names, int id) =>
        (uint)id < (uint)names.Count && names[id].Length > 0 ? names[id] : $"#{id}";

    private static string Hex(uint value) => value.ToString("X8");

    private static string Gender(int gender) => gender == 1 ? "Самка" : "Самец";

    private static async Task<bool> UnsupportedAsync(Grid host, string title, ISaveEngineSession session)
    {
        if (MonFieldService.IsSupported(session)) return false;
        await EditorMenu.ShowAsync(host, title, "These fields are only editable on official-format saves.", "OK");
        return true;
    }

    // ── Form & shiny ──

    public static async Task<bool> FormAndShinyAsync(Grid host, ISaveEngineSession session, int box, int slot)
    {
        const string title = "Форма и шайни";
        if (await UnsupportedAsync(host, title, session)) return false;
        var data = Data;
        var dirty = false;
        while (true)
        {
            var form = MonFieldService.GetForm(session, box, slot);
            var argument = MonFieldService.GetFormArgument(session, box, slot);
            var mint = MonFieldService.GetStatNature(session, box, slot);
            var shiny = session.Generation >= 3 ? MonFieldService.GetShiny(session, box, slot) : null;

            var options = new List<PadOption>();
            var keys = new Dictionary<string, string>();
            void Add(string key, PadOption option) { options.Add(option); keys[option.Label] = key; }
            if (form is not null)
                Add("form", new PadOption($"Форма · {form.Choices[Math.Min(form.Form, form.Choices.Count - 1)].Name}", IconPath: "evolve"));
            if (argument is not null)
                Add("arg", new PadOption($"{argument.Label} · {ArgumentValue(argument)}", IconPath: "evolve"));
            if (mint is { } statNature)
                Add("mint", new PadOption($"Характер характеристик · {NameOf(data.NatureNames, statNature)}", IconPath: "stats",
                    Detail: "Определяет характеристики, не меняя исходный характер."));
            if (shiny is not null)
            {
                Add("shiny", new PadOption($"Shiny · {ShinyLabel(shiny)}", IconPath: "shiny"));
                Add("pid", new PadOption($"PID · {Hex(shiny.Pid)}", IconPath: "hex",
                    Detail: "Значение личности, влияющее на статус шайни."));
                if (shiny.EncryptionConstant is { } ec)
                    Add("ec", new PadOption($"Константа шифрования · {Hex(ec)}", IconPath: "hex",
                        Detail: "Внутреннее число, используемое для шифрования данных."));
            }
            if (options.Count == 0)
            {
                await EditorMenu.ShowAsync(host, title, "Формат покемона не содержит этих полей.", "OK");
                return dirty;
            }

            var choice = await EditorMenu.ShowAsync(host, title, null, options.ToArray());
            if (choice is null || !keys.TryGetValue(choice, out var key)) return dirty;
            dirty |= key switch
            {
                "form" => await PickFormAsync(host, session, box, slot, form!),
                "arg" => await EditArgumentAsync(host, session, box, slot, argument!),
                "mint" => await PickMintAsync(host, session, box, slot, data),
                "shiny" => await PickShinyAsync(host, session, box, slot, shiny!),
                "pid" => await EditPidAsync(host, session, box, slot, shiny!),
                "ec" => await EditEcAsync(host, session, box, slot, shiny!),
                _ => false,
            };
        }
    }

    private static string ArgumentValue(FormArgumentField a) => a.Kind switch
    {
        FormArgumentKind.Named => a.Value < a.Names.Count ? a.Names[(int)a.Value] : a.Value.ToString(),
        FormArgumentKind.Triple or FormArgumentKind.TripleParty => $"{a.Remain} of {a.Max}",
        _ => a.Value.ToString(),
    };

    private static string ShinyLabel(ShinyField s) => s.Kind switch
    {
        ShinyKind.Square => "Квадратный",
        ShinyKind.Star when s.SupportsKind => "Звёздный",
        ShinyKind.Star => "Шайни",
        _ => "Не шайни",
    };

    private static async Task<bool> PickFormAsync(Grid host, ISaveEngineSession session, int box, int slot, FormFieldInfo form)
    {
        var items = form.Choices.Select(c => new PickItem(c.Form, c.Name, null,
                c.BattleOnly ? "Существует только во время боя" : !c.InGame ? "Нет в этой игре" : null)
            { Muted = c.BattleOnly || !c.InGame }).ToList();
        var pick = await PickerMenu.ShowAsync(host, "Форма", items, form.Form,
            filter: new PickerFilter("В этой игре", "Показать всё", item => !item.Muted, StartOn: !Services.HaXMode.IsOn));
        if (pick is null || pick.Id == form.Form) return false;
        var result = MonFieldService.SetForm(session, box, slot, pick.Id);
        var message = string.Join(" ", result.Notes);
        if (result.Warning is not null) message = message.Length == 0 ? result.Warning : $"{message}\n{result.Warning}";
        if (message.Length > 0) await EditorMenu.ShowAsync(host, pick.Name, message, "OK");
        return result.Changed;
    }

    private static async Task<bool> EditArgumentAsync(Grid host, ISaveEngineSession session, int box, int slot, FormArgumentField argument)
    {
        if (argument.Kind == FormArgumentKind.TripleParty && box != -1)
        {
            await EditorMenu.ShowAsync(host, argument.Label, MonFieldService.PartyOnlyTimer, "OK");
            return false;
        }
        uint? value;
        if (argument.Kind == FormArgumentKind.Named)
        {
            var pick = await PickerMenu.ShowAsync(host, argument.Label,
                argument.Names.Select((name, i) => new PickItem(i, name)).ToList(), (int)argument.Value);
            value = pick is null ? null : (uint)pick.Id;
        }
        else
        {
            var triple = argument.Kind is FormArgumentKind.Triple or FormArgumentKind.TripleParty;
            var current = triple ? argument.Remain : (int)Math.Min(argument.Value, int.MaxValue);
            var typed = await StatsPopup.ShowSingleAsync(host, argument.Label, current, (int)argument.Max, $"0 - {argument.Max}");
            value = typed is { } t ? (uint)t : null;
        }
        if (value is null) return false;
        MonFieldService.SetFormArgument(session, box, slot, value.Value);
        return true;
    }

    private static async Task<bool> PickMintAsync(Grid host, ISaveEngineSession session, int box, int slot, IGameDataService data)
    {
        var current = MonFieldService.GetStatNature(session, box, slot);
        var preview = MonFieldService.PreviewStatNature(session, box, slot);
        var pick = await NaturePicker.ShowAsync(host, data.NatureNames, current, preview, "Характер характеристик (мята)");
        if (pick is null || pick.Id == current) return false;
        MonFieldService.SetStatNature(session, box, slot, pick.Id);
        return true;
    }

    private static async Task<bool> PickShinyAsync(Grid host, ISaveEngineSession session, int box, int slot, ShinyField shiny)
    {
        PadOption[] options = shiny.SupportsKind
            ? [new("Звёздный", IconPath: "shiny"), new("Квадратный", IconPath: "shiny"), new("Не шайни", IconPath: "clear")]
            : [new("Шайни", IconPath: "shiny"), new("Не шайни", IconPath: "clear")];
        var message = shiny.PidLinked ? "The PID is re-rolled: in Gen 3-5 that can also change the nature, gender or ability." : null;
        var choice = await EditorMenu.ShowAsync(host, "Шайни", message, options);
        var kind = choice switch
        {
            "Звёздный" or "Шайни" => ShinyKind.Star,
            "Квадратный" => ShinyKind.Square,
            "Не шайни" => ShinyKind.None,
            _ => (ShinyKind?)null,
        };
        return kind is { } k && MonFieldService.SetShinyKind(session, box, slot, k);
    }

    private static async Task<bool> EditPidAsync(Grid host, ISaveEngineSession session, int box, int slot, ShinyField shiny)
    {
        if (shiny.PidLinked && !await EditorMenu.ConfirmAsync(host, "PID",
                "In Gen 3-5 the PID decides shininess, gender and the ability slot (and the nature in Gen 3/4). Changing it changes them too.",
                "Изменить PID"))
            return false;
        var value = await AskHexAsync(host, "PID", shiny.Pid);
        if (value is null) return false;
        var result = MonFieldService.SetPid(session, box, slot, value.Value);
        if (result.Changes.Count > 0) await EditorMenu.ShowAsync(host, "PID", string.Join("\n", result.Changes), "OK");
        return result.Changed;
    }

    private static async Task<bool> EditEcAsync(Grid host, ISaveEngineSession session, int box, int slot, ShinyField shiny)
    {
        var value = await AskHexAsync(host, "Константа шифрования", shiny.EncryptionConstant ?? 0);
        if (value is null) return false;
        var result = MonFieldService.SetEncryptionConstant(session, box, slot, value.Value);
        if (result.Changes.Count > 0) await EditorMenu.ShowAsync(host, "Константа шифрования", string.Join("\n", result.Changes), "OK");
        return result.Changed;
    }

    private static async Task<uint?> AskHexAsync(Grid host, string title, uint current)
    {
        while (true)
        {
            var text = await TextPopup.ShowLineAsync(host, title, "8 hex digits", Hex(current));
            if (text is null) return null;
            if (MonFieldService.TryParseHex(text, out var value)) return value;
            await EditorMenu.ShowAsync(host, title, "Введи до 8 шестнадцатеричных цифр (0–9, A–F).", "OK");
        }
    }

    // ── Trainers & memories ──

    public static async Task<bool> TrainersAsync(Grid host, ISaveEngineSession session, int box, int slot)
    {
        const string title = "Тренеры";
        if (await UnsupportedAsync(host, title, session)) return false;
        var dirty = false;
        while (true)
        {
            var t = MonFieldService.GetTrainers(session, box, slot);
            var otMemory = MonFieldService.GetMemory(session, box, slot, handler: false);
            var htMemory = MonFieldService.GetMemory(session, box, slot, handler: true);
            var named = t.HandlerName.Length > 0;
            var languages = t.HandlerLanguage is null ? [] : MonFieldService.GetHandlerLanguageChoices(session, box, slot);

            var options = new List<PadOption>();
            var keys = new Dictionary<string, string>();
            void Add(string key, PadOption option) { options.Add(option); keys[option.Label] = key; }
            if (t.OtGender is { } otGender) Add("otg", new PadOption($"Пол исходного тренера · {Gender(otGender)}", IconPath: "profile", Detail: "Пол исходного тренера."));
            if (t.HasHandler)
            {
                Add("otf", new PadOption($"Дружба с исходным тренером · {t.OtFriendship}", IconPath: "heart", Detail: "Дружба с исходным тренером."));
                Add("htn", new PadOption($"Владелец · {(named ? t.HandlerName : "none")}", IconPath: "profile",
                    Detail: "The last trainer it was traded to; empty means never traded."));
                if (named)
                {
                    Add("htg", new PadOption($"Пол текущего тренера · {Gender(t.HandlerGender)}", IconPath: "gender"));
                    if (t.HandlerLanguage is { } language)
                        Add("htl", new PadOption($"Язык текущего тренера · {languages.FirstOrDefault(l => l.Id == language)?.Name ?? "Нет"}", IconPath: "rename"));
                    Add("htf", new PadOption($"Дружба с текущим тренером · {t.HandlerFriendship}", IconPath: "heart", Detail: "Дружба с тренером после обмена."));
                    Add("cur", new PadOption($"Now with · {(t.CurrentHandler == 1 ? t.HandlerName : "its OT")}", IconPath: "link",
                        Detail: "Who has it now; friendship and memories follow that trainer."));
                }
            }
            if (otMemory is not null) Add("otm", new PadOption("Память об исходном тренере", IconPath: "info", Detail: otMemory.Editable ? otMemory.Text : otMemory.Reason));
            if (htMemory is not null) Add("htm", new PadOption("Память о текущем тренере", IconPath: "info", Detail: htMemory.Editable ? htMemory.Text : htMemory.Reason));
            if (options.Count == 0)
            {
                await EditorMenu.ShowAsync(host, title, "This Pokémon's format stores no trainer details beyond the OT.", "OK");
                return dirty;
            }

            var choice = await EditorMenu.ShowAsync(host, title, null, options.ToArray());
            if (choice is null || !keys.TryGetValue(choice, out var key)) return dirty;
            TrainerFieldsEdit? edit = null;
            switch (key)
            {
                case "otg": edit = await AskGenderAsync(host, "Пол исходного тренера") is { } g1 ? new TrainerFieldsEdit(OtGender: g1) : null; break;
                case "htg": edit = await AskGenderAsync(host, "Пол текущего тренера") is { } g2 ? new TrainerFieldsEdit(HandlerGender: g2) : null; break;
                case "otf": edit = await StatsPopup.ShowSingleAsync(host, "Дружба с исходным тренером", t.OtFriendship, 255, "0 - 255") is { } f1 ? new TrainerFieldsEdit(OtFriendship: f1) : null; break;
                case "htf": edit = await StatsPopup.ShowSingleAsync(host, "Дружба с текущим тренером", t.HandlerFriendship, 255, "0 - 255") is { } f2 ? new TrainerFieldsEdit(HandlerFriendship: f2) : null; break;
                case "htn":
                    var name = await TextPopup.ShowLineAsync(host, "Текущий тренер", $"Up to {t.MaxNameLength} characters · empty = never traded", t.HandlerName);
                    if (name is not null && name.Length > t.MaxNameLength)
                        await EditorMenu.ShowAsync(host, "Текущий тренер", $"В этом формате имя может содержать до {t.MaxNameLength} символов.", "OK");
                    else if (name is not null && name != t.HandlerName) edit = new TrainerFieldsEdit(HandlerName: name);
                    break;
                case "htl":
                    var lang = await PickerMenu.ShowAsync(host, "Язык текущего тренера", languages.Select(l => new PickItem(l.Id, l.Name)).ToList(), t.HandlerLanguage);
                    edit = lang is null ? null : new TrainerFieldsEdit(HandlerLanguage: lang.Id);
                    break;
                case "cur":
                    var holder = await EditorMenu.ShowAsync(host, "Now with", "Who the Pokémon is currently with: its friendship and memories follow that trainer.",
                        new PadOption("Исходный тренер", IconPath: "trainer"), new PadOption(t.HandlerName, IconPath: "link"));
                    edit = holder is null ? null : new TrainerFieldsEdit(CurrentHandler: holder == t.HandlerName ? 1 : 0);
                    break;
                case "otm": dirty |= await EditMemoryAsync(host, session, box, slot, otMemory!); break;
                case "htm": dirty |= await EditMemoryAsync(host, session, box, slot, htMemory!); break;
            }
            if (edit is null) continue;
            MonFieldService.ApplyTrainerEdit(session, box, slot, edit);
            dirty = true;
        }
    }

    private static async Task<int?> AskGenderAsync(Grid host, string title)
    {
        var choice = await EditorMenu.ShowAsync(host, title, null,
            new PadOption("Самец", IconPath: "male"), new PadOption("Самка", IconPath: "female"));
        return choice switch { "Самец" => 0, "Самка" => 1, _ => null };
    }

    /// <summary>Memory → its argument (when it takes one) → intensity → feeling, like PKHeX's memory editor.</summary>
    private static async Task<bool> EditMemoryAsync(Grid host, ISaveEngineSession session, int box, int slot, MemoryField field)
    {
        var who = field.Handler ? "Память о текущем тренере" : "Память об исходном тренере";
        if (!field.Editable)
        {
            await EditorMenu.ShowAsync(host, who, field.Reason, "OK");
            return false;
        }

        var start = MonFieldService.GetMemoryOptions(session, box, slot, field.Handler, field.Memory);
        var memory = await PickerMenu.ShowAsync(host, who,
            start.Memories.Select(m => new PickItem(m.Id, m.Name) { Muted = !m.Legal }).ToList(), field.Memory,
            filter: new PickerFilter("Легален", "Показать всё", item => item.Id == 0 || !item.Muted, StartOn: !Services.HaXMode.IsOn));
        if (memory is null) return false;
        if (memory.Id == 0)
        {
            MonFieldService.SetMemory(session, box, slot, new MemoryEdit(field.Handler, 0, 0, 0, 0));
            return true;
        }

        var options = MonFieldService.GetMemoryOptions(session, box, slot, field.Handler, memory.Id);
        var variable = 0;
        if (options.ArgumentCategory.Length > 0 && options.Arguments.Count > 1)
        {
            var argument = await PickerMenu.ShowAsync(host, options.ArgumentCategory,
                options.Arguments.Select(a => new PickItem(a.Id, a.Name)).ToList(), memory.Id == field.Memory ? field.Variable : null);
            if (argument is null) return false;
            variable = argument.Id;
        }
        var intensity = await PickerMenu.ShowAsync(host, "Intensity",
            options.Intensities.Where(i => i.Id > 0).Select(i => new PickItem(i.Id, i.Name) { Muted = !i.Legal }).ToList(),
            memory.Id == field.Memory ? field.Intensity : null,
            filter: new PickerFilter("Легален", "Показать всё", item => !item.Muted, StartOn: !Services.HaXMode.IsOn));
        if (intensity is null) return false;
        var feeling = await PickerMenu.ShowAsync(host, "Чувство",
            options.Feelings.Select(f => new PickItem(f.Id, f.Name) { Muted = !f.Legal }).ToList(),
            memory.Id == field.Memory ? field.Feeling : null,
            filter: new PickerFilter("Легален", "Показать всё", item => !item.Muted, StartOn: !Services.HaXMode.IsOn));
        if (feeling is null) return false;

        MonFieldService.SetMemory(session, box, slot, new MemoryEdit(field.Handler, memory.Id, intensity.Id, feeling.Id, variable));
        var text = MonFieldService.GetMemory(session, box, slot, field.Handler)?.Text;
        if (text is not null) await EditorMenu.ShowAsync(host, who, text, "OK");
        return true;
    }

    // ── Technical Records ──

    private const int GiveAllId = -1, ClearId = -2;

    public static async Task<bool> TechRecordsAsync(Grid host, ISaveEngineSession session, int box, int slot)
    {
        const string title = "Технические записи";
        if (await UnsupportedAsync(host, title, session)) return false;
        var dirty = false;
        int? highlight = null;
        while (true)
        {
            var records = MonFieldService.GetTechRecords(session, box, slot);
            if (records is null)
            {
                await EditorMenu.ShowAsync(host, title, "Technical Records are stored by Sword/Shield, Scarlet/Violet and Legends: Z-A Pokémon.", "OK");
                return dirty;
            }
            var learned = records.Count(r => r.Learned);
            var items = new List<PickItem>
            {
                new(GiveAllId, "Выдать все доступные записи", "confirm", "Все записи, доступные этому виду или его эволюциям"),
                new(ClearId, "Очистить все записи", "delete"),
            };
            items.AddRange(records.Select(r => new PickItem(r.Index, $"{r.Label} {r.Name}", null, r.Permitted ? null : "Недоступно этому виду")
            {
                TypeId = r.Type,
                Tag = r.Learned ? "Изучено" : null,
                TagColor = r.Learned ? UiTokens.Green : null,
                Muted = !r.Permitted,
                Keywords = r.Label,
            }));
            var pick = await PickerMenu.ShowAsync(host, $"{title} · {learned} learned", items, highlight,
                filter: new PickerFilter("Доступно", "Показать всё", item => item.Id < 0 || !item.Muted, StartOn: !Services.HaXMode.IsOn));
            if (pick is null) return dirty;
            highlight = pick.Id;
            switch (pick.Id)
            {
                case GiveAllId: MonFieldService.SetAllLegalTechRecords(session, box, slot); break;
                case ClearId: MonFieldService.ClearTechRecords(session, box, slot); break;
                default:
                    var record = records.First(r => r.Index == pick.Id);
                    MonFieldService.SetTechRecord(session, box, slot, record.Index, !record.Learned);
                    break;
            }
            dirty = true;
        }
    }
}
