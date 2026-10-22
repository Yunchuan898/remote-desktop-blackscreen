# 一键编译脚本：用系统自带 csc.exe 编译为单个免依赖 exe
$ErrorActionPreference = "Stop"

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$fw  = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"

if (-not (Test-Path $csc)) {
    Write-Host "未找到 csc.exe，请确认安装了 .NET Framework 4.x" -ForegroundColor Red
    exit 1
}

Write-Host "正在编译 BlackScreenHelper.exe ..." -ForegroundColor Cyan

& $csc /nologo /target:winexe /out:BlackScreenHelper.exe `
    "/r:$fw\System.dll" `
    "/r:$fw\System.Core.dll" `
    "/r:$fw\System.Drawing.dll" `
    "/r:$fw\System.Windows.Forms.dll" `
    BlackScreenHelper.cs

if ($LASTEXITCODE -ne 0) {
    Write-Host "编译失败，退出码 $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "编译成功 -> BlackScreenHelper.exe" -ForegroundColor Green
