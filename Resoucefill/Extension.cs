namespace Resourcefill
{
    public static class Extension
    {
        /// <summary>首字母大写</summary>
        public static string FirstCharToUpper(this string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return char.ToUpper(input[0]) + input.Substring(1);
        }
    }
}