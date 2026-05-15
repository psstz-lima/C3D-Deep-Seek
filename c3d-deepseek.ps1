# ============================================================
# C3D DeepSeek Bridge - PowerShell + COM
# Conecta no Civil 3D 2026 em execucao e usa DeepSeek API
# ============================================================

$ErrorActionPreference = "Stop"
$scriptDir = "C:\Users\paulo.lima\OneDrive - ATERPA\00. PERSONALIZADOS\AUTODESK\PERSONALIZADOS\DEEPSEEK"

# ---------- CARREGAR CHAVE API ----------
$envPath = Join-Path $scriptDir ".env"
$apiKey = ""
if (Test-Path $envPath) {
    Get-Content $envPath | ForEach-Object {
        if ($_ -match "^DEEPSEEK_API_KEY=(.+)") {
            $apiKey = $Matches[1].Trim()
        }
    }
}
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    Write-Host "ERRO: DEEPSEEK_API_KEY nao encontrada no .env" -ForegroundColor Red
    pause
    exit 1
}

# ---------- CONECTAR AO CIVIL 3D ----------
Write-Host "Conectando ao Civil 3D 2026..." -ForegroundColor Cyan
try {
    $c3d = [System.Runtime.InteropServices.Marshal]::GetActiveObject("AutoCAD.Application.25.1")
    $doc = $c3d.ActiveDocument
    Write-Host "OK: Conectado ao $($c3d.Name) - $($doc.Name)" -ForegroundColor Green
} catch {
    Write-Host "ERRO: Civil 3D 2026 nao esta em execucao." -ForegroundColor Red
    Write-Host "Abra o Civil 3D 2026 e tente novamente." -ForegroundColor Yellow
    pause
    exit 1
}

# ---------- SYSTEM PROMPT ----------
$systemPrompt = @"
Voce e um especialista em Autodesk Civil 3D 2026 e AutoCAD 2026.
Responda em portugues do Brasil, de forma clara e direta.

Se o usuario pedir uma acao que possa ser executada por comando, inclua o comando exato no formato:
##COMANDO##comando_aqui##FIM##

Exemplos:
- "crie um retangulo" -> ##COMANDO##RECTANG##FIM##
- "desenhe uma polilinha" -> ##COMANDO##PLINE##FIM##
- "zoom extensao" -> ##COMANDO##ZOOM E##FIM##
- "desenhe circulo raio 5" -> ##COMANDO##CIRCLE 0,0 5##FIM##
- "apagar tudo" -> ##COMANDO##ERASE ALL##FIM##

Somente use ##COMANDO## quando houver um comando claro do AutoCAD para executar.
Em perguntas conceituais, responda normalmente sem ##COMANDO##.

Comandos especificos do Civil 3D:
- Superficie TIN: CREATESURFACE
- Alinhamento: CREATEDALIGNMENT
- Corredor: CREATECORRIDOR
- Perfil: CREATEPROFILEVIEW
- Montagem: CREATEASSEMBLY
- Curvas de nivel: CONTOURINTERVAL
- Grade: CREATEGRADING
- Lote: CREATEPARCEL
"@

# ---------- FUNCAO: Chamar DeepSeek ----------
function Invoke-DeepSeek {
    param([string]$question)
    
    $body = @{
        model = "deepseek-chat"
        messages = @(
            @{role = "system"; content = $systemPrompt},
            @{role = "user"; content = $question}
        )
        temperature = 0.3
        max_tokens = 800
    } | ConvertTo-Json -Depth 5

    # Corrige encoding Unicode: PowerShell 5.1 corrompe caracteres acentuados
    # Converte para UTF-8 bytes explicitamente
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    $bodyBytes = $utf8NoBom.GetBytes($body)

    $headers = @{
        Authorization = "Bearer $apiKey"
        Accept = "application/json"
    }

    $response = Invoke-RestMethod -Uri "https://api.deepseek.com/v1/chat/completions" `
        -Method Post -Headers $headers -ContentType "application/json; charset=utf-8" -Body $bodyBytes
    
    return $response.choices[0].message.content.Trim()
}

# ---------- FUNCAO: Extrair comando ----------
function Extract-Command {
    param([string]$response)
    if ($response -match "##COMANDO##(.+?)##FIM##") {
        $cmd = $Matches[1].Trim()
        $textOnly = $response -replace "##COMANDO##.*?##FIM##", ""
        return @{Command = $cmd; Text = $textOnly.Trim()}
    }
    return @{Command = ""; Text = $response}
}

# ---------- FUNCAO: Coletar contexto do desenho ----------
function Get-C3DContext {
    $ctx = @()
    $ctx += "Desenho: $($doc.Name)"

    # Layers
    try {
        $layers = @($doc.Layers | ForEach-Object { $_.Name })
        $ctx += "Layers ($($layers.Count)): $($layers -join ', ')"
    } catch { }

    # Entidades basicas
    try {
        $ms = $doc.ModelSpace
        $lines = 0; $plines = 0; $circles = 0; $texts = 0; $mtexts = 0; $blocks = 0
        foreach ($e in $ms) {
            switch ($e.EntityName) {
                "AcDbLine" { $lines++ }
                "AcDbPolyline" { $plines++ }
                "AcDbCircle" { $circles++ }
                "AcDbText" { $texts++ }
                "AcDbMText" { $mtexts++ }
                "AcDbBlockReference" { $blocks++ }
            }
        }
        $ctx += "Entidades: $lines linhas, $plines polilinhas, $circles circulos, $texts textos, $mtexts MTexts, $blocks blocos"
    } catch { }

    # Civil 3D objects via COM
    try {
        $civil = $c3d.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0")
        $cDoc = $civil.ActiveDocument

        # Alignments
        try { $a = $cDoc.Alignments; if ($a.Count -gt 0) { $names = @($a | % { $_.Name }); $ctx += "Alinhamentos ($($a.Count)): $($names -join ', ')" } } catch { }
        # Surfaces
        try { $s = $cDoc.Surfaces; if ($s.Count -gt 0) { $names = @($s | % { $_.Name }); $ctx += "Superficies ($($s.Count)): $($names -join ', ')" } } catch { }
        # Corridors
        try { $c = $cDoc.Corridors; if ($c.Count -gt 0) { $names = @($c | % { $_.Name }); $ctx += "Corredores ($($c.Count)): $($names -join ', ')" } } catch { }
        # Pipe Networks
        try { $p = $cDoc.PipeNetworks; if ($p.Count -gt 0) { $names = @($p | % { $_.Name }); $ctx += "Redes tubulacao ($($p.Count)): $($names -join ', ')" } } catch { }
        # Sites
        try { $si = $cDoc.Sites; if ($si.Count -gt 0) { $names = @($si | % { $_.Name }); $ctx += "Sites ($($si.Count)): $($names -join ', ')" } } catch { }
        # Profiles
        try { $pr = $cDoc.Profiles; if ($pr.Count -gt 0) { $ctx += "Perfis: $($pr.Count)" } } catch { }
        # Assemblies
        try { $as = $cDoc.Assemblies; if ($as.Count -gt 0) { $ctx += "Montagens: $($as.Count)" } } catch { }
        # Parcels
        try { $pa = $cDoc.Parcels; if ($pa.Count -gt 0) { $ctx += "Lotes: $($pa.Count)" } } catch { }
        # Feature Lines
        try { $fl = $cDoc.FeatureLines; if ($fl.Count -gt 0) { $ctx += "Feature Lines: $($fl.Count)" } } catch { }
        # Gradings
        try { $gr = $cDoc.Gradings; if ($gr.Count -gt 0) { $ctx += "Gradings: $($gr.Count)" } } catch { }
    } catch { }

    return ($ctx -join "`n")
}

# ---------- FUNCAO: Enviar comando pro Civil 3D ----------
function Send-C3DCommand {
    param([string]$command)
    try {
        $doc.SendCommand($command + "`n")
        Write-Host "OK: Comando '$command' enviado para o Civil 3D!" -ForegroundColor Green
    } catch {
        Write-Host "ERRO ao enviar comando: $_" -ForegroundColor Red
    }
}

# ---------- LOOP PRINCIPAL ----------
Clear-Host
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  C3D DeepSeek Bridge - Assistente IA para Civil 3D 2026" -ForegroundColor Cyan
Write-Host "  Conectado: $($doc.Name)" -ForegroundColor Green
Write-Host "  Digite 'exit' para sair" -ForegroundColor DarkGray
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

while ($true) {
    Write-Host "Voce" -ForegroundColor Yellow -NoNewline
    $question = Read-Host " >"
    
    if ([string]::IsNullOrWhiteSpace($question)) { continue }
    if ($question.Trim().ToLower() -eq "exit") { break }
    
    Write-Host "DeepSeek" -ForegroundColor Cyan -NoNewline
    Write-Host " pensando..." -ForegroundColor DarkGray
    
    try {
        # Coleta contexto do desenho e anexa a pergunta
        $context = Get-C3DContext
        $fullQuestion = "[CONTEXTO DO DESENHO ATUAL]`n$context`n`n[PERGUNTA DO USUARIO]`n$question"
        $response = Invoke-DeepSeek -question $fullQuestion
        $parsed = Extract-Command -response $response
        
        Write-Host ""
        Write-Host "DeepSeek" -ForegroundColor Cyan -NoNewline
        Write-Host " > $($parsed.Text)" -ForegroundColor White
        
        if ($parsed.Command) {
            Write-Host ""
            Write-Host "COMANDO" -ForegroundColor Magenta -NoNewline
            Write-Host " > $($parsed.Command)" -ForegroundColor Yellow
            
            $exec = Read-Host "`nExecutar no Civil 3D? [S]im / [N]ao"
            if ($exec.Trim().ToUpper() -eq "S") {
                Send-C3DCommand -command $parsed.Command
            }
        }
    } catch {
        Write-Host "ERRO: $_" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host ("-" * 60) -ForegroundColor DarkGray
    Write-Host ""
}

Write-Host "`nSessao encerrada." -ForegroundColor Cyan
