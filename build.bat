@echo off
rem Builds Sentinel.exe with the C# compiler that ships with Windows (.NET Framework 4). Nothing to install.
rem The settings window (ui\) and WebView2, the Edge engine that shows it (lib\), go inside the exe: one file to download.
cd /d "%~dp0"
setlocal enabledelayedexpansion
set RES=
for %%f in (lib\*.dll) do set RES=!RES! /resource:%%f,%%~nxf
for %%f in (ui\*.html ui\*.css ui\*.js) do set RES=!RES! /resource:%%f,ui/%%~nxf
for %%f in (ui\fonts\*.woff2) do set RES=!RES! /resource:%%f,ui/fonts/%%~nxf
echo Compiling Sentinel...
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /optimize+ /target:winexe /out:Sentinel.exe ^
  /win32icon:icon\sentinel.ico /win32manifest:sentinel.manifest ^
  /resource:icon\sentinel.ico,Sentinel.ico /resource:icon\sentinel-off.ico,SentinelOff.ico %RES% ^
  /reference:lib\Microsoft.Web.WebView2.Core.dll /reference:lib\Microsoft.Web.WebView2.WinForms.dll /reference:System.Web.Extensions.dll ^
  Sentinel.cs SettingsForm.cs AssemblyInfo.cs
if %errorlevel% neq 0 (
    echo Compilation failed.
    pause
    exit /b %errorlevel%
)
echo Packing Sentinel-portable.zip (the same exe plus Sentinel.ini)...
powershell -NoProfile -Command "$d = Join-Path $env:TEMP 'sentinel-portable'; Remove-Item $d -Recurse -Force -ErrorAction SilentlyContinue; New-Item -ItemType Directory $d | Out-Null; Copy-Item Sentinel.exe $d; Set-Content (Join-Path $d 'Sentinel.ini') '; Sentinel settings. While this file sits next to Sentinel.exe, nothing goes to the registry.'; Compress-Archive (Join-Path $d '*') Sentinel-portable.zip -Force; Remove-Item $d -Recurse -Force"
echo Done: Sentinel.exe and Sentinel-portable.zip
