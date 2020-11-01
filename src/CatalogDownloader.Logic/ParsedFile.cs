namespace Knapcode.CatalogDownloader
{
    class ParsedFile<T>
    {
        public ParsedFile(string path, T value)
        {
            Path = path;
            Value = value;
        }

        public string Path { get; }
        public T Value { get; }
    }
}
