using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Sitiowebb.Services
{
    public class MailerEmailSender : IAppEmailSender
    {
        private readonly HttpClient _httpClient;
        private readonly EmailSettings _settings;

        public MailerEmailSender(HttpClient httpClient, IOptions<EmailSettings> options)
        {
            _httpClient = httpClient;
            _settings = options.Value;
        }

        public async Task SendAsync(string toEmail, string subject, string htmlMessage)
        {
            // Si falta algo crítico, no intentamos enviar
            if (string.IsNullOrWhiteSpace(_settings.ApiKey) ||
                string.IsNullOrWhiteSpace(_settings.From) ||
                string.IsNullOrWhiteSpace(toEmail))
            {
                Console.WriteLine("[MailerEmailSender] Falta ApiKey, From o toEmail. No se envía correo.");
                return;
            }

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

            var payload = new
            {
                from = new
                {
                    email = _settings.From,
                    name = _settings.FromName
                },
                to = new[]
                {
                    new { email = toEmail }
                },
                subject = subject,
                html = htmlMessage
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync("https://api.mailersend.com/v1/email", content);

                Console.WriteLine($"[MailerEmailSender] Status: {(int)response.StatusCode} {response.StatusCode}");
                var respBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[MailerEmailSender] Body: {respBody}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MailerEmailSender] Error enviando correo: " + ex.Message);
            }
        }
    }
}