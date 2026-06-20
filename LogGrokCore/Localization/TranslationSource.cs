using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace LogGrokCore.Localization
{
    /// <summary>
    /// Runtime-switchable source of localized strings. XAML binds to the indexer
    /// (<c>[Key]</c>); changing <see cref="CurrentCulture"/> raises a change for the
    /// indexer so every bound element re-reads its string without a restart.
    /// </summary>
    public sealed class TranslationSource : INotifyPropertyChanged
    {
        public static TranslationSource Instance { get; } = new();

        private readonly ResourceManager _resourceManager =
            new("LogGrokCore.Localization.Strings", typeof(TranslationSource).Assembly);

        private CultureInfo _currentCulture = CultureInfo.InvariantCulture;

        private TranslationSource()
        {
        }

        /// <summary>The languages offered in the UI. The first entry is the base language.</summary>
        public static IReadOnlyList<LanguageInfo> AvailableLanguages { get; } = new[]
        {
            new LanguageInfo("en", "English"),
            new LanguageInfo("ru", "Русский"),
            new LanguageInfo("de", "Deutsch"),
            new LanguageInfo("es", "Español"),
            new LanguageInfo("fr", "Français"),
            new LanguageInfo("pt-BR", "Português (Brasil)"),
            new LanguageInfo("ja", "日本語"),
            new LanguageInfo("pl", "Polski"),
        };

        public string this[string key] =>
            _resourceManager.GetString(key, _currentCulture) ?? key;

        /// <summary>
        /// Returns <paramref name="key"/> translated into every available language.
        /// Used for startup errors shown before a language can be chosen (e.g. the
        /// settings file that holds the language preference failed to load), so the
        /// message is readable regardless of the user's language.
        /// </summary>
        public IEnumerable<(LanguageInfo Language, string Text)> GetAllTranslations(string key)
        {
            foreach (var language in AvailableLanguages)
            {
                var culture = language.Code == "en"
                    ? CultureInfo.InvariantCulture
                    : CultureInfo.GetCultureInfo(language.Code);
                yield return (language, _resourceManager.GetString(key, culture) ?? key);
            }
        }

        public CultureInfo CurrentCulture
        {
            get => _currentCulture;
            set
            {
                if (Equals(_currentCulture, value))
                    return;

                _currentCulture = value;

                // Keep the rest of the framework (validation messages, formatting, etc.) in sync.
                CultureInfo.CurrentUICulture = value;
                CultureInfo.DefaultThreadCurrentUICulture = value;

                // Empty/indexer name signals "all bindings on this source are stale".
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            }
        }

        /// <summary>
        /// Applies a culture by code (e.g. "ru", "pt-BR"). Unknown or empty codes fall back
        /// to the base language. Returns the code that was actually applied.
        /// </summary>
        public string SetCulture(string? languageCode)
        {
            var match = ResolveLanguage(languageCode);
            CurrentCulture = match.Code == "en"
                ? CultureInfo.InvariantCulture
                : CultureInfo.GetCultureInfo(match.Code);
            return match.Code;
        }

        private static LanguageInfo ResolveLanguage(string? languageCode)
        {
            if (!string.IsNullOrWhiteSpace(languageCode))
            {
                foreach (var language in AvailableLanguages)
                {
                    if (string.Equals(language.Code, languageCode, StringComparison.OrdinalIgnoreCase))
                        return language;
                }
            }

            return AvailableLanguages[0];
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public sealed record LanguageInfo(string Code, string DisplayName);
}
