using HistoryConnect.Models;
using HistoryConnect.Servicii;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace HistoryConnect.Pages.Cont.LogareInregistrare;

public class LoginModel : PageModel
{
    private readonly SignInManager<Utilizator> _signInManager;
    private readonly UserManager<Utilizator> _userManager;
    private readonly ILogger<LoginModel> _logger;
    private readonly ServiciuEmail _serviciuEmail;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public ForgotPasswordInputModel ForgotPasswordInput { get; set; } = new();

    [BindProperty]
    public ResetPasswordInputModel ResetPasswordInput { get; set; } = new();

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public string? SuccessMessage { get; set; }

    public bool AfisareFormularParolaUitata { get; set; }

    public bool AfisareFormularResetare { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Emailul este obligatoriu")]
        [EmailAddress(ErrorMessage = "Format de email invalid")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Parola este obligatorie")]
        [DataType(DataType.Password)]
        public string Parola { get; set; } = string.Empty;

        [Display(Name = "Tine-ma minte")]
        public bool TineMaMinte { get; set; }
    }

    public class ForgotPasswordInputModel
    {
        [Required(ErrorMessage = "Emailul este obligatoriu")]
        [EmailAddress(ErrorMessage = "Format de email invalid")]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordInputModel
    {
        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string ResetCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Parola noua este obligatorie")]
        [StringLength(100, MinimumLength = 6,
            ErrorMessage = "Parola trebuie sa aiba minim 6 caractere")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)[a-zA-Z\d@$!%*?&]{6,}$",
            ErrorMessage = "Parola trebuie sa contina litera mare, litera mica si o cifra")]
        [DataType(DataType.Password)]
        public string ParolaNoua { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirmarea parolei este obligatorie")]
        [Compare("ParolaNoua", ErrorMessage = "Parolele nu se potrivesc")]
        [DataType(DataType.Password)]
        public string ConfirmaParolaNoua { get; set; } = string.Empty;
    }

    public LoginModel(
        SignInManager<Utilizator> signInManager,
        UserManager<Utilizator> userManager,
        ILogger<LoginModel> logger,
        ServiciuEmail serviciuEmail)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
        _serviciuEmail = serviciuEmail;
    }

    public IActionResult OnGet(
        string? returnUrl = null,
        bool forgotPassword = false,
        bool resetPassword = false,
        string? email = null,
        string? resetCode = null)
    {
        if (resetPassword)
        {
            AfisareFormularResetare = true;
            ResetPasswordInput.Email = email ?? string.Empty;
            ResetPasswordInput.ResetCode = resetCode ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(resetCode))
                ModelState.AddModelError(string.Empty, "Linkul de resetare este invalid.");

            return Page();
        }

        if (forgotPassword)
        {
            AfisareFormularParolaUitata = true;
            return Page();
        }

        if (User.Identity?.IsAuthenticated ?? false)
            return RedirectToPage("/Index");

        if (!string.IsNullOrEmpty(ErrorMessage))
            ModelState.AddModelError(string.Empty, ErrorMessage);

        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostTrimiteResetareAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        ModelState.Clear();

        if (!TryValidateModel(ForgotPasswordInput, nameof(ForgotPasswordInput)))
        {
            AfisareFormularParolaUitata = true;
            return Page();
        }

        var utilizator = await _userManager.FindByEmailAsync(ForgotPasswordInput.Email);
        if (utilizator != null && await _userManager.IsEmailConfirmedAsync(utilizator))
        {
            var tokenResetare = await _userManager.GeneratePasswordResetTokenAsync(utilizator);
            var tokenCodificat = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(tokenResetare));
            var linkResetare = Url.Page(
                "/Cont/Logare-Inregistrare/Login",
                pageHandler: null,
                values: new
                {
                    resetPassword = true,
                    email = ForgotPasswordInput.Email,
                    resetCode = tokenCodificat,
                    returnUrl
                },
                protocol: Request.Scheme);

            if (!string.IsNullOrWhiteSpace(linkResetare))
            {
                try
                {
                    await _serviciuEmail.TrimiteResetareParolaAsync(ForgotPasswordInput.Email, linkResetare);
                    _logger.LogInformation("Email de resetare trimis pentru: {Email}", ForgotPasswordInput.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Eroare la trimiterea emailului de resetare pentru {Email}.", ForgotPasswordInput.Email);
                }
            }
        }

        SuccessMessage = "Un link de resetare a fost trimis la adresa ta de email.";
        return Page();
    }

    public async Task<IActionResult> OnPostReseteazaParolaAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        ModelState.Clear();

        if (!TryValidateModel(ResetPasswordInput, nameof(ResetPasswordInput)))
        {
            AfisareFormularResetare = true;
            return Page();
        }

        var utilizator = await _userManager.FindByEmailAsync(ResetPasswordInput.Email);
        if (utilizator == null)
        {
            SuccessMessage = "Parola a fost resetata. Te poti autentifica.";
            return Page();
        }

        try
        {
            var tokenResetare = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(ResetPasswordInput.ResetCode));
            var rezultat = await _userManager.ResetPasswordAsync(utilizator, tokenResetare, ResetPasswordInput.ParolaNoua);

            if (rezultat.Succeeded)
            {
                SuccessMessage = "Parola a fost resetata. Te poti autentifica.";
                return Page();
            }

            AfisareFormularResetare = true;
            foreach (var eroare in rezultat.Errors)
                ModelState.AddModelError(string.Empty, eroare.Description);
        }
        catch (FormatException)
        {
            AfisareFormularResetare = true;
            ModelState.AddModelError(string.Empty, "Linkul de resetare este invalid.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ReturnUrl = returnUrl;
        ModelState.Clear();

        if (!TryValidateModel(Input, nameof(Input)))
            return Page();

        var utilizator = await _userManager.FindByEmailAsync(Input.Email);
        if (utilizator == null)
        {
            ModelState.AddModelError(string.Empty, "Email sau parola incorecte.");
            return Page();
        }

        if (!await _userManager.IsEmailConfirmedAsync(utilizator))
        {
            var parolaCorecta = await _userManager.CheckPasswordAsync(utilizator, Input.Parola);
            if (!parolaCorecta)
                await _userManager.AccessFailedAsync(utilizator);

            var mesaj = parolaCorecta
                ? "Trebuie sa confirmi emailul inainte de autentificare. Verifica inboxul."
                : "Email sau parola incorecte.";

            ModelState.AddModelError(string.Empty, mesaj);
            return Page();
        }

        var rezultat = await _signInManager.PasswordSignInAsync(
            utilizator.UserName!,
            Input.Parola,
            Input.TineMaMinte,
            lockoutOnFailure: true);

        if (rezultat.Succeeded)
        {
            _logger.LogInformation("Utilizatorul {Email} s-a logat cu succes.", Input.Email);

            var esteAdmin = await _userManager.IsInRoleAsync(utilizator, "Administrator");
            if (esteAdmin && returnUrl == Url.Content("~/"))
                return RedirectToPage("/Admin/AdminDashboard");

            return LocalRedirect(returnUrl);
        }

        if (rezultat.IsLockedOut)
        {
            _logger.LogWarning("Contul utilizatorului {Email} a fost blocat.", Input.Email);
            ModelState.AddModelError(string.Empty, "Contul tau a fost blocat temporar. Incearca din nou mai tarziu.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Email sau parola incorecte.");
        }

        return Page();
    }
}