using FluentAssertions;
using KamatekCrm.ApplicationCore.Services;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Services;

namespace KamatekCrm.Tests.Services;

public sealed class ProjectQuoteWorkbenchTests
{
    /// <summary>
    /// TEST 1 (Non-Destructive Tree Merge):
    /// Mevcut düğümlerinde malzeme bulunan bir yapı ağacına yeni blok/kat eklendiğinde
    /// eski düğümlerdeki malzemelerin silinmeden korunduğunu doğrular.
    /// </summary>
    [Fact]
    public void Test1_NonDestructiveTreeMerge_PreservesExistingItems()
    {
        var generator = new StructureGeneratorService();

        // 1. Başlangıçta 1 Blok, 2 Kat, 2 Dairelik bir yapı oluştur
        var root = generator.GenerateApartment("A Blok", 2, 2, includeEntrance: false);
        root.Should().NotBeNull();

        // 1. Katta Daire 1'i bul ve içine kalemler ekle
        var floor1 = root.Children.FirstOrDefault(c => c.Name == "1. Kat");
        floor1.Should().NotBeNull();
        var flat1 = floor1!.Children.FirstOrDefault(c => c.Name == "Daire 1");
        flat1.Should().NotBeNull();

        flat1!.Items.Add(new ScopeNodeItem
        {
            Id = 101,
            Name = "4MP IP Kamera",
            Quantity = 2,
            UnitPrice = 750m,
            UnitCost = 400m
        });
        flat1.Items.Add(new ScopeNodeItem
        {
            Id = 102,
            Name = "Manyetik Kontak",
            Quantity = 4,
            UnitPrice = 120m,
            UnitCost = 50m
        });

        // 2. Yıkıcı olmayan Delta Merge ile Kat sayısını 2'den 4'e çıkar
        generator.MergeApartment(root, 4, 2, includeEntrance: false);

        // 3. Doğrula: Daire 1'deki kalemler silinmedi ve korundu
        var preservedFloor1 = root.Children.FirstOrDefault(c => c.Name == "1. Kat");
        preservedFloor1.Should().NotBeNull();
        var preservedFlat1 = preservedFloor1!.Children.FirstOrDefault(c => c.Name == "Daire 1");
        preservedFlat1.Should().NotBeNull();
        preservedFlat1!.Items.Should().HaveCount(2);
        preservedFlat1.Items[0].Name.Should().Be("4MP IP Kamera");
        preservedFlat1.Items[0].Quantity.Should().Be(2);
        preservedFlat1.Items[1].Name.Should().Be("Manyetik Kontak");
        preservedFlat1.Items[1].Quantity.Should().Be(4);

        // Ve yeni 3. Kat ve 4. Kat ağaca eklenmiş olmalı
        root.Children.Should().Contain(c => c.Name == "3. Kat");
        root.Children.Should().Contain(c => c.Name == "4. Kat");
    }

    /// <summary>
    /// TEST 2 (Bottom-Up Recursive Rollup & Multiplier):
    /// Çok katmanlı (Site -> Blok -> Kat -> Daire) ve çarpanlı bir ağaçta alt kalem fiyatı/miktarı
    /// değiştiğinde kök toplamın ve fiyatlama politikasının doğru hesaplandığını doğrular.
    /// </summary>
    [Fact]
    public void Test2_BottomUpRecursiveRollupAndMultiplier_CalculatesTotalsCorrectly()
    {
        // Project (x1) -> Block (x2 özdeş blok) -> Floor (x3 kat) -> Flat (x4 daire)
        var projectNode = new ScopeNode { Name = "Gül Sitesi", Type = NodeType.Project, Multiplier = 1 };
        var blockNode = new ScopeNode { Name = "Tip Blok", Type = NodeType.Block, Multiplier = 2 };
        var floorNode = new ScopeNode { Name = "Tip Kat", Type = NodeType.Floor, Multiplier = 3 };
        var flatNode = new ScopeNode { Name = "Tip Daire", Type = NodeType.Flat, Multiplier = 4 };

        projectNode.AddChild(blockNode);
        blockNode.AddChild(floorNode);
        floorNode.AddChild(flatNode);

        // Daire içine kalem ekle: 2 adet, Birim Fiyat: 100 TL, Alış Maliyeti: 40 TL
        var testItem = new ScopeNodeItem
        {
            Id = 201,
            Name = "Gaz Sensörü",
            Quantity = 2,
            UnitPrice = 100m,
            UnitCost = 40m
        };
        flatNode.Items.Add(testItem);

        // Kümülatif çarpan: 1 * 2 * 3 * 4 = 24
        // Tek daire toplam satış: 2 * 100 = 200 TL
        // Daire (x4): 200 * 4 = 800 TL
        // Kat (x3): 800 * 3 = 2,400 TL
        // Blok (x2): 2,400 * 2 = 4,800 TL
        // Project (x1): 4,800 TL
        ScopeNode.RecalculateTreeTotals(projectNode);

        flatNode.NodeSubTotal.Should().Be(800m);
        floorNode.NodeSubTotal.Should().Be(2400m);
        blockNode.NodeSubTotal.Should().Be(4800m);
        projectNode.RecursiveTotal.Should().Be(4800m);

        // Maliyet doğrulaması: 2 * 40 * 24 = 1,920 TL
        projectNode.RecursiveTotalCost.Should().Be(1920m);

        // ProjectQuotePricingPolicy ile kök uyumunu test et
        var pricingResult = ProjectQuotePricingPolicy.Calculate(new[] { projectNode }, discountPercent: 0m, vatRate: 0m);
        pricingResult.IsSuccess.Should().BeTrue();
        pricingResult.Value!.GrossRevenue.Should().Be(4800m);
        pricingResult.Value.TotalCost.Should().Be(1920m);
        pricingResult.Value.TotalQuantity.Should().Be(48); // 2 adet * 24 = 48 adet

        // Kalem miktarını 2'den 5'e çıkarınca toplam anında güncellenmeli (5 * 100 * 24 = 12,000 TL)
        testItem.Quantity = 5;
        ScopeNode.RecalculateTreeTotals(projectNode);

        projectNode.RecursiveTotal.Should().Be(12000m);
        projectNode.RecursiveTotalCost.Should().Be(4800m); // 5 * 40 * 24 = 4,800 TL
    }

    /// <summary>
    /// TEST 3 (Clone & Broadcast):
    /// Örnek bir daire düğümündeki kalemlerin diğer benzer daire düğümlerine
    /// doğru kopyalandığını ve ağaç toplamının güncellendiğini doğrular.
    /// </summary>
    [Fact]
    public void Test3_CloneAndBroadcast_CopiesItemsToSiblingNodes()
    {
        var generator = new StructureGeneratorService();
        // 1 Blok, 2 Kat, her katta 2 Daire
        var root = generator.GenerateApartment("B Blok", 2, 2, includeEntrance: false);
        var floor1 = root.Children.First(c => c.Name == "1. Kat");
        var daire1 = floor1.Children.First(c => c.Name == "Daire 1");
        var daire2 = floor1.Children.First(c => c.Name == "Daire 2");

        var floor2 = root.Children.First(c => c.Name == "2. Kat");
        var daire3 = floor2.Children.First(c => c.Name == "Daire 3");
        var daire4 = floor2.Children.First(c => c.Name == "Daire 4");

        // Daire 1'e prototip kalemler ekle
        daire1.Items.Add(new ScopeNodeItem { Name = "Duman Dedektörü", Quantity = 1, UnitPrice = 300m, UnitCost = 150m });
        daire1.Items.Add(new ScopeNodeItem { Name = "Siren", Quantity = 1, UnitPrice = 200m, UnitCost = 80m });

        // Daire 1'in kalemlerini Daire 2, 3 ve 4'e toplu çoğalt (Broadcast)
        ProjectScopeService.BroadcastItemsToTargets(daire1, new[] { daire2, daire3, daire4 }, clearTargetFirst: true);

        // Her dairede 2 kalem olmalı
        daire2.Items.Should().HaveCount(2);
        daire3.Items.Should().HaveCount(2);
        daire4.Items.Should().HaveCount(2);

        // Ağaç toplamını yeniden hesapla
        ScopeNode.RecalculateTreeTotals(root);

        // Daire başı toplam: 300 + 200 = 500 TL
        // 4 daire * 500 TL = 2,000 TL
        root.RecursiveTotal.Should().Be(2000m);
        root.RecursiveTotalCost.Should().Be(920m); // 4 * (150 + 80) = 920 TL
    }

    /// <summary>
    /// TEST 4 (WBS vs Consolidated BOM Parity):
    /// Yapı ağacı mahal toplamı ile Konsolide İcmal (BOM) tablosu toplamının
    /// iskonto ve KDV sonrasında Largest Remainder yöntemiyle kuruşu kuruşuna (0.00 TL farkla)
    /// eşit çıktığını doğrular.
    /// </summary>
    [Fact]
    public void Test4_WbsVsConsolidatedBomParity_MatchesToThePenny()
    {
        // Karmaşık bir ağaç oluştur
        var root = new ScopeNode { Name = "Proje", Type = NodeType.Project };
        var blockA = new ScopeNode { Name = "A Blok", Type = NodeType.Block, Multiplier = 2 };
        var floor1 = new ScopeNode { Name = "1. Kat", Type = NodeType.Floor, Multiplier = 3 };
        var floor2 = new ScopeNode { Name = "2. Kat", Type = NodeType.Floor, Multiplier = 1 };

        root.AddChild(blockA);
        blockA.AddChild(floor1);
        blockA.AddChild(floor2);

        // Aynı ürünleri (SKU) farklı düğümlere küsuratlı fiyatlarla ekle
        floor1.Items.Add(new ScopeNodeItem { ProductId = 1, Name = "IP Kamera 4MP", Quantity = 3, UnitPrice = 333.33m, UnitCost = 150m });
        floor1.Items.Add(new ScopeNodeItem { ProductId = 2, Name = "Cat6 Kablo (mt)", Quantity = 55, UnitPrice = 12.75m, UnitCost = 6.20m });

        floor2.Items.Add(new ScopeNodeItem { ProductId = 1, Name = "IP Kamera 4MP", Quantity = 2, UnitPrice = 333.33m, UnitCost = 150m });
        floor2.Items.Add(new ScopeNodeItem { ProductId = 3, Name = "PoE Switch 8 Port", Quantity = 1, UnitPrice = 1250.50m, UnitCost = 800m });

        // İskonto ve KDV uygula
        decimal discountPercent = 13.5m;
        decimal kdvRate = 20m;

        // 1. WBS Toplamını Hesapla
        var wbsResult = ProjectScopeService.CalculateScopeTotals(new[] { root }, discountPercent, kdvRate);

        // 2. Konsolide İcmal (BOM) Tablosunu Hesapla
        var bomResult = ProjectScopeService.GenerateConsolidatedBom(new[] { root }, discountPercent, kdvRate);

        // 3. Kuruş Dengeleme Doğrulaması: Aralarında 0.00 TL bile fark kalmamalı!
        bomResult.GrandTotal.Should().Be(wbsResult.GrandTotal);
        bomResult.TotalNetRevenue.Should().Be(wbsResult.NetTotal);
        bomResult.TotalVat.Should().Be(wbsResult.TaxTotal);
        bomResult.TotalDiscount.Should().Be(wbsResult.OverallDiscount);
        bomResult.TotalGrossRevenue.Should().Be(wbsResult.SubTotal);

        // Konsolide kalemlerin toplamı da Genel Toplama kuruşu kuruşuna eşit olmalı
        bomResult.Items.Sum(item => item.GrandTotal).Should().Be(bomResult.GrandTotal);

        // Ürün 1 (IP Kamera) kümülatif miktar kontrolü:
        // floor1: 3 adet * 3 (kat) * 2 (blok) = 18 adet
        // floor2: 2 adet * 1 (kat) * 2 (blok) = 4 adet
        // Toplam = 22 adet
        var cameraBom = bomResult.Items.FirstOrDefault(i => i.ProductId == 1);
        cameraBom.Should().NotBeNull();
        cameraBom!.TotalQuantity.Should().Be(22);
    }

    /// <summary>
    /// TEST 5 (Target Price Goal-Seek):
    /// Kullanıcının hedef genel toplam girdiğinde iskonto oranının ve tutarının
    /// kuruş hatasız çözüldüğünü ve toplamın hedefe oturduğunu doğrular.
    /// </summary>
    [Fact]
    public void Test5_TargetPriceGoalSeek_SolvesDiscountAccurately()
    {
        var root = new ScopeNode { Name = "Teklif", Type = NodeType.Project };
        root.Items.Add(new ScopeNodeItem { Name = "Ana Santral", Quantity = 1, UnitPrice = 15_000m, UnitCost = 8_000m });
        root.Items.Add(new ScopeNodeItem { Name = "İşçilik & Montaj", Quantity = 1, UnitPrice = 5_000m, UnitCost = 1_000m });

        // Brüt Toplam: 20,000 TL, KDV %20 => İskontosuz Genel Toplam: 24,000 TL
        decimal kdvRate = 20m;
        var initial = ProjectScopeService.CalculateScopeTotals(new[] { root }, 0m, kdvRate);
        initial.GrandTotal.Should().Be(24_000m);

        // Kullanıcı 20,000 TL (KDV Dahil) Düz Hedef Fiyat istiyor
        decimal targetGrandTotal = 20_000m;

        // Goal-Seek Algoritması:
        // targetNet = targetGrandTotal / (1 + kdvRate/100) = 20,000 / 1.20 = 16,666.67 TL
        // requiredDiscount = gross - targetNet = 20,000 - 16,666.67 = 3,333.33 TL
        // discountPercent = (3,333.33 / 20,000) * 100 = 16.67%
        decimal targetNet = FinancialCalculator.RoundMoney(targetGrandTotal / (1m + kdvRate / 100m));
        decimal requiredDiscountAmount = initial.SubTotal - targetNet;
        decimal discountPercent = Math.Round((requiredDiscountAmount / initial.SubTotal) * 100m, 2, MidpointRounding.ToEven);

        // Yeni oranla hesapla
        var solved = ProjectScopeService.CalculateScopeTotals(new[] { root }, discountPercent, kdvRate);

        // Hedefe 1 TL tolerans dahilinde (veya kuruş farkıyla) tam oturduğunu doğrula
        Math.Abs(solved.GrandTotal - targetGrandTotal).Should().BeLessThanOrEqualTo(1.00m);
        solved.NetTotal.Should().BeLessThan(initial.SubTotal);
        solved.OverallDiscount.Should().BeGreaterThan(0m);
    }
}
