using System.Dynamic;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using HttpMultipartParser;
using HttpParser;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace wWw.Saz
{
    // ReSharper disable once UnusedMember.Global
    public static class HttpExtension
    {
        public const string HeaderSeparator = "\r\n\r\n";
        public const string Host = nameof(Host);
        public const string Method = nameof(Method);
        public const string HttpVersion = nameof(HttpVersion);
        public const string ContentType = "Content-Type";
        public const string FormData = "form-data";
        public const string MultiPart = "multipart/form-data";
        public const string Accept = nameof(Accept);
        public const string Authorization = nameof(Authorization);
        public const string ContentLength = "Content-Length";
        public const string ContentDisposition = "Content-Disposition";
        public const string Boundary = "alamofire.boundary.";

        private static readonly Regex ContentLengthRegex = new("\\\r\nContent-Length: (.*?)\\\r\n", RegexOptions.Compiled);
        
        // ReSharper disable once UnusedMember.Global
        public static async Task<object> Request(this HttpClient client, 
            SazEntry entry, ParameterPart[]? parameters = null, FilePart[]? files = null, bool bodyIsJson = false, bool returnIsJson = false)
        {
            var template = await entry.GetRequestAsync();
            var request = template.GetRequest(true, parameters, files,null,null,bodyIsJson);
            return returnIsJson? await client.RequestJson(request): await client.Request(request);
        }

        /// <summary>
        ///     https://stackoverflow.com/questions/2698999/how-to-reuse-socket-in-net
        ///     https://stackoverflow.com/questions/5038546/socket-disconnectbool-reuse/8567731
        ///     https://social.msdn.microsoft.com/Forums/en-US/51ae5e69-2ae5-4015-8f6c-9e78902808b1/reuse-socket-after-calling-shutdown?forum=ncl
        ///     chậm : https://stackoverflow.com/questions/9915101/performance-of-receiveasync-vs-beginreceive
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="send"></param>
        /// <returns></returns>
        // ReSharper disable once UnusedMember.Global
        public static string HttpRequest(string host, int port, string send)
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(host, port);
            socket.Send(Encoding.ASCII.GetBytes(send));
            var reading = true;
            var headerString = string.Empty;
            var bodyBuff = Array.Empty<byte>();
            while (reading)
            {
                var buffer = new byte[1];
                socket.Receive(buffer, 0, 1, 0);
                headerString += Encoding.ASCII.GetString(buffer);
                if (!headerString.Contains(HeaderSeparator)) continue;
                var contentLengthStr = ContentLengthRegex.Match(headerString).Groups[1].ToString();
                var contentLength = int.Parse(contentLengthStr);
                reading = false;
                bodyBuff = new byte[contentLength];
                socket.Receive(bodyBuff, 0, contentLength, 0);
            }

            var body = Encoding.ASCII.GetString(bodyBuff);
            socket.Close();
            return body;
        }

        public static HttpMethod GetMethod(this ParsedHttpRequest request)
        {
            return new HttpMethod(request.Headers.GetHeadersValue(Method));
        }

        public static string? GetContentType(this ParsedHttpRequest request)
        {
            var contentType = request.Headers.GetHeadersValue(ContentType);
            return contentType?.Split(';')[0];
        }

        /// <summary>
        ///     validate : check content type & content length vs request-body
        /// </summary>
        /// <param name="request"></param>
        /// <param name="parameters"></param>
        /// <param name="files"></param>
        /// <param name="bodyIsJson"></param>
        /// <returns></returns>
        public static HttpContent? GetBody(
            this ParsedHttpRequest request,
            IReadOnlyList<ParameterPart>? parameters,
            IReadOnlyList<FilePart>? files, bool bodyIsJson)
        {
            if (request.RequestBody is null) return null;
            var contentType = request.GetContentType() ?? throw new Exception(nameof(GetContentType));
            return contentType.HeaderEquals(MultiPart)
                ? request.RequestBody.GetMultiParts().GetFormData(parameters, files)
                : bodyIsJson
                    ? GetStringContentJson(request.RequestBody, contentType, parameters)
                    : GetStringContent(request.RequestBody, contentType, parameters);
        }

        public static HttpContent? GetStringContent(
            this string? body,
            string contentType,
            IReadOnlyList<ParameterPart>? parameters = null)
        {
            if (body is null) return null;
            if (parameters is null) return new StringContent(body, null, contentType);
            var values = HttpUtility.ParseQueryString(body);
            foreach (var parameter in parameters)
            {
                if (values.AllKeys.Contains(parameter.Name)) values[parameter.Name] = parameter.Data;
                else values.Add(parameter.Name, parameter.Data);
            }

            return new StringContent(values.ToString() ?? throw new InvalidOperationException(), null, contentType);
        }

        public static HttpContent? GetStringContentJson(
            this string? body,
            string contentType,
            IReadOnlyList<ParameterPart>? parameters = null)
        {
            if (body is null) return null;
            if (parameters is null) return new StringContent(body, null, contentType);
            var o = ParseJson(body);
            foreach (var part in parameters) Replace(o, part.Name, part.Data);
            return new StringContent(JsonConvert.SerializeObject(o), null, contentType);
        }

        public static void Replace(this ExpandoObject obj, string name, string value)
        {
            if (obj is null) throw new Exception("obj is null");

            while (true)
            {
                if (obj is null) break;

                IDictionary<string, object> o = obj;
                if (o is null) throw new Exception(nameof(Replace));
                if (!name.Contains('.'))
                {
                    o[name] = value;
                    return;
                }

                var nameArr = name.Split('.', 2);
                obj = o[nameArr[0]] as ExpandoObject;
                name = nameArr[1];
            }
        }

        public static string GetUtf8(string latin1)
        {
            var bytes = Encoding.GetEncoding("iso-8859-1").GetBytes(latin1);
            return Encoding.UTF8.GetString(bytes);
        }

        public static MultipartFormDataContent GetFormData(
            this MultipartFormDataParser parts,
            IReadOnlyList<ParameterPart>? parameters = null,
            IReadOnlyList<FilePart>? files = null)
        {
            var boundary = Boundary + DateTime.Now.Ticks;
            var formContent = new MultipartFormDataContent(boundary);

            formContent.Headers.ContentDisposition = new ContentDispositionHeaderValue(FormData);

            foreach (var part in parts.Files)
                if (files == null || !files.Any(f => f.Name.Equals(part.Name)))
                    formContent.Add(part.GetContent(), part.Name.Quote());

            if (files is not null)
                foreach (var part in files)
                    formContent.Add(part.GetContent(), part.Name.Quote());

            foreach (var part in parts.Parameters)
                if (parameters == null || !parameters.Any(p => p.Name.Equals(part.Name)))
                    formContent.Add(part.GetContent(), part.Name.Quote());

            if (parameters is not null)
                foreach (var part in parameters)
                    formContent.Add(part.GetContent(), part.Name.Quote());

            formContent.Headers.Remove(ContentType);
            formContent.Headers.TryAddWithoutValidation(ContentType, MultiPart + "; boundary=" + boundary);

            return formContent;
        }

        public static HttpContent GetContent(this ParameterPart parameter)
        {
            var value = GetUtf8(parameter.Data).Replace("\r\n", "\n");
            var content = new StringContent(value);
            content.Headers.ContentType = null;
            return content;
        }

        public static HttpContent GetContent(this FilePart file)
        {
            var content = new StreamContent(file.Data);
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue(file.ContentDisposition)
            {
                Name = file.Name.Quote(),
                FileName = file.FileName.Quote()
            };
            content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            return content;
        }

        public static MultipartFormDataParser GetMultiParts(this string body)
        {
            using var stream = new MemoryStream(Encoding.Latin1.GetBytes(body));
            var parts = MultipartFormDataParser.Parse(stream, Encoding.Latin1);
            return parts;
        }

        /// <summary>
        ///     https://github.com/workwhileweb/http-parser
        ///     https://stackoverflow.com/questions/35907642/custom-header-to-httpclient-request
        ///     HttpWebRequestBuilder.InitializeWebRequest(parsed);
        ///     TO-DO : xử lý cookie
        /// </summary>
        /// <param name="request"></param>
        /// <param name="parseBody"></param>
        /// <param name="authorization">[Bearer-TOKEN]</param>
        /// <param name="files"></param>
        /// <param name="accept">application/json ...</param>
        /// <param name="parameters"></param>
        /// <param name="bodyIsJson"></param>
        /// <returns></returns>
        public static HttpRequestMessage GetRequest(
            this ParsedHttpRequest request,
            bool parseBody,
            IReadOnlyList<ParameterPart>? parameters,
            IReadOnlyList<FilePart>? files,
            string? accept,
            string? authorization, bool bodyIsJson)
        {
            var message = new HttpRequestMessage
            {
                Method = request.GetMethod(),
                RequestUri = request.Uri,
                //Headers = {{"evil", "live"}, {"hello", "world"}},
                Content = parseBody ? request.GetBody(parameters, files, bodyIsJson) : null
            };
            foreach (var (key, value) in request.Headers)
            {
                if (key.HeaderEquals(ContentLength)) continue;
                if (key.HeaderEquals(ContentType)) continue;
                if (key.HeaderEquals(HttpVersion)) continue;
                if (key.HeaderEquals(Method)) continue;
                if (key.HeaderEquals(Host)) continue;
                if (key.HeaderEquals(ContentDisposition)) continue;
                message.Headers.Add(key, value);
            }

            //var cookies = request.Cookies.Select(ck => ck.Key + "=" + ck.Value);
            //var cookie = string.Join("; ", cookies);
            //message.Headers.Add("Cookie", cookie);

            if (authorization is not null) message.Headers.Add(Authorization, authorization);
            if (accept is not null) message.Headers.Add(Accept, accept);
            return message;
        }
        
        public static HttpRequestMessage GetRequest(
            this string request,
            bool parseBody,
            IReadOnlyList<ParameterPart>? parameters,
            IReadOnlyList<FilePart>? files,
            string? accept,
            string? authorization, bool bodyIsJson)
        {
            var parsed = Parser.ParseRawRequest(request);
            return parsed.GetRequest(parseBody, parameters, files, accept, authorization, bodyIsJson);
        }

        public static async Task<string> Request(this HttpClient client, HttpRequestMessage message)
        {
            using var response = await client.SendAsync(message);
            return await response.Content.ReadAsStringAsync();
        }

        public static async Task<string> Request(this HttpClient client, string requestRaw)
        {
            using var message = GetRequest(requestRaw, true, null,null,null,null,false);
            return await Request(client, message);
        }

        /// <summary>
        ///     https://makolyte.com/csharp-deserialize-json-to-dynamic-object/
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        public static dynamic ParseJson(this string html)
        {
            if (string.IsNullOrWhiteSpace(html)) throw new Exception(nameof(html));
            //dynamic json = JsonSerializer.Deserialize<ExpandoObject>(html);
            var converter = new ExpandoObjectConverter();
            dynamic? json = JsonConvert.DeserializeObject<ExpandoObject>(html, converter);
            if (json is null) throw new Exception(html);
            return json;
        }

        // ReSharper disable once UnusedMember.Global
        public static async Task<dynamic> RequestJson(this HttpClient client, HttpRequestMessage request)
        {
            var html = await Request(client, request);
            return ParseJson(html);
        }

        // ReSharper disable once UnusedMember.Global
        public static async Task<dynamic> RequestJson(this HttpClient client, string request)
        {
            var html = await Request(client, request);
            return ParseJson(html);
        }

        public static async Task<string> RequestOld(this string content)
        {
            var parsed = Parser.ParseRawRequest(content);
            var request = HttpWebRequestBuilder.InitializeWebRequest(parsed);
            using var response = await request.GetResponseAsync();
            await using var stream = response.GetResponseStream();
            if (stream is null) throw new Exception(nameof(RequestOld));
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        // ReSharper disable once UnusedMember.Global
        public static async Task<object> RequestJsonOld(this string request)
        {
            var html = await RequestOld(request);
            if (string.IsNullOrWhiteSpace(html)) throw new Exception(nameof(RequestJsonOld));
            return ParseJson(html);
        }

        public static void SaveFile(this IReadOnlyList<FilePart> files, string folder)
        {
            foreach (var file in files) SaveFile(file, folder);
        }

        public static void SaveFile(this FilePart file, string folder)
        {
            var fileName = Path.Combine(folder, file.FileName);
            using var fileStream = File.Create(fileName);
            file.Data.Seek(0, SeekOrigin.Begin);
            file.Data.CopyTo(fileStream);
            fileStream.Flush();
        }

        /// <summary>
        ///     https://github.com/Http-Multipart-Data-Parser/Http-Multipart-Data-Parser
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public static void SaveFile(this MultipartFormDataParser multiParts, string folder)
        {
            multiParts.Files.SaveFile(folder);
        }

        public static bool HeaderEquals(this string source, string destination)
        {
            return source.Equals(destination, StringComparison.CurrentCultureIgnoreCase);
        }

        public static string GetHeadersValue(this Dictionary<string, string> headers, string key)
        {
            return headers.FirstOrDefault(pair => HeaderEquals(pair.Key, key)).Value;
        }
    }
}