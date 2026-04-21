using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace Demo.Services
{
    public interface IPasswordResetService
    {
        Task<string> GenerateResetTokenAsync(string email);
        Task<bool> ValidateResetTokenAsync(string email, string token);
        Task<bool> ResetPasswordAsync(string email, string token, string newPassword);
        Task SendResetEmailAsync(string email, string token);
    }

    public class PasswordResetService : IPasswordResetService
    {
        private readonly DB _db;
        private readonly Helper _helper;
        private readonly IConfiguration _configuration;

        public PasswordResetService(DB db, Helper helper, IConfiguration configuration)
        {
            _db = db;
            _helper = helper;
            _configuration = configuration;
        }

        public async Task<string> GenerateResetTokenAsync(string email)
        {
            var user = _db.Users.Find(email);
            if (user == null)
                return null;

            // Generate a secure random token
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            
            // Set token and expiry (24 hours from now)
            user.ResetToken = token;
            user.ResetTokenExpiry = DateTime.UtcNow.AddHours(24);
            
            await _db.SaveChangesAsync();
            return token;
        }

        public async Task<bool> ValidateResetTokenAsync(string email, string token)
        {
            var user = _db.Users.Find(email);
            if (user == null || user.ResetToken != token || user.ResetTokenExpiry == null)
                return false;

            return user.ResetTokenExpiry > DateTime.UtcNow;
        }

        public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword)
        {
            var user = _db.Users.Find(email);
            if (user == null || user.ResetToken != token || user.ResetTokenExpiry == null)
                return false;

            if (user.ResetTokenExpiry <= DateTime.UtcNow)
                return false;

            // Update password and clear reset token
            user.Hash = _helper.HashPassword(newPassword);
            user.ResetToken = null;
            user.ResetTokenExpiry = null;
            
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task SendResetEmailAsync(string email, string token)
        {
            var resetUrl = $"{_configuration["AppUrl"]}/Account/ResetPasswordConfirm?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
            
            var smtpUser = _configuration["Smtp:User"];
            var smtpPass = _configuration["Smtp:Pass"];
            var smtpName = _configuration["Smtp:Name"];
            var smtpHost = _configuration["Smtp:Host"];
            var smtpPort = int.Parse(_configuration["Smtp:Port"]);

            var mailMessage = new MailMessage
            {
                From = new MailAddress(smtpUser, smtpName),
                Subject = "Password Reset - JikkenRice",
                Body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                            <h2 style='color: #d9232e; border-bottom: 3px solid #d9232e; padding-bottom: 10px;'>
                                🔐 Password Reset Request
                            </h2>
                            
                            <p>Hello,</p>
                            
                            <p>You have requested to reset your password for your JikkenRice account.</p>
                            
                            <p>Click the button below to reset your password:</p>
                            
                            <div style='text-align: center; margin: 30px 0;'>
                                <a href='{resetUrl}' 
                                   style='background: linear-gradient(135deg, #d9232e 0%, #b91c2c 100%); 
                                          color: white; 
                                          padding: 15px 30px; 
                                          text-decoration: none; 
                                          border-radius: 10px; 
                                          font-weight: bold; 
                                          display: inline-block;'>
                                    Reset Password
                                </a>
                            </div>
                            
                            <p><strong>Or copy and paste this link:</strong></p>
                            <p style='background: #f8f9fa; padding: 10px; border-radius: 5px; word-break: break-all;'>
                                {resetUrl}
                            </p>
                            
                            <p><strong>Important:</strong></p>
                            <ul>
                                <li>This link will expire in 24 hours</li>
                                <li>If you didn't request this reset, please ignore this email</li>
                                <li>For security, this link can only be used once</li>
                            </ul>
                            
                            <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
                            
                            <p style='color: #666; font-size: 14px;'>
                                Best regards,<br>
                                <strong>JikkenRice Team</strong>
                            </p>
                        </div>
                    </body>
                    </html>",
                IsBodyHtml = true
            };

            mailMessage.To.Add(email);

            using var smtpClient = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new System.Net.NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };

            await smtpClient.SendMailAsync(mailMessage);
        }
    }
}
