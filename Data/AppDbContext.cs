using HistoryConnect.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HistoryConnect.Data;

public class AppDbContext : IdentityDbContext<Utilizator, IdentityRole<int>, int>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Student> Studenti { get; set; }
    public DbSet<Administrator> Administratori { get; set; }
    public DbSet<Avatar> Avatare { get; set; }
    public DbSet<Perioada> Perioade { get; set; }
    public DbSet<Capitol> Capitole { get; set; }
    public DbSet<Lectie> Lectii { get; set; }
    public DbSet<Quiz> Quizuri { get; set; }
    public DbSet<Intrebare> Intrebari { get; set; }
    public DbSet<VariantaRaspuns> VarianteRaspuns { get; set; }
    public DbSet<Insigna> Insigne { get; set; }
    public DbSet<ProgresLectie> ProgresLectii { get; set; }
    public DbSet<ProgresQuiz> ProgresQuizuri { get; set; }
    public DbSet<IstoricRaspunsuri> IstoricRaspunsuri { get; set; }
    public DbSet<CabinetInsigne> CabinetInsigne { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<IdentityRole<int>>().ToTable("roluri");
        builder.Entity<IdentityUserRole<int>>(e =>
        {
            e.ToTable("utilizator_roluri");
        });

        builder.Entity<IdentityUserClaim<int>>().ToTable("utilizator_claims");
        builder.Entity<IdentityUserLogin<int>>().ToTable("utilizator_loginuri");
        builder.Entity<IdentityRoleClaim<int>>().ToTable("rol_claims");
        builder.Entity<IdentityUserToken<int>>().ToTable("utilizator_tokeni");

        // ── Utilizator ─────────────────────────────────────────────────
        builder.Entity<Utilizator>(e =>
        {
            e.ToTable("utilizator");
            e.Property(u => u.Id).HasColumnName("id_utilizator");
            e.Property(u => u.Email).HasColumnName("email").HasMaxLength(256).IsRequired();
            e.Property(u => u.PasswordHash).HasColumnName("parola").IsRequired();
            e.Property(u => u.Nume).HasColumnName("nume").HasMaxLength(100).IsRequired();
            e.Property(u => u.DataInregistrare).HasColumnName("data_inregistrare")
             .HasDefaultValueSql("now()");
            e.Property(u => u.IdAvatar).HasColumnName("id_avatar");

            // Cheie semantica
            e.HasIndex(u => u.Email).IsUnique();

            e.HasOne(u => u.Avatar)
             .WithMany()
             .HasForeignKey(u => u.IdAvatar)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Avatar ───────────────────────────────────────────────
        builder.Entity<Avatar>(e =>
        {
            e.ToTable("avatar", t =>
            {
                t.HasCheckConstraint("CK_avatar_nivel_necesar_valid", "nivel_necesar >= 1 AND nivel_necesar <= 20");
            });
            e.HasKey(a => a.IdAvatar);
            e.Property(a => a.IdAvatar).HasColumnName("id_avatar");
            e.Property(a => a.NumeAvatar).HasColumnName("nume_avatar").HasMaxLength(100).IsRequired();
            e.Property(a => a.UrlPoza).HasColumnName("url_poza").HasMaxLength(255).IsRequired();
            e.Property(a => a.NivelNecesar).HasColumnName("nivel_necesar").HasDefaultValue(1);

            // Cheie semantica
            e.HasIndex(a => a.NumeAvatar).IsUnique();
        });

        builder.Entity<Avatar>().HasData(
            new Avatar { IdAvatar = 1, NumeAvatar = "Default",         UrlPoza = "Poze/avatar_default.png",  NivelNecesar = 1  },
            new Avatar { IdAvatar = 2, NumeAvatar = "Burebista",       UrlPoza = "Poze/Burebista.png",       NivelNecesar = 3  },
            new Avatar { IdAvatar = 3, NumeAvatar = "Decebal",         UrlPoza = "Poze/Decebal.png",         NivelNecesar = 5  },
            new Avatar { IdAvatar = 4, NumeAvatar = "Traian",          UrlPoza = "Poze/Traian.png",          NivelNecesar = 7  },
            new Avatar { IdAvatar = 5, NumeAvatar = "Vlad Tepes",      UrlPoza = "Poze/Vlad_Tepes.png",      NivelNecesar = 10 },
            new Avatar { IdAvatar = 6, NumeAvatar = "Stefan cel Mare", UrlPoza = "Poze/Stefan_cel_Mare.png", NivelNecesar = 13 },
            new Avatar { IdAvatar = 7, NumeAvatar = "Mihai Viteazul",  UrlPoza = "Poze/Mihai_Viteazu.png",   NivelNecesar = 15 }
        );

        // ── Student ──────────────────────────────────────────────
        builder.Entity<Student>(e =>
        {
            e.ToTable("student", t =>
            {
                t.HasCheckConstraint("CK_student_nivel_curent_min_1", "nivel_curent >= 1");
                t.HasCheckConstraint("CK_student_xp_total_non_negative", "xp_total >= 0");
            });
            e.HasKey(s => s.IdUtilizator);
            e.Property(s => s.IdUtilizator).HasColumnName("id_utilizator");
            e.Property(s => s.XpTotal).HasColumnName("xp_total").HasDefaultValue(0);
            e.Property(s => s.NivelCurent).HasColumnName("nivel_curent").HasDefaultValue(1);

            e.HasOne(s => s.Utilizator)
             .WithOne(u => u.Student)
             .HasForeignKey<Student>(s => s.IdUtilizator)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Administrator ─────────────────────────────────────────
        builder.Entity<Administrator>(e =>
        {
            e.ToTable("administrator");
            e.HasKey(a => a.IdUtilizator);
            e.Property(a => a.IdUtilizator).HasColumnName("id_utilizator");
            e.Property(a => a.DataNumire).HasColumnName("data_numire").IsRequired();

            e.HasOne(a => a.Utilizator)
             .WithOne(u => u.Administrator)
             .HasForeignKey<Administrator>(a => a.IdUtilizator)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Perioada ─────────────────────────────────────────────
        builder.Entity<Perioada>(e =>
        {
            e.ToTable("perioada", t =>
            {
                t.HasCheckConstraint(
                    "CK_perioada_interval_valid",
                    "inceput IS NULL OR sfarsit IS NULL OR sfarsit > inceput"
                );
            });
            e.HasKey(p => p.IdPerioada);
            e.Property(p => p.IdPerioada).HasColumnName("id_perioada");
            e.Property(p => p.Nume).HasColumnName("nume").HasMaxLength(150).IsRequired();
            e.Property(p => p.Descriere).HasColumnName("descriere").HasMaxLength(500);
            e.Property(p => p.Inceput).HasColumnName("inceput");
            e.Property(p => p.Sfarsit).HasColumnName("sfarsit");
            e.Property(p => p.UrlImagine).HasColumnName("url_imagine").HasMaxLength(255);

            // Cheie semantica
            e.HasIndex(p => p.Nume).IsUnique();
        });

        // ── Capitol ──────────────────────────────────────────────
        builder.Entity<Capitol>(e =>
        {
            e.ToTable("capitol", t =>
            {
                t.HasCheckConstraint("CK_capitol_nr_ordine_min_1", "nr_ordine >= 1");
            });
            e.HasKey(c => c.IdCapitol);
            e.Property(c => c.IdCapitol).HasColumnName("id_capitol");
            e.Property(c => c.IdPerioada).HasColumnName("id_perioada");
            e.Property(c => c.Titlu).HasColumnName("titlu").HasMaxLength(120).IsRequired();
            e.Property(c => c.NrOrdine).HasColumnName("nr_ordine").HasDefaultValue(1);

            // Cheie semantica
            e.HasIndex(c => c.Titlu).IsUnique();

            e.HasOne(c => c.Perioada)
             .WithMany(p => p.Capitole)
             .HasForeignKey(c => c.IdPerioada)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Lectie ───────────────────────────────────────────────
        builder.Entity<Lectie>(e =>
        {
            e.ToTable("lectie", t =>
            {
                t.HasCheckConstraint("CK_lectie_ordine_min_1", "ordine >= 1");
                t.HasCheckConstraint("CK_lectie_tip_valid", "tip IN ('Istorie', 'Traditii')");
            });
            e.HasKey(l => l.IdLectie);
            e.Property(l => l.IdLectie).HasColumnName("id_lectie");
            e.Property(l => l.IdCapitol).HasColumnName("id_capitol");
            e.Property(l => l.Titlu).HasColumnName("titlu").HasMaxLength(200).IsRequired();
            e.Property(l => l.Tip).HasColumnName("tip").HasConversion<string>();
            e.Property(l => l.Continut).HasColumnName("continut");
            e.Property(l => l.Ordine).HasColumnName("ordine").HasDefaultValue(1);
            e.Property(l => l.AnEveniment).HasColumnName("an_eveniment");
            e.Property(l => l.UrlImagine).HasColumnName("url_imagine").HasMaxLength(255);

            // Cheie semantica
            e.HasIndex(l => l.Titlu).IsUnique();

            e.HasOne(l => l.Capitol)
             .WithMany(c => c.Lectii)
             .HasForeignKey(l => l.IdCapitol)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Quiz ─────────────────────────────────────────────────
        builder.Entity<Quiz>(e =>
        {
            e.ToTable("quiz", t =>
            {
                t.HasCheckConstraint("CK_quiz_xp_completare_non_negative", "xp_completare >= 0");
                t.HasCheckConstraint("CK_quiz_timp_positive", "timp > 0");
            });
            e.HasKey(q => q.IdQuiz);
            e.Property(q => q.IdQuiz).HasColumnName("id_quiz");
            e.Property(q => q.IdLectie).HasColumnName("id_lectie");
            e.Property(q => q.Titlu).HasColumnName("titlu").HasMaxLength(200).IsRequired();
            e.Property(q => q.XpCompletare).HasColumnName("xp_completare").HasDefaultValue(0);
            e.Property(q => q.Timp).HasColumnName("timp");
            e.Property(q => q.Feedback).HasColumnName("feedback").HasMaxLength(500);

            // Cheie semantica
            e.HasIndex(q => new { q.IdLectie, q.Titlu }).IsUnique();

            e.HasOne(q => q.Lectie)
             .WithMany(l => l.Quizuri)
             .HasForeignKey(q => q.IdLectie)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Intrebare ────────────────────────────────────────────
        builder.Entity<Intrebare>(e =>
        {
            e.ToTable("intrebare", t =>
            {
                t.HasCheckConstraint("CK_intrebare_timp_min_5", "timp >= 5");
                t.HasCheckConstraint("CK_intrebare_tip_valid", "tip IN ('Grila', 'AdevaratFals')");
            });
            e.HasKey(i => i.IdIntrebare);
            e.Property(i => i.IdIntrebare).HasColumnName("id_intrebare");
            e.Property(i => i.IdQuiz).HasColumnName("id_quiz");
            e.Property(i => i.Text).HasColumnName("text").HasMaxLength(500).IsRequired();
            e.Property(i => i.Tip).HasColumnName("tip").HasConversion<string>();
            e.Property(i => i.Feedback).HasColumnName("feedback").HasMaxLength(500).IsRequired();
            e.Property(i => i.Timp).HasColumnName("timp").IsRequired();
            e.Property(i => i.UrlImagine).HasColumnName("url_imagine").HasMaxLength(255);

            // Cheie semantica
            e.HasIndex(i => new { i.IdQuiz, i.Text }).IsUnique();

            e.HasOne(i => i.Quiz)
             .WithMany(q => q.Intrebari)
             .HasForeignKey(i => i.IdQuiz)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── VariantaRaspuns ──────────────────────────────────────
        builder.Entity<VariantaRaspuns>(e =>
        {
            e.ToTable("varianta_raspuns", t =>
            {
                t.HasCheckConstraint("CK_varianta_raspuns_punctaj_non_negative", "punctaj >= 0");
            });
            e.HasKey(v => v.IdVarianta);
            e.Property(v => v.IdVarianta).HasColumnName("id_varianta");
            e.Property(v => v.IdIntrebare).HasColumnName("id_intrebare");
            e.Property(v => v.Text).HasColumnName("text").HasMaxLength(200).IsRequired();
            e.Property(v => v.Corect).HasColumnName("corect").HasDefaultValue(false);
            e.Property(v => v.Punctaj).HasColumnName("punctaj").HasDefaultValue(0);

            // Cheie semantica
             e.HasIndex(v => new { v.IdIntrebare, v.Text }).IsUnique();

            e.HasOne(v => v.Intrebare)
             .WithMany(i => i.Variante)
             .HasForeignKey(v => v.IdIntrebare)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Insigna ──────────────────────────────────────────────
        builder.Entity<Insigna>(e =>
        {
            e.ToTable("insigna", t =>
            {
                t.HasCheckConstraint("CK_insigna_prag_conditie_non_negative", "prag_conditie >= 0");
                t.HasCheckConstraint("CK_insigna_tip_conditie_valid", "tip_conditie IN ('LectiiCompletate', 'QuizuriCompletate', 'ToateLectiile', 'XpAtins', 'NivelAtins')");
            });
            e.HasKey(i => i.IdInsigna);
            e.Property(i => i.IdInsigna).HasColumnName("id_insigna");
            e.Property(i => i.Nume).HasColumnName("nume").HasMaxLength(150).IsRequired();
            e.Property(i => i.ConditiiObtinere).HasColumnName("conditii_obtinere").HasMaxLength(300).IsRequired();
            e.Property(i => i.UrlImagine).HasColumnName("url_imagine").HasMaxLength(255);
            e.Property(i => i.TipConditie).HasColumnName("tip_conditie").HasConversion<string>().IsRequired();
            e.Property(i => i.PragConditie).HasColumnName("prag_conditie").HasDefaultValue(0);

            // Cheie semantica
            e.HasIndex(i => i.Nume).IsUnique();
        });

        // ── ProgresLectie ────────────────────────────────────────
        builder.Entity<ProgresLectie>(e =>
        {
            e.ToTable("progres_lectie");
            e.HasKey(p => p.IdProgresL);
            e.Property(p => p.IdProgresL).HasColumnName("id_progres_l");
            e.Property(p => p.IdUtilizator).HasColumnName("id_utilizator");
            e.Property(p => p.IdLectie).HasColumnName("id_lectie");
            e.Property(p => p.Completata).HasColumnName("completata").HasDefaultValue(false);
            e.Property(p => p.DataCompletare).HasColumnName("data_completare");

            // Cheie semantica & Tuplu
            e.HasIndex(p => new { p.IdUtilizator, p.IdLectie }).IsUnique();

            e.HasOne(p => p.Utilizator)
             .WithMany()
             .HasForeignKey(p => p.IdUtilizator)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Lectie)
             .WithMany()
             .HasForeignKey(p => p.IdLectie)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ProgresQuiz ──────────────────────────────────────────
        builder.Entity<ProgresQuiz>(e =>
        {
            e.ToTable("progres_quiz", t =>
            {
                t.HasCheckConstraint("CK_progres_quiz_scor_valid", "scor >= 0 AND scor <= 100");
                t.HasCheckConstraint("CK_progres_quiz_xp_acordat_non_negative", "xp_acordat >= 0");
            });
            e.HasKey(p => p.IdProgresQ);
            e.Property(p => p.IdProgresQ).HasColumnName("id_progres_q");
            e.Property(p => p.IdUtilizator).HasColumnName("id_utilizator");
            e.Property(p => p.IdQuiz).HasColumnName("id_quiz");
            e.Property(p => p.Scor).HasColumnName("scor").HasDefaultValue(0);
            e.Property(p => p.Evaluat).HasColumnName("evaluat").HasDefaultValue(false);
            e.Property(p => p.DataCompletare).HasColumnName("data_completare").IsRequired();
            e.Property(p => p.XpAcordat).HasColumnName("xp_acordat").HasDefaultValue(0);

            // Cheie semantica
            e.HasIndex(p => new { p.IdUtilizator, p.IdQuiz, p.DataCompletare }).IsUnique();

            e.HasOne(p => p.Utilizator)
             .WithMany()
             .HasForeignKey(p => p.IdUtilizator)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Quiz)
             .WithMany()
             .HasForeignKey(p => p.IdQuiz)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── IstoricRaspunsuri ─────────────────────────────────────
        builder.Entity<IstoricRaspunsuri>(e =>
        {
            e.ToTable("istoric_raspunsuri");
            e.HasKey(i => i.IdIstoric);
            e.Property(i => i.IdIstoric).HasColumnName("id_istoric");
            e.Property(i => i.IdUtilizator).HasColumnName("id_utilizator");
            e.Property(i => i.IdProgresQ).HasColumnName("id_progres_q");
            e.Property(i => i.IdIntrebare).HasColumnName("id_intrebare");
            e.Property(i => i.IdVarianta).HasColumnName("id_varianta");

            // Cheie semantica
            e.HasIndex(i => new { i.IdIntrebare, i.IdProgresQ, i.IdVarianta }).IsUnique();

            e.HasOne(i => i.Utilizator)
             .WithMany()
             .HasForeignKey(i => i.IdUtilizator)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.ProgresQuiz)
             .WithMany()
             .HasForeignKey(i => i.IdProgresQ)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Intrebare)
             .WithMany()
             .HasForeignKey(i => i.IdIntrebare)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(i => i.VariantaRaspuns)
             .WithMany()
             .HasForeignKey(i => i.IdVarianta)
             .OnDelete(DeleteBehavior.SetNull)
             .IsRequired(false);
        });

        // ── CabinetInsigne ───────────────────────────────────────
        builder.Entity<CabinetInsigne>(e =>
        {
            e.ToTable("cabinet_insigne");
            e.HasKey(c => new { c.IdUtilizator, c.IdInsigna }); 
            e.Property(c => c.IdUtilizator).HasColumnName("id_utilizator");
            e.Property(c => c.IdInsigna).HasColumnName("id_insigna");
            e.Property(c => c.DataObtinere).HasColumnName("data_obtinere").IsRequired();

            e.HasOne(c => c.Utilizator)
             .WithMany()
             .HasForeignKey(c => c.IdUtilizator)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Insigna)
             .WithMany()
             .HasForeignKey(c => c.IdInsigna)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}