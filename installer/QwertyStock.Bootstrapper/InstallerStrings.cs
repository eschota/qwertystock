namespace QwertyStock.Bootstrapper;

/// <summary>Localized UI strings for the bootstrapper window and pipeline status.</summary>
public static class InstallerStrings
{
    private static bool IsRu => InstallerLocale.Current == InstallerLanguage.Russian;

    public const string BrandLine1 = "QWERTYSTOCK";
    public const string BrandLine2 = "NEXT GEN MICROSTOCK";

    public static string Tagline =>
        IsRu
            ? "ваш собственный сток у вас на устройстве."
            : "Your own microstock on your device.";

    public static string IntroBody =>
        IsRu
            ? "Мы ставим Python, Git и приложение QwertyStock на этот компьютер, затем откроем рабочий экран в браузере."
            : "We install Python, Git, and the QwertyStock app on this PC, then open your workspace in the browser.";

    /// <summary>Short note under intro — why the first run takes long.</summary>
    public static string WhySlowFootnote =>
        IsRu
            ? "Почему долго: первый раз качается Python и Git (~50 МБ), затем репозиторий с GitHub, потом pip тянет пакеты с PyPI по одному. Антивирус и скорость сети сильно влияют — это нормально."
            : "Why it’s slow: first run downloads Python and Git (~50 MB), then syncs the repo from GitHub, then pip fetches packages from PyPI. Antivirus and network speed matter a lot — that’s expected.";

    public static string PipStartingSummary(int topLevelLines) =>
        IsRu
            ? $"pip: установка по requirements.txt (~{topLevelLines} зависимостей в списке)…"
            : $"pip: installing from requirements.txt (~{topLevelLines} top-level entries)…";

    public static string PipInstallFailed(int exitCode, string stderrTail) =>
        IsRu
            ? $"pip завершился с кодом {exitCode}. Последние строки:\n{stderrTail}"
            : $"pip exited with code {exitCode}. Last output:\n{stderrTail}";

    public static string StatusStarting => IsRu ? "Запуск…" : "Starting…";

    public static string FormatVersion(string semantic) =>
        IsRu ? $"Версия {semantic}" : $"Version {semantic}";

    public static string CloseTooltip => IsRu ? "Закрыть" : "Close";

    public static string AppTitle => "QwertyStock";

    public static string ProgressPreparing => IsRu ? "Подготовка…" : "Preparing…";

    public static string ProgressNetwork => IsRu ? "Проверка сети и прокси…" : "Checking network and proxy…";

    public static string ProgressSelfUpdate => IsRu ? "Проверка обновлений установщика…" : "Checking for installer updates…";

    public static string ProgressPythonGit =>
        IsRu
            ? "Загрузка Python и Git… Это может занять несколько минут — идёт скачивание."
            : "Downloading Python and Git… This may take a few minutes (large downloads).";

    public static string ProgressRepo =>
        IsRu
            ? "Синхронизация репозитория… Подождите — идёт загрузка истории и файлов."
            : "Syncing repository… Please wait — fetching history and files.";

    public static string ProgressPip =>
        IsRu
            ? "Установка зависимостей Python (pip)… Обычно это самый долгий шаг — идёт скачивание пакетов."
            : "Installing Python dependencies (pip)… This step is often the slowest — downloading packages.";

    public static string ProgressPort => IsRu ? "Проверка порта 3000…" : "Checking port 3000…";

    public static string ProgressServer => IsRu ? "Запуск веб-сервера…" : "Starting web server…";

    public static string ProgressWaitHttp =>
        IsRu
            ? "Ожидание ответа сервера на http://localhost:3000 … Первый запуск может занять минуту."
            : "Waiting for the server at http://localhost:3000 … First start can take up to a minute.";

    public static string ProgressBrowser => IsRu ? "Открытие браузера…" : "Opening browser…";

    public static string ProgressDone => IsRu ? "Готово. Сервер запущен." : "Done. Server is running.";

    public static string ErrorWindowsOnly =>
        IsRu
            ? "Этот установщик работает только в Windows."
            : "This installer runs on Windows only.";

    public static string ErrorRepoMissingWebServer =>
        IsRu
            ? "В репозитории нет папки qwertystock_web_server/. Убедитесь, что она есть в ветке на сервере."
            : "Repository is missing qwertystock_web_server/. Ensure it exists on the server branch.";

    public static string ErrorPortInUse =>
        IsRu
            ? "Порт 3000 занят. Закройте другое приложение, которое его использует."
            : "Port 3000 is in use. Close the other application using it.";
}
