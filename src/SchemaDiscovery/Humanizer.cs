using System;
using System.Linq;
using System.Text.RegularExpressions;
using Humanizer;

namespace SchemaDiscovery
{
    public class Humanizer : IHumanizer
    {
        public string ToPluralPascalCase(string input, CultureLanguages culture)
        {
            if (input is null) throw new ArgumentNullException(nameof(input));
            if (input.Length == 0) return input;

            if (culture != CultureLanguages.English && culture != CultureLanguages.Spanish)
                throw new ArgumentOutOfRangeException(nameof(culture), culture, "Unsupported culture.");

            // Split on underscores and on lower->upper boundaries so both
            // 'project_member' and 'ProjectMember' yield ["project", "member"].
            var words = input
                .Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
                .SelectMany(part => Regex.Split(part, "(?<=[a-z0-9])(?=[A-Z])"))
                .Where(w => w.Length > 0)
                .ToArray();

            if (words.Length == 0) return input;

            // English pluralizes the last word (the head noun is last: "WorkItems");
            // Spanish pluralizes the first (the head noun comes first: "ElementosDeTrabajo").
            var index = culture == CultureLanguages.English ? words.Length - 1 : 0;
            words[index] = ToPluralCase(words[index], culture);

            return string.Concat(words.Select(Capitalize));
        }

        private static string Capitalize(string word)
        {
            var rest = word.Substring(1);
            if (word.Length > 1 && word.ToUpperInvariant() == word) rest = rest.ToLowerInvariant();
            return char.ToUpperInvariant(word[0]) + rest;
        }

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
