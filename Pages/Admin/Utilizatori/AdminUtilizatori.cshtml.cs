using HistoryConnect.Data;
using HistoryConnect.Models;
using HistoryConnect.Servicii;
using HistoryConnect.ViewModels.AdminUtilizatori;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HistoryConnect.Pages.Admin.Utilizatori;

[Authorize(Roles = "Administrator")]
public class UtilizatoriIndexModel : PageModel
{
    private const string AprobareProvider = "AdminAprobari";
    private const string AprobarePromovare = "PromovareStudent";
    private const string AprobareStergere = "StergereStudent";

    private readonly AppDbContext _db;
    private readonly UserManager<Utilizator> _userManager;
    private readonly ServiciuInsigna _serviciuInsigna;

    public UtilizatoriIndexModel(AppDbContext db, UserManager<Utilizator> userManager, ServiciuInsigna serviciuInsigna)
    {
        _db = db;
        _userManager = userManager;
        _serviciuInsigna = serviciuInsigna;
    }

    public List<UtilizatorViewModel> Utilizatori { get; set; } = new();

    public int CurrentUserId { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string? Cautare { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FiltruRol { get; set; }

    [TempData] public string? Mesaj { get; set; }
    [TempData] public string? MesajEroare { get; set; }

    public async Task OnGetAsync()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        CurrentUserId = currentUser?.Id ?? 0;

        var roleAdmin = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator");
        var idRoleAdmin = roleAdmin?.Id ?? -1;

        var adminIds = await _db.UserRoles
            .Where(ur => ur.RoleId == idRoleAdmin)
            .Select(ur => ur.UserId)
            .ToHashSetAsync();

        var usersQuery = _db.Users
            .Include(u => u.Student)
            .Include(u => u.Administrator)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(Cautare))
        {
            var c = Cautare.ToLower();
            usersQuery = usersQuery.Where(u =>
                u.Nume.ToLower().Contains(c) ||
                (u.UserName != null && u.UserName.ToLower().Contains(c)) ||
                (u.Email != null && u.Email.ToLower().Contains(c)));
        }

        if (!string.IsNullOrEmpty(FiltruRol))
        {
            usersQuery = FiltruRol == "Administrator"
                ? usersQuery.Where(u => adminIds.Contains(u.Id))
                : usersQuery.Where(u => !adminIds.Contains(u.Id));
        }

        var users = await usersQuery.OrderBy(u => u.Nume).ToListAsync();

        var tokenAprobari = await _db.UserTokens
            .Where(t => t.LoginProvider == AprobareProvider &&
                        (t.Name == AprobarePromovare || t.Name == AprobareStergere))
            .ToListAsync();

        var aprobariPromovare = tokenAprobari
            .Where(t => t.Name == AprobarePromovare)
            .Select(t => new { t.UserId, Detalii = CitesteDetaliiAprobare(t.Value) })
            .Where(t => t.Detalii != null)
            .GroupBy(t => t.UserId)
            .ToDictionary(g => g.Key, g => g.First().Detalii!);

        var aprobariStergere = tokenAprobari
            .Where(t => t.Name == AprobareStergere)
            .Select(t => new { t.UserId, Detalii = CitesteDetaliiAprobare(t.Value) })
            .Where(t => t.Detalii != null)
            .GroupBy(t => t.UserId)
            .ToDictionary(g => g.Key, g => g.First().Detalii!);

        Utilizatori = users.Select(u =>
        {
            aprobariPromovare.TryGetValue(u.Id, out var aprobarePromovare);
            aprobariStergere.TryGetValue(u.Id, out var aprobareStergere);

            return new UtilizatorViewModel
            {
                Id = u.Id,
                Nume = u.Nume,
                NumeUtilizator = u.UserName ?? "",
                Email = u.Email ?? "",
                Rol = adminIds.Contains(u.Id) ? "Administrator" : "Student",
                XpTotal = u.Student?.XpTotal ?? 0,
                NivelCurent = u.Student?.NivelCurent ?? 1,
                DataInregistrare = u.DataInregistrare,
                DataNumire = u.Administrator?.DataNumire,
                AprobarePromovareInitiataDeId = aprobarePromovare?.IdAdministrator,
                AprobarePromovareInitiataDe = aprobarePromovare?.NumeAdministrator,
                AprobarePromovareData = aprobarePromovare?.DataCerere,
                AprobareStergereInitiataDeId = aprobareStergere?.IdAdministrator,
                AprobareStergereInitiataDe = aprobareStergere?.NumeAdministrator,
                AprobareStergereData = aprobareStergere?.DataCerere
            };
        }).ToList();
    }

    public async Task<IActionResult> OnPostStergeAsync(int idUtilizator)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            MesajEroare = "Trebuie sa fii autentificat ca administrator.";
            return RedirectToPage();
        }

        if (currentUser.Id == idUtilizator)
        {
            MesajEroare = "Nu iti poti sterge propriul cont de administrator!";
            return RedirectToPage();
        }

        var user = await _db.Users
            .Include(u => u.Student)
            .Include(u => u.Administrator)
            .FirstOrDefaultAsync(u => u.Id == idUtilizator);

        if (user == null)
        {
            MesajEroare = "Utilizatorul nu a fost gasit.";
            return RedirectToPage();
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains("Administrator"))
        {
            MesajEroare = "Conturile de administrator sunt protejate si nu pot fi sterse din acest panou.";
            return RedirectToPage();
        }

        if (!await AreConfirmareAltAdminAsync(user, currentUser, AprobareStergere, "stergerea"))
            return RedirectToPage();

        var result = await _userManager.DeleteAsync(user);
        if (result.Succeeded)
            Mesaj = $"Utilizatorul '{user.Nume}' a fost sters cu succes.";
        else
            MesajEroare = $"Eroare la stergerea utilizatorului: {IdentityErrors(result)}";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSchimbaRolAsync(int idUtilizator)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            MesajEroare = "Trebuie sa fii autentificat ca administrator.";
            return RedirectToPage();
        }

        if (currentUser.Id == idUtilizator)
        {
            MesajEroare = "Nu iti poti schimba propriul rol!";
            return RedirectToPage();
        }

        var user = await _db.Users
            .Include(u => u.Student)
            .Include(u => u.Administrator)
            .FirstOrDefaultAsync(u => u.Id == idUtilizator);

        if (user == null)
        {
            MesajEroare = "Utilizatorul nu a fost gasit.";
            return RedirectToPage();
        }

        var roles = await _userManager.GetRolesAsync(user);
        bool esteAdmin = roles.Contains("Administrator");

        if (!esteAdmin && !await AreConfirmareAltAdminAsync(user, currentUser, AprobarePromovare, "promovarea"))
            return RedirectToPage();

        await using var tranzactie = await _db.Database.BeginTransactionAsync();

        try
        {
            if (esteAdmin)
            {
                await StergeAprobariUtilizatorAsync(user.Id, AprobarePromovare, AprobareStergere);

                var removeAdmin = await _userManager.RemoveFromRoleAsync(user, "Administrator");
                if (!removeAdmin.Succeeded)
                {
                    await tranzactie.RollbackAsync();
                    MesajEroare = $"Rolul Administrator nu a putut fi eliminat: {IdentityErrors(removeAdmin)}";
                    return RedirectToPage();
                }

                if (user.Administrator != null)
                    _db.Administratori.Remove(user.Administrator);

                if (user.Student == null)
                {
                    var xpTotal = await _db.ProgresQuizuri
                        .Where(pq => pq.IdUtilizator == user.Id && pq.Evaluat)
                        .SumAsync(pq => (int?)pq.XpAcordat) ?? 0;

                    _db.Studenti.Add(new Student
                    {
                        IdUtilizator = user.Id,
                        XpTotal = xpTotal,
                        NivelCurent = ServiciuProgres.CalculeazaNivel(xpTotal)
                    });
                }

                var addStudent = await _userManager.AddToRoleAsync(user, "Student");
                if (!addStudent.Succeeded)
                {
                    await tranzactie.RollbackAsync();
                    MesajEroare = $"Rolul Student nu a putut fi adaugat: {IdentityErrors(addStudent)}";
                    return RedirectToPage();
                }

                await _db.SaveChangesAsync();

                await _serviciuInsigna.VerificaSiAcordaInsigne(user.Id);

                await tranzactie.CommitAsync();

                Mesaj = $"'{user.Nume}' a fost retrogradat la rolul Student.";
            }
            else
            {
                await StergeAprobariUtilizatorAsync(user.Id, AprobareStergere);

                var removeStudent = await _userManager.RemoveFromRoleAsync(user, "Student");
                if (!removeStudent.Succeeded && roles.Contains("Student"))
                {
                    await tranzactie.RollbackAsync();
                    MesajEroare = $"Rolul Student nu a putut fi eliminat: {IdentityErrors(removeStudent)}";
                    return RedirectToPage();
                }

                if (user.Student != null)
                    _db.Studenti.Remove(user.Student);

                if (user.Administrator == null)
                {
                    _db.Administratori.Add(new Administrator
                    {
                        IdUtilizator = user.Id,
                        DataNumire = DateTime.UtcNow
                    });
                }

                var addAdmin = await _userManager.AddToRoleAsync(user, "Administrator");
                if (!addAdmin.Succeeded)
                {
                    await tranzactie.RollbackAsync();
                    MesajEroare = $"Rolul Administrator nu a putut fi adaugat: {IdentityErrors(addAdmin)}";
                    return RedirectToPage();
                }

                await _db.SaveChangesAsync();
                await tranzactie.CommitAsync();

                Mesaj = $"'{user.Nume}' a fost promovat la Administrator.";
            }
        }
        catch (Exception ex)
        {
            await tranzactie.RollbackAsync();
            MesajEroare = $"Rolul nu a putut fi schimbat: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRefuzaAprobareAsync(int idUtilizator, string actiune)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            MesajEroare = "Trebuie sa fii autentificat ca administrator.";
            return RedirectToPage();
        }

        var detaliiActiune = MapeazaActiuneAprobare(actiune);
        if (detaliiActiune == null)
        {
            MesajEroare = "Cererea de aprobare nu este valida.";
            return RedirectToPage();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == idUtilizator);
        if (user == null)
        {
            MesajEroare = "Utilizatorul nu a fost gasit.";
            return RedirectToPage();
        }

        var token = await _db.UserTokens.FirstOrDefaultAsync(t =>
            t.UserId == idUtilizator &&
            t.LoginProvider == AprobareProvider &&
            t.Name == detaliiActiune.Value.NumeToken);

        if (token == null)
        {
            MesajEroare = "Cererea nu mai exista sau a fost deja rezolvata.";
            return RedirectToPage();
        }

        var detalii = CitesteDetaliiAprobare(token.Value);
        _db.UserTokens.Remove(token);
        await _db.SaveChangesAsync();

        var esteAnulare = detalii?.IdAdministrator == currentUser.Id;
        Mesaj = esteAnulare
            ? $"Cererea pentru {detaliiActiune.Value.Descriere} lui '{user.Nume}' a fost anulata."
            : $"Cererea pentru {detaliiActiune.Value.Descriere} lui '{user.Nume}' a fost refuzata.";

        return RedirectToPage();
    }

    private async Task<bool> AreConfirmareAltAdminAsync(
        Utilizator utilizatorVizate,
        Utilizator adminCurent,
        string numeActiune,
        string descriereActiune)
    {
        var token = await _db.UserTokens.FirstOrDefaultAsync(t =>
            t.UserId == utilizatorVizate.Id &&
            t.LoginProvider == AprobareProvider &&
            t.Name == numeActiune);

        if (token == null)
        {
            if (!await ExistaAltAdministratorAsync(adminCurent.Id))
            {
                MesajEroare = "Pentru aceasta actiune este nevoie de cel putin doi administratori.";
                return false;
            }

            _db.UserTokens.Add(new IdentityUserToken<int>
            {
                UserId = utilizatorVizate.Id,
                LoginProvider = AprobareProvider,
                Name = numeActiune,
                Value = ScrieDetaliiAprobare(adminCurent)
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                Mesaj = $"Cererea pentru {descriereActiune} lui '{utilizatorVizate.Nume}' a fost deja înregistrată de alt administrator.";
                return false;
            }

            Mesaj = $"Cererea pentru {descriereActiune} lui '{utilizatorVizate.Nume}' a fost inregistrata. Un alt administrator trebuie sa confirme.";
            return false;
        }

        var detalii = CitesteDetaliiAprobare(token.Value);
        if (detalii == null)
        {
            _db.UserTokens.Remove(token);
            await _db.SaveChangesAsync();
            MesajEroare = "Cererea de aprobare existenta era invalida si a fost anulata. Incearca din nou.";
            return false;
        }

        if (detalii.IdAdministrator == adminCurent.Id)
        {
            MesajEroare = $"Cererea pentru {descriereActiune} lui '{utilizatorVizate.Nume}' a fost deja initiata de tine. Este nevoie de confirmarea altui administrator.";
            return false;
        }

        _db.UserTokens.Remove(token);
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<bool> ExistaAltAdministratorAsync(int idAdminCurent)
    {
        var idRoleAdmin = await _db.Roles
            .Where(r => r.Name == "Administrator")
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync();

        return idRoleAdmin.HasValue && await _db.UserRoles
            .AnyAsync(ur => ur.RoleId == idRoleAdmin.Value && ur.UserId != idAdminCurent);
    }

    private async Task StergeAprobariUtilizatorAsync(int idUtilizator, params string[] numeActiuni)
    {
        var tokens = await _db.UserTokens
            .Where(t => t.UserId == idUtilizator &&
                        t.LoginProvider == AprobareProvider &&
                        numeActiuni.Contains(t.Name))
            .ToListAsync();

        if (tokens.Count > 0)
        {
            _db.UserTokens.RemoveRange(tokens);
            await _db.SaveChangesAsync();
        }
    }

    private static (string NumeToken, string Descriere)? MapeazaActiuneAprobare(string? actiune)
    {
        return actiune?.ToLowerInvariant() switch
        {
            "promovare" => (AprobarePromovare, "promovarea"),
            "stergere" => (AprobareStergere, "stergerea"),
            _ => null
        };
    }

    private static string IdentityErrors(IdentityResult result)
    {
        return string.Join(" ", result.Errors.Select(e => e.Description));
    }

    private static string ScrieDetaliiAprobare(Utilizator admin)
    {
        return JsonSerializer.Serialize(new DetaliiAprobare(admin.Id, admin.Nume, DateTime.UtcNow));
    }

    private static DetaliiAprobare? CitesteDetaliiAprobare(string? valoare)
    {
        if (string.IsNullOrWhiteSpace(valoare))
            return null;

        try
        {
            return JsonSerializer.Deserialize<DetaliiAprobare>(valoare);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record DetaliiAprobare(int IdAdministrator, string NumeAdministrator, DateTime DataCerere);
}