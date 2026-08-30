using HistoryConnect.Data;
using HistoryConnect.Models;
using Microsoft.EntityFrameworkCore;

namespace HistoryConnect.Servicii;

public class ServiciuLectie
{
    private readonly AppDbContext _db;

    public ServiciuLectie(AppDbContext db)
    {
        _db = db;
    }

    public Task<List<Perioada>> GetToatePerioadeleCuCapitoleAsync() =>
        _db.Perioade
           .Include(p => p.Capitole)
           .OrderBy(p => p.Inceput)
           .ToListAsync();

    public Task<Perioada?> GetPerioadaCuLectiiAsync(int idPerioada) =>
        _db.Perioade
           .Include(p => p.Capitole)
               .ThenInclude(c => c.Lectii)
           .FirstOrDefaultAsync(p => p.IdPerioada == idPerioada);

    public Task<Lectie?> GetLectieCompletaAsync(int idLectie) =>
        _db.Lectii
           .Include(l => l.Capitol)
           .Include(l => l.Quizuri)
               .ThenInclude(q => q.Intrebari)
                   .ThenInclude(i => i.Variante)
           .FirstOrDefaultAsync(l => l.IdLectie == idLectie);

    public async Task<int?> GetPrimulCapitolIdAsync(int idPerioada)
    {
        var capitol = await _db.Capitole
            .Where(c => c.IdPerioada == idPerioada)
            .OrderBy(c => c.NrOrdine)
            .FirstOrDefaultAsync();
        return capitol?.IdCapitol;
    }

    public async Task<int?> GetPrimulCapitolIdLiberGlobalAsync()
    {
        var primaPerioada = await _db.Perioade
            .OrderBy(p => p.Inceput)
            .FirstOrDefaultAsync();

        return primaPerioada == null ? null : await GetPrimulCapitolIdAsync(primaPerioada.IdPerioada);
    }

    public Task<List<Lectie>> GetLectiiOrdonateAsync(TipLectie tip) =>
        _db.Lectii
           .Include(l => l.Capitol)
               .ThenInclude(c => c!.Perioada)
           .Where(l => l.Tip == tip)
           .OrderBy(l => l.Capitol!.Perioada!.Inceput)
           .ThenBy(l => l.Capitol!.NrOrdine)
           .ThenBy(l => l.Ordine)
           .ThenBy(l => l.IdLectie)
           .ToListAsync();


    public Lectie? GetUrmatoareaLectieNeterminata(
        List<Lectie> lectiiOrdonate, int idUltimaLectieCompletata, HashSet<int> lectiiCompletateIds)
    {
        var indexUltima = lectiiOrdonate.FindIndex(l => l.IdLectie == idUltimaLectieCompletata);
        if (indexUltima < 0) return null;

        return lectiiOrdonate
            .Skip(indexUltima + 1)
            .FirstOrDefault(l => !lectiiCompletateIds.Contains(l.IdLectie));
    }

    public bool EsteLectieAccesibila(int? idCapitolLectie, int? primulCapitolIdLiber, bool esteLogat) =>
        esteLogat || (idCapitolLectie.HasValue && idCapitolLectie == primulCapitolIdLiber);

    public async Task<bool> EsteCapitolLiberAsync(int? idCapitol)
    {
        var primulCapitolIdLiber = await GetPrimulCapitolIdLiberGlobalAsync();
        return EsteLectieAccesibila(idCapitol, primulCapitolIdLiber, esteLogat: false);
    }
}