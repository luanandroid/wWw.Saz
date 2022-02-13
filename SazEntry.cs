using System.Threading.Tasks;

namespace wWw.Saz
{
    public class SazEntry
    {
        public string Name { get; }
        public SazFile Saz { get; }
        public string Index { get; }
        public DataBag Db { get; }

        /// <summary>
        /// https://blog.stephencleary.com/2013/01/async-oop-3-properties.html
        /// </summary>
        public async Task<string> GetRequestAsync()
        {
            if (Db.IsSet(Name)) return Db.GetS(Name);
            var result = await Saz.GetRequestAsync(Index);
            Db.Set(Name, result);
            return result;
        }

        public void SetRequest(string value)
        {
            Db.Set(Name,value);
        }

        public SazEntry(string name, SazFile saz, string index, DataBag db)
        {
            Name = name;
            Saz = saz;
            Index = index;
            Db = db;
        }
    }
}