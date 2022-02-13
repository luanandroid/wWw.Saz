namespace wWw.Saz
{
    public static class StringExtension
    {
        public static string Quote(this string source)
        {
            return "\"" + source + "\"";
        }
    }
}