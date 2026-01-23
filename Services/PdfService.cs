using System;
using System.Collections.Generic;
using System.Linq;
using KamatekCrm.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KamatekCrm.Services
{
    public class PdfService
    {
        public void GenerateProjectQuote(ServiceProject project, List<ScopeNode> rootNodes, string filePath)
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI"));

                    page.Header().Element(c => ComposeHeader(c, project));
                    page.Content().Element(c => ComposeContent(c, project, rootNodes));
                    page.Footer().Element(c => ComposeFooter(c));
                });
            })
            .GeneratePdf(filePath);
        }

        private void ComposeHeader(IContainer container, ServiceProject project)
        {
            container.Column(column =>
            {
                column.Spacing(10);
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("KAMATEK").Bold().FontSize(24).FontColor(Colors.Blue.Darken2);
                        col.Item().Text("Elektronik Güvenlik & Otomasyon Sistemleri").FontSize(11).FontColor(Colors.Grey.Darken2);
                    });

                    row.RelativeItem().AlignRight().Column(col =>
                    {
                         col.Item().Border(1).BorderColor(Colors.Blue.Darken2).Background("#E3F2FD").Padding(10).Column(inner =>
                        {
                            inner.Item().Text("TEKNİK VE TİCARİ TEKLİF").Bold().FontSize(14).FontColor(Colors.Blue.Darken3).AlignCenter();
                            inner.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Blue.Darken2);
                            inner.Item().PaddingTop(5).Text($"Tarih: {DateTime.Now:dd.MM.yyyy}").FontSize(10).AlignCenter();
                            inner.Item().Text($"Teklif No: {project.ProjectCode}").FontSize(10).AlignCenter();
                        });
                    });
                });
                column.Item().PaddingTop(10).LineHorizontal(2).LineColor(Colors.Blue.Darken3);
            });
        }

        private void ComposeContent(IContainer container, ServiceProject project, List<ScopeNode> rootNodes)
        {
            container.PaddingVertical(20).Column(column =>
            {
                column.Spacing(20);
                
                // Executive Summary
                column.Item().Text($"Sayın {project.Customer?.FullName ?? "Müşteri"}").SemiBold();
                column.Item().Text(project.Title).Bold();
                
                // Placeholder for Table
                column.Item().PaddingTop(20).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(20).Column(c => 
                {
                    c.Item().AlignCenter().Text("Detaylı Malzeme Listesi").Bold();
                    c.Item().AlignCenter().Text($"(Toplam {rootNodes.Sum(n => n.RecursiveItemCount)} kalem ürün)").Italic();
                    c.Item().PaddingTop(10).AlignCenter().Text($"Genel Toplam: {rootNodes.Sum(n => n.RecursiveTotal):N2} TL").FontSize(14).Bold().FontColor(Colors.Green.Darken2);
                });
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().AlignRight().Text(x => x.CurrentPageNumber());
            });
        }

        public void GenerateServiceForm(ServiceJob job, string filePath)
        {
             Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    
                    page.Header().Text("SERVICE FORM").AlignCenter();
                    page.Content().Text($"Job ID: {job.Id}");
                });
            })
            .GeneratePdf(filePath);
        }
    }
}
