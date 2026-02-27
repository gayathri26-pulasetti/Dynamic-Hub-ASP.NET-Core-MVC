
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using MimeKit;
using MVCDHProject.Models;
using System.Net;
using System.Text;

namespace MVCDHProject.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> userManager;
        private readonly SignInManager<IdentityUser> signInManager;

        public AccountController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
        }

        // ─── REGISTER ────────────────────────────────────────────

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterViewModel userModel)
        {
            if (!ModelState.IsValid)
                return View(userModel);

            IdentityUser identityUser = new IdentityUser
            {
                UserName = userModel.Name,
                Email = userModel.Email,
                PhoneNumber = userModel.Mobile
            };

            var result = await userManager.CreateAsync(identityUser, userModel.Password);

            if (result.Succeeded)
            {
                // Generate email confirmation token
                var token = await userManager.GenerateEmailConfirmationTokenAsync(identityUser);

                var confirmationUrlLink = Url.Action(
                    "ConfirmEmail",
                    "Account",
                    new { UserId = identityUser.Id, Token = token },
                    Request.Scheme);

                Send(identityUser, confirmationUrlLink, "Email Confirmation Link");

                TempData["Title"] = "Email Confirmation Link";
                TempData["Message"] = "A confirm email link has been sent to your registered mail.";
                return View("DisplayMessages");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(userModel);
        }

        // ─── LOGIN ───────────────────────────────────────────────

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel loginModel, string returnUrl = null)
        {
            if (!ModelState.IsValid)
                return View(loginModel);

            var user = await userManager.FindByNameAsync(loginModel.Name);

            if (user == null)
            {
                ModelState.AddModelError("", "Invalid login attempt.");
                return View(loginModel);
            }

            if (!user.EmailConfirmed)
            {
                ModelState.AddModelError("", "Please confirm your email before logging in.");
                return View(loginModel);
            }

            var result = await signInManager.PasswordSignInAsync(
                loginModel.Name,
                loginModel.Password,
                loginModel.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
            {
                TempData["Title"] = "Account Locked";
                TempData["Message"] = "Too many failed attempts. Please try again later.";
                return View("DisplayMessages");
            }

            ModelState.AddModelError("", "Invalid login attempt.");
            return View(loginModel);
        }

        // ─── LOGOUT ──────────────────────────────────────────────

        [AllowAnonymous]
        public async Task<IActionResult> Logout()
        {
            await signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        // ─── CONFIRM EMAIL ───────────────────────────────────────

        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (userId == null || token == null)
            {
                TempData["Title"] = "Invalid Email Confirmation Link.";
                TempData["Message"] = "Email confirmation link is invalid.";
                return View("DisplayMessages");
            }

            var user = await userManager.FindByIdAsync(userId);

            if (user == null)
            {
                TempData["Title"] = "Invalid User Id.";
                TempData["Message"] = "User Id in confirm email link is invalid.";
                return View("DisplayMessages");
            }

            var result = await userManager.ConfirmEmailAsync(user, token);

            if (result.Succeeded)
            {
                TempData["Title"] = "Email Confirmation Success.";
                TempData["Message"] = "Email confirmed. You can now login.";
                return View("DisplayMessages");
            }

            StringBuilder errors = new StringBuilder();
            foreach (var error in result.Errors)
                errors.Append(error.Description);

            TempData["Title"] = "Confirmation Email Failure";
            TempData["Message"] = errors.ToString();
            return View("DisplayMessages");
        }

        // ─── FORGOT PASSWORD ─────────────────────────────────────

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await userManager.FindByNameAsync(model.Name);

            if (user != null && await userManager.IsEmailConfirmedAsync(user))
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(user);

                var encodedToken = WebEncoders.Base64UrlEncode(
                    Encoding.UTF8.GetBytes(token));

                var link = Url.Action(
                    "ResetPassword",
                    "Account",
                    new { userId = user.Id, token = encodedToken },
                    protocol: Request.Scheme);

                Send(user, link, "Reset Password Link");

                TempData["Title"] = "Reset Password Link";
                TempData["Message"] = "Reset password link has been sent to your mail.";
                return View("DisplayMessages");
            }

            TempData["Title"] = "Reset Password Mail Generation Failed.";
            TempData["Message"] = "Invalid username or email not confirmed.";
            return View("DisplayMessages");
        }

        // ─── RESET PASSWORD ──────────────────────────────────────

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string userId, string token)
        {
            if (userId == null || token == null)
                return RedirectToAction("Login");

            return View(new ResetPasswordViewModel
            {
                UserId = userId,
                Token = token
            });
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await userManager.FindByIdAsync(model.UserId);

            if (user == null)
            {
                TempData["Title"] = "Invalid User";
                TempData["Message"] = "User not found.";
                return View("DisplayMessages");
            }

            var decodedToken = Encoding.UTF8.GetString(
                WebEncoders.Base64UrlDecode(model.Token));

            var result = await userManager.ResetPasswordAsync(user, decodedToken, model.Password);

            if (result.Succeeded)
            {
                TempData["Title"] = "Success";
                TempData["Message"] = "Password reset successfully.";
                return View("DisplayMessages");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        // ─── EXTERNAL LOGIN (GOOGLE) ─────────────────────────────

        [HttpPost]
        [AllowAnonymous]
        public IActionResult ExternalLogin(string returnUrl, string Provider)
        {
            var url = Url.Action("CallBack", "Account", new { ReturnUrl = returnUrl });
            var properties = signInManager.ConfigureExternalAuthenticationProperties(Provider, url);
            return new ChallengeResult(Provider, properties);
        }

        [AllowAnonymous]
        public async Task<IActionResult> CallBack(string returnUrl)
        {
            if (string.IsNullOrEmpty(returnUrl))
                returnUrl = "~/";

            var info = await signInManager.GetExternalLoginInfoAsync();

            if (info == null)
            {
                ModelState.AddModelError("", "Error loading external login information.");
                return View("Login");
            }

            var signInResult = await signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, false, true);

            if (signInResult.Succeeded)
                return LocalRedirect(returnUrl);

            var email = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

            if (email != null)
            {
                var user = await userManager.FindByEmailAsync(email);

                if (user == null)
                {
                    user = new IdentityUser { UserName = email, Email = email };

                    var createResult = await userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        foreach (var error in createResult.Errors)
                            ModelState.AddModelError("", error.Description);
                        return View("Login");
                    }
                }

                await userManager.AddLoginAsync(user, info);
                await signInManager.SignInAsync(user, false);
                return LocalRedirect(returnUrl);
            }

            TempData["Title"] = "Error";
            TempData["Message"] = "Email claim not received from third party provider.";
            return RedirectToAction("DisplayMessages");
        }

        // ─── DISPLAY MESSAGES ────────────────────────────────────

        [AllowAnonymous]
        public IActionResult DisplayMessages()
        {
            return View();
        }

        // ─── SEND EMAIL (SINGLE METHOD) ──────────────────────────

        public void Send(IdentityUser identityUser, string requestLink, string subject)
        {
            StringBuilder mailBody = new StringBuilder();
            mailBody.Append("Hello " + identityUser.UserName + "<br /><br />");

            if (subject == "Email Confirmation Link")
                mailBody.Append("Click on the link below to confirm your email:");
            else if (subject == "Reset Password Link")
                mailBody.Append("Click on the link below to reset your password:");

            mailBody.Append("<br />");
            mailBody.Append($"<a href=\"{requestLink}\">Click Here</a>");
            mailBody.Append("<br /><br />Regards<br /><br />Customer Support.");

            BodyBuilder bodyBuilder = new BodyBuilder
            {
                HtmlBody = mailBody.ToString()
            };

            MimeMessage mailMessage = new MimeMessage();
            mailMessage.From.Add(new MailboxAddress("Customer Support", "pulasettigayathri@gmail.com"));
            mailMessage.To.Add(new MailboxAddress(identityUser.UserName, identityUser.Email));
            mailMessage.Subject = subject;
            mailMessage.Body = bodyBuilder.ToMessageBody();

            using SmtpClient smtpClient = new SmtpClient();
            smtpClient.Connect("smtp.gmail.com", 465, true);
            smtpClient.Authenticate("pulasettigayathri@gmail.com", "pvvbgbilawekmloj");
            smtpClient.Send(mailMessage);
            smtpClient.Disconnect(true);
        }
    }
}