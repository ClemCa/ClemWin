using static ClemWin.Extensions;

namespace ClemWin
{
    public static class Searcher
    {
        public static int ScoreSearch(string source, string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
                return 0;

            int score = 10;
            if (source == searchText)
            {
                return score; // 10
            }
            score--;
            source = source.ToLowerInvariant();
            searchText = searchText.ToLowerInvariant();

            if (source.StartsWith(searchText, StringComparison.Ordinal))
            {
                return score; // 9
            }
            score--;
            if (source.StartsWith(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return score; // 8
            }
            score--;
            if (source.StartsWith(searchText, StringComparison.InvariantCultureIgnoreCase))
            {
                return score; // 7
            }
            score--;
            if (source.Contains(searchText, StringComparison.Ordinal))
            {
                return score; // 6
            }
            score--;
            if (source.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return score; // 5
            }
            score--;
            if (source.Contains(searchText, StringComparison.InvariantCultureIgnoreCase))
            {
                return score; // 4
            }
            score--;
            int scoringCursor = 0;
            string[] words = searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int fullScore = score;
            int failureScore = score - 2;
            int allWordsScore = score;
            foreach (string word in words)
            {
                if (allWordsScore == fullScore)
                {
                    if (source.Contains(word, scoringCursor, StringComparison.Ordinal, out int fullIndex))
                    {
                        scoringCursor = fullIndex + word.Length;
                        continue;
                    }
                    else
                    {
                        allWordsScore--;
                    }
                }
                // == invariantScore
                if (source.Contains(word, scoringCursor, StringComparison.InvariantCultureIgnoreCase, out int index))
                {
                    scoringCursor = index + word.Length;
                    continue;
                }
                else
                {
                    allWordsScore--;
                    break;
                }
            }
            if (allWordsScore > failureScore)
            {
                return allWordsScore; // 4 or 3
            }
            return 0;
        }
    }
}