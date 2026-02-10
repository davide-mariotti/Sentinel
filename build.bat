@echo off
echo Compiling Sentinel...
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:Sentinel.exe /win32icon:sentinel.ico Sentinel.cs SettingsForm.cs AssemblyInfo.cs
if %errorlevel% neq 0 (
    echo Compilation Failed!
    pause
    exit /b %errorlevel%
)
echo Compilation Successful!
pause
