using HistoryConnect.Data;
using HistoryConnect.Models;
using HistoryConnect.Servicii;
using HistoryConnect.ViewModels.Quiz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace HistoryConnect.Pages;

public class QuizModel : PageModel
{
    private const int SCOR_MINIM_TRECERE = 50;

    private readonly AppDbContext _db;
    private readonly UserManager<Utilizator> _userManager;
    private readonly ServiciuInsigna _serviciuInsigna;
    private readonly ServiciuLectie _serviciuLectie;
    private readonly ILogger<QuizModel> _logger;

    public QuizModel(AppDbContext db, UserManager<Utilizator> userManager,
        ServiciuInsigna serviciuInsigna, ServiciuLectie serviciuLectie, ILogger<QuizModel> logger)
    {
        _db               = db;
        _userManager      = userManager;
        _serviciuInsigna  = serviciuInsigna;
        _serviciuLectie   = serviciuLectie;
        _logger           = logger;
    }

    public HistoryConnect.Models.Quiz? QuizActiv { get; set; }
    public Lectie? Lectie { get; set; }
    public bool DejaCompletat { get; set; }
    public bool DejaPromovat { get; set; }
    public List<ProgresQuiz> IstoricCompletari { get; set; } = new();


    public async Task<IActionResult> OnGetAsync(int quizId)
    {
        QuizActiv = await _db.Quizuri
            .Include(q => q.Intrebari).ThenInclude(i => i.Variante)
            .Include(q => q.Lectie).ThenInclude(l => l!.Capitol)
            .FirstOrDefaultAsync(q => q.IdQuiz == quizId);

        if (QuizActiv == null) return Page();
        Lectie = QuizActiv.Lectie;

        bool esteLogat = User.Identity?.IsAuthenticated ?? false;
        if (!esteLogat)
        {
            if (!await _serviciuLectie.EsteCapitolLiberAsync(Lectie?.IdCapitol))
                return RedirectToPage("/Index");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            IstoricCompletari = await _db.ProgresQuizuri
                .Where(pq => pq.IdUtilizator == user.Id && pq.IdQuiz == quizId)
                .OrderByDescending(pq => pq.DataCompletare)
                .ToListAsync();

            DejaCompletat = IstoricCompletari.Any();
            DejaPromovat  = IstoricCompletari.Any(pq => pq.Scor >= SCOR_MINIM_TRECERE);
        }

        return Page();
    }


    public async Task<IActionResult> OnPostSaveResultAsync([FromBody] SaveResultRequest? req)
    {
        if (req == null || req.QuizId <= 0)
            return new JsonResult(new { success = false, eroare = "Cerere invalidă." }) { StatusCode = 400 };

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return new JsonResult(new { success = false, nelogat = true, eroare = "Trebuie să fii logat." }) { StatusCode = 401 };

        bool esteStudent = await _userManager.IsInRoleAsync(user, "Student");
        if (!esteStudent)
            return new JsonResult(new { success = false, eroare = "Doar elevii pot salva rezultate la quiz." }) { StatusCode = 403 };

        var quiz = await _db.Quizuri
            .Include(q => q.Intrebari).ThenInclude(i => i.Variante)
            .FirstOrDefaultAsync(q => q.IdQuiz == req.QuizId);

        if (quiz == null)
            return new JsonResult(new { success = false, eroare = "Quiz-ul nu există." }) { StatusCode = 404 };

        int totalIntrebari = quiz.Intrebari.Count;
        if (totalIntrebari == 0)
            return new JsonResult(new { success = false, eroare = "Quiz-ul nu are întrebări configurate." }) { StatusCode = 400 };

        var nrVarianteCorecte = quiz.Intrebari
            .ToDictionary(
                i => i.IdIntrebare,
                i => i.Variante.Count(v => v.Corect));

        var varianteCorectePerIntrebare = quiz.Intrebari
            .ToDictionary(
                i => i.IdIntrebare,
                i => i.Variante.Where(v => v.Corect).Select(v => v.IdVarianta).ToHashSet());

        var maxPuncteIntrebare = quiz.Intrebari
            .ToDictionary(
                i => i.IdIntrebare,
                i => i.Variante.Where(v => v.Corect).Sum(v => v.Punctaj));

        int maxPuncteTotal = maxPuncteIntrebare.Values.Sum();
        if (maxPuncteTotal <= 0)
            return new JsonResult(new { success = false, eroare = "Quiz-ul nu are punctaj configurat corect." }) { StatusCode = 400 };

        var raspunsuriAll = (req.Raspunsuri ?? new List<RaspunsItem>())
            .Where(r => r != null && r.IdVariante != null)
            .ToList();

        var idIntrebariValide = quiz.Intrebari
            .Select(i => i.IdIntrebare)
            .ToHashSet();
        var variantePerIntrebare = quiz.Intrebari
            .ToDictionary(
                i => i.IdIntrebare,
                i => i.Variante.Select(v => v.IdVarianta).ToHashSet());
        var raspunsuriValide = raspunsuriAll
            .Where(r =>
            {
                if (!idIntrebariValide.Contains(r.IdIntrebare)) return false;
                var varianteOk = variantePerIntrebare[r.IdIntrebare];
                return r.IdVariante.TrueForAll(idV => varianteOk.Contains(idV));
            })
            .ToList();

        bool areDuplicate = raspunsuriValide
            .GroupBy(r => r.IdIntrebare)
            .Any(g => g.Count() > 1);
        if (areDuplicate)
            return new JsonResult(new { success = false, eroare = "Cererea conține răspunsuri duplicate." }) { StatusCode = 400 };

        bool areVarianteDuplicate = raspunsuriValide
            .Any(r => r.IdVariante.Count != r.IdVariante.Distinct().Count());
        if (areVarianteDuplicate)
            return new JsonResult(new { success = false, eroare = "Cererea conține variante duplicate pentru aceeași întrebare." }) { StatusCode = 400 };

        foreach (var r in raspunsuriValide)
        {
            if (nrVarianteCorecte[r.IdIntrebare] == 1 && r.IdVariante.Count > 1)
                return new JsonResult(new { success = false, eroare = $"Întrebarea {r.IdIntrebare} nu permite răspunsuri multiple." }) { StatusCode = 400 };
        }

        if (raspunsuriAll.Count != raspunsuriValide.Count)
            _logger.LogWarning(
                "[SECURITY] Utilizator {UserId} a trimis {NrInvalide} răspunsuri invalide pentru quiz {QuizId}.",
                user.Id, raspunsuriAll.Count - raspunsuriValide.Count, req.QuizId);
        double scorTotal;
        try
        {
            var puncteVariantePerIntrebare = quiz.Intrebari
                .ToDictionary(
                    i => i.IdIntrebare,
                    i => i.Variante.ToDictionary(v => v.IdVarianta, v => v.Punctaj));

            scorTotal = 0;
            foreach (var r in raspunsuriValide)
            {
                if (!varianteCorectePerIntrebare.TryGetValue(r.IdIntrebare, out var corecte) || corecte.Count == 0) continue;
                if (!maxPuncteIntrebare.TryGetValue(r.IdIntrebare, out var maxPunctajIntrebare) || maxPunctajIntrebare <= 0) continue;

                var puncteVarianteIntrebare = puncteVariantePerIntrebare[r.IdIntrebare];
                double punctajObtinut = 0;
                foreach (var idV in r.IdVariante)
                {
                    if (!puncteVarianteIntrebare.TryGetValue(idV, out var punctajVarianta)) continue;
                    punctajObtinut += corecte.Contains(idV) ? punctajVarianta : -punctajVarianta;
                }
                scorTotal += Math.Clamp(punctajObtinut, 0.0, (double)maxPunctajIntrebare);
            }
        }
        catch (Exception exScor)
        {
            _logger.LogError(exScor,
                "Eroare la calculul scorului pentru quiz {QuizId}, utilizator {UserId}.",
                req.QuizId, user.Id);
            return new JsonResult(new { success = false, eroare = "Eroare la calcularea scorului. Contactează un administrator." }) { StatusCode = 500 };
        }
        int scorCalculat = (int)Math.Round(scorTotal / maxPuncteTotal * 100);

        var recentSubmission = await _db.ProgresQuizuri
            .Where(pq => pq.IdUtilizator == user.Id && pq.IdQuiz == req.QuizId)
            .OrderByDescending(pq => pq.DataCompletare)
            .FirstOrDefaultAsync();

        if (recentSubmission?.DataCompletare > DateTime.UtcNow.AddSeconds(-2))
        {
            _logger.LogWarning(
                "[SECURITY] RATE LIMIT: Utilizator {UserId} a trimis submisii prea rapid pentru quiz {QuizId}.",
                user.Id, req.QuizId);
            return new JsonResult(new { success = false, eroare = "Submisii prea rapide. Încearcă din nou mai târziu." }) { StatusCode = 429 };
        }

        using var tranzactie = await _db.Database.BeginTransactionAsync();
        try
        {
            var recentInTx = await _db.ProgresQuizuri
                .Where(pq => pq.IdUtilizator == user.Id && pq.IdQuiz == req.QuizId)
                .OrderByDescending(pq => pq.DataCompletare)
                .FirstOrDefaultAsync();

            if (recentInTx?.DataCompletare > DateTime.UtcNow.AddSeconds(-2))
            {
                await tranzactie.RollbackAsync();
                return new JsonResult(new { success = false, eroare = "Submisii prea rapide. Încearcă din nou mai târziu." }) { StatusCode = 429 };
            }

            var student = await _db.Studenti.FirstOrDefaultAsync(s => s.IdUtilizator == user.Id);
            if (student == null)
            {
                student = new Student { IdUtilizator = user.Id, XpTotal = 0, NivelCurent = 1 };
                _db.Studenti.Add(student);
            }

            bool aTrecut = scorCalculat >= SCOR_MINIM_TRECERE;

            bool existaPromovareAnterioara = await _db.ProgresQuizuri
                .AnyAsync(pq => pq.IdUtilizator == user.Id
                             && pq.IdQuiz == req.QuizId
                             && pq.Scor >= SCOR_MINIM_TRECERE);

            bool acordaXp = aTrecut && !existaPromovareAnterioara;

            int xpAdded = 0;
            if (acordaXp)
            {
                xpAdded = quiz.XpCompletare;
                student.XpTotal   += xpAdded;
                student.NivelCurent = ServiciuProgres.CalculeazaNivel(student.XpTotal);

                if (quiz.IdLectie > 0)
                {
                    var progresLectie = await _db.ProgresLectii
                        .FirstOrDefaultAsync(pl => pl.IdUtilizator == user.Id && pl.IdLectie == quiz.IdLectie);

                    if (progresLectie == null)
                        _db.ProgresLectii.Add(new ProgresLectie
                        {
                            IdUtilizator    = user.Id,
                            IdLectie        = quiz.IdLectie,
                            Completata      = true,
                            DataCompletare  = DateTime.UtcNow
                        });
                    else
                    {
                        progresLectie.Completata     = true;
                        progresLectie.DataCompletare = DateTime.UtcNow;
                    }
                }
            }

            var progresQuiz = new ProgresQuiz
            {
                IdUtilizator   = user.Id,
                IdQuiz         = req.QuizId,
                Scor           = scorCalculat,
                Evaluat        = acordaXp,
                XpAcordat      = xpAdded,
                DataCompletare = DateTime.UtcNow
            };
            _db.ProgresQuizuri.Add(progresQuiz);
            await _db.SaveChangesAsync();

            foreach (var r in raspunsuriValide)
            {
                if (r.IdVariante.Count == 0)
                {
                    _db.IstoricRaspunsuri.Add(new IstoricRaspunsuri
                    {
                        IdUtilizator = user.Id,
                        IdProgresQ   = progresQuiz.IdProgresQ,
                        IdIntrebare  = r.IdIntrebare,
                        IdVarianta   = null
                    });
                }
                else
                {
                    foreach (var idV in r.IdVariante)
                    {
                        _db.IstoricRaspunsuri.Add(new IstoricRaspunsuri
                        {
                            IdUtilizator = user.Id,
                            IdProgresQ   = progresQuiz.IdProgresQ,
                            IdIntrebare  = r.IdIntrebare,
                            IdVarianta   = idV
                        });
                    }
                }
            }

            await _db.SaveChangesAsync();
            await tranzactie.CommitAsync();

            if (acordaXp)
            {
                try { await _serviciuInsigna.VerificaSiAcordaInsigne(user.Id); }
                catch (Exception exInsigne)
                {
                    _logger.LogWarning(exInsigne,
                        "Acordare insigne eșuată pentru {UserId} — ignorată.",
                        user.Id);
                }
            }

            return new JsonResult(new { success = true, xpAdded, nivelNou = student.NivelCurent, scor = scorCalculat });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Eroare la salvarea rezultatelor quiz {QuizId} pentru utilizatorul {UserId}.",
                req.QuizId, user.Id);
            await tranzactie.RollbackAsync();
            return new JsonResult(new { success = false, eroare = "Eroare la salvare. Încearcă din nou." }) { StatusCode = 500 };
        }
    }
}