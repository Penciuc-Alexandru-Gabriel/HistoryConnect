using HistoryConnect.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HistoryConnect.Pages.Admin.GestiuneQuizuri;

[Authorize(Roles = "Administrator")]
public class EditeazaQuizModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ILogger<EditeazaQuizModel> _logger;
    public EditeazaQuizModel(AppDbContext db, ILogger<EditeazaQuizModel> logger)
    {
        _db = db;
        _logger = logger;
    }

    public List<LectieSelectieItem> LectiiSelectie { get; set; } = new();
    public string QuizJsonInitial { get; set; } = "null";
    public bool EsteEditare { get; set; }
public class LectieSelectieItem
{
    public int IdLectie { get; set; }
    public int IdPerioada { get; set; }
    public int IdCapitol { get; set; }
    public string Perioada { get; set; } = "";
    public string Capitol { get; set; } = "";
    public string Text { get; set; } = "";
    public string Tip { get; set; } = "";
}
    public async Task<IActionResult> OnGetAsync(int id = 0)
    {
        await IncarcaLectii();
        EsteEditare = id > 0;

        if (id > 0)
        {
            var quiz = await _db.Quizuri
                .Include(q => q.Intrebari.OrderBy(i => i.IdIntrebare))
                    .ThenInclude(i => i.Variante.OrderBy(v => v.IdVarianta))
                .FirstOrDefaultAsync(q => q.IdQuiz == id);

            if (quiz == null) return NotFound();

            var dto = new
            {
                idQuiz = quiz.IdQuiz,
                idLectie = quiz.IdLectie,
                titlu = quiz.Titlu,
                xpCompletare = quiz.XpCompletare,
                timp = quiz.Timp,
                feedback = quiz.Feedback ?? "",
                intrebari = quiz.Intrebari.Select(i => new
                {
                    idIntrebare = i.IdIntrebare,
                    text = i.Text,
                    tip = i.Tip.ToString(),
                    timp = i.Timp,
                    feedback = i.Feedback,
                    urlImagine = i.UrlImagine ?? "",
                    variante = i.Variante.Select(v => new
                    {
                        idVarianta = v.IdVarianta,
                        text = v.Text,
                        corect = v.Corect,
                        punctaj = v.Punctaj
                    }).ToList()
                }).ToList()
            };

            QuizJsonInitial = System.Text.Json.JsonSerializer.Serialize(dto);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSalveazaAsync([FromBody] QuizCompletRequest? req)
    {
        if (req == null)
            return new JsonResult(new { ok = false, eroare = "Cerere invalidă." });

        if (string.IsNullOrWhiteSpace(req.Titlu))
            return new JsonResult(new { ok = false, eroare = "Titlul quiz-ului este obligatoriu." });

        if (req.IdLectie <= 0)
            return new JsonResult(new { ok = false, eroare = "Selectează o lecție." });

        if (req.XpCompletare < 0)
            return new JsonResult(new { ok = false, eroare = "XP-ul nu poate fi negativ." });

        req.Intrebari ??= new List<IntrebareRequest>();
        if (req.Intrebari.Count == 0)
            return new JsonResult(new { ok = false, eroare = "Quiz-ul trebuie să aibă cel puțin o întrebare." });

        var lectieExista = await _db.Lectii.AnyAsync(l => l.IdLectie == req.IdLectie);
        if (!lectieExista)
            return new JsonResult(new { ok = false, eroare = "Lecție invalidă." });

        foreach (var iReq in req.Intrebari)
        {
            iReq.Variante ??= new List<VariantaRequest>();
            var textIntrebare = iReq.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(textIntrebare))
                return new JsonResult(new { ok = false, eroare = "Fiecare întrebare trebuie să aibă text." });

            if ((iReq.Timp ?? 5) < 5)
                return new JsonResult(new { ok = false, eroare = $"Întrebarea «{textIntrebare}» trebuie să aibă timp de cel puțin 5 secunde." });

            if (iReq.Variante.Count == 0)
                return new JsonResult(new { ok = false, eroare = $"Întrebarea «{textIntrebare}» trebuie să aibă variante de răspuns." });

            if (iReq.Variante.All(v => !v.Corect))
                return new JsonResult(new { ok = false, eroare = $"Întrebarea «{textIntrebare}» trebuie să aibă cel puțin un răspuns corect." });

            foreach (var vReq in iReq.Variante)
            {
                if (string.IsNullOrWhiteSpace(vReq.Text))
                    return new JsonResult(new { ok = false, eroare = $"Întrebarea «{textIntrebare}» are o variantă fără text." });

                if (vReq.Punctaj < 0)
                    return new JsonResult(new { ok = false, eroare = $"Întrebarea «{textIntrebare}» are punctaj negativ." });
            }
        }

        using var tranzactie = await _db.Database.BeginTransactionAsync();
        try
        {
            HistoryConnect.Models.Quiz quiz;
            if (req.IdQuiz == 0)
            {
                quiz = new HistoryConnect.Models.Quiz();
                _db.Quizuri.Add(quiz);
            }
            else
            {
                quiz = await _db.Quizuri
                    .Include(q => q.Intrebari).ThenInclude(i => i.Variante)
                    .FirstOrDefaultAsync(q => q.IdQuiz == req.IdQuiz)
                    ?? throw new InvalidOperationException("Quiz negăsit.");
            }

            quiz.IdLectie = req.IdLectie;
            quiz.Titlu = req.Titlu.Trim();
            quiz.XpCompletare = req.XpCompletare;
            quiz.Feedback = string.IsNullOrWhiteSpace(req.Feedback) ? null : req.Feedback.Trim();

            var idIntrebariExistente = quiz.Intrebari.Select(i => i.IdIntrebare).ToHashSet();
            var idIntrebariPrimite = req.Intrebari.Where(i => i.IdIntrebare > 0)
                                                   .Select(i => i.IdIntrebare).ToHashSet();

            foreach (var idSters in idIntrebariExistente.Except(idIntrebariPrimite))
            {
                var intrebareDeSters = quiz.Intrebari.First(i => i.IdIntrebare == idSters);
                _db.Intrebari.Remove(intrebareDeSters);
            }

            foreach (var iReq in req.Intrebari)
            {
                HistoryConnect.Models.Intrebare intrebare;
                if (iReq.IdIntrebare == 0)
                {
                    intrebare = new HistoryConnect.Models.Intrebare { Quiz = quiz };
                    _db.Intrebari.Add(intrebare);
                }
                else
                {
                    intrebare = quiz.Intrebari.FirstOrDefault(i => i.IdIntrebare == iReq.IdIntrebare)
                        ?? throw new InvalidOperationException("Întrebare invalidă pentru acest quiz.");
                }

                intrebare.Text = iReq.Text!.Trim();
                intrebare.Tip = Enum.TryParse<HistoryConnect.Models.TipIntrebare>(iReq.Tip, out var tip)
                    ? tip
                    : HistoryConnect.Models.TipIntrebare.Grila;
                intrebare.Timp = iReq.Timp ?? 5;
                intrebare.Feedback = iReq.Feedback?.Trim() ?? "";
                intrebare.UrlImagine = string.IsNullOrWhiteSpace(iReq.UrlImagine) ? null : iReq.UrlImagine.Trim();

                var idVarianteExistente = intrebare.Variante.Select(v => v.IdVarianta).ToHashSet();
                var idVariantePrimite = iReq.Variante!.Where(v => v.IdVarianta > 0)
                                                       .Select(v => v.IdVarianta).ToHashSet();

                foreach (var idSters in idVarianteExistente.Except(idVariantePrimite))
                {
                    var variantaDeSters = intrebare.Variante.First(v => v.IdVarianta == idSters);
                    _db.VarianteRaspuns.Remove(variantaDeSters);
                }

                foreach (var vReq in iReq.Variante!)
                {
                    HistoryConnect.Models.VariantaRaspuns varianta;
                    if (vReq.IdVarianta == 0)
                    {
                        varianta = new HistoryConnect.Models.VariantaRaspuns { Intrebare = intrebare };
                        _db.VarianteRaspuns.Add(varianta);
                    }
                    else
                    {
                        varianta = intrebare.Variante.FirstOrDefault(v => v.IdVarianta == vReq.IdVarianta)
                            ?? throw new InvalidOperationException("Variantă invalidă pentru această întrebare.");
                    }

                    varianta.Text = vReq.Text!.Trim();
                    varianta.Corect = vReq.Corect;
                    varianta.Punctaj = vReq.Punctaj;
                }
            }

            quiz.Timp = (req.Timp.HasValue && req.Timp.Value > 0)
                ? req.Timp.Value
                : req.Intrebari.Sum(i => i.Timp ?? 5);

            await _db.SaveChangesAsync();

            await tranzactie.CommitAsync();
            return new JsonResult(new { ok = true, idQuiz = quiz.IdQuiz, timp = quiz.Timp });
        }
        catch (Exception ex)
        {
            await tranzactie.RollbackAsync();
            _logger.LogError(ex, "Eroare la salvarea quiz {QuizId}.", req.IdQuiz);
            return new JsonResult(new { ok = false, eroare = "Eroare la salvare. Încearcă din nou." });
        }
    }

   private async Task IncarcaLectii()
{
    var perioade = await _db.Perioade
        .Include(p => p.Capitole).ThenInclude(c => c.Lectii)
        .OrderBy(p => p.Inceput)
        .ToListAsync();

    LectiiSelectie = perioade
        .SelectMany(p => p.Capitole.OrderBy(c => c.NrOrdine)
            .SelectMany(c => c.Lectii
                .OrderBy(l => l.Tip)
                .ThenBy(l => l.Ordine)
                .Select(l => new LectieSelectieItem
                {
                    IdLectie = l.IdLectie,
                    IdPerioada = p.IdPerioada,
                    IdCapitol = c.IdCapitol,
                    Perioada = p.Nume,
                    Capitol = c.Titlu,
                    Tip = l.Tip.ToString(),
                    Text = l.Titlu
                })))
        .ToList();
}
}

public class QuizCompletRequest
{
    public int IdQuiz { get; set; }
    public int IdLectie { get; set; }
    public string Titlu { get; set; } = "";
    public int XpCompletare { get; set; }
    public int? Timp { get; set; }
    public string? Feedback { get; set; }
    public List<IntrebareRequest>? Intrebari { get; set; } = new();
}

public class IntrebareRequest
{
    public int IdIntrebare { get; set; }
    public string? Text { get; set; } = "";
    public string Tip { get; set; } = "Grila";
    public int? Timp { get; set; }
    public string? UrlImagine { get; set; }
    public string? Feedback { get; set; } = "";
    public List<VariantaRequest>? Variante { get; set; } = new();
}

public class VariantaRequest
{
    public int IdVarianta { get; set; }
    public string? Text { get; set; } = "";
    public bool Corect { get; set; }
    public int Punctaj { get; set; }
}