@echo off
echo ============================================
echo  C3DDeepSeek - Deploy para Civil 3D 2026
echo ============================================
echo.

setlocal enabledelayedexpansion

cd /d "%~dp0"

set "SOURCE=bin\Release\net8.0-windows"
set "TARGET=%USERPROFILE%\OneDrive - ATERPA\00. PERSONALIZADOS\AUTODESK\PERSONALIZADOS\DEEPSEEK\C3DDeepSeek.bundle\Contents"

if not exist "%SOURCE%\C3DDeepSeek.dll" (
    echo ERRO: DLL nao encontrada. Execute build.bat primeiro.
    pause
    exit /b 1
)

echo [1/3] Criando estrutura de bundle...
if not exist "%TARGET%" mkdir "%TARGET%"

echo [2/3] Copiando arquivos...
copy /Y "%SOURCE%\*.dll" "%TARGET%\" >nul
copy /Y "%SOURCE%\*.json" "%TARGET%\" >nul 2>nul
copy /Y ".env" "%TARGET%\" >nul 2>&1
copy /Y "README.txt" "%TARGET%\" >nul 2>&1

echo [3/3] Criando PackageContents.xml...
(
echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<ApplicationPackage
echo   SchemaVersion="1.0"
echo   AppVersion="1.0.0"
echo   Author="C3D DeepSeek"
echo   Name="C3DDeepSeekAI"
echo   Description="Assistente IA DeepSeek para AutoCAD/Civil 3D 2026"
echo   HelpFile="./Contents/README.txt"^>
echo   ^<CompanyDetails
echo     Name="C3D DeepSeek"
echo     Url="https://deepseek.com" /^>
echo   ^<Components^>
echo     ^<RuntimeRequirements
echo       OS="Win64"
echo       Platform="AutoCAD"
echo       SeriesMin="R25.1"
echo       SeriesMax="R25.1" /^>
echo     ^<ComponentEntry
echo       AppName="C3DDeepSeek"
echo       AppDescription="DeepSeek AI Chat para Civil 3D"
echo       ModuleName="./Contents/C3DDeepSeek.dll"
echo       LoadOnAutoCADStartup="True"
echo       LoadOnCommandInvocation="False"^>
echo       ^<Commands GroupName="DeepSeekAI"^>
echo         ^<Command Global="DEEPSEEK" Local="DEEPSEEK" /^>
echo         ^<Command Global="DSASK" Local="DSASK" /^>
echo         ^<Command Global="CONFIGDS" Local="CONFIGDS" /^>
echo         ^<Command Global="DSREPORT" Local="DSREPORT" /^>
echo         ^<Command Global="DSANALYZE" Local="DSANALYZE" /^>
echo         ^<Command Global="DSMODEL" Local="DSMODEL" /^>
echo         ^<Command Global="DSCHECK" Local="DSCHECK" /^>
echo         ^<Command Global="DSCOMPARE" Local="DSCOMPARE" /^>
echo         ^<Command Global="DSWORKFLOW" Local="DSWORKFLOW" /^>
echo         ^<Command Global="DSCALC" Local="DSCALC" /^>
echo         ^<Command Global="DSIMPORT" Local="DSIMPORT" /^>
echo         ^<Command Global="DSEXPORT" Local="DSEXPORT" /^>
echo         ^<Command Global="DSCODE" Local="DSCODE" /^>
echo         ^<Command Global="DSOPTIMIZE" Local="DSOPTIMIZE" /^>
echo         ^<Command Global="DSASSEMBLY" Local="DSASSEMBLY" /^>
echo         ^<Command Global="DSSHEETS" Local="DSSHEETS" /^>
echo         ^<Command Global="DSCLASH" Local="DSCLASH" /^>
echo         ^<Command Global="DSDIM" Local="DSDIM" /^>
echo         ^<Command Global="DSDESIGN" Local="DSDESIGN" /^>
echo         ^<Command Global="DSBIM" Local="DSBIM" /^>
echo         ^<Command Global="DSTRANSFORM" Local="DSTRANSFORM" /^>
echo         ^<Command Global="DSTEMPLATE" Local="DSTEMPLATE" /^>
echo         ^<Command Global="DSEARTH" Local="DSEARTH" /^>
echo         ^<Command Global="DSBRUCKNER" Local="DSBRUCKNER" /^>
echo         ^<Command Global="DSDRAINAGE" Local="DSDRAINAGE" /^>
echo         ^<Command Global="DSGOOGLEMAPS" Local="DSGOOGLEMAPS" /^>
echo         ^<Command Global="DSCONNECT" Local="DSCONNECT" /^>
echo         ^<Command Global="DSSECTIONS" Local="DSSECTIONS" /^>
echo       ^</Commands^>
echo     ^</ComponentEntry^>
echo   ^</Components^>
echo ^</ApplicationPackage^>
) > "%TARGET%\..\PackageContents.xml"

echo.
echo [4/4] Atualizando registro do AutoCAD para carregar do OneDrive...
set "PS_CMD=$dll='%TARGET%\C3DDeepSeek.dll'; $codes=@('ACAD-9100:409','ACAD-9101:409','ACAD-9102:409','ACAD-9104:409'); foreach($c in $codes){ $p='HKCU:\SOFTWARE\Autodesk\AutoCAD\R25.1\'+$c+'\Applications\C3DDeepSeek'; New-Item -Path $p -Force | Out-Null; Set-ItemProperty -Path $p -Name 'DESCRIPTION' -Value 'C3D DeepSeek AI' -Type String; Set-ItemProperty -Path $p -Name 'LOADCTRLS' -Value 2 -Type DWord; Set-ItemProperty -Path $p -Name 'LOADER' -Value $dll -Type String; Set-ItemProperty -Path $p -Name 'MANAGED' -Value 1 -Type DWord; }"
powershell -ExecutionPolicy Bypass -Command "%PS_CMD%"

echo.
echo Deploy concluido!
echo.
echo Arquivos em: %TARGET%
echo.
echo Para usar no Civil 3D 2026:
echo   1. NETLOAD
echo   2. Selecione: %TARGET%\C3DDeepSeek.dll
echo   3. DEEPSEEK
echo.
echo Ou execute o atalho na Area de Trabalho:
echo   C3D DeepSeek Bridge
echo.
pause
