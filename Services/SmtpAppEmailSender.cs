using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Sitiowebb.Services
{
    public class SmtpAppEmailSender : IAppEmailSender
    {
        private readonly EmailSettings _settings;

        public SmtpAppEmailSender(IOptions<EmailSettings> options)
        {
            _settings = options.Value;
        }

        public async Task SendAsync(string toEmail, string subject, string htmlMessage)
        {
            // Si falta algo crítico, salimos sin hacer nada
            if (string.IsNullOrWhiteSpace(_settings.Host) ||
                _settings.Port <= 0 ||
                string.IsNullOrWhiteSpace(_settings.From) ||
                string.IsNullOrWhiteSpace(toEmail))
            {
                return;
            }

            // Envolvemos con tu template bonito
            var niceHtml = EmailTemplate.Build(
                title: subject,
                introText: "Hello,",
                mainText: htmlMessage,
                buttonText: "Open Arkose dashboard",
                buttonUrl: "https://sitiowebb-production.up.railway.app/ManagerOnly/Requests",
                footerText: "You received this email because your user is registered in the Arkose Labs availability tool."
            );

            using var msg = new MailMessage(_settings.From, toEmail)
            {
                Subject = subject,
                Body = niceHtml,
                IsBodyHtml = true
            };

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(
                    _settings.UserName,
                    _settings.Password
                )
            };

            try
            {
                await client.SendMailAsync(msg);
            }
            catch
            {
                // MUY IMPORTANTE: no re-lanzamos la excepción.
                // Si el correo falla, la web sigue respondiendo igual.
                // Aquí podrías loguear si algún día añades logs.
            }
        }
    }
}