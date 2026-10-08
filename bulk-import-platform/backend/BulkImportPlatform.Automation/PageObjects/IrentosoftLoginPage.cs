using Microsoft.Playwright;

namespace BulkImportPlatform.Automation.PageObjects;

public enum LoginResult
{
    Success,
    AccessDenied,
    Timeout
}

/// <summary>
/// Ported from IrentoQA's Pages/Auth/LoginPage.cs. The success/failure signals (URL contains
/// "Dashboard"; "Access Denied" text) were verified live and are kept as-is. The original's
/// login-field selectors were purely positional (first input = username, second = password) -
/// fragile, since any layout change reordering inputs silently breaks every RPA run's first
/// step. Hardened here to try attribute-based selectors first, falling back to positional only
/// if no match is found; the real attributes should be confirmed during the live verification
/// pass (see the project plan's Phase 6) rather than assumed.
/// </summary>
public class IrentosoftLoginPage
{
    private readonly IPage _page;

    public IrentosoftLoginPage(IPage page)
    {
        _page = page;
    }

    public async Task<LoginResult> LoginAsync(string username, string password)
    {
        await _page.GotoAsync("https://live.irentosoft.com/");

        // name='userName' / 'userPassword' confirmed on the live login page.
        var usernameField = _page.Locator("input[name='userName'], input[name='username'], input[type='email']").First;
        var passwordField = _page.Locator("input[name='userPassword'], input[type='password']").First;

        if (await usernameField.CountAsync() == 0 || await passwordField.CountAsync() == 0)
        {
            // Fallback: positional, matching IrentoQA's original (unverified attribute names).
            usernameField = _page.Locator("input").First;
            passwordField = _page.Locator("input").Nth(1);
        }

        await usernameField.FillAsync(username);
        await passwordField.FillAsync(password);

        await _page.Locator("button:has-text('Login')").ClickAsync(new LocatorClickOptions { Force = true });
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        try
        {
            await _page.WaitForURLAsync("**/Dashboard", new() { Timeout = 15000 });
            return LoginResult.Success;
        }
        catch (TimeoutException)
        {
            if (await _page.Locator("text=Access Denied").CountAsync() > 0)
            {
                return LoginResult.AccessDenied;
            }
            return LoginResult.Timeout;
        }
    }
}
