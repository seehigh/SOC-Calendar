using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Sitiowebb.Services
{
    public class MailerSendEmailSender : IAppEmailSender
    {
        private readonly EmailSettings _settings;
        private readonly HttpClient _http;

        public MailerSendEmailSender(IOptions<EmailSettings> options, HttpClient http)
        {
            _settings = options.Value;
            _http = http;
        }

        public async Task SendAsync(string toEmail, string subject, string htmlMessage)
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey) ||
                string.IsNullOrWhiteSpace(_settings.From))
                return;

            var url = "https://api.mailersend.com/v1/email";

            // plantilla bonita
            var niceHtml = EmailTemplate.Build(
                title: subject,
                introText: "Hello,",
                mainText: htmlMessage,
                buttonText: "Open Arkose dashboard",
                buttonUrl: "https://sitiowebb-production.up.railway.app/ManagerOnly/Requests",
                footerText: "This email was sent automatically by the Arkose availability system."
            );

            var payload = new
            {
                from = new { email = _settings.From, name = _settings.FromName },
                to = new[] { new { email = toEmail } },
                subject = subject,
                html = niceHtml
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

            try
            {
                var response = await _http.PostAsync(url, content);
                // si falla no romper la web
            }
            catch
            { 
                // no explotar la web por fallos del email
            }
        }
    }
}