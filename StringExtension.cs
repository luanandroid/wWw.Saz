using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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