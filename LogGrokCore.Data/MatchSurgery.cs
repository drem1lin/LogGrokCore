using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace LogGrokCore.Data
{
    /// <summary>
    /// Fast access to a <see cref="Match"/>'s internal capture arrays, avoiding the per-call
    /// allocations of the public <see cref="Match.Groups"/> API on the parsing hot path.
    /// This relies on private framework fields (<c>_matches</c>/<c>_matchcount</c>); if a future
    /// runtime renames them, <see cref="IsAvailable"/> is false and callers fall back to the
    /// public API (see <c>RegexBasedLineParser</c>) instead of the whole parser crashing.
    /// </summary>
    public static class MatchSurgery
    {
        private static readonly Func<Match, int[][]>? GetMatchesDelegate;
        private static readonly Func<Match, int[]>? GetMatchCountDelegate;

        /// <summary>True when the internal-field fast path is usable on this runtime.</summary>
        public static bool IsAvailable { get; }

        static MatchSurgery()
        {
            try
            {
                GetMatchesDelegate = GetGetFieldDelegate<Match, int[][]>(
                    typeof(Match).GetField("_matches", BindingFlags.NonPublic | BindingFlags.Instance));
                GetMatchCountDelegate = GetGetFieldDelegate<Match, int[]>(
                    typeof(Match).GetField("_matchcount", BindingFlags.NonPublic | BindingFlags.Instance));
                IsAvailable = true;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(
                    $"MatchSurgery fast path unavailable; falling back to Match.Groups. {ex.Message}");
                IsAvailable = false;
            }
        }

        private static Func<TSource, TValue> GetGetFieldDelegate<TSource, TValue>(FieldInfo? fieldInfo)
        {
            if (fieldInfo == null) throw new ArgumentNullException(nameof(fieldInfo));

            ParameterExpression sourceParameter =
                Expression.Parameter(typeof(TSource), "source");

            MemberExpression fieldExpression = Expression.Field(sourceParameter, fieldInfo);

            LambdaExpression lambda =
                Expression.Lambda(typeof(Func<TSource, TValue>), fieldExpression, sourceParameter);

            return (Func<TSource, TValue>)lambda.Compile();
        }
        public static int[][] GetCaptures(Match match)
        {
            return GetMatchesDelegate!(match);
        }

        public static int[] GetMatchCounts(Match match)
        {
            return GetMatchCountDelegate!(match);
        }
    }
}