@echo off
echo ============================================
echo Building Solus Manifest App
echo ============================================
echo.

REM Clean previous build
echo [1/3] Cleaning previous build...
dotnet clean
if %errorlevel% neq 0 (
    echo ERROR: Clean failed!
    pause
    exit /b %errorlevel%
)
echo.

REM Build Release
echo [2/3] Building Release configuration...
dotnet publish -c Release -r win-x64
if %errorlevel% neq 0 (
    echo ERROR: Build failed!
    pause
    exit /b %errorlevel%
)
echo.

REM Show results
echo [3/3] Build complete!
echo.
echo Output location:
echo %~dp0bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\
echo.
echo Executable:
dir /b "%~dp0bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\SolusManifestApp.exe"
echo.

REM Show file size
for %%I in ("%~dp0bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\SolusManifestApp.exe") do echo Size: %%~zI bytes (%%~zI / 1048576 = %%~zI MB approx)
echo.

echo Build successful! Press any key to exit.
pause
