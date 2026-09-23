using System.Text;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Uninstaller;

namespace LeslieCleaner.Modules.Uninstaller.Residual;

/// <summary>
/// Scores an installed application against a residual query without allowing a
/// publisher-only match to override a conflicting product name.
/// </summary>
public static class ResidualMatchScorer
{
    /// <summary>Returns a non-negative score for a candidate, or -1 when it cannot match.</summary>
    public static int Score(InstalledApp candidate, ResidualQuery query)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(query);

        string product = Normalize(query.ProductName);
        string publisher = Normalize(query.Publisher);
        if (product.Length == 0 && publisher.Length == 0)
        {
            return -1;
        }

        int score = 0;
        bool productMatched = false;
        if (product.Length > 0)
        {
            int productScore = ScoreField(candidate.Name, product, exact: 100, prefix: 75, contains: 45);
            if (productScore < 0)
            {
                // A supplied product name is the strongest identity signal. Do not
                // accept an unrelated application merely because its publisher matches.
                return -1;
            }

            score += productScore;
            productMatched = true;
        }

        if (publisher.Length > 0)
        {
            int publisherScore = ScoreField(candidate.Publisher, publisher, exact: 90, prefix: 65, contains: 40);
            if (publisherScore >= 0)
            {
                score += publisherScore;
            }
            else if (!productMatched)
            {
                // Publisher-only queries must still identify the publisher.
                return -1;
            }
            else
            {
                // Registry entries occasionally omit Publisher. Keep an exact product
                // match, but rank it below a candidate with corroborating metadata.
                score -= 25;
            }
        }

        return score;
    }

    private static int ScoreField(string? value, string query, int exact, int prefix, int contains)
    {
        string normalized = Normalize(value);
        if (normalized.Length == 0)
        {
            return -1;
        }

        if (normalized.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return exact;
        }

        if (normalized.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return prefix;
        }

        return normalized.Contains(query, StringComparison.OrdinalIgnoreCase) ? contains : -1;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.Normalize(NormalizationForm.FormKC).Trim();
        var builder = new StringBuilder(normalized.Length);
        bool previousWhitespace = false;
        foreach (char character in normalized)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWhitespace)
                {
                    builder.Append(' ');
                }

                previousWhitespace = true;
                continue;
            }

            builder.Append(character);
            previousWhitespace = false;
        }

        return builder.ToString();
    }
}
