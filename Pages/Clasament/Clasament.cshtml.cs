using HistoryConnect.Data;
using HistoryConnect.Models;
using HistoryConnect.ViewModels.Clasament;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HistoryConnect.Pages.Clasament;

public class ClasamentModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<Utilizator> _userManager;

    public ClasamentModel(AppDbContext db, UserManager<Utilizator> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public List<EntryClasament> TopGlobal { get; set; } = new();
    public List<EntryClasament> TopLunar { get; set; } = new();
    public int? IdUtilizatorCurent { get; set; }
    public string LunaAfisata { get; set; } = "";

    public async Task OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        IdUtilizatorCurent = user?.Id;

        LunaAfisata = DateTime.UtcNow.ToString("MMMM yyyy",
            new System.Globalization.CultureInfo("ro-RO"));

        var studentiTopGlobal = await _db.Studenti
            .Include(s => s.Utilizator)
            .OrderByDescending(s => s.XpTotal)
            .ThenBy(s => s.IdUtilizator)
            .Take(10)
            .ToListAsync();

        TopGlobal = new List<EntryClasament>();
        int? xpAnteriorGlobal = null;
        int pozitieGlobalaCurenta = 0;
        for (int i = 0; i < studentiTopGlobal.Count; i++)
        {
            var s = studentiTopGlobal[i];
            if (xpAnteriorGlobal == null || s.XpTotal != xpAnteriorGlobal)
            {
                pozitieGlobalaCurenta = i + 1;
            }
            xpAnteriorGlobal = s.XpTotal;
            TopGlobal.Add(CreeazaEntryGlobal(s, pozitieGlobalaCurenta));
        }

        if (IdUtilizatorCurent is int idCurent &&
            !TopGlobal.Any(e => e.IdUtilizator == idCurent))
        {
            var studentCurent = await _db.Studenti
                .Include(s => s.Utilizator)
                .FirstOrDefaultAsync(s => s.IdUtilizator == idCurent);

            if (studentCurent != null)
            {
                var pozitieCurenta = await _db.Studenti
                    .CountAsync(s => s.XpTotal > studentCurent.XpTotal) + 1;

                TopGlobal.Add(CreeazaEntryGlobal(studentCurent, pozitieCurenta));
            }
        }

        var primaZiLuna = new DateTime(
            DateTime.UtcNow.Year,
            DateTime.UtcNow.Month,
            1,
            0, 0, 0,
            DateTimeKind.Utc);

        var primaZiLunaUrmatoare = primaZiLuna.AddMonths(1);

        var xpLunar = await _db.Studenti
            .Include(s => s.Utilizator)
            .Select(s => new
            {
                Student = s,
                XpLunar = _db.ProgresQuizuri
                    .Where(pq => pq.IdUtilizator == s.IdUtilizator
                              && pq.Evaluat
                              && pq.XpAcordat > 0
                              && pq.DataCompletare >= primaZiLuna
                              && pq.DataCompletare < primaZiLunaUrmatoare)
                    .Sum(pq => (int?)pq.XpAcordat) ?? 0
            })
            .Where(x => x.XpLunar > 0)
            .OrderByDescending(x => x.XpLunar)
            .ThenBy(x => x.Student.Utilizator!.UserName)
            .Take(10)
            .ToListAsync();

        TopLunar = new List<EntryClasament>();
        int? xpAnteriorLunar = null;
        int pozitieLunaraCurenta = 0;
        for (int i = 0; i < xpLunar.Count; i++)
        {
            var x = xpLunar[i];
            if (xpAnteriorLunar == null || x.XpLunar != xpAnteriorLunar)
            {
                pozitieLunaraCurenta = i + 1;
            }
            xpAnteriorLunar = x.XpLunar;

            TopLunar.Add(new EntryClasament
            {
                Pozitie = pozitieLunaraCurenta,
                IdUtilizator = x.Student.IdUtilizator,
                Nume = x.Student.Utilizator?.UserName ?? "—",
                Initiala = (x.Student.Utilizator?.UserName is { Length: > 0 } n ? n[..1] : "?").ToUpper(),
                XpTotal = x.Student.XpTotal,
                NivelCurent = x.Student.NivelCurent,
                ValoareAfisata = $"+{x.XpLunar:N0} XP"
            });
        }

        if (IdUtilizatorCurent is int idCurentLunar &&
            !TopLunar.Any(e => e.IdUtilizator == idCurentLunar))
        {
            var studentCurentLunar = await _db.Studenti
                .Include(s => s.Utilizator)
                .FirstOrDefaultAsync(s => s.IdUtilizator == idCurentLunar);

            if (studentCurentLunar != null)
            {
                var xpLunarCurent = await _db.ProgresQuizuri
                    .Where(pq => pq.IdUtilizator == idCurentLunar
                              && pq.Evaluat
                              && pq.XpAcordat > 0
                              && pq.DataCompletare >= primaZiLuna
                              && pq.DataCompletare < primaZiLunaUrmatoare)
                    .SumAsync(pq => (int?)pq.XpAcordat) ?? 0;

                var pozitieLunara = await _db.Studenti
    .CountAsync(s => (_db.ProgresQuizuri
        .Where(pq => pq.IdUtilizator == s.IdUtilizator
                  && pq.Evaluat
                  && pq.XpAcordat > 0
                  && pq.DataCompletare >= primaZiLuna
                  && pq.DataCompletare < primaZiLunaUrmatoare)
        .Sum(pq => (int?)pq.XpAcordat) ?? 0) > xpLunarCurent) + 1;

                TopLunar.Add(new EntryClasament
                {
                    Pozitie = pozitieLunara,
                    IdUtilizator = studentCurentLunar.IdUtilizator,
                    Nume = studentCurentLunar.Utilizator?.UserName ?? "—",
                    Initiala = (studentCurentLunar.Utilizator?.UserName is { Length: > 0 } n ? n[..1] : "?").ToUpper(),
                    XpTotal = studentCurentLunar.XpTotal,
                    NivelCurent = studentCurentLunar.NivelCurent,
                    ValoareAfisata = $"+{xpLunarCurent:N0} XP"
                });
            }
        }
    }

    private static EntryClasament CreeazaEntryGlobal(Student s, int pozitie) => new()
    {
        Pozitie = pozitie,
        IdUtilizator = s.IdUtilizator,
        Nume = s.Utilizator?.UserName ?? "—",
        Initiala = (s.Utilizator?.UserName is { Length: > 0 } n ? n[..1] : "?").ToUpper(),
        XpTotal = s.XpTotal,
        NivelCurent = s.NivelCurent,
        ValoareAfisata = $"{s.XpTotal:N0} XP"
    };
}