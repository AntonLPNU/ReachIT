using System.Windows;
using System.Windows.Input;
using ReachIT.Application.Contracts;
using ReachIT.Application.Services;

namespace ReachIT;

public partial class AccountLoginWindow : Window
{
    private readonly IAccountService _accountService;

    public AccountLoginWindow(IAccountService accountService)
    {
        _accountService = accountService;
        InitializeComponent();
        PasswordBox.Password = AccountService.DeveloperPassword;
    }

    public bool SignedIn { get; private set; }

    private async void OnSignInClick(object sender, RoutedEventArgs e)
    {
        await SignInAsync().ConfigureAwait(true);
    }

    private async void OnUseDeveloperClick(object sender, RoutedEventArgs e)
    {
        LoginTextBox.Text = AccountService.DeveloperLogin;
        PasswordBox.Password = AccountService.DeveloperPassword;
        await SignInAsync().ConfigureAwait(true);
    }

    private async void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await SignInAsync().ConfigureAwait(true);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async Task SignInAsync()
    {
        StatusText.Text = ResourceText("Account.Checking", "Checking login and password...");
        await _accountService.EnsureDeveloperAccountAsync().ConfigureAwait(true);

        var user = await _accountService
            .SignInAsync(LoginTextBox.Text.Trim(), PasswordBox.Password)
            .ConfigureAwait(true);

        if (user is null)
        {
            StatusText.Text = ResourceText("Account.InvalidCredentials", "Not signed in: login or password is incorrect.");
            return;
        }

        SignedIn = true;
        var signedInMessage = string.Format(
            ResourceText("Account.SignedInFormat", "Signed in as {0}."),
            user.DisplayName);
        StatusText.Text = signedInMessage;
        MessageBox.Show(
            signedInMessage,
            ResourceText("Account.WindowTitle", "ReachIT Account"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        DialogResult = true;
        Close();
    }

    private static string ResourceText(string key, string fallback)
    {
        return System.Windows.Application.Current?.TryFindResource(key) as string ?? fallback;
    }
}
