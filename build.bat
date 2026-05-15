@echo off
echo ============================================
echo  C3DDeepSeek - Build Plugin Civil 3D 2026
echo ============================================
echo.

cd /d "%~dp0"

echo [1/2] Restaurando pacotes NuGet...
dotnet restore C3DDeepSeek.csproj
if %ERRORLEVEL% NEQ 0 (
    echo ERRO: Falha ao restaurar pacotes.
    pause
    exit /b 1
)

echo.
echo [2/2] Compilando plugin...
dotnet build C3DDeepSeek.csproj -c Release
if %ERRORLEVEL% NEQ 0 (
    echo ERRO: Falha ao compilar.
    pause
    exit /b 1
)

echo.
echo Build concluido com sucesso!
echo DLL gerada em: bin\Release\net8.0-windows\C3DDeepSeek.dll
echo.
echo Agora execute deploy.bat para instalar no OneDrive.
echo.
