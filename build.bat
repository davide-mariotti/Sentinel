@echo off
rem Builds Sentinel.exe with the C# compiler that ships with Windows (.NET Framework 4). Nothing to install.
cd /d "%~dp0"
echo Compiling Sentinel...
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /optimize+ /target:winexe /out:Sentinel.exe ^
  /win32icon:icon\sentinel.ico /win32manifest:sentinel.manifest ^
  /resource:icon\sentinel.ico,Sentinel.ico /resource:icon\sentinel-off.ico,SentinelOff.ico ^
  Sentinel.cs SettingsForm.cs AssemblyInfo.cs
if %errorlevel% neq 0 (
    echo Compilation failed.
    pause
    exit /b %errorlevel%
)
echo Packing Sentinel-portable.zip (the same exe plus Sentinel.ini)...
powershell -NoProfile -Command "$d = Join-Path $env:TEMP 'sentinel-portable'; Remove-Item $d -Recurse -Force -ErrorAction SilentlyContinue; New-Item -ItemType Directory $d | Out-Null; Copy-Item Sentinel.exe $d; Set-Content (Join-Path $d 'Sentinel.ini') '; Sentinel settings. While this file sits next to Sentinel.exe, nothing goes to the registry.'; Compress-Archive (Join-Path $d '*') Sentinel-portable.zip -Force; Remove-Item $d -Recurse -Force"
echo Done: Sentinel.exe and Sentinel-portable.zip
