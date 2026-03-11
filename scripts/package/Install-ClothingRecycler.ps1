param(
    [string]$InstallRoot,
    [switch]$NoStart,
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Windows.Forms

$appName = "衣物回收管理"
$publisher = "ClothingRecycler"
$version = "__VERSION__"
$sourceRoot = $PSScriptRoot
$defaultInstallRoot = Join-Path $env:LOCALAPPDATA "Programs\ClothingRecycler"
$startMenuFolder = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\ClothingRecycler"
$desktopShortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "$appName.lnk"
$startMenuShortcutPath = Join-Path $startMenuFolder "$appName.lnk"
$uninstallShortcutPath = Join-Path $startMenuFolder "卸载$appName.lnk"
$uninstallRegistryKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ClothingRecycler"
$exeName = "ClothingRecycler.Desktop.exe"
$excludedNames = @(
    "Install-ClothingRecycler.ps1",
    "Install-ClothingRecycler.bat"
)

function Get-PreviousInstallRoot {
    if (-not (Test-Path $uninstallRegistryKey)) {
        return $null
    }

    $value = (Get-ItemProperty -Path $uninstallRegistryKey -ErrorAction SilentlyContinue).InstallLocation
    if ([string]::IsNullOrWhiteSpace($value)) {
        return $null
    }

    return [System.IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($value))
}

function Test-SamePath {
    param(
        [string]$Left,
        [string]$Right
    )

    if ([string]::IsNullOrWhiteSpace($Left) -or [string]::IsNullOrWhiteSpace($Right)) {
        return $false
    }

    $normalizedLeft = [System.IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($Left)).TrimEnd('\')
    $normalizedRight = [System.IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($Right)).TrimEnd('\')
    return $normalizedLeft.Equals($normalizedRight, [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-NestedPath {
    param(
        [string]$Path,
        [string]$OtherPath
    )

    $normalizedPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\') + '\'
    $normalizedOtherPath = [System.IO.Path]::GetFullPath($OtherPath).TrimEnd('\') + '\'
    return $normalizedPath.StartsWith($normalizedOtherPath, [System.StringComparison]::OrdinalIgnoreCase)
}

function New-AppShortcut {
    param(
        [string]$ShortcutPath,
        [string]$TargetPath,
        [string]$WorkingDirectory,
        [string]$IconPath
    )

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = $TargetPath
    $shortcut.WorkingDirectory = $WorkingDirectory
    $shortcut.IconLocation = $IconPath
    $shortcut.Save()
}

function Select-InstallRoot {
    param(
        [string]$InitialPath,
        [string]$PreviousInstallRoot
    )

    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = "选择衣物回收管理的安装目录"
    $dialog.ShowNewFolderButton = $true
    $dialog.SelectedPath = $InitialPath

    try {
        $result = $dialog.ShowDialog()
        if ($result -ne [System.Windows.Forms.DialogResult]::OK -or [string]::IsNullOrWhiteSpace($dialog.SelectedPath)) {
            return $null
        }

        return $dialog.SelectedPath
    }
    finally {
        $dialog.Dispose()
    }
}

function Confirm-NonEmptyTarget {
    param([string]$TargetPath)

    $result = [System.Windows.Forms.MessageBox]::Show(
        "目标目录不是空文件夹：$TargetPath`r`n`r`n继续安装会把程序文件复制到这个目录中。是否继续？",
        $appName,
        [System.Windows.Forms.MessageBoxButtons]::YesNo,
        [System.Windows.Forms.MessageBoxIcon]::Warning)

    return $result -eq [System.Windows.Forms.DialogResult]::Yes
}

function Resolve-InstallRoot {
    param(
        [string]$RequestedInstallRoot,
        [string]$PreviousInstallRoot,
        [switch]$QuietMode
    )

    if ([string]::IsNullOrWhiteSpace($RequestedInstallRoot)) {
        $candidate = if ([string]::IsNullOrWhiteSpace($PreviousInstallRoot)) { $defaultInstallRoot } else { $PreviousInstallRoot }
    } else {
        $candidate = $RequestedInstallRoot
    }

    $candidate = [System.IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($candidate.Trim().Trim('"')))

    if (Test-Path $candidate -PathType Leaf) {
        throw "选择的安装路径已经是一个文件，请改选文件夹。"
    }

    $root = [System.IO.Path]::GetPathRoot($candidate)
    if (-not [string]::IsNullOrWhiteSpace($root) -and $candidate.TrimEnd('\').Equals($root.TrimEnd('\'), [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "不能直接安装到磁盘根目录，请选择一个专用文件夹。"
    }

    if (-not [string]::IsNullOrWhiteSpace($PreviousInstallRoot) -and -not (Test-SamePath $candidate $PreviousInstallRoot)) {
        if ((Test-NestedPath $candidate $PreviousInstallRoot) -or (Test-NestedPath $PreviousInstallRoot $candidate)) {
            throw "新安装目录不能是旧安装目录的父目录或子目录，请选择一个独立文件夹。"
        }
    }

    New-Item -ItemType Directory -Force -Path $candidate | Out-Null

    $writeTest = Join-Path $candidate (".install-write-test-" + [Guid]::NewGuid().ToString("N") + ".tmp")
    try {
        Set-Content -Path $writeTest -Value "ok" -Encoding ascii
    }
    catch {
        throw "所选安装目录不可写，请选择其他目录，或改用管理员权限重试。"
    }
    finally {
        Remove-Item -Path $writeTest -Force -ErrorAction SilentlyContinue
    }

    $hasEntries = (Get-ChildItem -Path $candidate -Force -ErrorAction SilentlyContinue | Measure-Object).Count -gt 0
    if ($hasEntries -and -not (Test-SamePath $candidate $PreviousInstallRoot)) {
        if ($QuietMode) {
            throw "静默安装要求目标目录为空，或等于当前已安装目录。"
        }

        if (-not (Confirm-NonEmptyTarget $candidate)) {
            throw [System.OperationCanceledException]::new("用户取消安装。")
        }
    }

    return $candidate
}

function Try-DeletePreviousInstall {
    param(
        [string]$PreviousInstallRoot,
        [string]$CurrentInstallRoot
    )

    if ([string]::IsNullOrWhiteSpace($PreviousInstallRoot) -or (Test-SamePath $PreviousInstallRoot $CurrentInstallRoot)) {
        return
    }

    if (-not (Test-Path $PreviousInstallRoot -PathType Container)) {
        return
    }

    $previousExe = Join-Path $PreviousInstallRoot $exeName
    if (-not (Test-Path $previousExe -PathType Leaf)) {
        return
    }

    try {
        Remove-Item -Path $PreviousInstallRoot -Recurse -Force -ErrorAction Stop
    }
    catch {
        # 旧目录清理失败不影响当前安装。
    }
}

$previousInstallRoot = Get-PreviousInstallRoot

if (-not $Quiet -and [string]::IsNullOrWhiteSpace($InstallRoot)) {
    $initialPath = if ([string]::IsNullOrWhiteSpace($previousInstallRoot)) { $defaultInstallRoot } else { $previousInstallRoot }
    $selected = Select-InstallRoot -InitialPath $initialPath -PreviousInstallRoot $previousInstallRoot
    if ([string]::IsNullOrWhiteSpace($selected)) {
        exit 0
    }

    $InstallRoot = $selected
}

$installRoot = Resolve-InstallRoot -RequestedInstallRoot $InstallRoot -PreviousInstallRoot $previousInstallRoot -QuietMode:$Quiet
$exePath = Join-Path $installRoot $exeName
$uninstallBatPath = Join-Path $installRoot "Uninstall-ClothingRecycler.bat"

Get-Process "ClothingRecycler.Desktop" -ErrorAction SilentlyContinue | Stop-Process -Force

New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
New-Item -ItemType Directory -Force -Path $startMenuFolder | Out-Null

Get-ChildItem -Path $sourceRoot -Force | Where-Object { $_.Name -notin $excludedNames } | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination (Join-Path $installRoot $_.Name) -Recurse -Force
}

New-AppShortcut -ShortcutPath $desktopShortcutPath -TargetPath $exePath -WorkingDirectory $installRoot -IconPath $exePath
New-AppShortcut -ShortcutPath $startMenuShortcutPath -TargetPath $exePath -WorkingDirectory $installRoot -IconPath $exePath
New-AppShortcut -ShortcutPath $uninstallShortcutPath -TargetPath $uninstallBatPath -WorkingDirectory $installRoot -IconPath $exePath

New-Item -Path $uninstallRegistryKey -Force | Out-Null
Set-ItemProperty -Path $uninstallRegistryKey -Name "DisplayName" -Value $appName
Set-ItemProperty -Path $uninstallRegistryKey -Name "DisplayVersion" -Value $version
Set-ItemProperty -Path $uninstallRegistryKey -Name "Publisher" -Value $publisher
Set-ItemProperty -Path $uninstallRegistryKey -Name "InstallLocation" -Value $installRoot
Set-ItemProperty -Path $uninstallRegistryKey -Name "DisplayIcon" -Value $exePath
Set-ItemProperty -Path $uninstallRegistryKey -Name "UninstallString" -Value ('"{0}"' -f $uninstallBatPath)
Set-ItemProperty -Path $uninstallRegistryKey -Name "NoModify" -Value 1 -Type DWord
Set-ItemProperty -Path $uninstallRegistryKey -Name "NoRepair" -Value 1 -Type DWord

Try-DeletePreviousInstall -PreviousInstallRoot $previousInstallRoot -CurrentInstallRoot $installRoot

Write-Output "Installed to: $installRoot"
Write-Output "Start Menu shortcut: $startMenuShortcutPath"
Write-Output "Desktop shortcut: $desktopShortcutPath"

if (-not $NoStart) {
    Start-Process -FilePath $exePath -WorkingDirectory $installRoot
}
