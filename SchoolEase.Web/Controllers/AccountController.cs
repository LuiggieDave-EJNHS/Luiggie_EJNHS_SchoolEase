using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using SchoolEase.Web.Data;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;
using SchoolEase.Web.Models.ViewModels;
using SchoolEase.Web.Services;

namespace SchoolEase.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<AppUser> _signInManager;
    private readonly UserManager<AppUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly IAuditService _audit;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager,
        ApplicationDbContext db,
        IEmailSender emailSender,
        IAuditService audit,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _db = db;
        _emailSender = emailSender;
        _audit = audit;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(null);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var attemptedEmail = model.Email?.Trim() ?? "(blank)";
        var user = await _userManager.FindByEmailAsync(attemptedEmail);

        if (user is null)
        {
            // Record the attempt (with IP, captured automatically) so probing is visible in Audit Logs.
            await _audit.LogAsync("Failed Login", "Account", null, $"Unknown email address: {attemptedEmail}");
            ModelState.AddModelError(string.Empty, "Incorrect email address or password.");
            return View(model);
        }

        if (!user.IsActive)
        {
            await _audit.LogAsync("Failed Login", "Account", user.Id, $"Deactivated account: {attemptedEmail}",
                userName: user.FullName, userId: user.Id);
            ModelState.AddModelError(string.Empty, "This account has been deactivated. Please contact the school administrator.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            await _audit.LogAsync("User Login", "Account", user.Id, $"{user.FullName} signed in.", userName: user.FullName, userId: user.Id);
            _logger.LogInformation("User {Email} signed in.", user.Email);
            return RedirectToLocal(model.ReturnUrl);
        }

        if (result.IsNotAllowed)
        {
            await _audit.LogAsync("Failed Login", "Account", user.Id, $"Unconfirmed email: {attemptedEmail}",
                userName: user.FullName, userId: user.Id);
            ModelState.AddModelError(string.Empty, "You must confirm your email before you can sign in. Please check your inbox.");
            return View(model);
        }

        if (result.IsLockedOut)
        {
            await _audit.LogAsync("Account Locked Out", "Account", user.Id,
                $"Too many failed attempts for {attemptedEmail}. Account locked for 10 minutes.",
                userName: user.FullName, userId: user.Id);

            // Only a Super Administrator has a recovery code, so only offer the
            // alternate route for that account.
            if (await _userManager.IsInRoleAsync(user, Roles.SuperAdministrator))
            {
                ViewData["ShowRecoveryLink"] = true;
            }

            ModelState.AddModelError(string.Empty, "Too many failed attempts. This account is locked for 10 minutes.");
            return View(model);
        }

        await _audit.LogAsync("Failed Login", "Account", user.Id, $"Incorrect password for {attemptedEmail}",
            userName: user.FullName, userId: user.Id);
        ModelState.AddModelError(string.Empty, "Incorrect email address or password.");
        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var name = User.Identity?.Name;
        await _audit.LogAsync("User Logout", "Account", null, $"{name} signed out.");
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Recover()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(null);
        }

        return View(new RecoverViewModel());
    }

    /// <summary>
    /// Alternate verification for the Super Administrator. A valid recovery code
    /// lifts the lockout, consumes the code and signs the administrator back in.
    /// Refuses any account that is not currently locked out.
    /// </summary>
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Recover(RecoverViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        const string invalid = "That recovery code is not valid for this account.";

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null || !await _userManager.IsInRoleAsync(user, Roles.SuperAdministrator))
        {
            ModelState.AddModelError(string.Empty, invalid);
            return View(model);
        }

        if (!await _userManager.IsLockedOutAsync(user))
        {
            ModelState.AddModelError(string.Empty, "This account is not locked out, so no recovery code is needed. Please sign in normally.");
            return View(model);
        }

        if (!await RecoveryCodes.MatchesAsync(_userManager, user, model.Code))
        {
            await _audit.LogAsync("Recovery Code Rejected", "Account", user.Id,
                $"An invalid recovery code was entered for {user.Email}.",
                userName: user.FullName, userId: user.Id);
            _logger.LogWarning("Invalid recovery code entered for {Email}.", user.Email);
            ModelState.AddModelError(string.Empty, invalid);
            return View(model);
        }

        // The code is proof of ownership: lift the lockout, retire the code and sign in.
        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.SetLockoutEndDateAsync(user, null);
        await RecoveryCodes.ClearAsync(_userManager, user);

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        await _audit.LogAsync("Recovery Code Used", "Account", user.Id,
            $"{user.FullName} unlocked their account with a recovery code.",
            userName: user.FullName, userId: user.Id);
        _logger.LogInformation("User {Email} regained access with a recovery code.", user.Email);

        await _signInManager.SignInAsync(user, isPersistent: false);
        TempData["Success"] = "Recovery code accepted. You are signed in — please generate a new recovery code from System Settings.";
        return RedirectToAction("Index", "Dashboard");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(null);
        }

        return View(new RegisterViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var existingUser = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "An account with this email address already exists.");
            return View(model);
        }

        var user = new AppUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            FullName = model.FullName.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = false
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            // Assign the requested role (Student or Former Student)
            var role = model.RegistrationRole == Roles.Student ? Roles.Student : Roles.FormerStudent;
            await _userManager.AddToRoleAsync(user, role);

            // Create a minimal student profile for Former Students so they can request documents immediately
            if (role == Roles.FormerStudent)
            {
                var names = model.FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var firstName = names.FirstOrDefault() ?? "Former";
                var lastName = names.Length > 1 ? string.Join(" ", names.Skip(1)) : "Student";

                var student = new Student
                {
                    UserId = user.Id,
                    FirstName = firstName,
                    LastName = lastName,
                    Email = user.Email,
                    Gender = model.Gender ?? Gender.Male,
                    EnrollmentStatus = EnrollmentStatus.Graduated,
                    IsActive = true,
                    StudentNumber = $"ALM-{DateTime.UtcNow:yyyyMMddHHmmss}"
                };

                _db.Students.Add(student);
                await _db.SaveChangesAsync();
            }

            await _audit.LogAsync("User Registration", "Account", user.Id, $"{user.FullName} registered as {role}.", userName: user.FullName, userId: user.Id);
            _logger.LogInformation("New user {Email} registered as {Role}.", user.Email, role);

            // Generate email confirmation token
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, token }, Request.Scheme);

            // Send actual email - wrapped in try/catch to prevent registration crash if SMTP fails
            try
            {
                var logoUrl = Url.Content("~/images/logo.jpg");
                if (logoUrl.StartsWith("~")) logoUrl = logoUrl.Substring(1);
                var fullLogoUrl = $"{Request.Scheme}://{Request.Host}{logoUrl}";

                var emailBody = EmailTemplates.AccountActivation(user.FullName, confirmationLink!, fullLogoUrl);
                
                await _emailSender.SendEmailAsync(user.Email!, "Activate your SchoolEase Account", emailBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send registration email to {Email}. The account was created but requires manual activation.", user.Email);
                TempData["Warning"] = "Account created, but we couldn't send the activation email. Please contact the administrator to activate your account.";
            }

            return View("RegisterSuccess", model.Email);
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        if (userId == null || token == null)
        {
            return RedirectToAction("Index", "Home");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userId}'.");
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (result.Succeeded)
        {
            await _audit.LogAsync("Email Confirmed", "Account", user.Id, $"{user.FullName} confirmed their email.", userName: user.FullName, userId: user.Id);
            TempData["Success"] = "Thank you for confirming your email. You can now sign in.";
            return RedirectToAction(nameof(Login));
        }

        return View("Error");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied(string? returnUrl = null)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View(new AccessDeniedViewModel { ReturnUrl = returnUrl });
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        // Send each role to the landing page for its role.
        if (User.IsInRole(Roles.Student) || User.IsInRole(Roles.FormerStudent)) return RedirectToAction("Dashboard", "Student");
        if (User.IsInRole(Roles.Teacher)) return RedirectToAction("Dashboard", "Teacher");
        return RedirectToAction("Index", "Dashboard");
    }
}
