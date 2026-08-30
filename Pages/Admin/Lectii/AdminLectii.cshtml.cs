using HistoryConnect.Data;
using HistoryConnect.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HistoryConnect.Pages.Admin.Lectii;

[Authorize(Roles = "Administrator")]
public class LectiiIndexModel : PageModel
{
    private readonly AppDbContext _db;
    public LectiiIndexModel(AppDbContext db) => _db = db;

    public List<Perioada> Perioade { get; set; } = new();

    [TempData] public string? Mesaj { get; set; }

    public async Task OnGetAsync()
    {
        Perioade = await _db.Perioade
            .Include(p => p.Capitole)
                .ThenInclude(c => c.Lectii)
            .OrderBy(p => p.Inceput)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostStergeAsync(int idLectie)
    {
        var lectie = await _db.Lectii.FindAsync(idLectie);
        if (lectie != null)
        {
            _db.Lectii.Remove(lectie);
            await _db.SaveChangesAsync();
            Mesaj = "Lecția a fost ștearsă.";
        }
        return RedirectToPage();
    }
}