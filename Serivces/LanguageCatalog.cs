using MenuFFS.Models;

namespace MenuFFS.Services;

public static class LanguageCatalog
{
    public static readonly IReadOnlyList<LanguageOption> TargetLanguages =
    [
        new("ro", "Română"),
        new("en", "Engleză"),
        new("es", "Spaniolă"),
        new("ca", "Catalană"),
        new("it", "Italiană"),
        new("fr", "Franceză"),
        new("de", "Germană"),
        new("pt", "Portugheză"),
        new("nl", "Neerlandeză"),
        new("el", "Greacă"),
        new("tr", "Turcă"),
        new("hu", "Maghiară"),
        new("pl", "Poloneză"),
        new("cs", "Cehă"),
        new("sk", "Slovacă"),
        new("bg", "Bulgară"),
        new("hr", "Croată"),
        new("sr", "Sârbă"),
        new("sq", "Albaneză"),
        new("uk", "Ucraineană"),
        new("ru", "Rusă"),
        new("ja", "Japoneză"),
        new("ko", "Coreeană"),
        new("zh", "Chineză")
    ];

    public static readonly IReadOnlyList<LanguageOption> SourceLanguages =
    [
        new("auto", "Detectare automată"),
        .. TargetLanguages
    ];

    public static string ResolveSource(string code)
    {
        var language = SourceLanguages.FirstOrDefault(item =>
            string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));

        return language?.Name
            ?? throw new MenuValidationException("Limba sursă selectată nu este acceptată.");
    }

    public static string ResolveTarget(string code)
    {
        var language = TargetLanguages.FirstOrDefault(item =>
            string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));

        return language?.Name
            ?? throw new MenuValidationException("Limba de traducere selectată nu este acceptată.");
    }
}
