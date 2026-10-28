using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Moq;
using Xunit;

namespace KamatekCrm.Tests.Services;

public sealed class ProjectQuotePdfTests : IDisposable
{
    private readonly string _tempPdfPath;
    private readonly string _tempImagePath;

    public ProjectQuotePdfTests()
    {
        _tempPdfPath = Path.Combine(Path.GetTempPath(), $"test_quote_{Guid.NewGuid():N}.pdf");
        _tempImagePath = Path.Combine(Path.GetTempPath(), $"test_img_{Guid.NewGuid():N}.png");

        // 1x1 piksel geçerli PNG dosyası oluştur
        var pngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
        File.WriteAllBytes(_tempImagePath, pngBytes);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempPdfPath)) File.Delete(_tempPdfPath);
            if (File.Exists(_tempImagePath)) File.Delete(_tempImagePath);
        }
        catch { }
    }

    private PdfService CreatePdfService()
    {
        var auditMock = new Mock<IAuditTrailService>();
        auditMock.Setup(x => x.WriteAsync(
            It.IsAny<AuditActionType>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var pdpMock = new Mock<IPersonalDataProtectionService>();
        return new PdfService(pdpMock.Object, auditMock.Object);
    }

    [Fact]
    public void GenerateProjectQuote_WithProductImage_GeneratesPdfSuccessfully()
    {
        var pdfService = CreatePdfService();
        var generator = new StructureGeneratorService();
        var root = generator.GenerateApartment("A Blok", 1, 1, includeEntrance: false);
        var floor1 = root.Children[0];
        var flat1 = floor1.Children[0];
        flat1.Items.Add(new ScopeNodeItem
        {
            ProductName = "IP Kamera 4MP",
            UnitPrice = 1500m,
            Quantity = 2,
            UnitCost = 1000m,
            ImagePath = _tempImagePath
        });
        root.NotifyTotalsChanged();

        var project = new ServiceProject
        {
            Id = 1,
            ProjectCode = "PRJ-IMG-001",
            Title = "Resimli Proje Teklifi",
            Customer = new Customer { FullName = "Resim Test Müşterisi" }
        };

        pdfService.GenerateProjectQuote(project, new List<ScopeNode> { root }, _tempPdfPath);

        File.Exists(_tempPdfPath).Should().BeTrue();
    }
}
