using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;
using SchoolEase.Web.Models.ViewModels;
using SchoolEase.Web.Services;

namespace SchoolEase.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<AppUser> _signInManager;
    private readonly UserManager<AppUser> _userManager;
    private readonly IAuditService _audit;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager,
        IAuditService audit,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
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

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Incorrect email address or password.");
            return View(model);
        }

        if (!user.IsActive)
        {
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
            ModelState.AddModelError(string.Empty, "You must confirm your email before you can sign in. Please check your inbox.");
            return View(model);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Too many failed attempts. This account is locked for 10 minutes.");
            return View(model);
        }

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
            EmailConfirmed = true // Auto-confirm for this demo/local setup
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            // Assign the requested role (Student or Former Student)
            var role = model.RegistrationRole == Roles.Student ? Roles.Student : Roles.FormerStudent;
            await _userManager.AddToRoleAsync(user, role);

            await _audit.LogAsync("User Registration", "Account", user.Id, $"{user.FullName} registered as {role}.", userName: user.FullName, userId: user.Id);
            _logger.LogInformation("New user {Email} registered as {Role}.", user.Email, role);

            // Generate email confirmation token
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, token }, Request.Scheme);

            // LOG the confirmation link (in a real app, send via email)
            _logger.LogWarning("EMAIL CONFIRMATION LINK FOR {Email}: {Link}", user.Email, confirmationLink);

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
