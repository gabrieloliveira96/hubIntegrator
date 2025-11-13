# Como Gerar o PDF da Documentação

Este guia explica como converter o arquivo `Proposta_Arquitetura_TOTVS_HubIntegracao.md` para PDF.

## Opção 1: Usando Pandoc (Recomendado)

### Pré-requisitos

1. **Pandoc**: https://github.com/jgm/pandoc/releases
   - Windows: Baixe o instalador `.msi`
   - Ou use Chocolatey: `choco install pandoc`

2. **LaTeX** (necessário para gerar PDFs):
   - **MiKTeX** (Windows): https://miktex.org/download
   - **TeX Live** (Linux/Mac): https://www.tug.org/texlive/

### Executar

```powershell
# No diretório raiz do projeto
.\docs\generate-pdf.ps1
```

O script irá:
- Verificar se pandoc está instalado
- Converter o Markdown para PDF
- Aplicar formatação (margens, fontes, TOC)
- Gerar o arquivo `docs/Proposta_Arquitetura_TOTVS_HubIntegracao.pdf`

## Opção 2: Usando Ferramentas Online

Se você não tem pandoc/LaTeX instalado, pode usar ferramentas online:

### Markdown to PDF

1. **md2pdf.netlify.app**
   - Acesse: https://md2pdf.netlify.app/
   - Cole o conteúdo do arquivo `.md`
   - Clique em "Download PDF"

2. **Dillinger.io**
   - Acesse: https://dillinger.io/
   - Cole o conteúdo
   - Clique em "Export as" → "PDF"

3. **Markdown to PDF**
   - Acesse: https://www.markdowntopdf.com/
   - Faça upload do arquivo `.md`
   - Baixe o PDF

### Nota sobre Diagramas Mermaid

Os diagramas Mermaid no documento **não serão renderizados** automaticamente em ferramentas online simples.

Para renderizar diagramas:
1. Use o [Mermaid Live Editor](https://mermaid.live/)
2. Exporte cada diagrama como PNG/SVG
3. Insira as imagens no documento antes de converter

## Opção 3: Usando VS Code

### Extensão: Markdown PDF

1. Instale a extensão "Markdown PDF" no VS Code
2. Abra o arquivo `.md`
3. Pressione `Ctrl+Shift+P` (ou `Cmd+Shift+P` no Mac)
4. Digite "Markdown PDF: Export (pdf)"
5. O PDF será gerado na mesma pasta

### Nota

A extensão Markdown PDF pode não renderizar diagramas Mermaid. Considere usar a extensão "Markdown Preview Mermaid Support" primeiro.

## Opção 4: Usando Docker (Alternativa)

Se você tem Docker instalado:

```bash
# Criar um container com pandoc
docker run --rm -v "${PWD}/docs:/data" pandoc/latex:latest \
  Proposta_Arquitetura_TOTVS_HubIntegracao.md \
  -o Proposta_Arquitetura_TOTVS_HubIntegracao.pdf \
  --pdf-engine=xelatex \
  -V geometry:margin=2.5cm \
  --toc
```

## Formatação Aplicada

O PDF gerado terá:

- **Cabeçalho**: "TOTVS – Tech Lead .NET"
- **Rodapé**: "Versão 1.0 – © 2025"
- **Sumário automático**: Com 3 níveis de profundidade
- **Margens**: 2.5cm em todos os lados
- **Fonte**: 11pt
- **Links coloridos**: Azul
- **Syntax highlighting**: Tema Tango

## Troubleshooting

### Erro: "pandoc não encontrado"

- Instale pandoc: https://github.com/jgm/pandoc/releases
- Ou use Chocolatey: `choco install pandoc`
- Reinicie o terminal após instalar

### Erro: "LaTeX não encontrado"

- Instale MiKTeX (Windows): https://miktex.org/download
- Ou TeX Live (Linux/Mac): https://www.tug.org/texlive/
- Reinicie o terminal após instalar

### Diagramas Mermaid não aparecem

- Use o Mermaid Live Editor para exportar como imagem
- Insira as imagens no documento antes de converter
- Ou use uma ferramenta que suporte Mermaid (como VS Code com extensões)

### PDF muito grande

- Reduza o tamanho das imagens antes de inserir
- Use compressão de PDF após gerar

## Resultado Esperado

O PDF final deve ter aproximadamente **50-60 páginas** (dependendo do tamanho das imagens) e incluir:

- Capa com título e informações
- Sumário automático
- Todos os 14 capítulos da documentação
- Diagramas (se renderizados)
- Anexos com exemplos de payloads

---

**Dúvidas?** Consulte a documentação do projeto ou entre em contato com a equipe de desenvolvimento.

