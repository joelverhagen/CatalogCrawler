namespace Knapcode.CatalogDownloader
{
    interface ICursorProvider
    {
        ICursor GetCursor(string catalogIndexPath);
    }
}
