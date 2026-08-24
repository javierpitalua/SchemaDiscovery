namespace SchemaDiscovery
{
    public enum CultureLanguages
    {
        English,
        Spanish,
        undefined
    }

    public interface IHumanizer
    {
        /// <summary>
        /// Converts a string to PascalCase. For example, "my_table_name" becomes "MyTableName".
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        string ToPascalCase(string input);

        /// <summary>
        /// Converts a string to plural form. For example, "User" becomes "Users".
        /// </summary>
        /// <param name="input"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        string ToPluralCase(string input, CultureLanguages culture);


        /// <summary>
        /// Converts a string to a human-readable form. For example, "MyTableName" becomes "My Table Name".
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        string ToHumanReadable(string input);

    }
}
