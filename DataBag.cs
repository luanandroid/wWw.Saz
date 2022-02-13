using System.Text;
using Newtonsoft.Json;

namespace wWw.Saz
{
    /// <summary>
    /// https://inspiration.nlogic.ca/en/a-comparison-of-newtonsoft.json-and-system.text.json
    /// </summary>
    public class DataBag
    {
        private readonly Dictionary<string, object> _values;

        public DataBag(string filePath)
        {
            FilePath = filePath;
            if (File.Exists(FilePath))
            {
                var content = File.ReadAllText(FilePath,Encoding.Latin1);
                _values = JsonConvert.DeserializeObject<Dictionary<string, object>>(content)?? new Dictionary<string, object>();
            }
            else
            {
                _values = new Dictionary<string, object>();
            }
        }

        public string FilePath { get; }

        public string GetS(string name)
        {
            return (string) Get(name);
        }

        // ReSharper disable once UnusedMember.Global
        public dynamic GetD(string name)
        {
            return Get(name);
        }

        public object Get(string name)
        {
            return _values.ContainsKey(name) ? _values[name] : throw new Exception(name);
        }

        /// <summary>
        /// nếu muốn replace khác, gets và replace rồi save
        /// db.Replace("mobile-info", "login-response.session", "581c3061-4094-418d-ba13-8e04e27eb234.f1fea237a7a1df39648ad7e8da270f57c55f8cb7");
        /// extract value từ 1 var khác và replace vào var cũ
        /// </summary>
        /// <param name="name"></param>
        /// <param name="newValueName"></param>
        /// <param name="oldValue"></param>
        // ReSharper disable once UnusedMember.Global
        public void Replace(string name, string newValueName, string oldValue)
        {
            var o = GetS(name);
            if (o is null) throw new Exception(name);
            if (!o.Contains(oldValue)) throw new Exception(oldValue);
            var value = GetS(newValueName);
            if (value is null) throw new Exception(newValueName);
            Set(name, o.Replace(oldValue, value));
        }

        public bool IsSet(string name)
        {
            return _values.ContainsKey(name);
        }

        /// <summary>
        /// nếu muốn extract trực tiếp, getd và get
        /// db.Extract("login-response", "session");
        /// </summary>
        /// <param name="name"></param>
        /// <param name="subName"></param>
        // ReSharper disable once UnusedMember.Global
        public void Extract(string name, string subName)
        {
            dynamic o = Get(name);
            if (o is null) throw new Exception(name);
            var dic = (IDictionary<string, object>) o;
            Set(name + "." + subName, dic[subName]);
        }

        /// <summary>
        ///     set này nên tạo 1 bản backup nếu replace
        ///     có thêm time đồ
        /// db.Set("mobile-info", await saz.GetRequestAsync("[#209]"));
        /// db.Set("mobile-info-response", await client.RequestJson(db.GetS("mobile-info")));
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void Set(string name, object value)
        {
            if (_values.ContainsKey(name)) _values[name] = value;
            else _values.Add(name, value);
            var json = JsonConvert.SerializeObject(_values);
            File.WriteAllText(FilePath, json,Encoding.Latin1);
        }
    }
}