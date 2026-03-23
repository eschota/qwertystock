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
            ? "Устанавливаем всё необходимое для работы QwertyStock, затем откроем рабочий экран в браузере."
            : "We install everything needed to run QwertyStock, then open your workspace in the browser.";

    public static string PipStartingSummary(int topLevelLines) =>
        IsRu
            ? $"pip: установка по requirements.txt (~{topLevelLines} зависимостей в списке)…"
            : $"pip: installing from requirements.txt (~{topLevelLines} top-level entries)…";

    public static string PipInstallFailed(int exitCode, string stderrTail) =>
        IsRu
            ? $"pip завершился с кодом {exitCode}. Последние строки:\n{stderrTail}"
            : $"pip exited with code {exitCode}. Last output:\n{stderrTail}";

    public static string PipEtaRough(int secondsTotal) =>
        IsRu
            ? $"оценка времени pip: ~{secondsTotal} с (зависит от сети и пакетов)"
            : $"pip time estimate: ~{secondsTotal}s (depends on network & packages)";

    public static string PipEtaRemaining(int seconds) =>
        IsRu ? $"осталось ~{seconds} с (оценка)" : $"~{seconds}s left (estimate)";

    public static string StatusStarting => IsRu ? "Запуск…" : "Starting…";

    public static string FormatVersion(string semantic) =>
        IsRu ? $"Версия {semantic}" : $"Version {semantic}";

    public static string CloseTooltip => IsRu ? "Закрыть" : "Close";

    public static string AppTitle => "QwertyStock";

    public static string ProgressPreparing => IsRu ? "Подготовка…" : "Preparing…";

    /// <summary>Compact transfer ETA label (e.g. <c>2:10</c>).</summary>
    public static string ProgressShortEta(string timeLike) =>
        IsRu ? $"~{timeLike}" : $"ETA {timeLike}";

    public static string ProgressNetwork => IsRu ? "Проверка сети и прокси…" : "Checking network and proxy…";

    public static string ProgressCheckingDisk =>
        IsRu ? "Проверка свободного места на диске…" : "Checking free disk space…";

    public static string ErrorDiskUnknown =>
        IsRu ? "Не удалось определить диск для установки." : "Could not determine the install drive.";

    public static string ErrorDiskNotReady =>
        IsRu ? "Диск недоступен." : "The drive is not ready.";

    public static string ErrorInsufficientDiskSpace =>
        IsRu
            ? "Недостаточно свободного места. Сейчас доступно: {0}. Нужно не меньше: {1}."
            : "Not enough free disk space. Available: {0}. Required at least: {1}.";

    public static string NetworkErrorTitle =>
        IsRu ? "Проблема с сетью" : "Network problem";

    public static string ErrorInstallFailedTitle =>
        IsRu ? "Ошибка установки" : "Installation failed";

    public static string OpenLogsFolderButton =>
        IsRu ? "Открыть папку с логами" : "Open log folder";

    public static string RetryInstall =>
        IsRu ? "Повторить" : "Retry";

    public static string CloseAfterError =>
        IsRu ? "Закрыть" : "Close";

    public static string CancelRetryInstall =>
        IsRu ? "Отмена" : "Cancel";

    public static string LogFileHint =>
        IsRu
            ? $"Подробности в журнале: {InstallerPaths.InstallerLog}"
            : $"Details are logged to: {InstallerPaths.InstallerLog}";

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

    public static string ProgressPort =>
        IsRu ? $"Проверка порта {InstallerPaths.ServerPort}…" : $"Checking port {InstallerPaths.ServerPort}…";

    public static string ProgressPortFreeing =>
        IsRu
            ? $"Порт {InstallerPaths.ServerPort} занят — завершаем процесс, который его слушает…"
            : $"Port {InstallerPaths.ServerPort} is in use — stopping the process that holds it…";

    public static string ProgressServer => IsRu ? "Запуск веб-сервера…" : "Starting web server…";

    public static string ProgressWaitHttp =>
        IsRu
            ? $"Ожидание ответа сервера на http://127.0.0.1:{InstallerPaths.ServerPort} … Первый запуск может занять минуту."
            : $"Waiting for the server at http://127.0.0.1:{InstallerPaths.ServerPort} … First start can take up to a minute.";

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
            ? $"Порт {InstallerPaths.ServerPort} занят. Закройте другое приложение, которое его использует."
            : $"Port {InstallerPaths.ServerPort} is in use. Close the other application using it.";

    public static string ProgressManifest =>
        IsRu ? "Загрузка манифеста установщика…" : "Loading installer manifest…";

    public static string ErrorMirrorNeedsPythonSha256 =>
        IsRu
            ? "В version.json для зеркала Python укажите pythonEmbedZipSha256 (SHA256 совпадает с оригинальным архивом)."
            : "Set pythonEmbedZipSha256 in version.json for a custom Python zip mirror (must match the official archive bytes).";

    public static string ErrorMirrorNeedsMinGitSha256 =>
        IsRu
            ? "В version.json для зеркала MinGit укажите minGitZipSha256."
            : "Set minGitZipSha256 in version.json for a custom MinGit zip mirror.";

    public static string ErrorMirrorNeedsGetPipSha256 =>
        IsRu
            ? "В version.json для зеркала get-pip укажите getPipSha256."
            : "Set getPipSha256 in version.json for a custom get-pip.py mirror.";

    public static string UninstallConfirm =>
        IsRu
            ? "Удалить данные QwertyStock из этого компьютера (папка в AppData, автозагрузка, локальный сервер)? Сам файл установщика нужно удалить вручную, если он у вас в Загрузках."
            : "Remove QwertyStock data from this PC (AppData folder, startup entry, local server)? Delete the installer file yourself if it is still in Downloads.";

    public static string UninstallDone =>
        IsRu
            ? "Данные QwertyStock удалены."
            : "QwertyStock data has been removed.";

    public static string UninstallDeleteFailed =>
        IsRu
            ? "Не удалось полностью удалить папку данных: {0}"
            : "Could not fully delete the data folder: {0}";

    public static string TrayTooltipFormat(string productVersion) =>
        IsRu
            ? $"QwertyStock {productVersion} — локальный кабинет"
            : $"QwertyStock {productVersion} — local cabinet";

    public static string TrayOpenCabinet =>
        IsRu ? "Открыть кабинет" : "Open cabinet";

    public static string TrayForceUpdate =>
        IsRu ? "Принудительно обновить репозиторий" : "Update repository now";

    public static string TrayBalloonTitle => "QwertyStock";

    public static string TrayForceUpdateStartedBody =>
        IsRu
            ? "Перезапуск сервера кабинета, затем git (fetch, reset, pip)…"
            : "Restarting cabinet server, then git (fetch, reset, pip)…";

    public static string TrayForceUpdateAlreadyCurrentBody =>
        IsRu
            ? "После git: репозиторий уже актуален. Первый перезапуск для принудительного обновления уже выполнен."
            : "After git: repository is already up to date. Forced server reload was already applied first.";

    public static string TrayForceUpdateUpdatedBody =>
        IsRu
            ? "Есть изменения: код или зависимости обновлены, сервер кабинета перезапущен."
            : "Updates applied (code or dependencies); cabinet server restarted.";

    public static string TrayForceUpdateErrorTitle =>
        IsRu ? "Синхронизация не удалась" : "Sync failed";

    /// <summary>Git/pip lock не взят: сервер уже перезапущен в начале ручного обновления.</summary>
    public static string TrayForceUpdateLockWaitTimeoutBody =>
        IsRu
            ? "Сервер кабинета уже перезапущен, но git-синхронизацию начать не удалось (lock занят >2 мин). Повторите позже или перезапустите QwertyStock."
            : "Cabinet server was already restarted, but git sync could not start (lock held >2 min). Retry later or restart QwertyStock.";

    public static string TrayForceUpdateCancelledOrTimeoutBody =>
        IsRu
            ? "Синхронизация остановлена по таймауту (45 мин) или отменена."
            : "Sync stopped (45 min timeout) or was cancelled.";

    public static string TrayExit =>
        IsRu ? "Выйти" : "Exit";
}
