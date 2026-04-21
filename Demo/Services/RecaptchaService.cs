using System.Text.Json;

namespace Demo.Services
{
    public interface IRecaptchaService
    {
        Task<bool> ValidateRecaptchaAsync(string recaptchaResponse);
    }

    public class RecaptchaService : IRecaptchaService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public RecaptchaService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<bool> ValidateRecaptchaAsync(string recaptchaResponse)
        {
            if (string.IsNullOrEmpty(recaptchaResponse))
                return false;

            var secretKey = _configuration["Recaptcha:SecretKey"];
            if (string.IsNullOrEmpty(secretKey))
                return false;

            var requestUri = $"https://www.google.com/recaptcha/api/siteverify?secret={secretKey}&response={recaptchaResponse}";

            try
            {
                var response = await _httpClient.PostAsync(requestUri, null);
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var recaptchaResult = JsonSerializer.Deserialize<RecaptchaResponse>(jsonResponse);

                return recaptchaResult?.Success == true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class RecaptchaResponse
    {
        public bool Success { get; set; }
        public string[] ErrorCodes { get; set; } = Array.Empty<string>();
    }
}
