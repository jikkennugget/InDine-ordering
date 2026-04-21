using System.Text.Json;

namespace Demo.Services
{
    public interface IHCaptchaService
    {
        Task<bool> ValidateHCaptchaAsync(string hCaptchaResponse);
    }

    public class HCaptchaService : IHCaptchaService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public HCaptchaService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<bool> ValidateHCaptchaAsync(string hCaptchaResponse)
        {
            if (string.IsNullOrEmpty(hCaptchaResponse))
                return false;

            var secretKey = _configuration["HCaptcha:SecretKey"];
            if (string.IsNullOrEmpty(secretKey))
                return false;

            var formData = new List<KeyValuePair<string, string>>
            {
                new("secret", secretKey),
                new("response", hCaptchaResponse)
            };

            var formContent = new FormUrlEncodedContent(formData);

            try
            {
                var response = await _httpClient.PostAsync("https://hcaptcha.com/siteverify", formContent);
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var hCaptchaResult = JsonSerializer.Deserialize<HCaptchaResponse>(jsonResponse);

                return hCaptchaResult?.Success == true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class HCaptchaResponse
    {
        public bool Success { get; set; }
        public string[] ErrorCodes { get; set; } = Array.Empty<string>();
    }
}
