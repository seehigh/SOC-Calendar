using System;

namespace Sitiowebb.Services
{
    public class EmailSettings
    {
        public string ApiKey { get; set; } = "";
        public string From { get; set; } = "";
        public string FromName { get; set; } = "Arkose Labs Notifications";
    }
}