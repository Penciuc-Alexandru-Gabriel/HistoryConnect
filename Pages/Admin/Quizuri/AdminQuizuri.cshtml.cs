using HistoryConnect.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HistoryConnect.Pages.Admin.GestiuneQuizuri;

[Authorize(Roles = "Administrator")]
public class QuizuriIndexModel : PageModel
{
    private readonly AppDbContext _db;
    public QuizuriIndexModel(AppDbContext db) => _db = db;

    public List<HistoryConnect.Models.Quiz> Quizuri { get; set; } = new();

    [BindProperty(SupportsGet = true)]
public string? FiltruTip { get; set; }

    [TempData] public string? Mesaj { get; set; }

    public async Task OnGetAsync()
{
    var query = _db.Quizuri
        .Include(q => q.Intrebari)
        .Include(q => q.Lectie)
            .ThenInclude(l => l!.Capitol)
                .ThenInclude(c => c!.Perioada)
        .AsQueryable();

    if (Enum.TryParse<HistoryConnect.Models.TipLectie>(FiltruTip, ignoreCase: true, out var tipLectie))
    {
        query = query.Where(q => q.Lectie != null && q.Lectie.Tip == tipLectie);
    }

    Quizuri = await query
        .OrderBy(q => q.Lectie!.Capitol!.Perioada!.Inceput)
        .ThenBy(q => q.Lectie!.Capitol!.NrOrdine)
        .ThenBy(q => q.Lectie!.Ordine)
        .ThenBy(q => q.Titlu)
        .ToListAsync();
}

    public async Task<IActionResult> OnPostStergeAsync(int idQuiz)
    {
        var quiz = await _db.Quizuri.FindAsync(idQuiz);
        if (quiz != null)
        {
            _db.Quizuri.Remove(quiz);
            await _db.SaveChangesAsync();
            Mesaj = "Quiz-ul a fost șters.";
        }
        return RedirectToPage();
    }
}