using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace LogGrokCore.Search
{
    public readonly struct SearchPattern : IEquatable<SearchPattern>
    {
        public SearchPattern(string searchText, in bool isCaseSensitive, in bool useRegex)
        {
            Pattern = searchText;
            IsCaseSensitive = isCaseSensitive;
            UseRegex = useRegex;
            (IsValid, RegexParseError) = GetRegexParseError(useRegex, searchText);
        }

        public static SearchPattern Empty => new(string.Empty, false, true);

        public string Pattern { get; }
        public bool IsCaseSensitive { get; }
        public bool UseRegex { get; }
        public bool IsValid { get; } 
        public string RegexParseError { get; }
        public bool IsEmpty => string.IsNullOrEmpty(Pattern);
        
        public SearchPattern Clone()
        {
            return new(Pattern, IsCaseSensitive, UseRegex);
        }

        // Compiling a Regex (especially with RegexOptions.Compiled) is expensive and happens
        // on the UI thread per committed search; cache by effective (pattern, options).
        private static readonly ConcurrentDictionary<(string pattern, RegexOptions options), Regex> RegexCache = new();

        public Regex GetRegex(RegexOptions regexAdditionalOptions)
        {
            var regexOptions = IsCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            regexOptions |= regexAdditionalOptions;
            var pattern = UseRegex ? Pattern : Regex.Escape(Pattern);
            return RegexCache.GetOrAdd((pattern, regexOptions),
                static key => new Regex(key.pattern, key.options));
        }

        public bool Equals(SearchPattern other)
        {
            return Pattern == other.Pattern && IsCaseSensitive == other.IsCaseSensitive && UseRegex == other.UseRegex;
        }

        public override bool Equals(object? obj)
        {
            return obj is SearchPattern other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Pattern, IsCaseSensitive, UseRegex);
        }

        public override string ToString() => 
            IsEmpty ? "{Empty}" : $"{Pattern}, useRegex: {UseRegex}, caseSensitive: {IsCaseSensitive}";
        
        private static (bool isValid, string) GetRegexParseError(bool useRegex, string textToSearch)
        {
            if (!useRegex) return (true, string.Empty);
            try
            {
                _ = new Regex(textToSearch);
            }
            catch (RegexParseException e)
            {
                return (false, e.Message);
            }

            return (true, string.Empty);
        }
    }
}