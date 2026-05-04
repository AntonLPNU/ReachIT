using System.Windows;

namespace ReachIT.Presentation.Services;

public static class LocalizationService
{
    private const string DictionaryPrefix = "Resources/Localization/StringResources.";

    public static void ApplyLanguage(string? language)
    {
        var normalizedLanguage = string.Equals(language, "uk", StringComparison.OrdinalIgnoreCase)
            ? "uk"
            : "en";

        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        for (var i = dictionaries.Count - 1; i >= 0; i--)
        {
            var source = dictionaries[i].Source?.OriginalString;
            if (!string.IsNullOrWhiteSpace(source) &&
                source.Contains(DictionaryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                dictionaries.RemoveAt(i);
            }
        }

        dictionaries.Insert(0, new ResourceDictionary
        {
            Source = new Uri($"{DictionaryPrefix}{normalizedLanguage}.xaml", UriKind.Relative)
        });
    }

    public static string GetString(string key, string fallback)
    {
        return System.Windows.Application.Current.TryFindResource(key) as string ?? fallback;
    }
}
