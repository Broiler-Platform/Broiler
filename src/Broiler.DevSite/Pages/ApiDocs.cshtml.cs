using Broiler.DevSite.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Broiler.DevSite.Pages;

public class ApiDocsModel : PageModel
{
    private readonly ApiDocService _apiDocs;

    public ApiDocsModel(ApiDocService apiDocs) => _apiDocs = apiDocs;

    public List<ApiTypeDoc> Types { get; set; } = [];

    public void OnGet()
    {
        Types = _apiDocs.GetApiDocs();
    }
}
