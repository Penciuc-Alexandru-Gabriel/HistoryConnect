using HistoryConnect.Data;
using HistoryConnect.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HistoryConnect.Pages.Cont.Profil;

[Authorize]
public class ProfilModel : PageModel
{
    private readonly UserManager<Utilizator> _userManager;
    private readonly AppDbContext _db;

    public ProfilModel(UserManager<Utilizator> userManager, AppDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public Utilizator? Utilizator { get; set; }
    public Student? Student { get; set; }
    public List<Avatar> AvatareDisponibile { get; set; } = new();
    public List<CabinetInsigne> InsigneleUtilizatorului { get; set; } = new();
    public List<Insigna> ToateInsignele { get; set; } = new();
    public int LectiiCompletate { get; set; }
    public int TotalLectii { get; set; }
    public List<ProgresQuiz> IstoricQuizuri { get; set; } = new();
    public int QuizuriCompletate { get; set; }
    public Dictionary<int, int> CorectePerProgres { get; set; } = new();
    public Dictionary<int, int> TotalIntrebariPerQuiz { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        Utilizator = await _userManager.Users
            .Include(u => u.Avatar)
            .Include(u => u.Student)
            .FirstOrDefaultAsync(u => u.UserName == User.Identity!.Name);

        if (Utilizator == null) return RedirectToPage("/Cont/Logare-Inregistrare/Login");

        Student = Utilizator.Student;

        AvatareDisponibile = await _db.Avatare.OrderBy(a => a.NivelNecesar).ToListAsync();

        InsigneleUtilizatorului = await _db.CabinetInsigne
            .Include(ci => ci.Insigna)
            .Where(ci => ci.IdUtilizator == Utilizator.Id)
            .ToListAsync();

        ToateInsignele = await _db.Insigne.OrderBy(i => i.IdInsigna).ToListAsync();

        LectiiCompletate = await _db.ProgresLectii
            .CountAsync(p => p.IdUtilizator == Utilizator.Id && p.Completata);

        TotalLectii = await _db.Lectii.CountAsync();

        IstoricQuizuri = await _db.ProgresQuizuri
            .Include(pq => pq.Quiz)
                .ThenInclude(q => q!.Lectie)
            .Where(pq => pq.IdUtilizator == Utilizator.Id)
            .OrderByDescending(pq => pq.DataCompletare)
            .ToListAsync();

        var idProgrese = IstoricQuizuri.Select(pq => pq.IdProgresQ).ToList();
var idQuizuri = IstoricQuizuri
    .Select(pq => pq.IdQuiz)
    .Distinct()
    .ToList();

TotalIntrebariPerQuiz = await _db.Intrebari
    .Include(i => i.Variante)
    .Where(i => idQuizuri.Contains(i.IdQuiz))
    .GroupBy(i => i.IdQuiz)
    .ToDictionaryAsync(
        g => g.Key,
        g => g.Sum(i => i.Variante
            .Where(v => v.Corect)
            .Sum(v => v.Punctaj))
    );

CorectePerProgres = IstoricQuizuri
    .ToDictionary(
        pq => pq.IdProgresQ,
        pq =>
        {
            var punctajMaxim = TotalIntrebariPerQuiz.GetValueOrDefault(pq.IdQuiz);

            if (punctajMaxim <= 0)
            {
                return 0;
            }

            return (int)Math.Round(pq.Scor / 100.0 * punctajMaxim);
        });

        QuizuriCompletate = IstoricQuizuri
            .Select(pq => pq.IdQuiz)
            .Distinct()
            .Count();

        return Page();
    }

    public async Task<IActionResult> OnPostSchimbaAvatarAsync(int idAvatar)
    {
        var utilizator = await _userManager.GetUserAsync(User);
        if (utilizator == null) return RedirectToPage("/Cont/Logare-Inregistrare/Login");

        var avatar = await _db.Avatare.FindAsync(idAvatar);
        if (avatar == null)
        {
            TempData["Eroare"] = "Avatarul selectat nu există.";
            return RedirectToPage();
        }

        var student = await _db.Studenti
            .FirstOrDefaultAsync(s => s.IdUtilizator == utilizator.Id);

        var esteAdmin = await _userManager.IsInRoleAsync(utilizator, "Administrator");
        int nivelUtilizator = student?.NivelCurent ?? 1;
        if (!esteAdmin && nivelUtilizator < avatar.NivelNecesar)
        {
            TempData["Eroare"] = $"Ai nevoie de nivelul {avatar.NivelNecesar} pentru acest avatar.";
            return RedirectToPage();
        }

        utilizator.IdAvatar = idAvatar;
        await _userManager.UpdateAsync(utilizator);

        return RedirectToPage();
    }
}