using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Knapcode.CatalogCrawler
{
    static class JsonFileHelper
    {
        public static void WriteJson<T>(
            string path,
            T value,
            DateParseHandling dateParseHandling = DateParseHandling.DateTimeOffset,
            Formatting formatting = Formatting.Indented)
        {
            using var fileStream = new FileStream(path, FileMode.Create);
            using var textWriter = new StreamWriter(fileStream);
            using var jsonWriter = new JsonTextWriter(textWriter);
            var serializer = GetJsonSerializer(dateParseHandling);
            serializer.Formatting = formatting;
            serializer.Serialize(jsonWriter, value);
        }

        public static T ReadJson<T>(string path, DateParseHandling dateParseHandling = DateParseHandling.DateTimeOffset)
        {
            using var stream = File.OpenRead(path);
            return ReadJson<T>(stream, dateParseHandling);
        }

        public static T ReadJson<T>(Stream stream, DateParseHandling dateParseHandling = DateParseHandling.DateTimeOffset)
        {
            using var textReader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(textReader);
            var serializer = GetJsonSerializer(dateParseHandling);
            return serializer.Deserialize<T>(jsonReader);
        }

        static JsonSerializer GetJsonSerializer(DateParseHandling dateParseHandling)
        {
            var serializer = new JsonSerializer();
            serializer.DateParseHandling = dateParseHandling;
            serializer.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
            return serializer;
        }

        public static bool RewriteJson(string path, JsonFormatting jsonFormatting)
        {
            switch (jsonFormatting)
            {
                case JsonFormatting.Unchanged:
                    return false;

                case JsonFormatting.PrettyWhenUnindented:
                    var header = ReadFirstChars(path, 4);
                    if (!Regex.IsMatch(header, @"^(\{|\[)[\r\n]+ +"))
                    {
                        RewriteJson(path, Formatting.Indented);
                        return true;
                    }
                    return false;

                case JsonFormatting.Pretty:
                    RewriteJson(path, Formatting.Indented);
                    return true;

                case JsonFormatting.Minify:
                    RewriteJson(path, Formatting.None);
                    return true;

                default:
                    throw new NotImplementedException();
            }
        }

        static string ReadFirstChars(string path, int count)
        {
            using var fileStream = File.OpenRead(path);
            using var textReader = new StreamReader(fileStream);
            var buffer = new char[count];
            var length = textReader.ReadBlock(buffer, 0, buffer.Length);
            return new string(buffer, 0, length);
        }

        static void RewriteJson(string path, Formatting formatting)
        {
            var json = ReadJson<JToken>(path, DateParseHandling.None);
            WriteJson(path, json, DateParseHandling.None, formatting);
        }
    }
}
