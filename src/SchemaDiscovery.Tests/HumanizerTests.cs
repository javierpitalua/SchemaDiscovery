using System;
using SchemaDiscovery;


namespace SchemaDiscovery.Tests
{
    [TestFixture]
    public class HumanizerTests
    {
        private readonly IHumanizer _humanizer = new SchemaDiscovery.Humanizer();

        [Test]
        public void ToPascalCase_WithSnakeCaseInput_ConvertsToPascalCase()
        {
            Assert.That(_humanizer.ToPascalCase("my_table_name"), Is.EqualTo("MyTableName"));
        }

        [Test]
        public void ToPascalCase_WithSingleLowerCaseWord_CapitalizesFirstLetter()
        {
            Assert.That(_humanizer.ToPascalCase("user"), Is.EqualTo("User"));
        }

        [Test]
        public void ToPascalCase_WithAlreadyPascalCaseInput_IsIdempotent()
        {
            Assert.That(_humanizer.ToPascalCase("MyTableName"), Is.EqualTo("MyTableName"));
        }

        [Test]
        public void ToPascalCase_WithEmptyString_ReturnsEmptyString()
        {
            Assert.That(_humanizer.ToPascalCase(string.Empty), Is.EqualTo(string.Empty));
        }

        [Test]
        public void ToPascalCase_WithNullInput_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _humanizer.ToPascalCase(null!));
        }

        [Test]
        public void ToPluralCase_English_WithRegularNoun_AddsS()
        {
            Assert.That(_humanizer.ToPluralCase("User", CultureLanguages.English), Is.EqualTo("Users"));
        }

        [Test]
        public void ToPluralCase_English_WithConsonantYEnding_ReplacesYWithIes()
        {
            Assert.That(_humanizer.ToPluralCase("Category", CultureLanguages.English), Is.EqualTo("Categories"));
        }

        [Test]
        public void ToPluralCase_English_WithIrregularNoun_UsesIrregularForm()
        {
            Assert.That(_humanizer.ToPluralCase("Child", CultureLanguages.English), Is.EqualTo("Children"));
        }

        [Test]
        public void ToPluralCase_Spanish_WithConsonantEnding_AddsEs()
        {
            Assert.That(_humanizer.ToPluralCase("Comision", CultureLanguages.Spanish), Is.EqualTo("Comisiones"));
        }

        [Test]
        public void ToPluralCase_Spanish_WithVowelEnding_AddsS()
        {
            Assert.That(_humanizer.ToPluralCase("Casa", CultureLanguages.Spanish), Is.EqualTo("Casas"));
        }

        [Test]
        public void ToPluralCase_Spanish_WithZEnding_ReplacesZWithCes()
        {
            Assert.That(_humanizer.ToPluralCase("Luz", CultureLanguages.Spanish), Is.EqualTo("Luces"));
        }

        [Test]
        public void ToPluralCase_Spanish_WithConsonantEnding_Papel_AddsEs()
        {
            Assert.That(_humanizer.ToPluralCase("Papel", CultureLanguages.Spanish), Is.EqualTo("Papeles"));
        }

        [Test]
        public void ToPluralCase_WithUndefinedCulture_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _humanizer.ToPluralCase("User", CultureLanguages.undefined));
        }

        [Test]
        public void ToPluralCase_WithNullInput_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => _humanizer.ToPluralCase(null!, CultureLanguages.English));
        }

        [Test]
        public void ToHumanReadable_WithPascalCaseInput_InsertsSpacesAndTitleCases()
        {
            Assert.That(_humanizer.ToHumanReadable("MyTableName"), Is.EqualTo("My Table Name"));
        }

        [Test]
        public void ToHumanReadable_WithSnakeCaseInput_InsertsSpacesAndTitleCases()
        {
            Assert.That(_humanizer.ToHumanReadable("my_table_name"), Is.EqualTo("My Table Name"));
        }

        [Test]
        public void ToHumanReadable_WithSingleWord_ReturnsCapitalizedWord()
        {
            Assert.That(_humanizer.ToHumanReadable("user"), Is.EqualTo("User"));
        }

        [Test]
        public void ToHumanReadable_WithNullInput_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _humanizer.ToHumanReadable(null!));
        }
    }
}
