using Microsoft.EntityFrameworkCore;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class AppSettingsService : IAppSettingsService
{
    private readonly IDatabaseService _databaseService;

    public AppSettingsService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        var settings = await db.AppSettings.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (settings is not null)
        {
            return settings;
        }

        settings = new AppSettings
        {
            Theme = "Dark",
            Language = "en",
            SidePanelHotkey = "Ctrl+Alt+R"
        };

        db.AppSettings.Add(settings);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return settings;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        var existing = await db.AppSettings.FirstOrDefaultAsync(x => x.Id == settings.Id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            db.AppSettings.Add(settings);
        }
        else
        {
            existing.ShowRelatedTasksInTree = settings.ShowRelatedTasksInTree;
            existing.HideSidePanelAfterExternalFileOpen = settings.HideSidePanelAfterExternalFileOpen;
            existing.DefaultFocusMode = settings.DefaultFocusMode;
            existing.Theme = settings.Theme;
            existing.Language = settings.Language;
            existing.EnableNotifications = settings.EnableNotifications;
            existing.SidePanelHotkey = settings.SidePanelHotkey;
            existing.FloatingLogoHotkey = settings.FloatingLogoHotkey;
            existing.QuickAddTaskHotkey = settings.QuickAddTaskHotkey;
            existing.FocusModeHotkey = settings.FocusModeHotkey;
            existing.MainWindowHotkey = settings.MainWindowHotkey;
            existing.ShowFloatingLogoOnStartup = settings.ShowFloatingLogoOnStartup;
            existing.FloatingLogoLeft = settings.FloatingLogoLeft;
            existing.FloatingLogoTop = settings.FloatingLogoTop;
            existing.LastOpenedProjectPath = settings.LastOpenedProjectPath;
            existing.BackupLocationPath = settings.BackupLocationPath;
            existing.AllowedApplicationsSerialized = settings.AllowedApplicationsSerialized;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
