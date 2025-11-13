#!/usr/bin/env python3
"""
Script alternativo para gerar PDF a partir do Markdown usando Python.
Requer: markdown, pdfkit ou weasyprint
"""

import sys
import os
from pathlib import Path

def check_dependencies():
    """Verifica se as dependências estão instaladas."""
    missing = []
    
    try:
        import markdown
    except ImportError:
        missing.append("markdown")
    
    try:
        import pdfkit
    except ImportError:
        try:
            import weasyprint
        except ImportError:
            missing.append("pdfkit ou weasyprint")
    
    if missing:
        print("ERRO: Dependências faltando:", ", ".join(missing))
        print("\nPara instalar:")
        print("  pip install markdown pdfkit")
        print("  # ou")
        print("  pip install markdown weasyprint")
        print("\nNota: pdfkit requer wkhtmltopdf:")
        print("  Windows: https://wkhtmltopdf.org/downloads.html")
        return False
    
    return True

def generate_pdf_markdown_pdfkit(input_file, output_file):
    """Gera PDF usando markdown + pdfkit."""
    import markdown
    import pdfkit
    
    # Ler Markdown
    with open(input_file, 'r', encoding='utf-8') as f:
        md_content = f.read()
    
    # Converter para HTML
    html = markdown.markdown(md_content, extensions=['toc', 'fenced_code', 'tables'])
    
    # Adicionar CSS básico
    html_with_style = f"""
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset="UTF-8">
        <style>
            body {{
                font-family: Arial, sans-serif;
                margin: 2.5cm;
                line-height: 1.6;
            }}
            h1, h2, h3 {{
                color: #333;
            }}
            code {{
                background-color: #f4f4f4;
                padding: 2px 4px;
                border-radius: 3px;
            }}
            pre {{
                background-color: #f4f4f4;
                padding: 10px;
                border-radius: 5px;
                overflow-x: auto;
            }}
            table {{
                border-collapse: collapse;
                width: 100%;
            }}
            th, td {{
                border: 1px solid #ddd;
                padding: 8px;
                text-align: left;
            }}
            th {{
                background-color: #f2f2f2;
            }}
        </style>
    </head>
    <body>
        {html}
    </body>
    </html>
    """
    
    # Gerar PDF
    options = {
        'page-size': 'A4',
        'margin-top': '2.5cm',
        'margin-right': '2.5cm',
        'margin-bottom': '2.5cm',
        'margin-left': '2.5cm',
        'encoding': "UTF-8",
        'no-outline': None
    }
    
    try:
        pdfkit.from_string(html_with_style, output_file, options=options)
        return True
    except Exception as e:
        print(f"ERRO ao gerar PDF: {e}")
        return False

def generate_pdf_weasyprint(input_file, output_file):
    """Gera PDF usando markdown + weasyprint."""
    import markdown
    from weasyprint import HTML, CSS
    
    # Ler Markdown
    with open(input_file, 'r', encoding='utf-8') as f:
        md_content = f.read()
    
    # Converter para HTML
    html = markdown.markdown(md_content, extensions=['toc', 'fenced_code', 'tables'])
    
    # Adicionar CSS
    html_with_style = f"""
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset="UTF-8">
        <style>
            @page {{
                size: A4;
                margin: 2.5cm;
            }}
            body {{
                font-family: Arial, sans-serif;
                line-height: 1.6;
            }}
            h1, h2, h3 {{
                color: #333;
            }}
            code {{
                background-color: #f4f4f4;
                padding: 2px 4px;
                border-radius: 3px;
            }}
            pre {{
                background-color: #f4f4f4;
                padding: 10px;
                border-radius: 5px;
                overflow-x: auto;
            }}
            table {{
                border-collapse: collapse;
                width: 100%;
            }}
            th, td {{
                border: 1px solid #ddd;
                padding: 8px;
                text-align: left;
            }}
            th {{
                background-color: #f2f2f2;
            }}
        </style>
    </head>
    <body>
        {html}
    </body>
    </html>
    """
    
    try:
        HTML(string=html_with_style).write_pdf(output_file)
        return True
    except Exception as e:
        print(f"ERRO ao gerar PDF: {e}")
        return False

def main():
    """Função principal."""
    input_file = Path("docs/Proposta_Arquitetura_TOTVS_HubIntegracao.md")
    output_file = Path("docs/Proposta_Arquitetura_TOTVS_HubIntegracao.pdf")
    
    print("=== Gerador de PDF (Python) - Proposta de Arquitetura TOTVS ===")
    print()
    
    # Verificar dependências
    if not check_dependencies():
        sys.exit(1)
    
    # Verificar arquivo de entrada
    if not input_file.exists():
        print(f"ERRO: Arquivo não encontrado: {input_file}")
        sys.exit(1)
    
    print(f"Arquivo de entrada: {input_file}")
    print(f"Arquivo de saída: {output_file}")
    print()
    
    # Tentar gerar PDF
    print("Convertendo para PDF...")
    
    # Tentar pdfkit primeiro
    try:
        import pdfkit
        if generate_pdf_markdown_pdfkit(input_file, output_file):
            print()
            print("✓ PDF gerado com sucesso usando pdfkit!")
            print(f"  Localização: {output_file}")
            return
    except ImportError:
        pass
    
    # Tentar weasyprint
    try:
        import weasyprint
        if generate_pdf_weasyprint(input_file, output_file):
            print()
            print("✓ PDF gerado com sucesso usando weasyprint!")
            print(f"  Localização: {output_file}")
            return
    except ImportError:
        pass
    
    print()
    print("ERRO: Não foi possível gerar o PDF.")
    print("Verifique se as dependências estão instaladas corretamente.")

if __name__ == "__main__":
    main()

