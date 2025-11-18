namespace AstroView.WebApp.App.Models;

public class Paging
{
    public int MinPageSize { get; set; }
    public int MaxPageSize { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int MaxPageNumber { get; set; }
    public int RecordsCount { get; set; }
    public int SkipRecordsCount { get; set; }
    public List<int> PagingButtons { get; set; }

    public Paging(int minPageSize = 10, int maxPageSize = 50)
    {
        MinPageSize = minPageSize;
        MaxPageSize = maxPageSize;
        PagingButtons = new List<int>();
    }

    public void Calculate(int? pageNumber, int? pageSize, int recordsCount, int rangeBeforeAndAfter = 2)
    {
        PageNumber = pageNumber ?? 1;
        if (PageNumber < 1) PageNumber = 1;

        PageSize = pageSize ?? MinPageSize;
        if (PageSize < 10) PageSize = 10;
        if (PageSize > 50) PageSize = 50;

        RecordsCount = recordsCount;
        MaxPageNumber = (int)Math.Ceiling((double)recordsCount / PageSize);
        if (PageNumber > MaxPageNumber)
            PageNumber = MaxPageNumber;
        if (PageNumber < 1)
            PageNumber = 1;

        var minPage = 1;
        var maxPage = (int)Math.Ceiling((double)RecordsCount / PageSize);

        var beginPage = PageNumber - rangeBeforeAndAfter;
        var endPage = PageNumber + rangeBeforeAndAfter;

        if (beginPage < minPage)
        {
            endPage += -beginPage + 1;
            beginPage = minPage;
        }

        if (endPage > maxPage)
        {
            beginPage -= endPage - maxPage;
            if (beginPage < minPage)
            {
                beginPage = minPage;
            }
            endPage = maxPage;
        }

        PagingButtons = Enumerable.Range(beginPage, endPage + 1 - beginPage).ToList();

        SkipRecordsCount = PageSize * (PageNumber - 1);
    }
}
