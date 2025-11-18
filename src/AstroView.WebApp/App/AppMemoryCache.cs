using Microsoft.Extensions.Caching.Memory;

namespace AstroView.WebApp.App;

public class AppMemoryCache
{
    private static Dictionary<string, string> LastVisitedPageDic { get; set; }

    static AppMemoryCache()
    {
        LastVisitedPageDic = new Dictionary<string, string>();
    }

    private readonly IMemoryCache memoryCache;

    public AppMemoryCache(IMemoryCache memoryCache)
    {
        this.memoryCache = memoryCache;
    }

    public void SetUserLastVisitedPage(string userId, string pageUrl)
    {
        LastVisitedPageDic[$"LAST_VISITED_PAGE_USER_{userId}"] = pageUrl;
    }

    public string? GetUserLastVisitedPage(string userId)
    {
        LastVisitedPageDic.TryGetValue($"LAST_VISITED_PAGE_USER_{userId}", out var url);
        return url;
    }
}
