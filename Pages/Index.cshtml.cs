using HistoryConnect.Data;
using HistoryConnect.Models;
using HistoryConnect.Servicii;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HistoryConnect.Pages;

public class IndexModel : PageModel
{
    private readonly ServiciuLectie _lectieService;
    private readonly ServiciuProgres _progresService;
    private readonly UserManager<Utilizator> _userManager;
    private readonly AppDbContext _db;

    public IndexModel(ServiciuLectie lectieService, ServiciuProgres progresService, UserManager<Utilizator> userManager, AppDbContext db)
    {
        _lectieService = lectieService;
        _progresService = progresService;
        _userManager = userManager;
        _db = db;
    }

    public List<Perioada> ToatePerioadele { get; set; } = new();
    public Perioada? PerioadaSelectata { get; set; }
    public Capitol? CapitolSelectat { get; set; }
    public List<Lectie> LectiiAfisate { get; set; } = new();
    public Lectie? LectieActiva { get; set; }
    public bool AfiseazaLectie => LectieActiva != null;
    public bool EsteLogat { get; set; }
    public bool AccesPermis { get; set; } = true;
    public bool ArataPotup { get; set; } = false;

    public TipLectie TipCategorie => LectiiAfisate.FirstOrDefault()?.Tip ?? TipLectie.Istorie;
    public int? PrimulCapitolIdLiber { get; set; }
    public TipLectie CategorieCurenta { get; set; } = TipLectie.Istorie;
    public HashSet<int> LectiiCompletateIds { get; set; } = new();

    public Lectie? UrmatoareaLectie { get; set; }
    public int TotalLectii { get; set; }
    public int LectiiCompletate { get; set; }
    public int ProcentProgres => TotalLectii == 0 ? 0 : (int)Math.Round((double)LectiiCompletate / TotalLectii * 100);

    public string? UrlContinuare =>
        UrmatoareaLectie?.Capitol?.Perioada == null
            ? null
            : $@"/?period={UrmatoareaLectie.Capitol.Perioada.IdPerioada}&capitol={UrmatoareaLectie.IdCapitol}&category={UrmatoareaLectie.Tip}&lessonId={UrmatoareaLectie.IdLectie}";

    public async Task OnGetAsync(int? period, int? capitol, int? lessonId, string? category)
    {
        CategorieCurenta = Enum.TryParse<TipLectie>(category, ignoreCase: true, out var tip)
            ? tip : TipLectie.Istorie;

        EsteLogat = User.Identity?.IsAuthenticated ?? false;
        Lectie? lectieDinUrl = null;

        if (lessonId.HasValue)
        {
            lectieDinUrl = await _lectieService.GetLectieCompletaAsync(lessonId.Value);

            if (lectieDinUrl != null)
            {
                CategorieCurenta = lectieDinUrl.Tip;
                capitol ??= lectieDinUrl.IdCapitol;
            }
        }

        ToatePerioadele = await _lectieService.GetToatePerioadeleCuCapitoleAsync();
        var primaPerioada = ToatePerioadele.FirstOrDefault();

        if (primaPerioada != null)
            PrimulCapitolIdLiber = await _lectieService.GetPrimulCapitolIdAsync(primaPerioada.IdPerioada);

        int idPerioadaCurenta = (!EsteLogat)
            ? (primaPerioada?.IdPerioada ?? 0)
            : (period ?? primaPerioada?.IdPerioada ?? 0);

        PerioadaSelectata = await _lectieService.GetPerioadaCuLectiiAsync(idPerioadaCurenta);

        if (capitol.HasValue && PerioadaSelectata != null)
        {
            CapitolSelectat = PerioadaSelectata.Capitole.FirstOrDefault(c => c.IdCapitol == capitol.Value);

            if (CapitolSelectat != null)
                LectiiAfisate = CapitolSelectat.Lectii
                    .Where(l => l.Tip == CategorieCurenta)
                    .OrderBy(l => l.Ordine)
                    .ToList();
        }

        if (EsteLogat)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                LectiiCompletateIds = await _progresService.GetLectiiCompletateAsync(user.Id);

                var lectiiOrdonate = await _lectieService.GetLectiiOrdonateAsync(CategorieCurenta);

                TotalLectii = lectiiOrdonate.Count;
                LectiiCompletate = lectiiOrdonate.Count(l => LectiiCompletateIds.Contains(l.IdLectie));

                var ultimaLectieCompletata = await _progresService.GetUltimaLectieCompletataAsync(user.Id, CategorieCurenta);

                if (ultimaLectieCompletata != null)
                {
                    UrmatoareaLectie = _lectieService.GetUrmatoareaLectieNeterminata(
                        lectiiOrdonate, ultimaLectieCompletata.IdLectie, LectiiCompletateIds);
                }
            }
        }

        if (lectieDinUrl != null)
        {
            if (!_lectieService.EsteLectieAccesibila(lectieDinUrl.IdCapitol, PrimulCapitolIdLiber, EsteLogat))
            {
                AccesPermis = false;
                ArataPotup = true;
                return;
            }

            LectieActiva = lectieDinUrl;
        }
    }

    public async Task<IActionResult> OnPostFinalizeazaLectieAsync(int lectieId, string? returnUrl)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToPage("/Index");

        if (!await _db.Lectii.AnyAsync(l => l.IdLectie == lectieId))
            return RedirectToPage("/Index");

        await _progresService.FinalizeazaLectieAsync(user.Id, lectieId);
        return LocalRedirect(returnUrl ?? "/");
    }

    public async Task<IActionResult> OnPostDeFinalizatLectieAsync(int lectieId, string? returnUrl)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToPage("/Index");

        await _progresService.AnuleazaFinalizareaAsync(user.Id, lectieId);
        return LocalRedirect(returnUrl ?? "/");
    }
}