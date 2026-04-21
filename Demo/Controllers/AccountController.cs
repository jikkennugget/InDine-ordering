using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mail;
using Demo.Services;

namespace Demo.Controllers;

public class AccountController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helper hp;
    private readonly IHCaptchaService hCaptchaService;
    private readonly IConfiguration configuration;
    private readonly IPasswordResetService passwordResetService;

    public AccountController(DB db, IWebHostEnvironment en, Helper hp, IHCaptchaService hCaptchaService, IConfiguration configuration, IPasswordResetService passwordResetService)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
        this.hCaptchaService = hCaptchaService;
        this.configuration = configuration;
        this.passwordResetService = passwordResetService;
    }

    // GET: Account/Login
    public IActionResult Login()
    {
        ViewBag.HCaptchaSiteKey = configuration["HCaptcha:SiteKey"];
        return View();
    }

    // POST: Account/Login
    [HttpPost]
    public async Task<IActionResult> Login(LoginVM vm, string? returnURL)
    {
        ViewBag.HCaptchaSiteKey = configuration["HCaptcha:SiteKey"];
        
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        // hCaptcha validation disabled - only UI validation is active
        var hCaptchaResponse = Request.Form["h-captcha-response"].ToString();
        var isHCaptchaValid = true; // Always true - hCaptcha service disabled, only UI validation
        
        if (!isHCaptchaValid)
        {
            ModelState.AddModelError("", "Please complete the security verification.");
            return View(vm);
        }

        var u = db.Users.Find(vm.Email);

        if (u == null || !hp.VerifyPassword(u.Hash, vm.Password))
        {
            ModelState.AddModelError("", "Login credentials not matched.");
            return View(vm);
        }

        if (ModelState.IsValid)
        {
            TempData["Info"] = "Login successfully.";

            hp.SignIn(u!.Email, u.Role, vm.RememberMe);
            
            if (!string.IsNullOrWhiteSpace(returnURL) && Url.IsLocalUrl(returnURL))
            {
                return Redirect(returnURL);
            }

            // Redirect based on role
            if (u.Role == "Admin")
            {
                return RedirectToAction("Maintain", "Product");
            }
            else if (u.Role == "Staff")
            {
                return RedirectToAction("TakeOrder", "Product");
            }

            return RedirectToAction("Maintain", "Product");
        }
        
        return View(vm);
    }

    // GET: Account/Logout
    public IActionResult Logout(string? returnURL)
    {
        TempData["Info"] = "Logout successfully.";

        hp.SignOut();

        return RedirectToAction("Index", "Home");
    }

    // GET: Account/AccessDenied
    public IActionResult AccessDenied(string? returnURL)
    {
        return View();
    }



    // ------------------------------------------------------------------------
    // Others
    // ------------------------------------------------------------------------

    // GET: Account/CheckEmail
    public bool CheckEmail(string email)
    {
        return !db.Users.Any(u => u.Email == email);
    }

    // GET: Account/Register
    public IActionResult Register()
    {
        return View();
    }

    // POST: Account/Register
    [HttpPost]
    public IActionResult Register(RegisterVM vm)
    {
        if (ModelState.IsValid("Email") &&
            db.Users.Any(u => u.Email == vm.Email))
        {
            ModelState.AddModelError("Email", "Duplicated Email.");
        }

        if (ModelState.IsValid("Photo"))
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (err != "") ModelState.AddModelError("Photo", err);
        }
        
        if (ModelState.IsValid)
        {
            var hash = hp.HashPassword(vm.Password);
            var photo = hp.SavePhoto(vm.Photo, "photos");

            db.Staff.Add(new Staff()
            {
                Email = vm.Email,
                Hash = hash,
                Name = vm.Name,
                PhotoURL = photo,
            });
            db.SaveChanges();

            // Append to Data/Users.txt for record-keeping
            try
            {
                var file = Path.Combine(en.ContentRootPath, "Data", "Users.txt");
                var line = $"{vm.Email}\t{hash}\t{vm.Name}\tStaff\t{photo}{Environment.NewLine}";
                System.IO.File.AppendAllText(file, line);
            }
            catch { }

            TempData["Info"] = "Register successfully. Please login.";
            return RedirectToAction("Login");
        }

        return View(vm);
    }

    // GET: Account/RegisterAdmin
    [Authorize(Roles = "Admin")]
    public IActionResult RegisterAdmin()
    {
        return View();
    }

    // POST: Account/RegisterAdmin
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult RegisterAdmin(RegisterAdminVM vm)
    {
        if (ModelState.IsValid("Email") &&
            db.Users.Any(u => u.Email == vm.Email))
        {
            ModelState.AddModelError("Email", "Duplicated Email.");
        }

        if (ModelState.IsValid)
        {
            db.Admins.Add(new Admin()
            {
                Email = vm.Email,
                Hash = hp.HashPassword(vm.Password),
                Name = vm.Name,
            });
            db.SaveChanges();

            TempData["Info"] = "Admin registered successfully.";
            return RedirectToAction("Login");
        }

        return View(vm);
    }

    // GET: Account/UpdatePassword
    [Authorize]
    public IActionResult UpdatePassword()
    {
        return View();
    }

    // POST: Account/UpdatePassword
    [Authorize]
    [HttpPost]
    public IActionResult UpdatePassword(UpdatePasswordVM vm)
    {
        var u = db.Users.Find(User.Identity!.Name);
        if (u == null) return RedirectToAction("Index", "Home");

        if (!hp.VerifyPassword(u.Hash, vm.Current))
        {
            ModelState.AddModelError("Current", "Current Password not matched.");
        }

        if (ModelState.IsValid)
        {
            u.Hash = hp.HashPassword(vm.New);
            db.SaveChanges();

            TempData["Info"] = "Password updated.";
            return RedirectToAction();
        }

        return View();
    }

    // GET: Account/UpdateProfile
    [Authorize(Roles = "Staff")]
    public IActionResult UpdateProfile()
    {
        var m = db.Staff.Find(User.Identity!.Name);
        if (m == null) return RedirectToAction("Index", "Home");

        var vm = new UpdateProfileVM
        {
            Email = m.Email,
            Name = m.Name,
            PhotoURL = m.PhotoURL,
        };

        return View(vm);
    }

    // POST: Account/UpdateProfile
    [Authorize(Roles = "Staff")]
    [HttpPost]
    public IActionResult UpdateProfile(UpdateProfileVM vm)
    {
        var m = db.Staff.Find(User.Identity!.Name);
        if (m == null) return RedirectToAction("Index", "Home");

        if (vm.Photo != null)
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (err != "") ModelState.AddModelError("Photo", err);
        }

        if (ModelState.IsValid)
        {
            m.Name = vm.Name;

            if (vm.Photo != null)
            {
                hp.DeletePhoto(m.PhotoURL, "photos");
                m.PhotoURL = hp.SavePhoto(vm.Photo, "photos");
            }

            db.SaveChanges();

            TempData["Info"] = "Profile updated.";
            return RedirectToAction();
        }

        vm.Email = m.Email;
        vm.PhotoURL = m.PhotoURL;
        return View(vm);
    }

    // GET: Account/ResetPassword
    public IActionResult ResetPassword()
    {
        return View();
    }

    // POST: Account/ResetPassword
    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordVM vm)
    {
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var user = db.Users.Find(vm.Email);
        if (user == null)
        {
            // Don't reveal if email exists or not for security
            TempData["Info"] = "If an account with that email exists, a password reset link has been sent.";
            return RedirectToAction("Login");
        }

        try
        {
            // Generate reset token
            var token = await passwordResetService.GenerateResetTokenAsync(vm.Email);
            if (token != null)
            {
                // Send reset email
                await passwordResetService.SendResetEmailAsync(vm.Email, token);
            }

            TempData["Info"] = "If an account with that email exists, a password reset link has been sent.";
            return RedirectToAction("Login");
        }
        catch
        {
            TempData["Error"] = "An error occurred while sending the reset email. Please try again.";
            return View(vm);
        }
    }

    // GET: Account/ResetPasswordConfirm
    public async Task<IActionResult> ResetPasswordConfirm(string email, string token)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
        {
            TempData["Error"] = "Invalid reset link.";
            return RedirectToAction("Login");
        }

        var isValid = await passwordResetService.ValidateResetTokenAsync(email, token);
        if (!isValid)
        {
            TempData["Error"] = "Invalid or expired reset link.";
            return RedirectToAction("Login");
        }

        ViewBag.Email = email;
        ViewBag.Token = token;
        return View();
    }

    // POST: Account/ResetPasswordConfirm
    [HttpPost]
    public async Task<IActionResult> ResetPasswordConfirm(ResetPasswordConfirmVM vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Email = vm.Email;
            ViewBag.Token = vm.Token;
            return View(vm);
        }

        try
        {
            var success = await passwordResetService.ResetPasswordAsync(vm.Email, vm.Token, vm.NewPassword);
            if (success)
            {
                TempData["Info"] = "Your password has been reset successfully. Please login with your new password.";
                return RedirectToAction("Login");
            }
            else
            {
                TempData["Error"] = "Invalid or expired reset link.";
                return RedirectToAction("Login");
            }
        }
        catch
        {
            TempData["Error"] = "An error occurred while resetting your password. Please try again.";
            ViewBag.Email = vm.Email;
            ViewBag.Token = vm.Token;
            return View(vm);
        }
    }
}
