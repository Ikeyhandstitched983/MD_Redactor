$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Test-Command {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    return [bool](Get-Command -Name $Name -ErrorAction SilentlyContinue)
}

function Install-WithWinget {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageId,

        [switch]$UserScope
    )

    if (-not (Test-Command -Name "winget")) {
        throw "winget не найден. Невозможно автоматически установить пакет $PackageId."
    }

    $arguments = @(
        "install",
        "--id", $PackageId,
        "--exact",
        "--source", "winget",
        "--accept-package-agreements",
        "--accept-source-agreements",
        "--silent"
    )

    if ($UserScope) {
        $arguments += @("--scope", "user")
    }

    & winget @arguments
    return $LASTEXITCODE -eq 0
}

function Ensure-Command {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$PackageId
    )

    if (Test-Command -Name $Name) {
        Write-Host "$Name найден."
        return
    }

    Write-Host "$Name не найден. Пробую установить $PackageId через winget."
    if (-not (Install-WithWinget -PackageId $PackageId -UserScope)) {
        Write-Host "Установка $PackageId в профиль пользователя не удалась. Пробую стандартную установку winget."
        if (-not (Install-WithWinget -PackageId $PackageId)) {
            throw "Не удалось установить $PackageId."
        }
    }

    if (-not (Test-Command -Name $Name)) {
        throw "$Name по-прежнему не найден после установки. Перезапустите терминал и повторите сборку."
    }
}

function Ensure-DotNet10Sdk {
    Ensure-Command -Name "dotnet" -PackageId "Microsoft.DotNet.SDK.10"

    $sdks = & dotnet --list-sdks
    if ($sdks -match "^10\.") {
        Write-Host ".NET 10 SDK найден."
        return
    }

    Write-Host ".NET 10 SDK не найден. Пробую установить Microsoft.DotNet.SDK.10 через winget."
    if (-not (Install-WithWinget -PackageId "Microsoft.DotNet.SDK.10" -UserScope)) {
        Write-Host "Установка .NET 10 SDK в профиль пользователя не удалась. Пробую стандартную установку winget."
        if (-not (Install-WithWinget -PackageId "Microsoft.DotNet.SDK.10")) {
            throw "Не удалось установить .NET 10 SDK."
        }
    }

    $sdks = & dotnet --list-sdks
    if (-not ($sdks -match "^10\.")) {
        throw ".NET 10 SDK по-прежнему не найден после установки."
    }
}

function Test-WebView2Runtime {
    $clientId = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
    $registryPaths = @(
        "HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\$clientId",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\$clientId",
        "HKCU:\SOFTWARE\Microsoft\EdgeUpdate\Clients\$clientId"
    )

    foreach ($path in $registryPaths) {
        if (Test-Path -LiteralPath $path) {
            $properties = Get-ItemProperty -LiteralPath $path -Name "pv" -ErrorAction SilentlyContinue
            $version = if ($null -ne $properties) { $properties.pv } else { $null }
            if (-not [string]::IsNullOrWhiteSpace($version)) {
                Write-Host "WebView2 Runtime найден: $version."
                return $true
            }
        }
    }

    return $false
}

function Ensure-WebView2Runtime {
    if (Test-WebView2Runtime) {
        return
    }

    Write-Host "WebView2 Runtime не найден по реестру. Пробую установить Microsoft.EdgeWebView2Runtime через winget."
    if (-not (Install-WithWinget -PackageId "Microsoft.EdgeWebView2Runtime")) {
        throw "Не удалось установить WebView2 Runtime."
    }

    if (-not (Test-WebView2Runtime)) {
        Write-Warning "Установка завершилась, но bootstrap не смог подтвердить WebView2 Runtime по реестру."
    }
}

Ensure-Command -Name "git" -PackageId "Git.Git"
Ensure-DotNet10Sdk
Ensure-Command -Name "node" -PackageId "OpenJS.NodeJS.LTS"
Ensure-Command -Name "npm" -PackageId "OpenJS.NodeJS.LTS"
Ensure-WebView2Runtime

Write-Host "Окружение готово."
