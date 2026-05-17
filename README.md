# ReachIT

ReachIT - це Windows desktop-застосунок для керування робочим простором, задачами, файлами та фокус-сесіями. Проєкт побудований як локальний productivity hub: він допомагає швидко відкривати потрібний проєкт, вести задачі, прив'язувати їх до файлів, бачити активність і працювати без залежності від хмари.

## Що вміє застосунок

- Керує проєктами, задачами, підзадачами, статусами, історією та пов'язаними файлами.
- Показує dashboard із прогресом, задачами на сьогодні, активністю та статистикою продуктивності.
- Має файловий провідник проєкту з preview, metadata та прив'язкою файлів до задач.
- Підтримує focus mode, overlay-вікна, floating logo, quick menu і quick add task.
- Відстежує активність: активне вікно, процеси, Git-активність, зміни файлів і робочі одиниці часу.
- Генерує локальні HTML-звіти по проєкту.
- Працює offline-first: дані зберігаються локально, без обов'язкової cloud-інфраструктури.
- Використовує глобальні гарячі клавіші Windows, зокрема `Ctrl+Alt+R`.

## Технології

| Частина | Технології |
| --- | --- |
| UI | WPF, XAML, .NET 8 Windows |
| Архітектура | MVVM, layered structure |
| DI | Власний мінімальний контейнер `AppHost` |
| Дані | EF Core, SQLite |
| Тести | xUnit, Moq, EF Core InMemory |
| Windows інтеграції | `user32.dll`, tray icon, global hotkeys, WinForms bridge |
| UI ресурси | MahApps Metro IconPacks, SharpVectors |
| Логи | Локальний `ILocalLogger` у `%LOCALAPPDATA%\ReachIT\logs` |

## Архітектура

Проєкт розділений на кілька шарів:

- `Domain` - моделі та enum-и предметної області: проєкти, задачі, активність, підписки, focus-сесії, work items.
- `Application` - контракти та сервіси бізнес-логіки: задачі, прогрес, статистика, звіти, Git, focus mode, файловий аналіз.
- `Infrastructure` - доступ до SQLite/EF Core, репозиторії, логування, OS-сервіси.
- `Presentation` - WPF views, windows, view models, commands і UI-сервіси.
- `Bootstrap` - композиція залежностей у `AppHost`.
- `Resources` - стилі, шаблони, локалізація, іконки та converters.
- `ReachIT.Tests` - smoke/unit-тести для старту, DI, логування та пов'язаних сценаріїв.

Основний потік запуску:

1. `App.xaml.cs` створює та ініціалізує `AppHost`.
2. `AppHost` реєструє сервіси, репозиторії, view models і window manager.
3. `MainWindow` працює з `MainViewModel`.
4. `MainViewModel` керує навігацією між dashboard, задачами, файлами, статистикою, налаштуваннями та іншими екранами.
5. `DatabaseService` і `ReachItDbContext` відповідають за локальне сховище SQLite.

## Структура репозиторію

```text
ReachIT/
  Application/        Контракти та сервіси бізнес-логіки
  Bootstrap/          AppHost і реєстрація залежностей
  Domain/             Моделі та enum-и
  Infrastructure/     SQLite, репозиторії, логування, OS-інтеграції
  Presentation/       Views, ViewModels, Windows, Commands, UI-сервіси
  Resources/          Стилі, шаблони, локалізація, іконки
  App.xaml            Точка входу WPF
  MainWindow.xaml     Головне вікно застосунку

ReachIT.Tests/        xUnit-тести
artifacts/            Збіркові артефакти
PROJECT_FILE_GUIDE.txt Чернетковий довідник по файлах проєкту
ReachIT.sln           Visual Studio solution
```

## Вимоги

- Windows 10/11.
- .NET 8 SDK.
- Visual Studio 2022 з workload для .NET desktop development або .NET CLI.

Застосунок є Windows-only, оскільки використовує WPF, tray icon, Win32 API та глобальні hotkeys.

## Запуск локально

Клонувати репозиторій і перейти в корінь:

```powershell
git clone <repository-url>
cd ReachIT
```

Відновити залежності:

```powershell
dotnet restore
```

Зібрати solution:

```powershell
dotnet build ReachIT.sln
```

Запустити застосунок:

```powershell
dotnet run --project ReachIT\ReachIT.csproj
```

Або відкрити `ReachIT.sln` у Visual Studio та запустити через `F5`.

## Тести

Запуск тестів:

```powershell
dotnet test ReachIT.sln
```

Тестовий проєкт використовує `xUnit`, `Moq` і `Microsoft.EntityFrameworkCore.InMemory`.

## Локальні дані та логи

ReachIT зберігає робочі дані локально. Логи доступні тут:

```text
%LOCALAPPDATA%\ReachIT\logs
```

Це корисно для діагностики старту, помилок DI, проблем із базою, hotkeys або OS-інтеграціями.

## Developer tools

У Debug-конфігурації developer tools увімкнені за замовчуванням:

```xml
<EnableDeveloperTools>true</EnableDeveloperTools>
```

Для non-Debug збірок вони вимикаються автоматично. За потреби прапорець можна перевизначити через MSBuild property.

## Корисні файли

- `PROJECT_FILE_GUIDE.txt` - детальний довідник по файлах і модулях. Якщо треба швидко пояснити структуру проєкту іншому розробнику або AI-асистенту, починайте з нього.
- `Directory.Build.props` - спільні налаштування C# проєктів: nullable, implicit usings, language version.
- `ReachIT/Resources/Localization/StringResources.en.xaml` і `StringResources.uk.xaml` - локалізовані рядки UI.

## Поточні припущення

- Проєкт орієнтований на локальне використання без обов'язкового backend.
- Глобальні hotkeys можуть конфліктувати з іншими застосунками; у такому разі ReachIT має поводитися безпечно і лишати доступними in-app команди.
- Частина інтеграцій залежить від Windows API, тому кросплатформний запуск не підтримується.
