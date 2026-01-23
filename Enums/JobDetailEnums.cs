using System.ComponentModel;

namespace KamatekCrm.Enums
{
    /// <summary>
    /// Kamera tipi
    /// </summary>
    public enum CameraType
    {
        [Description("Dome")]
        Dome = 0,

        [Description("Bullet")]
        Bullet = 1,

        [Description("PTZ")]
        PTZ = 2
    }

    /// <summary>
    /// Kamera çözünürlüğü
    /// </summary>
    public enum Resolution
    {
        [Description("HD (720p/1080p)")]
        HD = 0,

        [Description("4K (Ultra HD)")]
        FourK = 1
    }

    /// <summary>
    /// Sistem tipi (IP/Analog)
    /// </summary>
    public enum SystemType
    {
        [Description("IP")]
        IP = 0,

        [Description("Analog")]
        Analog = 1
    }

    /// <summary>
    /// Monitör boyutu
    /// </summary>
    public enum MonitorSize
    {
        [Description("4.3\"")]
        Size_4_3 = 0,

        [Description("7\"")]
        Size_7 = 1,

        [Description("10\"")]
        Size_10 = 2
    }

    /// <summary>
    /// Kilit tipi
    /// </summary>
    public enum LockType
    {
        [Description("Strike (Mandallı)")]
        Strike = 0,

        [Description("Magnet (Mıknatıslı)")]
        Magnet = 1,

        [Description("Bolt (Sürgülü)")]
        Bolt = 2
    }

    /// <summary>
    /// Yangın alarm sistem tipi
    /// </summary>
    public enum FireAlarmSystemType
    {
        [Description("Conventional (Konvansiyonel)")]
        Conventional = 0,

        [Description("Addressable (Adresli)")]
        Addressable = 1
    }

    /// <summary>
    /// Hırsız alarm sistem tipi
    /// </summary>
    public enum BurglarAlarmSystemType
    {
        [Description("Wired (Kablolu)")]
        Wired = 0,

        [Description("Wireless (Kablosuz)")]
        Wireless = 1
    }

    /// <summary>
    /// Akıllı ev sistem tipi
    /// </summary>
    public enum SmartHomeSystemType
    {
        [Description("KNX")]
        KNX = 0,

        [Description("Zigbee")]
        Zigbee = 1,

        [Description("WiFi")]
        WiFi = 2
    }

    /// <summary>
    /// Altyapı durumu
    /// </summary>
    public enum InfrastructureStatus
    {
        [Description("Tamamlanmış")]
        Completed = 0,

        [Description("Eksik")]
        Incomplete = 1
    }

    /// <summary>
    /// Okuyucu tipi (PDKS)
    /// </summary>
    public enum ReaderType
    {
        [Description("Card (Kart)")]
        Card = 0,

        [Description("Finger (Parmak İzi)")]
        Finger = 1,

        [Description("Face (Yüz Tanıma)")]
        Face = 2
    }

    /// <summary>
    /// Entegrasyon tipi (PDKS)
    /// </summary>
    public enum IntegrationType
    {
        [Description("Yok")]
        None = 0,

        [Description("Lift (Asansör)")]
        Lift = 1,

        [Description("Turnstile (Turnike)")]
        Turnstile = 2,

        [Description("Lift + Turnstile")]
        Both = 3
    }

    /// <summary>
    /// Dağıtım tipi (Uydu)
    /// </summary>
    public enum DistributionType
    {
        [Description("Individual (Bireysel)")]
        Individual = 0,

        [Description("Central (Merkezi)")]
        Central = 1
    }

    /// <summary>
    /// Uydu seçimi
    /// </summary>
    public enum SatelliteType
    {
        [Description("Turksat")]
        Turksat = 0,

        [Description("Hotbird")]
        Hotbird = 1,

        [Description("Diğer")]
        Other = 2
    }

    /// <summary>
    /// Fiber tipi
    /// </summary>
    public enum FiberType
    {
        [Description("Single Mode")]
        SingleMode = 0,

        [Description("Multi Mode")]
        MultiMode = 1
    }

    /// <summary>
    /// Konnektör tipi
    /// </summary>
    public enum ConnectorType
    {
        [Description("SC")]
        SC = 0,

        [Description("LC")]
        LC = 1,

        [Description("ST")]
        ST = 2
    }

    /// <summary>
    /// Polish tipi
    /// </summary>
    public enum PolishType
    {
        [Description("APC (Angled Physical Contact)")]
        APC = 0,

        [Description("UPC (Ultra Physical Contact)")]
        UPC = 1
    }

    /// <summary>
    /// Sonlandırma kutusu tipi
    /// </summary>
    public enum TerminationBoxType
    {
        [Description("Rack (19\" Kabin)")]
        Rack = 0,

        [Description("Wall (Duvar Tipi)")]
        Wall = 1
    }
}
