using HistoryConnect.Data;
using HistoryConnect.Models;
using HistoryConnect.Pages.Cont.LogareInregistrare;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore; 
using System.Data;
using System.Text.Json;

namespace HistoryConnect.Pages.Cont.LogareInregistrare.Email;

public class ConfirmaEmailModel : PageModel
{
    private readonly UserManager<Utilizator> _userManager;
    private readonly ILogger<ConfirmaEmailModel> _logger;
    private readonly AppDbContext _db;
    private readonly ITimeLimitedDataProtector _protector;

    public ConfirmaEmailModel(
        UserManager<Utilizator> userManager,
        ILogger<ConfirmaEmailModel> logger,
        AppDbContext db,
        IDataProtectionProvider dataProtectionProvider)
    {
        _userManager = userManager;
        _logger      = logger;
        _db          = db;

        _protector = dataProtectionProvider
            .CreateProtector("HistoryConnect.PendingRegistration")
            .ToTimeLimitedDataProtector();
    }

    public string? Mesaj          { get; set; }
    public string? MesajEroare    { get; set; }
    public string? SuccessMessage { get; set; }

    [BindProperty]
    public string? Email { get; set; }

    public bool ConfirmatCuSucces { get; set; }
    public bool IsAsteptare       { get; set; }

    public async Task<IActionResult> OnGetAsync(string? email, string? token)
    {
        if (!string.IsNullOrWhiteSpace(email) && token == null)
        {
            IsAsteptare = true;
            Email = email;
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(token))
            return await ConfirmaInregistrareAsync(token);

        MesajEroare = "Link de confirmare invalid sau expirat.";
        return Page();
    }

    public async Task<IActionResult> OnPostTrimiteReconfirmareAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            IsAsteptare = true;
            MesajEroare = "Adresa de email lipsește.";
            return Page();
        }

        var utilizator = await _userManager.FindByEmailAsync(Email);
        if (utilizator != null && await _userManager.IsEmailConfirmedAsync(utilizator))
        {
            ConfirmatCuSucces = true;
            Mesaj = "✓ Contul tău este deja confirmat! Poți să te autentifici.";
            return Page();
        }

        if (utilizator != null)
        {
            IsAsteptare = true;
            MesajEroare = "Emailul tău nu este confirmat. Te rugăm să te înregistrezi din nou.";
            return Page();
        }

        IsAsteptare = true;
        MesajEroare = "Sesiunea a expirat. Te rugăm să te înregistrezi din nou " +
                      "pentru a primi un link nou de confirmare.";
        return Page();
    }

    private async Task<IActionResult> ConfirmaInregistrareAsync(string token)
    {
        PendingRegistrationData date;
        try
        {
            var payload = _protector.Unprotect(token);
            date = JsonSerializer.Deserialize<PendingRegistrationData>(payload)
                   ?? throw new InvalidOperationException("Payload deserializat ca null.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token de confirmare invalid sau expirat.");
            MesajEroare = "Link de confirmare invalid sau expirat. " +
                          "Înregistrează-te din nou — emailul tău este liber.";
            return Page();
        }

        Email = date.Email;

        if (await _userManager.FindByEmailAsync(date.Email) != null)
        {
            ConfirmatCuSucces = true;
            Mesaj = "✓ Contul este deja creat! Poți să te autentifici.";
            return Page();
        }

        if (await _userManager.FindByNameAsync(date.NumeUtilizator) != null)
        {
            MesajEroare = "Numele de utilizator a fost rezervat de altcineva între timp. " +
                          "Înregistrează-te din nou cu un alt nume.";
            return Page();
        }

        var avatarDefault = await _db.Avatare
            .OrderBy(a => a.NivelNecesar)
            .ThenBy(a => a.IdAvatar)
            .FirstOrDefaultAsync<Avatar>();

        var user = new Utilizator
        {
            UserName         = date.NumeUtilizator,
            Email            = date.Email,
            Nume             = date.NumeComplet,
            DataInregistrare = DateTime.UtcNow,
            EmailConfirmed   = true,
            PasswordHash     = date.ParolaHash,
            IdAvatar         = avatarDefault?.IdAvatar
        };

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            var rezultatCreate = await _userManager.CreateAsync(user);
            if (!rezultatCreate.Succeeded)
            {
                await tx.RollbackAsync();
                if (await _userManager.FindByEmailAsync(date.Email) != null)
                {
                    ConfirmatCuSucces = true;
                    Mesaj = "✓ Contul este deja creat! Poți să te autentifici.";
                    return Page();
                }

                MesajEroare = string.Join(" ", rezultatCreate.Errors.Select(e => e.Description));
                _logger.LogError("Eroare la crearea contului pentru {Email}: {Errors}",
                    date.Email, MesajEroare);
                return Page();
            }

            string rol = date.EsteAdmin ? "Administrator" : "Student";
            var rezultatRol = await _userManager.AddToRoleAsync(user, rol);

            if (!rezultatRol.Succeeded)
            {
                await tx.RollbackAsync();
                _logger.LogError("Nu s-a putut adăuga rolul {Rol} pentru {Email}", rol, date.Email);
                MesajEroare = "Nu s-a putut atribui rolul. Încearcă din nou.";
                return Page();
            }

            if (date.EsteAdmin)
                _db.Administratori.Add(new Administrator { IdUtilizator = user.Id, DataNumire = DateTime.UtcNow });
            else
                _db.Studenti.Add(new Student { IdUtilizator = user.Id, XpTotal = 0, NivelCurent = 1 });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            ConfirmatCuSucces = true;
            Mesaj = "✓ Contul tău a fost creat și confirmat! Poți să te autentifici acum.";
            _logger.LogInformation("Cont nou creat: {NumeUtilizator}, Rol: {Rol}",
                user.UserName, rol);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Eroare la salvarea datelor pentru {Email}.", date.Email);
            try { await tx.RollbackAsync(); } catch { }
            MesajEroare = "Eroare la salvarea datelor. Încearcă din nou.";
        }

        return Page();
    }
}