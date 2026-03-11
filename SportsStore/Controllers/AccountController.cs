using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SportsStore.Models.ViewModels;

namespace SportsStore.Controllers {

    public class AccountController : Controller {
        private UserManager<IdentityUser> userManager;
        private SignInManager<IdentityUser> signInManager;

        public AccountController(UserManager<IdentityUser> userMgr,
                SignInManager<IdentityUser> signInMgr) {
            userManager = userMgr;
            signInManager = signInMgr;
        }

        public ViewResult Login(string returnUrl) {
            return View(new LoginModel {
                ReturnUrl = returnUrl
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginModel loginModel) {
            if (ModelState.IsValid) {
                string? userName = loginModel.Name;
                string? password = loginModel.Password;
                if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password)) {
                    ModelState.AddModelError("", "Invalid name or password");
                    return View(loginModel);
                }

                IdentityUser? user =
                    await userManager.FindByNameAsync(userName);
                if (user != null) {
                    await signInManager.SignOutAsync();
                    if ((await signInManager.PasswordSignInAsync(user,
                            password, false, false)).Succeeded) {
                        return Redirect(loginModel?.ReturnUrl ?? "/Admin");
                    }
                }
                ModelState.AddModelError("", "Invalid name or password");
            }
            return View(loginModel);
        }

        [Authorize]
        public async Task<RedirectResult> Logout(string returnUrl = "/") {
            await signInManager.SignOutAsync();
            return Redirect(returnUrl);
        }
    }
}
