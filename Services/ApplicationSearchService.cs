using System.IO;
using AppLauncher.Models;

namespace AppLauncher.Services
{
    /// <summary>
    /// Ranks applications against a query: exact match > starts-with > contains > fuzzy subsequence.
    /// Case-insensitive, matches on both the display name and the underlying exe file name.
    /// </summary>
    public class ApplicationSearchService
    {
        public List<ApplicationItem> Search(IEnumerable<ApplicationItem> apps, string query, int maxResults = 9)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                // Empty search: show recently / frequently used apps first (spec §20)
                return apps
                    .OrderByDescending(a => a.LaunchCount)
                    .ThenByDescending(a => a.LastLaunchedUtc)
                    .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(maxResults)
                    .ToList();
            }

            string q = query.Trim();
            var scored = new List<(ApplicationItem app, int score)>();

            foreach (var app in apps)
            {
                int score = Score(app.Name, q);

                string exeName = Path.GetFileNameWithoutExtension(app.ExecutablePath);
                if (!string.IsNullOrEmpty(exeName))
                {
                    int exeScore = Score(exeName, q) - 50; // secondary signal, slightly penalized
                    if (exeScore > score) score = exeScore;
                }

                if (score > 0)
                {
                    score += Math.Min(app.LaunchCount, 20); // favorites bubble up on near-ties
                    scored.Add((app, score));
                }
            }

            return scored
                .OrderByDescending(s => s.score)
                .ThenBy(s => s.app.Name, StringComparer.OrdinalIgnoreCase)
                .Take(maxResults)
                .Select(s => s.app)
                .ToList();
        }

        private static int Score(string name, string query)
        {
            if (string.IsNullOrEmpty(name)) return 0;

            string n = name.ToLowerInvariant();
            string q = query.ToLowerInvariant();

            if (n == q) return 1000;
            if (n.StartsWith(q, StringComparison.Ordinal)) return 900 - Math.Min(n.Length - q.Length, 100);

            int idx = n.IndexOf(q, StringComparison.Ordinal);
            if (idx >= 0) return 700 - idx;

            return FuzzySubsequenceScore(n, q);
        }

        /// <summary>
        /// Positive score if every character of the query appears in order within name (not
        /// necessarily contiguous), rewarding consecutive runs. Zero if not every character matches.
        /// </summary>
        private static int FuzzySubsequenceScore(string name, string query)
        {
            int nameIdx = 0;
            int lastMatch = -1;
            int consecutiveBonus = 0;
            int total = 0;

            foreach (char c in query)
            {
                int found = name.IndexOf(c, nameIdx);
                if (found == -1) return 0;

                if (lastMatch != -1 && found == lastMatch + 1) consecutiveBonus += 5;

                total += 3;
                lastMatch = found;
                nameIdx = found + 1;
            }

            return 50 + total + consecutiveBonus - name.Length / 4;
        }
    }
}
