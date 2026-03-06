using Broiler.DevSite.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Broiler.DevSite.Pages;

public class ComplianceModel : PageModel
{
    private readonly ComplianceService _compliance;

    public ComplianceModel(ComplianceService compliance) => _compliance = compliance;

    public List<ChapterChecklist> Checklists { get; set; } = [];
    public int TotalItems { get; set; }
    public int TotalCompleted { get; set; }
    public double OverallPercent { get; set; }

    public void OnGet()
    {
        Checklists = _compliance.GetChecklists();
        TotalItems = Checklists.Sum(c => c.TotalItems);
        TotalCompleted = Checklists.Sum(c => c.CompletedItems);
        OverallPercent = TotalItems == 0 ? 0 : Math.Round((double)TotalCompleted / TotalItems * 100, 1);
    }
}
