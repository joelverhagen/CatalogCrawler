namespace Knapcode.CatalogDownloader
{
    interface ICursorFactory
    {
        ICursor GetCursor(string catalogIndexPath);
    }
}
