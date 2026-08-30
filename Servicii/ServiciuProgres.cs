using HistoryConnect.Data;
using HistoryConnect.Models;
using Microsoft.EntityFrameworkCore;

namespace HistoryConnect.Servicii;

public class ServiciuProgres
{
    private readonly AppDbContext _db;
    private readonly ServiciuInsigna _serviciuInsigna;

    public ServiciuProgres(AppDbContext db, ServiciuInsigna serviciuInsigna)
    {
        _db = db;
        _serviciuInsigna = serviciuInsigna;
    }
    public static int CalculeazaNivel(int xpTotal)
    {
        int nivel = 1;
        int xpNecesar = 0;

        while (true)
        {
            xpNecesar += nivel * 100;
            if (xpTotal < xpNecesar) break;
            nivel++;
            if (nivel >= 20) break; 
        }

        return nivel;
    }
public static int PragCumulativ(int nivel)
{
    int prag = 0;
    for (int i = 1; i < nivel; i++)
        prag += i * 100;
    return prag;
}
    public Task<ProgresLectie?> GetUltimaLectieCompletataAsync(int idUtilizator, TipLectie tip) =>
        _db.ProgresLectii
           .Include(p => p.Lectie)
           .Where(p => p.IdUtilizator == idUtilizator
                        && p.Completata
                        && p.Lectie!.Tip == tip)
           .OrderByDescending(p => p.DataCompletare)
           .FirstOrDefaultAsync();

    public async Task<HashSet<int>> GetLectiiCompletateAsync(int idUtilizator)
    {
        var ids = await _db.ProgresLectii
            .Where(pl => pl.IdUtilizator == idUtilizator && pl.Completata)
            .Select(pl => pl.IdLectie)
            .ToListAsync();
        return ids.ToHashSet();
    }

    public async Task FinalizeazaLectieAsync(int idUtilizator, int idLectie)
    {
        var progres = await _db.ProgresLectii
            .FirstOrDefaultAsync(pl => pl.IdUtilizator == idUtilizator
                                    && pl.IdLectie == idLectie);

        bool finalizareNoua = progres == null || !progres.Completata;

        if (progres == null)
        {
            _db.ProgresLectii.Add(new ProgresLectie
            {
                IdUtilizator = idUtilizator,
                IdLectie = idLectie,
                Completata = true,
                DataCompletare = DateTime.UtcNow
            });
        }
        else
        {
            progres.Completata = true;
            progres.DataCompletare = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        if (finalizareNoua)
            await _serviciuInsigna.VerificaSiAcordaInsigne(idUtilizator);
    }

    public async Task AnuleazaFinalizareaAsync(int idUtilizator, int idLectie)
    {
        var progres = await _db.ProgresLectii
            .FirstOrDefaultAsync(pl => pl.IdUtilizator == idUtilizator
                                    && pl.IdLectie == idLectie);
        if (progres != null)
        {
            progres.Completata = false;
            progres.DataCompletare = null;
            await _db.SaveChangesAsync();
            await _serviciuInsigna.RevocaInsignePerdute(idUtilizator);
        }
    }
}