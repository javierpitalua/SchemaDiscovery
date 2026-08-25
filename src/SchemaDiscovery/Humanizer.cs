using System;
using Humanizer;

namespace SchemaDiscovery
{
    public class Humanizer : IHumanizer
    {
        public string ToPascalCase(string input)
        {
            if (input is null) throw new ArgumentNullException(nameof(input));
            if (input.Length == 0) return input;

            return input.Pascalize();
        }

        public string ToPluralCase(string input, CultureLanguages culture)
        {
            if (input is null) throw new ArgumentNullException(nameof(input));
            if (input.Length == 0) return input;

            switch (culture)
            {
                case CultureLanguages.English:
                    return input.Pluralize();
                case CultureLanguages.Spanish:
                    return PluralizeSpanish(input);
                default:
                    throw new ArgumentOutOfRangeException(nameof(culture), culture, "Unsupported culture.");
            }
        }

        public string ToHumanReadable(string input)
        {
            if (input is null) throw new ArgumentNullException(nameof(input));
            if (input.Length == 0) return input;

            return input.Humanize(LetterCasing.Title);
        }

        // Basic Spanish pluralization: words ending in an unaccented vowel take "s";
        // words ending in "z" swap it for "c" and take "es" (e.g. "Luz" -> "Luces");
        // everything else (consonants and accented vowels) takes "es" (e.g. "Comision" -> "Comisiones").
        private static string PluralizeSpanish(string input)
        {
            var lastChar = input[input.Length - 1];
            var lastLower = char.ToLowerInvariant(lastChar);

            if (lastLower == 'z')
            {
                var replacement = char.IsUpper(lastChar) ? 'C' : 'c';
                return input.Substring(0, input.Length - 1) + replacement + "es";
            }

            if (lastLower == 'a' || lastLower == 'e' || lastLower == 'i' || lastLower == 'o' || lastLower == 'u')
            {
                return input + "s";
            }

            return input + "es";
        }
    }
}
