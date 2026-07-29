# run.ps1 - Build/Run generico para projetos C# (.NET) no Windows
#
# Coloque na raiz de qualquer repositorio .NET. O script:
#   1. descobre sozinho os projetos executaveis (ignora bibliotecas e testes)
#   2. pergunta a arquitetura: x86, x64 ou ambas
#   3. publica um UNICO .exe portatil (self-contained, sem instalar o .NET)
#   4. pergunta se quer executar - ENTER executa
#
# Uso interativo:  .\run.ps1
# Uso direto:      .\run.ps1 -Arch x64 -NoRun
#                  .\run.ps1 -Project src\App\App.csproj -Arch ambos
# Para fechar automaticamente uma versao publicada que esteja aberta:
#                  .\run.ps1 -Arch x64 -FecharEmExecucao
# Outras plataformas (nao executa ao final, o binario e de outro sistema):
#                  .\run.ps1 -Rid linux-x64
#                  .\run.ps1 -Rid linux-x64,osx-x64,osx-arm64

[CmdletBinding()]
param(
    # Caminho do .csproj. Se omitido, o script descobre e pergunta.
    [string]$Project,

    # Arquitetura alvo (Windows). Se omitida, o script pergunta.
    [ValidateSet('x86', 'x64', 'ambos')]
    [string]$Arch,

    # Publica para outras plataformas, ignorando o menu de arquitetura.
    # Ex.: -Rid linux-x64  |  -Rid osx-arm64  |  -Rid linux-x64,osx-x64
    # A execucao ao final e pulada: o binario e de outro sistema.
    [string[]]$Rid,

    # Executa sem perguntar.
    [switch]$Run,

    # Nao executa e nao pergunta (util em CI).
    [switch]$NoRun,

    # Pasta de saida dos executaveis.
    [string]$OutputDir = 'dist',

    [string]$Configuration = 'Release',

    # Fecha uma copia publicada que esteja bloqueando a substituicao do .exe.
    [switch]$FecharEmExecucao,

    # Pula a compilacao e so executa o que ja existe em $OutputDir.
    [switch]$SomenteExecutar
)

$ErrorActionPreference = 'Stop'
try { chcp 65001 | Out-Null } catch { }

$RaizProjeto = $PSScriptRoot

# ---------------------------------------------------------------------------
# Saida
# ---------------------------------------------------------------------------
function Write-Titulo($texto) {
    Write-Host ''
    Write-Host ('=' * 60) -ForegroundColor Cyan
    Write-Host "  $texto" -ForegroundColor Cyan
    Write-Host ('=' * 60) -ForegroundColor Cyan
    Write-Host ''
}

function Write-Passo($texto) { Write-Host "==> $texto" -ForegroundColor Cyan }
function Write-Ok($texto)    { Write-Host "    $texto" -ForegroundColor Green }
function Write-Aviso($texto) { Write-Host "    $texto" -ForegroundColor Yellow }

function Abortar($texto) {
    Write-Host ''
    Write-Host "ERRO: $texto" -ForegroundColor Red
    Write-Host ''
    exit 1
}

function Format-Tamanho([long]$bytes) {
    if ($bytes -ge 1GB) { return '{0:N1} GB' -f ($bytes / 1GB) }
    if ($bytes -ge 1MB) { return '{0:N1} MB' -f ($bytes / 1MB) }
    if ($bytes -ge 1KB) { return '{0:N0} KB' -f ($bytes / 1KB) }
    return "$bytes B"
}

function Test-ArquivoBloqueado([string]$caminho) {
    if (-not (Test-Path -LiteralPath $caminho -PathType Leaf)) { return $false }

    try {
        $stream = [System.IO.File]::Open(
            $caminho,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None)
        $stream.Dispose()
        return $false
    }
    catch [System.IO.IOException] {
        return $true
    }
    catch [System.UnauthorizedAccessException] {
        return $true
    }
}

function Confirmar-DestinoDisponivel([string]$destino) {
    if (-not (Test-Path -LiteralPath $destino -PathType Container)) { return }

    $bloqueados = @(
        Get-ChildItem -LiteralPath $destino -Filter *.exe -File -ErrorAction SilentlyContinue |
        Where-Object { Test-ArquivoBloqueado $_.FullName }
    )
    if ($bloqueados.Count -eq 0) { return }

    $nomes = $bloqueados.Name -join ', '
    Write-Aviso "Executavel em uso: $nomes"
    Write-Aviso 'O Windows nao permite substituir um programa enquanto ele esta aberto.'

    $deveFechar = $FecharEmExecucao.IsPresent
    if (-not $deveFechar -and -not $env:CI) {
        $resposta = Read-Host 'Fechar o BuildPC em execucao e continuar? [S/n]'
        $deveFechar = [string]::IsNullOrWhiteSpace($resposta) -or
                      $resposta.Trim().ToLowerInvariant() -in @('s', 'sim')
    }

    if (-not $deveFechar) {
        Abortar 'feche o executavel publicado ou use -FecharEmExecucao e tente novamente.'
    }

    foreach ($arquivo in $bloqueados) {
        $nomeProcesso = [System.IO.Path]::GetFileNameWithoutExtension($arquivo.Name)
        $processos = @(Get-Process -Name $nomeProcesso -ErrorAction SilentlyContinue)
        foreach ($processo in $processos) {
            try {
                $caminhoProcesso = $processo.Path
                if ($caminhoProcesso -and
                    -not [string]::Equals(
                        $caminhoProcesso,
                        $arquivo.FullName,
                        [System.StringComparison]::OrdinalIgnoreCase)) {
                    continue
                }
            }
            catch {
                # Se o Windows nao expuser o caminho, o nome ainda identifica
                # a instancia depois da confirmacao explicita do usuario.
            }

            Stop-Process -Id $processo.Id -Force -ErrorAction SilentlyContinue
        }
    }

    Start-Sleep -Milliseconds 400
    $aindaBloqueados = @($bloqueados | Where-Object {
        Test-ArquivoBloqueado $_.FullName
    })
    if ($aindaBloqueados.Count -gt 0) {
        Abortar 'o executavel continua bloqueado. Feche-o pelo Gerenciador de Tarefas e tente novamente.'
    }

    Write-Ok 'Executavel anterior fechado; publicacao liberada.'
}

# ---------------------------------------------------------------------------
# Descoberta de projetos executaveis
#
# Um projeto entra na lista se for OutputType Exe/WinExe e tiver um alvo que
# roda no Windows. Ficam de fora bibliotecas, projetos de teste e as "heads" de
# Android/iOS/browser, que nao geram .exe.
# ---------------------------------------------------------------------------
function Get-ProjetosExecutaveis {
    $arquivos = Get-ChildItem -Path $RaizProjeto -Filter *.csproj -Recurse -File |
                Where-Object { $_.FullName -notmatch '\\(bin|obj|\.git)\\' }

    $resultado = @()

    foreach ($arquivo in $arquivos) {
        $texto = Get-Content $arquivo.FullName -Raw

        $sdk = if ($texto -match 'Sdk="([^"]+)"') { $Matches[1] } else { 'Microsoft.NET.Sdk' }

        # Alguns SDKs geram executavel sem declarar <OutputType> no csproj -
        # e o caso do Sdk.Web, usado por projetos ASP.NET Core.
        $padraoExecutavel = $sdk -in @(
            'Microsoft.NET.Sdk.Web',
            'Microsoft.NET.Sdk.Worker',
            'Microsoft.NET.Sdk.BlazorWebAssembly'
        )

        $tipoSaida = if ($texto -match '<OutputType>\s*([^<]+?)\s*</OutputType>') {
            $Matches[1]
        } elseif ($padraoExecutavel) {
            'Exe'
        } else {
            'Library'
        }

        if ($tipoSaida -notmatch '^(Exe|WinExe)$') { continue }

        # Projeto de teste nao e alvo de publicacao.
        if ($texto -match '<IsTestProject>\s*true' -or
            $texto -match 'Microsoft\.NET\.Test\.Sdk' -or
            $texto -match 'PackageReference\s+Include="xunit' -or
            $texto -match 'PackageReference\s+Include="(NUnit|MSTest)') { continue }

        $alvos = @()
        if ($texto -match '<TargetFrameworks>\s*([^<]+?)\s*</TargetFrameworks>') {
            $alvos = $Matches[1] -split ';' | ForEach-Object { $_.Trim() }
        }
        elseif ($texto -match '<TargetFramework>\s*([^<]+?)\s*</TargetFramework>') {
            $alvos = @($Matches[1])
        }

        # Sem TFM no csproj, ele pode vir de Directory.Build.props: pergunta ao MSBuild.
        if ($alvos.Count -eq 0) {
            $alvos = @((& dotnet msbuild $arquivo.FullName -getProperty:TargetFramework 2>$null) | Select-Object -First 1)
        }

        $alvosWindows = $alvos | Where-Object {
            $_ -and $_ -notmatch '-(android|ios|maccatalyst|tvos|browser)'
        }
        if (-not $alvosWindows) { continue }

        $resultado += [pscustomobject]@{
            Nome    = [System.IO.Path]::GetFileNameWithoutExtension($arquivo.Name)
            Caminho = $arquivo.FullName
            Alvo    = $alvosWindows | Select-Object -First 1
            Relativo = $arquivo.FullName.Replace("$RaizProjeto\", '')
            # Aplicativos com janela são a opção natural para ENTER. APIs e
            # workers continuam disponíveis, mas aparecem depois no menu.
            Prioridade = if ($tipoSaida -eq 'WinExe') { 0 } elseif ($padraoExecutavel) { 2 } else { 1 }
        }
    }

    # Quem chama envolve em @(), o que garante array mesmo com zero ou um item.
    return $resultado | Sort-Object Prioridade, Nome
}

# ---------------------------------------------------------------------------
# Menus
# ---------------------------------------------------------------------------
function Select-Projeto($projetos) {
    if ($projetos.Count -eq 1) {
        Write-Ok "Projeto: $($projetos[0].Nome)"
        return $projetos[0]
    }

    Write-Host 'Projetos executaveis encontrados:' -ForegroundColor Yellow
    Write-Host ''
    for ($i = 0; $i -lt $projetos.Count; $i++) {
        Write-Host ('  {0}) {1}' -f ($i + 1), $projetos[$i].Nome) -ForegroundColor White
        Write-Host ('     {0}' -f $projetos[$i].Relativo) -ForegroundColor DarkGray
    }
    Write-Host ''

    while ($true) {
        $escolha = Read-Host "Projeto [1-$($projetos.Count)] (ENTER = 1)"
        if ([string]::IsNullOrWhiteSpace($escolha)) { return $projetos[0] }
        $n = 0
        if ([int]::TryParse($escolha, [ref]$n) -and $n -ge 1 -and $n -le $projetos.Count) {
            return $projetos[$n - 1]
        }
        Write-Aviso 'Opcao invalida.'
    }
}

function Select-Arquitetura {
    Write-Host 'Arquitetura do executavel:' -ForegroundColor Yellow
    Write-Host ''
    Write-Host '  1) x64   - 64 bits (recomendado)' -ForegroundColor White
    Write-Host '  2) x86   - 32 bits' -ForegroundColor White
    Write-Host '  3) ambos - gera os dois' -ForegroundColor White
    Write-Host ''

    while ($true) {
        $escolha = Read-Host 'Opcao [1-3] (ENTER = 1)'
        switch ($escolha) {
            ''  { return @('x64') }
            '1' { return @('x64') }
            '2' { return @('x86') }
            '3' { return @('x64', 'x86') }
            default { Write-Aviso 'Opcao invalida.' }
        }
    }
}

# ---------------------------------------------------------------------------
# Publicacao
#
# self-contained + PublishSingleFile = um .exe que roda em maquina sem .NET.
# IncludeNativeLibrariesForSelfExtract embute as .dll nativas (necessario para
# Avalonia, SQLite e afins).
#
# Trimming NAO e ligado: ele remove codigo alcancado so por reflexao e quebra
# em silencio bibliotecas como Avalonia, Npgsql e System.Text.Json.
# ---------------------------------------------------------------------------
function Publicar($projeto, $alvo, $pastaSaida) {
    # $alvo e 'x86'/'x64' (Windows) ou um RID completo como 'linux-x64'.
    $rid = if ($alvo -match '-') { $alvo } else { "win-$alvo" }
    $destino = Join-Path $pastaSaida $alvo

    Confirmar-DestinoDisponivel $destino
    Write-Passo "Publicando $($projeto.Nome) para $rid ..."

    $argumentos = @(
        'publish', $projeto.Caminho,
        '-c', $Configuration,
        '-r', $rid,
        '--self-contained', 'true',
        '-o', $destino,
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:EnableCompressionInSingleFile=true',
        '-p:DebugType=none',
        '-p:GenerateDocumentationFile=false',
        '--nologo',
        '-v', 'minimal'
    )

    # Out-Host mantem o log do build visivel sem deixa-lo entrar no pipeline -
    # sem isso, as linhas do dotnet virariam parte do valor de retorno.
    & dotnet @argumentos | Out-Host
    if ($LASTEXITCODE -ne 0) { Abortar "falha ao publicar $rid." }

    # No Windows o binario e .exe; em Linux/macOS nao tem extensao.
    $binario = Get-ChildItem -Path $destino -Filter *.exe -File |
               Sort-Object Length -Descending |
               Select-Object -First 1

    if (-not $binario) {
        # Nome como "App.Server.Admin" faz o PowerShell reportar ".Admin" como
        # extensao, entao nao da para filtrar por "sem extensao". A publicacao
        # single-file gera um binario grande ao lado de alguns .json pequenos:
        # pega-se o maior arquivo que nao seja metadado.
        $metadados = @('.json', '.pdb', '.xml', '.config', '.txt', '.md')

        $binario = Get-ChildItem -Path $destino -File |
                   Where-Object { $metadados -notcontains $_.Extension.ToLowerInvariant() -and $_.Length -gt 1MB } |
                   Sort-Object Length -Descending |
                   Select-Object -First 1
    }

    if (-not $binario) { Abortar "nenhum executavel gerado em '$destino'." }

    Write-Ok ("{0}  ({1})" -f $binario.Name, (Format-Tamanho $binario.Length))
    Write-Host ("    $($binario.FullName)") -ForegroundColor DarkGray

    return [pscustomobject]@{
        Arquitetura = $alvo
        Caminho     = $binario.FullName
        Tamanho     = $binario.Length
        # Binario de outro sistema nao pode ser executado aqui.
        Executavel  = ($alvo -notmatch '-') -or ($alvo -like 'win-*')
    }
}

# ---------------------------------------------------------------------------
# Fluxo principal
# ---------------------------------------------------------------------------
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Abortar 'SDK do .NET nao encontrado no PATH. Instale em https://dotnet.microsoft.com/download'
}

Write-Titulo 'Build / Run - .NET portable'

# --- Projeto -----------------------------------------------------------------
if ($Project) {
    $caminho = if ([System.IO.Path]::IsPathRooted($Project)) { $Project } else { Join-Path $RaizProjeto $Project }
    if (-not (Test-Path $caminho)) { Abortar "projeto nao encontrado: $Project" }

    $projetoEscolhido = [pscustomobject]@{
        Nome     = [System.IO.Path]::GetFileNameWithoutExtension($caminho)
        Caminho  = (Resolve-Path $caminho).Path
        Relativo = $Project
    }
    Write-Ok "Projeto: $($projetoEscolhido.Nome)"
}
else {
    Write-Passo 'Procurando projetos executaveis...'
    $projetos = @(Get-ProjetosExecutaveis)

    if ($projetos.Count -eq 0) {
        Abortar 'nenhum projeto executavel encontrado (OutputType Exe/WinExe) fora de testes.'
    }

    Write-Host ''
    $projetoEscolhido = Select-Projeto $projetos
}

$pastaSaida = if ([System.IO.Path]::IsPathRooted($OutputDir)) { $OutputDir } else { Join-Path $RaizProjeto $OutputDir }

# --- Compilacao --------------------------------------------------------------
$gerados = @()

if ($SomenteExecutar) {
    Write-Passo 'Pulando compilacao (-SomenteExecutar).'

    foreach ($arq in @('x64', 'x86')) {
        $destino = Join-Path $pastaSaida $arq
        if (Test-Path $destino) {
            $exe = Get-ChildItem -Path $destino -Filter *.exe -File |
                   Sort-Object Length -Descending | Select-Object -First 1
            if ($exe) {
                $gerados += [pscustomobject]@{
                    Arquitetura = $arq
                    Caminho     = $exe.FullName
                    Tamanho     = $exe.Length
                }
            }
        }
    }

    if ($gerados.Count -eq 0) { Abortar "nada publicado em '$pastaSaida'. Rode sem -SomenteExecutar." }
}
else {
    Write-Host ''

    # -Rid tem precedencia: publica para outra plataforma e pula o menu.
    # Chamado via "powershell -File", o array chega como uma unica string, entao
    # cada item e dividido por virgula.
    $arquiteturas = if ($Rid) {
        @($Rid | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    } elseif ($Arch) {
        switch ($Arch) {
            'ambos' { @('x64', 'x86') }
            default { @($Arch) }
        }
    } else {
        Select-Arquitetura
    }

    Write-Host ''
    Write-Passo "Configuracao: $Configuration | Alvos: $($arquiteturas -join ', ')"
    Write-Host ''

    $inicio = Get-Date
    foreach ($arq in $arquiteturas) {
        $gerados += Publicar $projetoEscolhido $arq $pastaSaida
        Write-Host ''
    }
    $duracao = (Get-Date) - $inicio

    Write-Titulo 'Compilacao concluida'
    foreach ($g in $gerados) {
        Write-Host ("  {0,-10}  {1,10}  {2}" -f $g.Arquitetura, (Format-Tamanho $g.Tamanho), $g.Caminho)
    }
    Write-Host ''
    Write-Host ("  Tempo: {0:mm\:ss}" -f $duracao) -ForegroundColor DarkGray
}

# --- Execucao ----------------------------------------------------------------
if ($NoRun) {
    Write-Host ''
    exit 0
}

# Binario de outra plataforma nao roda aqui: nem oferece a opcao.
$executaveis = @($gerados | Where-Object { $_.Executavel -ne $false })

if ($executaveis.Count -eq 0) {
    Write-Host ''
    Write-Ok 'Binarios gerados para outra plataforma; nada a executar nesta maquina.'
    Write-Host ''
    exit 0
}

# Prefere a arquitetura da maquina quando os dois foram gerados.
$arqMaquina = if ([Environment]::Is64BitOperatingSystem) { 'x64' } else { 'x86' }
$alvo = $executaveis | Where-Object { $_.Arquitetura -eq $arqMaquina } | Select-Object -First 1
if (-not $alvo) { $alvo = $executaveis | Select-Object -First 1 }
$gerados = $executaveis

if (-not $Run) {
    Write-Host ''
    Write-Host ('-' * 60) -ForegroundColor Cyan
    Write-Host "  ENTER  -> executar ($($alvo.Arquitetura))" -ForegroundColor Yellow
    if ($gerados.Count -gt 1) {
        $outra = $gerados | Where-Object { $_.Arquitetura -ne $alvo.Arquitetura } | Select-Object -First 1
        Write-Host "  o      -> executar a outra ($($outra.Arquitetura))" -ForegroundColor Yellow
    }
    Write-Host '  n      -> sair sem executar' -ForegroundColor Yellow
    Write-Host ('-' * 60) -ForegroundColor Cyan
    Write-Host ''

    $resposta = Read-Host 'Opcao'
    switch ($resposta.Trim().ToLower()) {
        'n'    { Write-Host ''; exit 0 }
        'nao'  { Write-Host ''; exit 0 }
        'o'    {
            $outra = $gerados | Where-Object { $_.Arquitetura -ne $alvo.Arquitetura } | Select-Object -First 1
            if ($outra) { $alvo = $outra }
        }
        default { }
    }
}

Write-Host ''
Write-Passo "Executando: $([System.IO.Path]::GetFileName($alvo.Caminho))  ($($alvo.Arquitetura))"
Write-Host ''

Start-Process -FilePath $alvo.Caminho -WorkingDirectory ([System.IO.Path]::GetDirectoryName($alvo.Caminho))
