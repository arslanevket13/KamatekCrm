using System;

namespace KamatekCrm.Shared.Models
{
    /// <summary>
    /// Kurumsal kimlik, PDF çıktı şablonları ve White-Label marka yapılandırma modeli.
    /// Hem yerel AppData üzerinde hem de LAN istemcileri arasında taşınabilir formatta saklanır.
    /// </summary>
    public class CompanyBrandProfile
    {
        // -------------------------------------------------------------
        // Kurumsal İsim ve Slogan
        // -------------------------------------------------------------
        public string CompanyName { get; set; } = "KAMATEK";
        public string CompanySubtitle { get; set; } = "ELEKTRİK VE GÜVENLİK SİSTEMLERİ";
        public string CompanySloganOrBranch { get; set; } = "ESKİŞEHİR DİAFON MERKEZİ";

        // -------------------------------------------------------------
        // İletişim ve Resmi Bilgiler
        // -------------------------------------------------------------
        public string Address { get; set; } = "Kurtuluş, Ziya Paşa Cd. 72/A Odunpazarı / Eskişehir";
        public string Phone { get; set; } = "0222 230 40 50";
        public string Email { get; set; } = "info@kamatekelektrik.com";
        public string Website { get; set; } = "www.kamatekelektrik.com";
        public string TaxOffice { get; set; } = "Battalgazi";
        public string TaxNumber { get; set; } = "";
        public string TradeRegistryNo { get; set; } = "";
        public string MersisNo { get; set; } = "";

        // -------------------------------------------------------------
        // Görsel ve Renk Kimliği (White-Label)
        // -------------------------------------------------------------
        /// <summary>
        /// İstemci makinedeki yerel logo dosya yolu (varsa).
        /// </summary>
        public string CustomLogoPath { get; set; } = "";

        /// <summary>
        /// Logonun gömülü Base64 dizesi (LAN istemcilerinde dosya olmasa dahi logonun kırılmasını engeller).
        /// </summary>
        public string CustomLogoBase64 { get; set; } = "";

        /// <summary>
        /// Şeffaf PNG dijital kaşe ve yetkili imza görseli Base64 dizesi.
        /// </summary>
        public string StampSignatureBase64 { get; set; } = "";

        /// <summary>
        /// Birincil kurumsal marka rengi (Varsayılan Kamatek Laciverti: #182649).
        /// </summary>
        public string PrimaryBrandColorHex { get; set; } = "#182649";

        /// <summary>
        /// İkincil / Vurgu kurumsal marka rengi (Varsayılan Kamatek Kırmızısı: #D11920).
        /// </summary>
        public string AccentBrandColorHex { get; set; } = "#D11920";

        // -------------------------------------------------------------
        // Banka / Ödeme Bilgileri
        // -------------------------------------------------------------
        public string BankName { get; set; } = "Ziraat Bankası";
        public string AccountHolder { get; set; } = "Kamatek Elektrik ve Güvenlik Sistemleri";
        public string IbanTry { get; set; } = "";
        public string IbanUsd { get; set; } = "";
        public string IbanEur { get; set; } = "";

        // -------------------------------------------------------------
        // Belge Şablon Metinleri ve Görünüm Tercihleri
        // -------------------------------------------------------------
        public int QuoteValidityDays { get; set; } = 7;

        public string QuoteTermsText { get; set; } =
            "1. Teklifimiz hazırlandığı tarihten itibaren belirtilen geçerlilik süresince yürürlüktedir.\n" +
            "2. Fiyatlarımıza belirtilmediği sürece KDV dahil değildir.\n" +
            "3. Montaj, devreye alma ve kablolama hizmetleri aksi belirtilmedikçe teklife dahildir.\n" +
            "4. Ürünlerimiz 2 (iki) yıl süreyle üretici/ithalatçı firma garantisi altındadır.\n" +
            "5. Ödemeler teklif onayında %50 peşin, iş tesliminde bakiye olarak tahsil edilir.";

        public string ServiceFormTermsText { get; set; } =
            "1. Teslim alınan cihazlar arıza tespit ve onarım amacıyla kabul edilmiştir.\n" +
            "2. 90 gün içinde teslim alınmayan cihazlardan firmamız sorumlu değildir.\n" +
            "3. Yapılan onarım ve değiştirilen parçalar 6 ay Kamatek servis garantisi altındadır.\n" +
            "4. Müşteri onayı olmaksızın ek maliyet getiren işlemler uygulanmaz.";

        public string WarrantyTermsText { get; set; } =
            "Bu belge kapsamındaki ürün ve montaj hizmetleri, fatura/teslim tarihinden itibaren malzeme ve işçilik hatalarına karşı garantilidir.";

        public bool ShowUnitPricesInPdf { get; set; } = true;
        public bool ShowProductImagesInPdf { get; set; } = true;
        public bool ShowStampInPdf { get; set; } = true;
        public string WatermarkText { get; set; } = "";

        /// <summary>
        /// Varsayılan Kamatek Elektrik ve Güvenlik Sistemleri (Eskişehir Diafon Merkezi) profilini üretir.
        /// </summary>
        public static CompanyBrandProfile CreateDefaultKamatek()
        {
            return new CompanyBrandProfile();
        }
    }
}

