namespace Knapcode.CatalogCrawler
{
    interface ICursorFactory
    {
        ICursor GetCursor(string catalogIndexPath);
    }
}
