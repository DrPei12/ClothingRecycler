using System.Diagnostics;
using System.Drawing;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

using Microsoft.Win32;

namespace ClothingRecycler.Installer;

internal static class Program
{
    private const string AppName = "衣物回收管理";
    private const string Publisher = "ClothingRecycler";
    private const string ExeName = "ClothingRecycler.Desktop.exe";
    private const string UninstallBatName = "Uninstall-ClothingRecycler.bat";
    private const string ResourceName = "ClothingRecycler.Installer.payload.zip";
    private const string UninstallRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ClothingRecycler";

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var quietMode = args.Any(static arg =>
            string.Equals(arg, "--quiet", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "/quiet", StringComparison.OrdinalIgnoreCase));

        try
        {
            var requestedInstallRoot = ParseInstallDirectoryArgument(args);
            var previousInstallRoot = ReadPreviousInstallRoot();
            var suggestedInstallRoot = NormalizePath(
                string.IsNullOrWhiteSpace(requestedInstallRoot)
                    ? previousInstallRoot ?? GetRecommendedInstallRoot()
                    : requestedInstallRoot);

            InstallSelection? selection;
            if (quietMode)
            {
                selection = new InstallSelection(suggestedInstallRoot, LaunchAfterInstall: false);
            }
            else
            {
                selection = ShowInstallSelectionDialog(suggestedInstallRoot, previousInstallRoot);
                if (selection is null)
                {
                    return 0;
                }
            }

            var installRoot = ValidateInstallRoot(selection.InstallRoot, previousInstallRoot, quietMode);
            var startMenuFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft",
                "Windows",
                "Start Menu",
                "Programs",
                "ClothingRecycler");
            var desktopShortcutPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                $"{AppName}.lnk");
            var startMenuShortcutPath = Path.Combine(startMenuFolder, $"{AppName}.lnk");
            var uninstallShortcutPath = Path.Combine(startMenuFolder, $"卸载{AppName}.lnk");
            var exePath = Path.Combine(installRoot, ExeName);
            var uninstallBatPath = Path.Combine(installRoot, UninstallBatName);
            var version = GetDisplayVersion();
            var tempRoot = Path.Combine(
                Path.GetTempPath(),
                "ClothingRecyclerInstaller",
                Guid.NewGuid().ToString("N"));
            string? cleanupWarningMessage = null;

            try
            {
                StopRunningApp();

                Directory.CreateDirectory(tempRoot);
                var payloadZipPath = Path.Combine(tempRoot, "payload.zip");
                var payloadExtractRoot = Path.Combine(tempRoot, "payload");
                ExtractPayload(payloadZipPath);
                ZipFile.ExtractToDirectory(payloadZipPath, payloadExtractRoot, overwriteFiles: true);

                var shouldRefreshExistingInstall = ShouldRefreshExistingInstall(installRoot, previousInstallRoot);
                PrepareInstallDirectory(installRoot, shouldRefreshExistingInstall);
                CopyDirectoryContents(payloadExtractRoot, installRoot);

                Directory.CreateDirectory(startMenuFolder);
                CreateShortcut(desktopShortcutPath, exePath, installRoot, exePath);
                CreateShortcut(startMenuShortcutPath, exePath, installRoot, exePath);
                CreateShortcut(uninstallShortcutPath, uninstallBatPath, installRoot, exePath);
                WriteUninstallRegistration(version, installRoot, exePath, uninstallBatPath);
                cleanupWarningMessage = TryDeletePreviousInstall(previousInstallRoot, installRoot);
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }

            if (!quietMode)
            {
                var successMessage = $"安装完成。{Environment.NewLine}{Environment.NewLine}安装目录：{installRoot}";
                var icon = MessageBoxIcon.Information;

                if (!string.IsNullOrWhiteSpace(cleanupWarningMessage))
                {
                    successMessage += $"{Environment.NewLine}{Environment.NewLine}{cleanupWarningMessage}";
                    icon = MessageBoxIcon.Warning;
                }

                MessageBox.Show(
                    successMessage,
                    AppName,
                    MessageBoxButtons.OK,
                    icon);

                if (selection.LaunchAfterInstall)
                {
                    StartInstalledApp(exePath, installRoot);
                }
            }
            else if (!string.IsNullOrWhiteSpace(cleanupWarningMessage))
            {
                Console.Error.WriteLine(cleanupWarningMessage);
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            if (!quietMode)
            {
                MessageBox.Show(
                    $"安装失败：{ex.Message}{Environment.NewLine}{Environment.NewLine}请重新解压发布包后再试，或联系开发者检查日志。",
                    AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            else
            {
                Console.Error.WriteLine(ex.Message);
            }

            return 1;
        }
    }

    private static string GetRecommendedInstallRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "ClothingRecycler");
    }

    private static string? ParseInstallDirectoryArgument(string[] args)
    {
        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];

            if (arg.StartsWith("--install-dir=", StringComparison.OrdinalIgnoreCase))
            {
                return arg["--install-dir=".Length..].Trim('"');
            }

            if (arg.StartsWith("/install-dir=", StringComparison.OrdinalIgnoreCase))
            {
                return arg["/install-dir=".Length..].Trim('"');
            }

            if (string.Equals(arg, "--install-dir", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "/install-dir", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new InvalidOperationException("缺少安装目录参数值。");
                }

                return args[index + 1].Trim('"');
            }
        }

        return null;
    }

    private static InstallSelection? ShowInstallSelectionDialog(string initialInstallRoot, string? previousInstallRoot)
    {
        using var form = new Form
        {
            Text = $"{AppName} 安装",
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(620, 250),
            Font = new Font("Microsoft YaHei UI", 9F),
            ShowIcon = true
        };

        using var titleLabel = new Label
        {
            AutoSize = true,
            Left = 20,
            Top = 18,
            Font = new Font(form.Font, FontStyle.Bold),
            Text = "选择安装目录"
        };

        using var descriptionLabel = new Label
        {
            AutoSize = false,
            Left = 20,
            Top = 46,
            Width = 580,
            Height = 36,
            Text = "建议选择一个专用文件夹。默认会使用当前用户的本地程序目录，也可以改到其他磁盘。"
        };

        using var pathLabel = new Label
        {
            AutoSize = true,
            Left = 20,
            Top = 96,
            Text = "安装到："
        };

        using var pathTextBox = new TextBox
        {
            Left = 20,
            Top = 118,
            Width = 470,
            Text = initialInstallRoot
        };

        using var browseButton = new Button
        {
            Left = 500,
            Top = 116,
            Width = 90,
            Height = 28,
            Text = "浏览..."
        };

        var noteText = string.IsNullOrWhiteSpace(previousInstallRoot)
            ? "默认推荐路径：%LOCALAPPDATA%\\Programs\\ClothingRecycler"
            : $"检测到已有安装，将默认沿用上次安装目录：{previousInstallRoot}";
        using var noteLabel = new Label
        {
            AutoSize = false,
            Left = 20,
            Top = 154,
            Width = 570,
            Height = 36,
            ForeColor = Color.FromArgb(90, 90, 90),
            Text = noteText
        };

        using var launchCheckBox = new CheckBox
        {
            Left = 20,
            Top = 196,
            Width = 220,
            Checked = true,
            Text = "安装完成后立即启动"
        };

        using var installButton = new Button
        {
            Left = 404,
            Top = 202,
            Width = 90,
            Height = 30,
            Text = "安装",
            DialogResult = DialogResult.OK
        };

        using var cancelButton = new Button
        {
            Left = 500,
            Top = 202,
            Width = 90,
            Height = 30,
            Text = "取消",
            DialogResult = DialogResult.Cancel
        };

        browseButton.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "选择安装文件夹",
                ShowNewFolderButton = true,
                SelectedPath = pathTextBox.Text
            };

            if (dialog.ShowDialog(form) == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                pathTextBox.Text = dialog.SelectedPath;
            }
        };

        installButton.Click += (_, _) =>
        {
            try
            {
                ValidateInstallRoot(pathTextBox.Text, previousInstallRoot, quietMode: false);
            }
            catch (OperationCanceledException)
            {
                form.DialogResult = DialogResult.None;
            }
            catch (Exception ex)
            {
                form.DialogResult = DialogResult.None;
                MessageBox.Show(form, ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        form.Controls.AddRange(
        [
            titleLabel,
            descriptionLabel,
            pathLabel,
            pathTextBox,
            browseButton,
            noteLabel,
            launchCheckBox,
            installButton,
            cancelButton
        ]);
        form.AcceptButton = installButton;
        form.CancelButton = cancelButton;

        if (form.ShowDialog() != DialogResult.OK)
        {
            return null;
        }

        return new InstallSelection(pathTextBox.Text, launchCheckBox.Checked);
    }

    private static string ValidateInstallRoot(string installRoot, string? previousInstallRoot, bool quietMode)
    {
        var normalizedInstallRoot = NormalizePath(installRoot);

        if (string.IsNullOrWhiteSpace(normalizedInstallRoot))
        {
            throw new InvalidOperationException("安装目录不能为空。");
        }

        if (File.Exists(normalizedInstallRoot))
        {
            throw new InvalidOperationException("选择的安装路径已经是一个文件，请改选文件夹。");
        }

        var driveRoot = Path.GetPathRoot(normalizedInstallRoot);
        if (!string.IsNullOrWhiteSpace(driveRoot) &&
            string.Equals(
                driveRoot.TrimEnd(Path.DirectorySeparatorChar),
                normalizedInstallRoot.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("不能直接安装到磁盘根目录，请选择一个专用文件夹。");
        }

        if (!string.IsNullOrWhiteSpace(previousInstallRoot) &&
            !AreSamePaths(previousInstallRoot, normalizedInstallRoot) &&
            (IsNestedPath(normalizedInstallRoot, previousInstallRoot) || IsNestedPath(previousInstallRoot, normalizedInstallRoot)))
        {
            throw new InvalidOperationException("新安装目录不能是旧安装目录的父目录或子目录，请选择一个独立文件夹。");
        }

        EnsureWritableDirectory(normalizedInstallRoot);

        if (Directory.Exists(normalizedInstallRoot) &&
            Directory.EnumerateFileSystemEntries(normalizedInstallRoot).Any() &&
            !AreSamePaths(previousInstallRoot, normalizedInstallRoot) &&
            !LooksLikeExistingInstall(normalizedInstallRoot))
        {
            if (quietMode)
            {
                throw new InvalidOperationException("静默安装要求目标目录为空，或等于当前已安装目录。");
            }

            var result = MessageBox.Show(
                $"目标目录不是空文件夹：{normalizedInstallRoot}{Environment.NewLine}{Environment.NewLine}继续安装会把程序文件解压到这个目录中。是否继续？",
                AppName,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
            {
                throw new OperationCanceledException("用户取消安装。");
            }
        }

        return normalizedInstallRoot;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
    }

    private static bool AreSamePaths(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            NormalizePath(left).TrimEnd(Path.DirectorySeparatorChar),
            NormalizePath(right).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNestedPath(string path, string otherPath)
    {
        var normalizedPath = NormalizePath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedOtherPath = NormalizePath(otherPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(normalizedOtherPath, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureWritableDirectory(string path)
    {
        Directory.CreateDirectory(path);
        var testFile = Path.Combine(path, $".install-write-test-{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(testFile, "ok");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("所选安装目录不可写，请选择其他目录，或改用管理员权限重试。", ex);
        }
        finally
        {
            try
            {
                if (File.Exists(testFile))
                {
                    File.Delete(testFile);
                }
            }
            catch
            {
            }
        }
    }

    private static string? ReadPreviousInstallRoot()
    {
        using var key = Registry.CurrentUser.OpenSubKey(UninstallRegistryPath, writable: false);
        var value = key?.GetValue("InstallLocation") as string;
        return string.IsNullOrWhiteSpace(value) ? null : NormalizePath(value);
    }

    private static void ExtractPayload(string destinationPath)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("安装载荷不存在，无法继续安装。");
        using var fileStream = File.Create(destinationPath);
        stream.CopyTo(fileStream);
    }

    private static string GetDisplayVersion()
    {
        var infoVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            return infoVersion.Split('+')[0];
        }

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version is null)
        {
            return "1.0.5";
        }

        return $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static bool ShouldRefreshExistingInstall(string installRoot, string? previousInstallRoot)
    {
        return AreSamePaths(previousInstallRoot, installRoot) || LooksLikeExistingInstall(installRoot);
    }

    private static bool LooksLikeExistingInstall(string installRoot)
    {
        if (!Directory.Exists(installRoot))
        {
            return false;
        }

        return File.Exists(Path.Combine(installRoot, ExeName))
            || File.Exists(Path.Combine(installRoot, UninstallBatName));
    }

    private static void PrepareInstallDirectory(string installRoot, bool shouldRefreshExistingInstall)
    {
        Directory.CreateDirectory(installRoot);

        if (!shouldRefreshExistingInstall)
        {
            return;
        }

        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(installRoot))
            {
                if (Directory.Exists(entry))
                {
                    Directory.Delete(entry, recursive: true);
                }
                else
                {
                    File.Delete(entry);
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("无法清理旧版本程序文件，请关闭可能占用这些文件的程序后重试。", ex);
        }
    }

    private static void CopyDirectoryContents(string sourceRoot, string destinationRoot)
    {
        Directory.CreateDirectory(destinationRoot);

        foreach (var directoryPath in Directory.GetDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, directoryPath);
            Directory.CreateDirectory(Path.Combine(destinationRoot, relativePath));
        }

        foreach (var filePath in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, filePath);
            var destinationPath = Path.Combine(destinationRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(filePath, destinationPath, overwrite: true);
        }
    }

    private static void StopRunningApp()
    {
        foreach (var process in Process.GetProcessesByName("ClothingRecycler.Desktop"))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch
            {
            }
        }
    }

    private static void StartInstalledApp(string exePath, string workingDirectory)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = true
        });
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory, string iconPath)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("当前系统无法创建快捷方式。");
        dynamic shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("无法初始化快捷方式服务。");
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.IconLocation = iconPath;
        shortcut.Save();
    }

    private static void WriteUninstallRegistration(string version, string installRoot, string exePath, string uninstallBatPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallRegistryPath, writable: true)
            ?? throw new InvalidOperationException("无法写入卸载注册信息。");

        key.SetValue("DisplayName", AppName);
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", Publisher);
        key.SetValue("InstallLocation", installRoot);
        key.SetValue("DisplayIcon", exePath);
        key.SetValue("UninstallString", $"\"{uninstallBatPath}\"");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static string? TryDeletePreviousInstall(string? previousInstallRoot, string installRoot)
    {
        if (string.IsNullOrWhiteSpace(previousInstallRoot) || AreSamePaths(previousInstallRoot, installRoot))
        {
            return null;
        }

        if (!Directory.Exists(previousInstallRoot))
        {
            return null;
        }

        var previousExePath = Path.Combine(previousInstallRoot, ExeName);
        if (!File.Exists(previousExePath))
        {
            return null;
        }

        try
        {
            Directory.Delete(previousInstallRoot, recursive: true);
            return null;
        }
        catch
        {
            return $"注意：新版本已经安装完成，但旧版本目录未能自动删除，请手动检查并清理：{previousInstallRoot}";
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed record InstallSelection(string InstallRoot, bool LaunchAfterInstall);
}
