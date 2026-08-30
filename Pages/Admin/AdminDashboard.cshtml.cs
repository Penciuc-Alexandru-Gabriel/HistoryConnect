using HistoryConnect.Data;
using HistoryConnect.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HistoryConnect.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class AdminIndexModel : PageModel
{
    private readonly AppDbContext _db;
    public AdminIndexModel(AppDbContext db) => _db = db;

    public int TotalStudenti        { get; set; }
    public int TotalLectii          { get; set; }
    public int TotalQuizuri         { get; set; }
    public int TotalCompletariQuiz  { get; set; }
    public int TotalCompletariLectie { get; set; }
    public int LectiiNeterminate    { get; set; }

    public List<StatStudent> TopStudenti { get; set; } = new();
    public List<StatQuiz>    QuizuriPopulare { get; set; } = new();
    public List<StatLectie>  LectiiPopulare  { get; set; } = new();

    public async Task OnGetAsync()
    {
        TotalStudenti        = await _db.Studenti.CountAsync();
        TotalLectii          = await _db.Lectii.CountAsync();
        TotalQuizuri         = await _db.Quizuri.CountAsync();
        TotalCompletariLectie = await _db.ProgresLectii.CountAsync(pl => pl.Completata);

        TotalCompletariQuiz = await _db.ProgresQuizuri.CountAsync();

        var lectiiCuProgres = await _db.ProgresLectii
            .Where(pl => pl.Completata)
            .Select(pl => pl.IdLectie)
            .Distinct()
            .ToListAsync();
        LectiiNeterminate = TotalLectii - lectiiCuProgres.Count;

        TopStudenti = await _db.Studenti
            .Include(s => s.Utilizator)
            .OrderByDescending(s => s.XpTotal)
            .Take(10)
            .Select(s => new StatStudent
            {
                Nume          = s.Utilizator!.Nume,
                NumeUtilizator = s.Utilizator.UserName ?? "",
                XpTotal       = s.XpTotal,
                NivelCurent   = s.NivelCurent
            })
            .ToListAsync();


        var top5Quizuri = await _db.ProgresQuizuri
            .GroupBy(pq => pq.IdQuiz)
            .Select(g => new { IdQuiz = g.Key, NrIncercari = g.Count(), ScorMediu = (int)g.Average(pq => pq.Scor) })
            .OrderByDescending(x => x.NrIncercari)
            .Take(5)
            .ToListAsync();

        var idQuizuri = top5Quizuri.Select(x => x.IdQuiz).ToList();
        var titluriQuizuri = await _db.Quizuri
            .Where(q => idQuizuri.Contains(q.IdQuiz))
            .Select(q => new { q.IdQuiz, q.Titlu })
            .ToListAsync();
        var titluriQMap = titluriQuizuri.ToDictionary(q => q.IdQuiz, q => q.Titlu);

        QuizuriPopulare = top5Quizuri
            .Select(x => new StatQuiz
            {
                IdQuiz      = x.IdQuiz,
                TitluQuiz   = titluriQMap.GetValueOrDefault(x.IdQuiz, ""),
                NrIncercari = x.NrIncercari,
                ScorMediu   = x.ScorMediu
            })
            .ToList();

        var top5Lectii = await _db.ProgresLectii
            .Where(pl => pl.Completata)
            .GroupBy(pl => pl.IdLectie)
            .Select(g => new { IdLectie = g.Key, NrCompletari = g.Count() })
            .OrderByDescending(x => x.NrCompletari)
            .Take(5)
            .ToListAsync();

        var idLectii = top5Lectii.Select(x => x.IdLectie).ToList();
        var titluriLectii = await _db.Lectii
            .Where(l => idLectii.Contains(l.IdLectie))
            .Select(l => new { l.IdLectie, l.Titlu })
            .ToListAsync();
        var titluriLMap = titluriLectii.ToDictionary(l => l.IdLectie, l => l.Titlu);

        LectiiPopulare = top5Lectii
            .Select(x => new StatLectie
            {
                IdLectie     = x.IdLectie,
                TitluLectie  = titluriLMap.GetValueOrDefault(x.IdLectie, ""),
                NrCompletari = x.NrCompletari
            })
            .ToList();
    }

    public record StatStudent(string Nume = "", string NumeUtilizator = "",
                              int XpTotal = 0, int NivelCurent = 1)
    {
        public StatStudent() : this("", "", 0, 1) { }
        public string Nume           { get; set; } = Nume;
        public string NumeUtilizator { get; set; } = NumeUtilizator;
        public int    XpTotal        { get; set; } = XpTotal;
        public int    NivelCurent    { get; set; } = NivelCurent;
    }

    public record StatQuiz(int IdQuiz = 0, string TitluQuiz = "",
                           int NrIncercari = 0, int ScorMediu = 0)
    {
        public StatQuiz() : this(0, "", 0, 0) { }
        public int    IdQuiz      { get; set; } = IdQuiz;
        public string TitluQuiz   { get; set; } = TitluQuiz;
        public int    NrIncercari { get; set; } = NrIncercari;
        public int    ScorMediu   { get; set; } = ScorMediu;
    }

    public record StatLectie(int IdLectie = 0, string TitluLectie = "", int NrCompletari = 0)
    {
        public StatLectie() : this(0, "", 0) { }
        public int    IdLectie     { get; set; } = IdLectie;
        public string TitluLectie  { get; set; } = TitluLectie;
        public int    NrCompletari { get; set; } = NrCompletari;
    }
}