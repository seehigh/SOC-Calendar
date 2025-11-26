using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Sitiowebb.Services
{
    public class SmtpAppEmailSender : IAppEmailSender
    {
        private readonly EmailSettings _settings;
        private readonly HttpClient _httpClient;

        public SmtpAppEmailSender(IOptions<EmailSettings> options, HttpClient httpClient)
        {
            _settings = options.Value;
            _httpClient = httpClient;
        }

        public async Task SendAsync(string toEmail, string subject, string htmlMessage)
        {
            // Si falta algo importante, no intentamos enviar
            if (string.IsNullOrWhiteSpace(_settings.ApiKey) ||
                string.IsNullOrWhiteSpace(_settings.From) ||
                string.IsNullOrWhiteSpace(toEmail))
            {
                return;
            }

            // Usamos tu template bonito
            var niceHtml = EmailTemplate.Build(
                title: subject,
                introText: "Hello,",
                mainText: htmlMessage,
                buttonText: "Open Arkose dashboard",
                buttonUrl: "https://sitiowebb-production.up.railway.app/ManagerOnly/Requests",
                footerText: "You received this email because your user is registered in the Arkose Labs availability tool."
            );

            var body = new
            {
                from = new { email = _settings.From, name = _settings.FromName },
                to = new[] { new { email = toEmail } },
                subject = subject,
                html = niceHtml
            };

            var json = JsonSerializer.Serialize(body);

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.mailersend.com/v1/email"
            );

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

            request.Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json"
            );

            try
            {
                var response = await _httpClient.SendAsync(request);
                // Si quieres, puedes revisar response.IsSuccessStatusCode
            }
            catch
            {
                // Importantísimo: si el correo falla, NO tiramos la web
            }
        }
    }
}