# Script para converter README.md para PDF
# Requer: pandoc e LaTeX (MiKTeX ou TeX Live)

param(
    [string]$InputFile = "docs/Proposta_Arquitetura_TOTVS_HubIntegracao.md",
    [string]$OutputFile = "docs/Proposta_Arquitetura_TOTVS_HubIntegracao.pdf"
)

Write-Host "=== Gerador de PDF - Proposta de Arquitetura TOTVS ===" -ForegroundColor Cyan
Write-Host ""

# Verificar se pandoc está instalado
$pandocPath = Get-Command pandoc -ErrorAction SilentlyContinue
if (-not $pandocPath) {
    Write-Host "ERRO: pandoc não encontrado!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Para instalar pandoc:" -ForegroundColor Yellow
    Write-Host "  1. Baixe de: https://github.com/jgm/pandoc/releases" -ForegroundColor Yellow
    Write-Host "  2. Ou use chocolatey: choco install pandoc" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Também é necessário LaTeX (MiKTeX ou TeX Live) para gerar PDFs." -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

# Verificar se o arquivo de entrada existe
if (-not (Test-Path $InputFile)) {
    Write-Host "ERRO: Arquivo não encontrado: $InputFile" -ForegroundColor Red
    exit 1
}

Write-Host "Arquivo de entrada: $InputFile" -ForegroundColor Green
Write-Host "Arquivo de saída: $OutputFile" -ForegroundColor Green
Write-Host ""

# Converter para PDF usando pandoc
Write-Host "Convertendo para PDF..." -ForegroundColor Cyan

$pandocArgs = @(
    $InputFile,
    "-o", $OutputFile,
    "--pdf-engine=xelatex",
    "-V", "geometry:margin=2.5cm",
    "-V", "fontsize=11pt",
    "-V", "documentclass=article",
    "-V", "colorlinks=true",
    "-V", "linkcolor=blue",
    "-V", "urlcolor=blue",
    "-V", "toccolor=blue",
    "--toc",
    "--toc-depth=3",
    "--highlight-style=tango",
    "-V", "title=Proposta de Arquitetura – Hub de Integração e Orquestração TOTVS",
    "-V", "author=Tech Lead .NET",
    "-V", "date=Janeiro 2025",
    "-V", "subtitle=Versão 1.0"
)

try {
    & pandoc $pandocArgs
    
    if (Test-Path $OutputFile) {
        Write-Host ""
        Write-Host "✓ PDF gerado com sucesso!" -ForegroundColor Green
        Write-Host "  Localização: $OutputFile" -ForegroundColor Green
        Write-Host ""
        
        # Tentar abrir o PDF
        $open = Read-Host "Deseja abrir o PDF agora? (S/N)"
        if ($open -eq "S" -or $open -eq "s") {
            Start-Process $OutputFile
        }
    } else {
        Write-Host ""
        Write-Host "ERRO: PDF não foi gerado. Verifique os logs acima." -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host ""
    Write-Host "ERRO ao converter: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Possíveis causas:" -ForegroundColor Yellow
    Write-Host "  1. LaTeX não está instalado (MiKTeX ou TeX Live)" -ForegroundColor Yellow
    Write-Host "  2. Fontes não encontradas" -ForegroundColor Yellow
    Write-Host "  3. Erro no formato do Markdown" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Alternativa: Use uma ferramenta online como:" -ForegroundColor Yellow
    Write-Host "  - https://www.markdowntopdf.com/" -ForegroundColor Yellow
    Write-Host "  - https://md2pdf.netlify.app/" -ForegroundColor Yellow
    exit 1
}

