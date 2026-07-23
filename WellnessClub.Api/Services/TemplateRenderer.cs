using System.Collections.Concurrent;
using System.Text;

namespace WellnessClub.Api.Services;

// Page HTML lives as plain files under Templates/ instead of embedded in C# strings; this does
// simple {{Token}} substitution so route handlers only ever build the dynamic fragments (loops,
// tables) in code and slot them into an otherwise static file.
public static class TemplateRenderer
{
    private static readonly ConcurrentDictionary<string, string> Cache = new();

    public static string RenderPage(string title, string bodyTemplateName, params (string Key, string Value)[] bodyTokens)
    {
        var body = Render(bodyTemplateName, bodyTokens);
        return Render("_Layout.html", ("Title", title), ("Body", body));
    }

    public static string Render(string templateName, params (string Key, string Value)[] tokens)
    {
        var template = Cache.GetOrAdd(templateName, LoadTemplate);
        var sb = new StringBuilder(template);

        foreach (var (key, value) in tokens)
            sb.Replace("{{" + key + "}}", value);

        return sb.ToString();
    }

    // Renders `templateName` once per item and joins the results — the loop itself stays in code,
    // but the markup for one item never does.
    public static string RenderEach<T>(string templateName, IEnumerable<T> items, Func<T, (string Key, string Value)[]> tokens) =>
        string.Join("", items.Select(item => Render(templateName, tokens(item))));

    private static string LoadTemplate(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Templates", name));
}
