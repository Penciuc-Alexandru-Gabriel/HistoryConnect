using HistoryConnect.Data;
using HistoryConnect.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HistoryConnect.Pages.Quiz;

[Authorize]
public class DetaliiQuizModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<Utilizator> _userManager;

    public DetaliiQuizModel(AppDbContext db, UserManager<Utilizator> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public ProgresQuiz? ProgresQuiz { get; set; }
    public List<RezultatIntrebare> Rezultate { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int idProgresQ)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToPage("/Index");

        ProgresQuiz = await _db.ProgresQuizuri
            .Include(pq => pq.Quiz)
                .ThenInclude(q => q!.Lectie)
            .FirstOrDefaultAsync(pq => pq.IdProgresQ == idProgresQ
                                    && pq.IdUtilizator == user.Id);

        if (ProgresQuiz == null) return RedirectToPage("/Cont/Profil/Profil");

        var intrebari = await _db.Intrebari
            .Include(i => i.Variante)
            .Where(i => i.IdQuiz == ProgresQuiz.IdQuiz)
            .OrderBy(i => i.IdIntrebare)
            .ToListAsync();

        var raspunsuri = await _db.IstoricRaspunsuri
            .Where(r => r.IdProgresQ == idProgresQ)
            .ToListAsync();

        foreach (var intrebare in intrebari)
        {
            var varianteCorecte = intrebare.Variante
                .Where(v => v.Corect)
                .OrderBy(v => v.IdVarianta)
                .ToList();

            var idVarianteCorecte = varianteCorecte
                .Select(v => v.IdVarianta)
                .ToHashSet();

            var idVarianteAlese = raspunsuri
                .Where(r => r.IdIntrebare == intrebare.IdIntrebare && r.IdVarianta.HasValue)
                .Select(r => r.IdVarianta!.Value)
                .ToHashSet();

            var varianteAlese = intrebare.Variante
                .Where(v => idVarianteAlese.Contains(v.IdVarianta))
                .OrderBy(v => v.IdVarianta)
                .ToList();

            var faraRaspuns = idVarianteAlese.Count == 0;
            var esteCorecta = !faraRaspuns && idVarianteAlese.SetEquals(idVarianteCorecte);
            var estePartial = !faraRaspuns
                && !esteCorecta
                && varianteAlese.Any(v => v.Corect);

            Rezultate.Add(new RezultatIntrebare
            {
                Intrebare = intrebare,
                VarianteAlese = varianteAlese,
                VarianteCorecte = varianteCorecte,
                EsteCorecta = esteCorecta,
                EstePartial = estePartial,
                FaraRaspuns = faraRaspuns
            });
        }

        return Page();
    }
}

public class RezultatIntrebare
{
    public Intrebare Intrebare { get; set; } = null!;
    public List<VariantaRaspuns> VarianteAlese { get; set; } = new();
    public List<VariantaRaspuns> VarianteCorecte { get; set; } = new();
    public bool EsteCorecta { get; set; }
    public bool EstePartial { get; set; }
    public bool FaraRaspuns { get; set; }
}