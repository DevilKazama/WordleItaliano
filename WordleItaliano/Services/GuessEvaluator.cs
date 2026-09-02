using WordleItaliano.Models;

namespace WordleItaliano.Services;

public static class GuessEvaluator
{
    public static TileState[] Evaluate(string guess, string solution)
    {
        var length = solution.Length;
        var result = Enumerable.Repeat(TileState.Absent, length).ToArray();
        var remaining = new Dictionary<char, int>();

        for (var i = 0; i < length; i++)
        {
            if (guess[i] == solution[i])
            {
                result[i] = TileState.Correct;
            }
            else
            {
                remaining[solution[i]] = remaining.GetValueOrDefault(solution[i]) + 1;
            }
        }

        for (var i = 0; i < length; i++)
        {
            if (result[i] == TileState.Correct)
            {
                continue;
            }

            var letter = guess[i];
            if (remaining.GetValueOrDefault(letter) > 0)
            {
                result[i] = TileState.Present;
                remaining[letter]--;
            }
        }

        return result;
    }
}
