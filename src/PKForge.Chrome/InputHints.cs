namespace PKForge.Chrome;

/// <summary>Labels for the same actions on desktop and console frontends.</summary>
public static class InputHints
{
    public static bool UseKeyboard { get; set; }

    public static string Label(string key) => UseKeyboard ? DesktopLabel(key) : key;

    public static string DesktopLabel(string key) => key switch
    {
        "A" => "Enter",
        "B" => "Esc",
        "L" => "PgUp",
        "R" => "PgDn",
        "LR" or "L/R" => "PgUp/PgDn",
        "+" or "START" or "Start" => "F10",
        "-" or "−" or "SELECT" or "Select" => "F2",
        "TAP" => "ЛКМ",
        _ => key,
    };
}
