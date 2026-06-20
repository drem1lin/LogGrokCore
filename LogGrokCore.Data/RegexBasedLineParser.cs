using System;
using System.Linq;
using System.Text.RegularExpressions;
using LogGrokCore.Data.Monikers;

namespace LogGrokCore.Data
{
    public class RegexBasedLineParser : ILineParser
    {
        private readonly Regex _regex;
        private readonly int _componentCount;
        private readonly int[] _fieldsToStore;

        public RegexBasedLineParser(LogMetaInformation logMetaInformation, 
            bool onlyIndexed = false)
        {
            _regex = new Regex(logMetaInformation.LineRegex, 
                onlyIndexed ? RegexOptions.Compiled : RegexOptions.Compiled | RegexOptions.Singleline);
            _componentCount = logMetaInformation.ComponentCount;
            _fieldsToStore = onlyIndexed
                ? logMetaInformation.IndexedFieldNumbers
                : Enumerable.Range(0, _componentCount).ToArray();
        }

        public ParseResult Parse(string input)
        {
            var placeholder = new int[LineMetaInformation.GetSizeInts(_componentCount)];
            if (!TryParse(input, 0, input.Length,
                new LineMetaInformation(placeholder.AsSpan(), _componentCount).ParsedLineComponents))
                throw new InvalidOperationException();
            return new ParseResult(_componentCount, placeholder);
        }

        public bool TryParse(string input, int beginning, int length,
            in ParsedLineComponents parsedLineComponents)
        {
            var match = _regex.Match(input, beginning, length);
            if (!match.Success)
                return false;

            var index = 0;

            var lastComponentStart = 0;
            var lastComponentLength = 0;

            // Fast path reads Match's internal arrays without allocating Group objects; if that
            // reflection isn't available on this runtime, fall back to the public Match.Groups API.
            var useFastPath = MatchSurgery.IsAvailable;
            var caps = useFastPath ? MatchSurgery.GetCaptures(match) : null;
            var matchCounts = useFastPath ? MatchSurgery.GetMatchCounts(match) : null;

            foreach (var fieldToStore in _fieldsToStore)
            {
                var groupNumber = fieldToStore + 1;

                bool hasValue;
                int componentStartIndex;
                int componentLength;
                if (useFastPath)
                {
                    var cap = caps![groupNumber];
                    hasValue = cap != null && matchCounts![groupNumber] > 0;
                    componentStartIndex = hasValue ? cap![0] : 0;
                    componentLength = hasValue ? cap![1] : 0;
                }
                else
                {
                    var group = match.Groups[groupNumber];
                    hasValue = group.Success;
                    componentStartIndex = hasValue ? group.Index : 0;
                    componentLength = hasValue ? group.Length : 0;
                }

                if (hasValue)
                {
                    lastComponentStart = componentStartIndex - beginning;
                    lastComponentLength = componentLength;
                }
                else
                {
                    lastComponentStart += lastComponentLength;
                    lastComponentLength = 0;
                }

                parsedLineComponents.ComponentStart(index) = lastComponentStart;
                parsedLineComponents.ComponentLength(index) = lastComponentLength;
                index++;
            }

            return true;
        }
    }
}