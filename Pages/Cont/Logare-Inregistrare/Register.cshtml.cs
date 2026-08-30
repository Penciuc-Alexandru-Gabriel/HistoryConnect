using HistoryConnect.Models;
using HistoryConnect.Servicii;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HistoryConnect.Pages.Cont.LogareInregistrare;

[EnableRateLimiting("RegisterLimit")]
public class RegisterModel : PageModel
{
    private readonly UserManager<Utilizator>         _userManager;
    private readonly ILogger<RegisterModel>    _logger;
    private readonly ServiciuEmail             _serviciuEmail;
    private readonly IPasswordHasher<Utilizator>     _passwordHasher;
    private readonly string?                   _codAdmin;
    private readonly ITimeLimitedDataProtector _protector;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Numele complet este obligatoriu")]
        [MaxLength(100, ErrorMessage = "Numele nu poate depasi 100 de caractere")]
        [Display(Name = "Nume Complet")]
        public string NumeComplet { get; set; } = string.Empty;
        [Required(ErrorMessage = "Numele de utilizator este obligatoriu")]
        [MaxLength(50, ErrorMessage = "Numele de utilizator nu poate depasi 50 de caractere")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$",
            ErrorMessage = "Doar litere, cifre si underscore (_) sunt permise")]
        [Display(Name = "Nume Utilizator")]
        public string NumeUtilizator { get; set; } = string.Empty;
        [Required(ErrorMessage = "Emailul este obligatoriu")]
        [MaxLength(254, ErrorMessage = "Emailul nu poate depasi 254 de caractere")]
        [EmailAddress(ErrorMessage = "Format de email invalid")]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
            ErrorMessage = "Format de email invalid")]
        public string Email { get; set; } = string.Empty;
        [Required(ErrorMessage = "Parola este obligatorie")]
        [StringLength(100, MinimumLength = 6,
            ErrorMessage = "Parola trebuie sa aiba minim 6 caractere")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)[a-zA-Z\d@$!%*?&]{6,}$",
            ErrorMessage = "Parola trebuie sa contina litera mare, litera mica si o cifra")]
        [Display(Name = "Parola")]
        public string Parola { get; set; } = string.Empty;
        [Required(ErrorMessage = "Confirmarea parolei este obligatorie")]
        [Compare("Parola", ErrorMessage = "Parolele nu se potrivesc")]
        [Display(Name = "Confirma Parola")]
        public string ConfirmaParola { get; set; } = string.Empty;

        [MaxLength(100, ErrorMessage = "Codul de acces nu poate depasi 100 de caractere")]
        [Display(Name = "Cod de Acces")]
        public string? CodAdmin { get; set; }
    }

    public RegisterModel(
        UserManager<Utilizator>       userManager,
        ILogger<RegisterModel>  logger,
        IConfiguration          configuration,
        IDataProtectionProvider dataProtectionProvider,
        IPasswordHasher<Utilizator>   passwordHasher,
        ServiciuEmail           serviciuEmail)
    {
        _userManager    = userManager;
        _logger         = logger;
        _serviciuEmail  = serviciuEmail;
        _passwordHasher = passwordHasher;
        _codAdmin       = configuration["AdminSettings:CodInvitatie"];
        _protector      = dataProtectionProvider
            .CreateProtector("HistoryConnect.PendingRegistration")
            .ToTimeLimitedDataProtector();
    }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Index");

        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(returnUrl) && !Url.IsLocalUrl(returnUrl))
            returnUrl = null;

        returnUrl ??= Url.Content("~/");
        if (!ModelState.IsValid)
            return Page();
        if (await _userManager.FindByNameAsync(Input.NumeUtilizator) != null)
        {
            ModelState.AddModelError(string.Empty, "Acest nume de utilizator este deja folosit.");
            return Page();
        }
        if (await _userManager.FindByEmailAsync(Input.Email) != null)
        {
            ModelState.AddModelError(string.Empty,
                "Acest email este deja asociat unui cont. Foloseste functia 'Am uitat parola' daca ai pierdut accesul.");
            return Page();
        }
        bool esteAdmin = !string.IsNullOrWhiteSpace(Input.CodAdmin)
                      && !string.IsNullOrWhiteSpace(_codAdmin)
                      && SecureCodEquals(Input.CodAdmin.Trim(), _codAdmin.Trim());

        var parolaHash = _passwordHasher.HashPassword(new Utilizator(), Input.Parola);
        var payload = JsonSerializer.Serialize(new PendingRegistrationData
        {
            NumeUtilizator = Input.NumeUtilizator,
            NumeComplet    = Input.NumeComplet,
            Email          = Input.Email,
            ParolaHash     = parolaHash,
            EsteAdmin      = esteAdmin
        });
        string tokenProtejat;
        try
        {
            tokenProtejat = _protector.Protect(payload, TimeSpan.FromHours(24));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Eroare la generarea tokenului. EmailHash={EmailHash}.",
                AnonymizeEmail(Input.Email));
            ModelState.AddModelError(string.Empty, "Eroare interna. Incearca din nou.");
            return Page();
        }
        string linkConfirmare;
        try
        {
            var link = Url.Page(
                "/Cont/Logare-Inregistrare/email/ConfirmaEmail",
                pageHandler: null,
                values: new { token = tokenProtejat },
                protocol: Request.Scheme);

            if (string.IsNullOrEmpty(link))
                throw new InvalidOperationException("Url.Page a returnat null sau string gol.");

            linkConfirmare = link;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Eroare la generarea URL-ului de confirmare. EmailHash={EmailHash}.",
                AnonymizeEmail(Input.Email));
            ModelState.AddModelError(string.Empty, "Eroare interna. Incearca din nou.");
            return Page();
        }

        try
        {
            await _serviciuEmail.TrimiteConfirmareAsync(Input.Email, linkConfirmare);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email de confirmare nereusit. EmailHash={EmailHash}.",
                AnonymizeEmail(Input.Email));
            ModelState.AddModelError(string.Empty,
                "Emailul de confirmare nu a putut fi trimis. Verifica adresa si incearca din nou.");
            return Page();
        }

        _logger.LogInformation(
            "Token de inregistrare generat. EmailHash={EmailHash}. Contul va fi creat dupa confirmare.",
            AnonymizeEmail(Input.Email));

        return RedirectToPage("email/ConfirmaEmail", new { email = Input.Email, returnUrl });
    }

    private static bool SecureCodEquals(string a, string b)
    {
        var hashA = SHA256.HashData(Encoding.UTF8.GetBytes(a));
        var hashB = SHA256.HashData(Encoding.UTF8.GetBytes(b));
        return CryptographicOperations.FixedTimeEquals(hashA, hashB);
    }

    private static string AnonymizeEmail(string email)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(email.ToLowerInvariant()));
        return Convert.ToHexString(hash)[..12];
    }
}

public sealed class PendingRegistrationData
{
    public string NumeUtilizator { get; set; } = string.Empty;
    public string NumeComplet    { get; set; } = string.Empty;
    public string Email          { get; set; } = string.Empty;
    public string ParolaHash     { get; set; } = string.Empty;
    public bool   EsteAdmin      { get; set; }
}