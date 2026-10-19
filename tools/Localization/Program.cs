using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

Console.OutputEncoding = Encoding.UTF8;
var root = Path.GetFullPath(args[0]);
var files = new[] { "src/PKForge.App/Views", "src/PKForge.App/ViewModels", "src/PKForge.Chrome" }
    .SelectMany(dir => Directory.EnumerateFiles(Path.Combine(root, dir), "*.cs"))
    .Append(Path.Combine(root, "src/PKForge.App/App.cs"));
var catalog = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
var translations = args.Contains("--apply")
    ? File.ReadAllLines(Path.Combine(root, "tools/Localization/ru.tsv"))
        .Where(line => line.Length > 0 && !line.StartsWith('#'))
        .Select(line => line.Split('\t', 2))
        .ToDictionary(parts => parts[0].Replace("\\n", "\n").Replace("\\u0020", " "),
            parts => parts[1].Replace("\\n", "\n").Replace("\\u0020", " "), StringComparer.Ordinal) : [];
var count = 0;
foreach (var file in files)
{
    var text = File.ReadAllText(file);
    var syntax = CSharpSyntaxTree.ParseText(text).GetRoot();
    foreach (var node in syntax.DescendantNodes())
    {
        var value = node switch
        {
            LiteralExpressionSyntax l when l.IsKind(SyntaxKind.StringLiteralExpression) => l.Token.ValueText,
            InterpolatedStringExpressionSyntax s => Translate.Template(s),
            _ => null,
        };
        if (value is null || !value.Any(char.IsAsciiLetter)) continue;
        if (!catalog.TryGetValue(value, out var used)) catalog[value] = used = [];
        used.Add(Path.GetFileName(file));
    }
    if (translations.Count == 0) continue;
    var updated = new Translate(translations).Visit(syntax)!.ToFullString();
    if (text != updated) { File.WriteAllText(file, updated, new UTF8Encoding(false)); count++; }
}
File.WriteAllText(Path.Combine(root, ".tools/ui-catalog.json"), JsonSerializer.Serialize(catalog,
    new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
Console.WriteLine($"{catalog.Count} strings; {count} files updated.");

sealed class Translate(Dictionary<string, string> translations) : CSharpSyntaxRewriter
{
    public static string Template(InterpolatedStringExpressionSyntax node)
    {
        var hole = 0;
        return string.Concat(node.Contents.Select(part => part is InterpolatedStringTextSyntax text
            ? text.TextToken.ValueText : "{" + hole++ + "}"));
    }
    public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node) =>
        node.IsKind(SyntaxKind.StringLiteralExpression) && !IsBindingPath(node) && translations.TryGetValue(node.Token.ValueText, out var value)
            ? SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(value)).WithTriviaFrom(node)
            : base.VisitLiteralExpression(node);
    private static bool IsBindingPath(LiteralExpressionSyntax node) =>
        node.Parent is ArgumentSyntax argument && argument.Parent is ArgumentListSyntax arguments &&
        ((arguments.Parent is ObjectCreationExpressionSyntax creation && creation.Type.ToString().EndsWith("Binding", StringComparison.Ordinal)
            && (argument.NameColon?.Name.Identifier.ValueText == "path" || arguments.Arguments.IndexOf(argument) == 0))
         || (arguments.Parent is InvocationExpressionSyntax call && call.Expression.ToString().EndsWith("SetBinding", StringComparison.Ordinal)
             && arguments.Arguments.IndexOf(argument) == 1));
    public override SyntaxNode? VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
    {
        var key = Template(node);
        var visited = (InterpolatedStringExpressionSyntax)base.VisitInterpolatedStringExpression(node)!;
        if (!translations.TryGetValue(key, out var value)) return visited;
        var holes = visited.Contents.OfType<InterpolationSyntax>().ToArray();
        var parts = System.Text.RegularExpressions.Regex.Split(value, @"(\{\d+\})");
        var content = string.Concat(parts.Select(part =>
            System.Text.RegularExpressions.Regex.IsMatch(part, @"^\{\d+\}$")
                ? holes[int.Parse(part[1..^1])].ToFullString()
                : JsonSerializer.Serialize(part, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })[1..^1].Replace("{", "{{").Replace("}", "}}")));
        return SyntaxFactory.ParseExpression("$\"" + content + "\"").WithTriviaFrom(node);
    }
}
