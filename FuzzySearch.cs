using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DevConsole
{
    public static class FuzzySearch
    {
        private static readonly Regex CleanRegex = new(@"[^a-zA-Z0-9]", RegexOptions.Compiled);

        /// <summary>
        /// Search a closest line from list of candidates
        /// </summary>
        public static string FindClosest(string input, IEnumerable<string> candidates)
        {
            if (string.IsNullOrWhiteSpace(input) || candidates == null) return null;

            string cleanInput = Normalize(input);
            if (string.IsNullOrEmpty(cleanInput)) return null;

            string bestMatch = null;
            int minDistance = int.MaxValue;

            foreach (string candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate)) continue;

                string cleanCandidate = Normalize(candidate);

                if (cleanCandidate.Equals(cleanInput, StringComparison.OrdinalIgnoreCase))
                    return candidate;

                if (cleanCandidate.Contains(cleanInput, StringComparison.OrdinalIgnoreCase) || 
                    cleanInput.Contains(cleanCandidate, StringComparison.OrdinalIgnoreCase))
                {
                    int lenDiff = Math.Abs(cleanCandidate.Length - cleanInput.Length);
                    if (lenDiff < minDistance)
                    {
                        minDistance = lenDiff;
                        bestMatch = candidate;
                    }
                    continue;
                }
                
                int distance = GetDistance(cleanInput, cleanCandidate);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestMatch = candidate;
                }
            }

            return bestMatch;
        }

        /// <summary>
        /// Search closest object from collection of T
        /// </summary>
        public static T FindClosest<T>(string input, IEnumerable<T> items, Func<T, string> keySelector)
        {
            if (items == null || keySelector == null) return default;

            T bestItem = default;
            int minDistance = int.MaxValue;
            string cleanInput = Normalize(input);

            foreach (var item in items)
            {
                if (item == null) continue;
                string key = keySelector(item);
                if (string.IsNullOrEmpty(key)) continue;

                string cleanKey = Normalize(key);

                if (cleanKey.Equals(cleanInput, StringComparison.OrdinalIgnoreCase))
                    return item;

                int distance = cleanKey.Contains(cleanInput, StringComparison.OrdinalIgnoreCase)
                    ? Math.Abs(cleanKey.Length - cleanInput.Length)
                    : GetDistance(cleanInput, cleanKey);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestItem = item;
                }
            }

            return bestItem;
        }

        /// <summary>
        /// Finds methods name using reflection
        /// </summary>
        public static string FindClosestMethod(Type targetType, string query, BindingFlags flags = BindingFlags.Public | BindingFlags.Static)
        {
            if (targetType == null) return null;

            var methodNames = targetType.GetMethods(flags)
                .Where(m => m.ReturnType == typeof(void) && m.GetParameters().Length == 0)
                .Select(m => m.Name);

            return FindClosest(query, methodNames);
        }

        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return CleanRegex.Replace(text, "").ToLowerInvariant();
        }

        public static int GetDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
            if (string.IsNullOrEmpty(t)) return s.Length;

            int n = s.Length;
            int m = t.Length;

            int[] prevRow = new int[m + 1];
            int[] currRow = new int[m + 1];

            for (int j = 0; j <= m; j++) prevRow[j] = j;

            for (int i = 1; i <= n; i++)
            {
                currRow[0] = i;
                char sChar = s[i - 1];

                for (int j = 1; j <= m; j++)
                {
                    int cost = (sChar == t[j - 1]) ? 0 : 1;
                    currRow[j] = Math.Min(
                        Math.Min(currRow[j - 1] + 1, prevRow[j] + 1),
                        prevRow[j - 1] + cost
                    );
                }

                Array.Copy(currRow, prevRow, m + 1);
            }

            return prevRow[m];
        }
    }
}