using System;
using System.IO.Compression;
using System.Threading.Tasks;

namespace wWw.Saz
{
    // ReSharper disable once UnusedMember.Global
    public class SazFile : IDisposable
    {
        private readonly string _home;
        private readonly ZipArchive _zip;

        public SazFile(string file)
        {
            File = file;
            if (!file.ToLower().EndsWith(".saz")) throw new Exception(file);

            _zip = ZipFile.OpenRead(File);
            _home = _zip.GetSazHome();
        }

        public string File { get; }

        public void Dispose()
        {
            _zip.Dispose();
            GC.SuppressFinalize(this);
        }

        public async Task<string> GetRequestAsync(string index)
        {
            return await _zip.GetSazRequestAsync(_home, index);
        }
    }
}