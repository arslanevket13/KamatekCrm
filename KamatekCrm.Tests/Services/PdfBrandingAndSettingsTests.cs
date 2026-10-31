using System;
using System.IO;
using System.Text;
using System.Threading;
using FluentAssertions;
using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Services;
using KamatekCrm.Settings;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Moq;
using Xunit;

namespace KamatekCrm.Tests.Services;

public sealed class PdfBrandingAndSettingsTests : IDisposable
{
    private readonly string _tempPdfPath;
    private readonly string _tempImagePath;
    private readonly string _tempFakeExePath;
    private readonly CompanyBrandProfile _originalProfile;

    public PdfBrandingAndSettingsTests()
    {
        _tempPdfPath = Path.Combine(Path.GetTempPath(), $"test_brand_quote_{Guid.NewGuid():N}.pdf");
        _tempImagePath = Path.Combine(Path.GetTempPath(), $"test_brand_logo_{Guid.NewGuid():N}.png");
        _tempFakeExePath = Path.Combine(Path.GetTempPath(), $"test_fake_logo_{Guid.NewGuid():N}.png");

        // Geçerli 1x1 piksel PNG oluştur
        var pngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
        File.WriteAllBytes(_tempImagePath, pngBytes);

        // Sahte MZ / EXE başlığı taşıyan sahte resim dosyası oluştur
        var fakeExeBytes = Encoding.ASCII.GetBytes("MZ_THIS_IS_AN_EXECUTABLE_FILE_NOT_AN_IMAGE");
        File.WriteAllBytes(_tempFakeExePath, fakeExeBytes);

        // Mevcut profil ayarını yedekle
        _originalProfile = AppSettings.GetBrandProfile();
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempPdfPath)) File.Delete(_tempPdfPath);
            if (File.Exists(_tempImagePath)) File.Delete(_tempImagePath);
            if (File.Exists(_tempFakeExePath)) File.Delete(_tempFakeExePath);
        }
        catch { }

        // Test bittiğinde orijinal profili geri yükle
        try
        {
            AppSettings.SaveBrandProfile(_originalProfile ?? CompanyBrandProfile.CreateDefaultKamatek());
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
    public void Default_CompanyBrandProfile_Matches_Kamatek_Identity()
    {
        // Act
        var profile = CompanyBrandProfile.CreateDefaultKamatek();

        // Assert
        profile.CompanyName.Should().Be("KAMATEK");
        profile.CompanySubtitle.Should().Be("ELEKTRİK VE GÜVENLİK SİSTEMLERİ");
        profile.CompanySloganOrBranch.Should().Be("ESKİŞEHİR DİAFON MERKEZİ");
        profile.PrimaryBrandColorHex.Should().Be("#182649");
        profile.AccentBrandColorHex.Should().Be("#D11920");
        profile.ShowUnitPricesInPdf.Should().BeTrue();
        profile.QuoteValidityDays.Should().Be(7);
        profile.QuoteTermsText.Should().Contain("geçerlilik süresince");
        profile.IbanTry.Should().NotBeNull();
    }

    [Fact]
    public void Save_And_Load_CustomBrandProfile_Persists_Correctly()
    {
        // Arrange
        var custom = new CompanyBrandProfile
        {
            CompanyName = "ACME ELEKTRONİK A.Ş.",
            CompanySubtitle = "AKILLI EV VE OTOMASYON",
            CompanySloganOrBranch = "ANKARA MERKEZ",
            PrimaryBrandColorHex = "#0F172A",
            AccentBrandColorHex = "#2563EB",
            BankName = "Garanti BBVA",
            AccountHolder = "ACME Elektronik San. Tic. Ltd.",
            IbanTry = "TR330006200000000000000001",
            ShowUnitPricesInPdf = false,
            QuoteValidityDays = 15,
            QuoteTermsText = "1. Teklifimiz 15 gün geçerlidir.\n2. Montaj fiyata dahildir.",
            CustomLogoBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="
        };

        // Act
        AppSettings.SaveBrandProfile(custom);
        AppSettings.ClearBrandProfileCache();
        var reloaded = AppSettings.GetBrandProfile();

        // Assert
        reloaded.CompanyName.Should().Be("ACME ELEKTRONİK A.Ş.");
        reloaded.CompanySubtitle.Should().Be("AKILLI EV VE OTOMASYON");
        reloaded.PrimaryBrandColorHex.Should().Be("#0F172A");
        reloaded.AccentBrandColorHex.Should().Be("#2563EB");
        reloaded.BankName.Should().Be("Garanti BBVA");
        reloaded.IbanTry.Should().Be("TR330006200000000000000001");
        reloaded.ShowUnitPricesInPdf.Should().BeFalse();
        reloaded.QuoteValidityDays.Should().Be(15);
        reloaded.CustomLogoBase64.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Reset_To_Default_Restores_Original_Kamatek_Profile()
    {
        // Arrange
        var modified = new CompanyBrandProfile
        {
            CompanyName = "GEÇİCİ TEST ŞİRKETİ",
            PrimaryBrandColorHex = "#990000"
        };
        AppSettings.SaveBrandProfile(modified);

        // Act
        AppSettings.SaveBrandProfile(CompanyBrandProfile.CreateDefaultKamatek());
        AppSettings.ClearBrandProfileCache();
        var restored = AppSettings.GetBrandProfile();

        // Assert
        restored.CompanyName.Should().Be("KAMATEK");
        restored.PrimaryBrandColorHex.Should().Be("#182649");
        restored.AccentBrandColorHex.Should().Be("#D11920");
    }

    [Fact]
    public void PdfService_Generates_Valid_Pdf_With_Default_Brand()
    {
        // Arrange
        AppSettings.SaveBrandProfile(CompanyBrandProfile.CreateDefaultKamatek());
        AppSettings.ClearBrandProfileCache();
        var pdfService = CreatePdfService();

        // Act
        pdfService.GenerateSampleQuotePreviewPdf(null, _tempPdfPath);

        // Assert
        File.Exists(_tempPdfPath).Should().BeTrue();
        var fileBytes = File.ReadAllBytes(_tempPdfPath);
        fileBytes.Length.Should().BeGreaterThan(100);

        // PDF dosya imzası doğrulaması: "%PDF" (0x25, 0x50, 0x44, 0x46)
        fileBytes[0].Should().Be(0x25);
        fileBytes[1].Should().Be(0x50);
        fileBytes[2].Should().Be(0x44);
        fileBytes[3].Should().Be(0x46);
    }

    [Fact]
    public void PdfService_Generates_Valid_Pdf_With_Custom_WhiteLabel_Brand_And_Package_Mode()
    {
        // Arrange: Özel White-Label profili ve birim fiyatsız paket modu
        var custom = new CompanyBrandProfile
        {
            CompanyName = "VİZYON GÜVENLİK SİSTEMLERİ",
            CompanySubtitle = "KAMERA & ALARM MERKEZİ",
            CompanySloganOrBranch = "İZMİR BÖLGE",
            PrimaryBrandColorHex = "#1E293B",
            AccentBrandColorHex = "#0284C7",
            BankName = "İş Bankası",
            AccountHolder = "Vizyon Güvenlik Ltd. Şti.",
            IbanTry = "TR550006400000000000000002",
            ShowUnitPricesInPdf = false, // Paket Teklif Modu (Birim fiyatları gizler)
            ShowStampInPdf = true,
            CustomLogoBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==",
            StampSignatureBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==",
            QuoteTermsText = "1. Paket teklifidir.\n2. 2 yıl yerinde servis garantisi mevcuttur."
        };
        AppSettings.SaveBrandProfile(custom);
        AppSettings.ClearBrandProfileCache();
        var pdfService = CreatePdfService();

        // Act
        pdfService.GenerateSampleQuotePreviewPdf(null, _tempPdfPath);

        // Assert
        File.Exists(_tempPdfPath).Should().BeTrue();
        var fileBytes = File.ReadAllBytes(_tempPdfPath);
        fileBytes.Length.Should().BeGreaterThan(100);

        // PDF başlığı geçerliliği
        fileBytes[0].Should().Be(0x25);
        fileBytes[1].Should().Be(0x50);
        fileBytes[2].Should().Be(0x44);
        fileBytes[3].Should().Be(0x46);
    }

    [Fact]
    public void FileSecurityValidator_Accepts_Valid_Png_And_Rejects_Invalid_Files()
    {
        // Act & Assert 1: Geçerli PNG doğrulanmalı (Hata fırlatmamalı)
        var validAction = () => FileSecurityValidator.ValidateImage(_tempImagePath);
        validAction.Should().NotThrow();

        // Act & Assert 2: Sahte MZ çalıştırılabilir başlığı taşıyan dosya reddedilmeli
        var fakeExeAction = () => FileSecurityValidator.ValidateImage(_tempFakeExePath);
        fakeExeAction.Should().Throw<InvalidOperationException>()
            .WithMessage("*Çalıştırılabilir dosya türleri yüklenemez*");
    }
}
