using HistoryConnect.Data;
using HistoryConnect.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace HistoryConnect.Pages.Admin.Lectii;

[Authorize(Roles = "Administrator")]
public class EditeazaLectieModel : PageModel
{
    private readonly AppDbContext _db;
    public EditeazaLectieModel(AppDbContext db) => _db = db;

    [BindProperty] public InputLectie Input { get; set; } = new();
    public List<SelectListItem> CapitoleSelectie { get; set; } = new();
    public bool EsteEditare => Input.IdLectie > 0;

    public class InputLectie
    {
        public int IdLectie { get; set; }

        [Required(ErrorMessage = "Titlul este obligatoriu")]
        [MaxLength(200)] public string Titlu { get; set; } = "";

        [Required(ErrorMessage = "Capitolul este obligatoriu")]
        public int IdCapitol { get; set; }

        public TipLectie Tip { get; set; } = TipLectie.Istorie;

        public string? Continut { get; set; }

        [MaxLength(255)] public string? UrlImagine { get; set; }

        public int? AnEveniment { get; set; }

        [Range(1, 9999)] public int Ordine { get; set; } = 1;
    }

    public async Task<IActionResult> OnGetAsync(int id = 0)
    {
        await IncarcaCapitole();

        if (id > 0)
        {
            var lectie = await _db.Lectii.FindAsync(id);
            if (lectie == null) return NotFound();

            Input = new InputLectie
            {
                IdLectie    = lectie.IdLectie,
                Titlu       = lectie.Titlu,
                IdCapitol   = lectie.IdCapitol,
                Tip         = lectie.Tip,
                Continut    = lectie.Continut,
                UrlImagine  = lectie.UrlImagine,
                AnEveniment = lectie.AnEveniment,
                Ordine      = lectie.Ordine
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await IncarcaCapitole();

        if (!ModelState.IsValid) return Page();

        var capitolExista = await _db.Capitole.AnyAsync(c => c.IdCapitol == Input.IdCapitol);
        if (!capitolExista)
        {
            ModelState.AddModelError("Input.IdCapitol", "Capitol invalid.");
            return Page();
        }

        if (Input.IdLectie == 0)
        {
            _db.Lectii.Add(new Lectie
            {
                IdCapitol   = Input.IdCapitol,
                Titlu       = Input.Titlu.Trim(),
                Tip         = Input.Tip,
                Continut    = Input.Continut?.Trim(),
                UrlImagine  = Input.UrlImagine?.Trim(),
                AnEveniment = Input.AnEveniment,
                Ordine      = Input.Ordine
            });
        }
        else
        {
            var lectie = await _db.Lectii.FindAsync(Input.IdLectie);
            if (lectie == null) return NotFound();

            lectie.IdCapitol   = Input.IdCapitol;
            lectie.Titlu       = Input.Titlu.Trim();
            lectie.Tip         = Input.Tip;
            lectie.Continut    = Input.Continut?.Trim();
            lectie.UrlImagine  = Input.UrlImagine?.Trim();
            lectie.AnEveniment = Input.AnEveniment;
            lectie.Ordine      = Input.Ordine;
        }

        await _db.SaveChangesAsync();
        TempData["Mesaj"] = Input.IdLectie == 0 ? "Lecție adăugată cu succes!" : "Lecție actualizată.";
        return RedirectToPage("/Admin/Lectii/AdminLectii");
    }

    private async Task IncarcaCapitole()
    {
        var perioade = await _db.Perioade
            .Include(p => p.Capitole)
            .OrderBy(p => p.Inceput)
            .ToListAsync();

        CapitoleSelectie = perioade
            .SelectMany(p => p.Capitole.OrderBy(c => c.NrOrdine)
                .Select(c => new SelectListItem(
                    $"{p.Nume} › {c.Titlu}",
                    c.IdCapitol.ToString())))
            .ToList();
    }
}