using System.Net;
using System.Net.Mail;
using System.Text;

namespace HistoryConnect.Servicii;

public class ServiciuEmail
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServiciuEmail> _logger;

    public ServiciuEmail(IConfiguration configuration, ILogger<ServiciuEmail> logger)
    {
        _configuration = configuration;
        _logger        = logger;
    }


    public async Task TrimiteConfirmareAsync(string email, string linkConfirmare)
    {
        var linkHtml = WebUtility.HtmlEncode(linkConfirmare);

        await TrimiteAsync(
            destinatar: email,
            subiect:    "Confirmă emailul pentru HistoryConnect",
            corpHtml:   $"""
                <p>Salut!</p>
                <p>Cineva (probabil tu) s-a înregistrat pe HistoryConnect cu această adresă de email.</p>
                <p>Apasă pe linkul de mai jos pentru a-ți crea contul.
                   Linkul este valabil <strong>24 de ore</strong>.</p>
                <p><a href="{linkHtml}">Confirmă și creează contul</a></p>
                <p>Dacă linkul nu funcționează, copiază adresa de mai jos în browser:</p>
                <p>{linkHtml}</p>
                <p>Dacă nu tu ai inițiat această înregistrare, poți ignora acest email.</p>
                """);
    }


    public async Task TrimiteResetareParolaAsync(string email, string linkResetare)
    {
        var linkHtml = WebUtility.HtmlEncode(linkResetare);

        await TrimiteAsync(
            destinatar: email,
            subiect:    "Resetare parolă HistoryConnect",
            corpHtml:   $"""
                <p>Salut!</p>
                <p>Am primit o cerere de resetare a parolei pentru contul tău HistoryConnect.</p>
                <p><a href="{linkHtml}">Resetează parola</a></p>
                <p>Dacă nu ai cerut resetarea parolei, poți ignora acest email.</p>
                <p>Dacă linkul nu funcționează, copiază adresa de mai jos în browser:</p>
                <p>{linkHtml}</p>
                """);
    }

    private async Task TrimiteAsync(string destinatar, string subiect, string corpHtml)
    {
        var smtpHost  = _configuration["EmailSettings:SmtpHost"];
        var fromEmail = _configuration["EmailSettings:FromEmail"];

        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(fromEmail))
        {
            _logger.LogWarning(
                "SMTP neconfigurat — emailul '{Subiect}' pentru {Destinatar} nu a fost trimis. " +
                "Corp mesaj: {Corp}",
                subiect, destinatar, corpHtml);
            return;
        }

        var smtpPort  = _configuration.GetValue("EmailSettings:SmtpPort", 587);
        var username  = _configuration["EmailSettings:Username"];
        var password  = _configuration["EmailSettings:Password"];
        var fromName  = _configuration["EmailSettings:FromName"] ?? "HistoryConnect";
        var enableSsl = _configuration.GetValue("EmailSettings:EnableSsl", true);

        using var client = new SmtpClient(smtpHost, smtpPort) { EnableSsl = enableSsl };

        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, password);

        using var mesaj = new MailMessage
        {
            From         = new MailAddress(fromEmail, fromName),
            Subject      = subiect,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml   = true,
            Body         = corpHtml
        };

        mesaj.To.Add(destinatar);
        await client.SendMailAsync(mesaj);
    }
}