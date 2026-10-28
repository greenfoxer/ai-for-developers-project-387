using System.Diagnostics;
using Microsoft.Playwright;

namespace CallCalendar.E2E.Tests;

/// Поднимает API и Web как реальные процессы на локальных портах и держит браузер Playwright.
/// Вне CI (без RUN_E2E=1) браузер не запускается — тест деградирует в no-op, чтобы обычный
/// `dotnet test` оставался зелёным без установки Chromium.
public sealed class E2EApp : IAsyncLifetime
{
    private const string ApiUrl = "http://localhost:5000";
    private const string WebUrl = "http://127.0.0.1:5173";

    private Process? _api;
    private Process? _web;
    private string? _dbPath;

    public bool Enabled { get; } =
        string.Equals(Environment.GetEnvironmentVariable("RUN_E2E"), "1", StringComparison.OrdinalIgnoreCase);

    public IPlaywright? Driver { get; private set; }
    public IBrowser? Browser { get; private set; }

    public async Task InitializeAsync()
    {
        if (!Enabled)
        {
            return;
        }

        _dbPath = Path.Combine(Path.GetTempPath(), $"callcalendar-e2e-{Guid.NewGuid():N}.db");
        var root = RepoRoot();

        // API жёстко слушает localhost:5000 (Program.cs); изолируем только БД.
        _api = StartDotnet(root, "src/CallCalendar.Api", ("ConnectionStrings__sqlite", $"Data Source={_dbPath}"));
        _web = StartDotnet(root, "src/CallCalendar.Web",
            ("ASPNETCORE_ENVIRONMENT", "Development"),
            ("ASPNETCORE_URLS", WebUrl),
            ("CallCalendarApi__BaseUrl", ApiUrl));

        await WaitUntilReady(ApiUrl + "/api/event-types");
        await WaitUntilReady(WebUrl + "/");

        Driver = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Driver.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }
        Driver?.Dispose();

        Kill(_web);
        Kill(_api);
        TryDeleteDb(_dbPath);
    }

    public async Task<IPage> NewPageAsync()
    {
        var page = await Browser!.NewPageAsync();
        await page.GotoAsync(WebUrl + "/book");
        return page;
    }

    private static Process StartDotnet(string root, string project, params (string Key, string Value)[] env)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in new[] { "run", "--project", project, "--no-launch-profile" })
        {
            startInfo.ArgumentList.Add(arg);
        }
        foreach (var (key, value) in env)
        {
            startInfo.Environment[key] = value;
        }

        var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => Dump(project, e.Data);
        process.ErrorDataReceived += (_, e) => Dump(project, e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static void Dump(string project, string? line)
    {
        if (line is not null)
        {
            Console.WriteLine($"[{project}] {line}");
        }
    }

    private static async Task WaitUntilReady(string url, int timeoutSeconds = 120)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var _ = await client.GetAsync(url);
                return; // сервер ответил — готов
            }
            catch (HttpRequestException)
            {
                // ещё не поднялся
            }
            catch (TaskCanceledException)
            {
                // таймаут подключения — ещё не поднялся
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Сервис {url} не поднялся за {timeoutSeconds} с.");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CallCalendar.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("CallCalendar.sln не найден вверх по дереву от тестовой сборки.");
    }

    private static void Kill(Process? process)
    {
        if (process is null || process.HasExited)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(10_000);
        }
        catch (InvalidOperationException)
        {
            // уже завершился
        }
    }

    private static void TryDeleteDb(string? path)
    {
        if (path is null)
        {
            return;
        }

        foreach (var suffix in new[] { "", "-shm", "-wal", "-journal" })
        {
            try
            {
                File.Delete(path + suffix);
            }
            catch (IOException)
            {
                // файл мог не создаться — неважно
            }
        }
    }
}
