using HistoryConnect.Data;
using HistoryConnect.Models;
using Microsoft.EntityFrameworkCore;

namespace HistoryConnect.Servicii;

public class ServiciuInsigna
{
    private readonly AppDbContext _db;

    public ServiciuInsigna(AppDbContext db)
    {
        _db = db;
    }
    public async Task VerificaSiAcordaInsigne(int idUtilizator)
    {
        var student = await _db.Studenti
            .FirstOrDefaultAsync(s => s.IdUtilizator == idUtilizator);

        if (student == null) return;

        var lectiiCompletate = await _db.ProgresLectii
            .CountAsync(p => p.IdUtilizator == idUtilizator && p.Completata);

        var quizuriCompletate = await _db.ProgresQuizuri
            .Where(pq => pq.IdUtilizator == idUtilizator)
            .Select(pq => pq.IdQuiz)
            .Distinct()
            .CountAsync();

        var totalLectii = await _db.Lectii.CountAsync();

        var toateInsignele = await _db.Insigne.ToListAsync();

        var insigneDetinute = await _db.CabinetInsigne
            .Where(ci => ci.IdUtilizator == idUtilizator)
            .Select(ci => ci.IdInsigna)
            .ToHashSetAsync();

        var deAcordat = toateInsignele
            .Where(i => !insigneDetinute.Contains(i.IdInsigna)
                     && EsteIndeplinita(i, student, lectiiCompletate, quizuriCompletate, totalLectii))
            .ToList();

        foreach (var insigna in deAcordat)
        {
            _db.CabinetInsigne.Add(new CabinetInsigne
            {
                IdUtilizator  = idUtilizator,
                IdInsigna     = insigna.IdInsigna,
                DataObtinere  = DateTime.UtcNow
            });
        }

        if (deAcordat.Count > 0)
            await _db.SaveChangesAsync();
    }

    private static bool EsteIndeplinita(
        Insigna insigna,
        Student student,
        int lectiiCompletate,
        int quizuriCompletate,
        int totalLectii)
    {
        return insigna.TipConditie switch
        {
            TipConditieInsigna.LectiiCompletate  => lectiiCompletate >= insigna.PragConditie,
            TipConditieInsigna.QuizuriCompletate => quizuriCompletate >= insigna.PragConditie,
            TipConditieInsigna.ToateLectiile     => totalLectii > 0 && lectiiCompletate >= totalLectii,
            TipConditieInsigna.XpAtins           => student.XpTotal >= insigna.PragConditie,
            TipConditieInsigna.NivelAtins        => student.NivelCurent >= insigna.PragConditie,
            _                                    => false
        };
    }
    public async Task RevocaInsignePerdute(int idUtilizator)
    {
        var student = await _db.Studenti
            .FirstOrDefaultAsync(s => s.IdUtilizator == idUtilizator);

        if (student == null) return;

        var lectiiCompletate = await _db.ProgresLectii
            .CountAsync(p => p.IdUtilizator == idUtilizator && p.Completata);

        var quizuriCompletate = await _db.ProgresQuizuri
            .Where(pq => pq.IdUtilizator == idUtilizator)
            .Select(pq => pq.IdQuiz)
            .Distinct()
            .CountAsync();

        var totalLectii = await _db.Lectii.CountAsync();

        var insigneDetinute = await _db.CabinetInsigne
            .Include(ci => ci.Insigna)
            .Where(ci => ci.IdUtilizator == idUtilizator)
            .ToListAsync();

        var deRevocat = insigneDetinute
            .Where(ci => ci.Insigna != null &&
                         !EsteIndeplinita(ci.Insigna, student, lectiiCompletate, quizuriCompletate, totalLectii))
            .ToList();

        if (deRevocat.Count > 0)
        {
            _db.CabinetInsigne.RemoveRange(deRevocat);
            await _db.SaveChangesAsync();
        }
    }
}