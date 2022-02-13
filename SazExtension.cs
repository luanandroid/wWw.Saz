using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace wWw.Saz
{
    public static class SazExtension
    {
        public static string ReadString(this ZipArchive zip, string name)
        {
            using var stream = GetStream(zip,name);
            return ReadString(stream);
        }

        public static Stream GetStream(this ZipArchive zip, string name)
        {
            var entry = zip.GetEntry(name);
            if (entry is null) throw new Exception(name);
            return entry.Open();
        }

        public static string ReadString(this Stream stream)
        {
            using var readStream = new StreamReader(stream);
            return readStream.ReadToEnd();
        }

        public static string GetSazHome(this ZipArchive zip)
        {
            return ReadString(zip, "_index.htm");
        }

        public static string GetSazIndex(this string sazHome, string index)
        {
            var tdS = "<td>" + index + "</td>";
            var td = sazHome.IndexOf(tdS, StringComparison.CurrentCulture);
            if (td < 0) throw new Exception(tdS);
            const string begin = "<a href='raw\\";
            var start = sazHome.LastIndexOf(begin, td, StringComparison.CurrentCulture);
            if (start < 0) throw new Exception(begin);
            start += begin.Length;
            var stop = sazHome.IndexOf('_', start);
            if (stop < 0) throw new Exception(nameof(stop));
            return sazHome[start..stop];
        }

        public static async Task<string> GetEntryStringAsync(this ZipArchiveEntry entry)
        {
            var tmpFile = Path.GetTempFileName();
            entry.ExtractToFile(tmpFile, true);
            //entry.ExtractToFile(@"C:\Users\AdminTuan\Desktop\1", true);
            var content = await File.ReadAllTextAsync(tmpFile, Encoding.Latin1);
            //File.WriteAllText(@"C:\Users\AdminTuan\Desktop\2", content,Encoding.Latin1);
            File.Delete(tmpFile);
            return content;
        }
        
        public static ZipArchiveEntry GetSazEntry(this ZipArchive zip, string? home, string index)
        {
            home ??= GetSazHome(zip);
            var indexStr = GetSazIndex(home, index);
            var realName = "raw/" + indexStr + "_c.txt";
            var entry = zip.GetEntry(realName);
            if (entry is null) throw new Exception(realName);
            return entry;
        }

        public static Task<string> GetSazRequestAsync(this ZipArchive zip, string index)
        {
            return GetSazRequestAsync(zip, null, index);
        }

        public static async Task<string> GetSazRequestAsync(this ZipArchive zip, string? home, string index)
        {
            var entry = GetSazEntry(zip, home, index);
            if (entry is null) throw new Exception(nameof(GetSazRequestAsync));
            return await GetEntryStringAsync(entry);
        }
    }
}