using System.Text.Json.Serialization;
using KamatekCrm.Enums;

namespace KamatekCrm.Models.JobDetails
{
    /// <summary>
    /// Tüm iş detay sınıfları için abstract base class
    /// </summary>
    [JsonDerivedType(typeof(CctvJobDetail), typeDiscriminator: "cctv")]
    [JsonDerivedType(typeof(VideoIntercomJobDetail), typeDiscriminator: "videointercom")]
    [JsonDerivedType(typeof(FireAlarmJobDetail), typeDiscriminator: "firealarm")]
    [JsonDerivedType(typeof(BurglarAlarmJobDetail), typeDiscriminator: "burglaralarm")]
    [JsonDerivedType(typeof(SmartHomeJobDetail), typeDiscriminator: "smarthome")]
    [JsonDerivedType(typeof(AccessControlJobDetail), typeDiscriminator: "accesscontrol")]
    [JsonDerivedType(typeof(SatelliteJobDetail), typeDiscriminator: "satellite")]
    [JsonDerivedType(typeof(FiberOpticJobDetail), typeDiscriminator: "fiberoptic")]
    public abstract class JobDetailBase
    {
        /// <summary>
        /// İş kategorisi
        /// </summary>
        public JobCategory Category { get; set; }

        /// <summary>
        /// Bina/İşyeri tipi (Tüm kategoriler için ortak)
        /// </summary>
        public BuildingType BuildingType { get; set; }
    }

    /// <summary>
    /// Güvenlik Kamera (CCTV) iş detayları
    /// </summary>
    public class CctvJobDetail : JobDetailBase
    {
        public CctvJobDetail()
        {
            Category = JobCategory.CCTV;
        }

        /// <summary>
        /// Kamera sayısı
        /// </summary>
        public int CameraCount { get; set; }

        /// <summary>
        /// Kamera tipi
        /// </summary>
        public CameraType CameraType { get; set; }

        /// <summary>
        /// Çözünürlük
        /// </summary>
        public Resolution Resolution { get; set; }

        /// <summary>
        /// Kayıt süresi (gün)
        /// </summary>
        public int RecordingDays { get; set; }

        /// <summary>
        /// Depolama kapasitesi (TB)
        /// </summary>
        public int StorageTB { get; set; }

        /// <summary>
        /// Sistem tipi (IP/Analog)
        /// </summary>
        public SystemType SystemType { get; set; }

        /// <summary>
        /// Gece görüş var mı?
        /// </summary>
        public bool IsNightVision { get; set; }

        /// <summary>
        /// Ses kaydı var mı?
        /// </summary>
        public bool HasAudio { get; set; }
    }

    /// <summary>
    /// Görüntülü Diafon iş detayları
    /// </summary>
    public class VideoIntercomJobDetail : JobDetailBase
    {
        public VideoIntercomJobDetail()
        {
            Category = JobCategory.VideoIntercom;
        }

        /// <summary>
        /// Daire sayısı
        /// </summary>
        public int ApartmentCount { get; set; }

        /// <summary>
        /// Monitör boyutu
        /// </summary>
        public MonitorSize MonitorSize { get; set; }

        /// <summary>
        /// Sistem tipi (IP/Analog)
        /// </summary>
        public SystemType SystemType { get; set; }

        /// <summary>
        /// Gece görüş var mı?
        /// </summary>
        public bool NightVision { get; set; }

        /// <summary>
        /// Mobil uygulama desteği var mı?
        /// </summary>
        public bool MobileAppSupport { get; set; }

        /// <summary>
        /// Kilit tipi
        /// </summary>
        public LockType LockType { get; set; }
    }

    /// <summary>
    /// Yangın Alarm iş detayları
    /// </summary>
    public class FireAlarmJobDetail : JobDetailBase
    {
        public FireAlarmJobDetail()
        {
            Category = JobCategory.FireAlarm;
        }

        /// <summary>
        /// Sistem tipi
        /// </summary>
        public FireAlarmSystemType SystemType { get; set; }

        /// <summary>
        /// Duman dedektörü sayısı
        /// </summary>
        public int SmokeDetectorCount { get; set; }

        /// <summary>
        /// Isı dedektörü sayısı
        /// </summary>
        public int HeatDetectorCount { get; set; }

        /// <summary>
        /// Siren sayısı
        /// </summary>
        public int SirenCount { get; set; }

        /// <summary>
        /// Gaz dedektörü var mı?
        /// </summary>
        public bool HasGasDetector { get; set; }

        /// <summary>
        /// Beam dedektörü var mı?
        /// </summary>
        public bool HasBeamDetector { get; set; }

        /// <summary>
        /// Kat sayısı
        /// </summary>
        public int FloorCount { get; set; }
    }

    /// <summary>
    /// Hırsız Alarm iş detayları
    /// </summary>
    public class BurglarAlarmJobDetail : JobDetailBase
    {
        public BurglarAlarmJobDetail()
        {
            Category = JobCategory.BurglarAlarm;
        }

        /// <summary>
        /// Sistem tipi
        /// </summary>
        public BurglarAlarmSystemType SystemType { get; set; }

        /// <summary>
        /// Hareket dedektörü sayısı
        /// </summary>
        public int MotionDetectorCount { get; set; }

        /// <summary>
        /// Manyetik kontak sayısı
        /// </summary>
        public int MagneticContactCount { get; set; }

        /// <summary>
        /// Panik butonu var mı?
        /// </summary>
        public bool HasPanicButton { get; set; }

        /// <summary>
        /// Harici siren var mı?
        /// </summary>
        public bool HasExternalSiren { get; set; }

        /// <summary>
        /// Bölge sayısı
        /// </summary>
        public int ZoneCount { get; set; }
    }

    /// <summary>
    /// Akıllı Ev iş detayları
    /// </summary>
    public class SmartHomeJobDetail : JobDetailBase
    {
        public SmartHomeJobDetail()
        {
            Category = JobCategory.SmartHome;
            ControlMethods = new List<string>();
        }

        /// <summary>
        /// Kontrol yöntemleri (App, Voice, Panel)
        /// </summary>
        public List<string> ControlMethods { get; set; }

        /// <summary>
        /// Senaryo sayısı
        /// </summary>
        public int ScenarioCount { get; set; }

        /// <summary>
        /// Oda sayısı
        /// </summary>
        public int RoomCount { get; set; }

        /// <summary>
        /// Sistem tipi
        /// </summary>
        public SmartHomeSystemType SystemType { get; set; }

        /// <summary>
        /// Su vanası kontrolü var mı?
        /// </summary>
        public bool HasWaterValveControl { get; set; }

        /// <summary>
        /// Altyapı durumu
        /// </summary>
        public InfrastructureStatus InfrastructureStatus { get; set; }
    }

    /// <summary>
    /// Kartlı Geçiş (PDKS) iş detayları
    /// </summary>
    public class AccessControlJobDetail : JobDetailBase
    {
        public AccessControlJobDetail()
        {
            Category = JobCategory.AccessControl;
        }

        /// <summary>
        /// Okuyucu tipi
        /// </summary>
        public ReaderType ReaderType { get; set; }

        /// <summary>
        /// Kullanıcı sayısı
        /// </summary>
        public int UserCount { get; set; }

        /// <summary>
        /// Kilit tipi
        /// </summary>
        public LockType LockType { get; set; }

        /// <summary>
        /// Yazılım gerekli mi?
        /// </summary>
        public bool SoftwareRequired { get; set; }

        /// <summary>
        /// Entegrasyon tipi
        /// </summary>
        public IntegrationType Integration { get; set; }
    }

    /// <summary>
    /// Uydu Sistemleri iş detayları
    /// </summary>
    public class SatelliteJobDetail : JobDetailBase
    {
        public SatelliteJobDetail()
        {
            Category = JobCategory.SatelliteSystem;
        }

        /// <summary>
        /// Çanak boyutu (cm)
        /// </summary>
        public int DishSize { get; set; }

        /// <summary>
        /// LNB sayısı
        /// </summary>
        public int LnbCount { get; set; }

        /// <summary>
        /// Alıcı sayısı
        /// </summary>
        public int ReceiverCount { get; set; }

        /// <summary>
        /// Dağıtım tipi
        /// </summary>
        public DistributionType DistributionType { get; set; }

        /// <summary>
        /// Uydu seçimi
        /// </summary>
        public SatelliteType Satellite { get; set; }
    }

    /// <summary>
    /// Fiber Optik iş detayları
    /// </summary>
    public class FiberOpticJobDetail : JobDetailBase
    {
        public FiberOpticJobDetail()
        {
            Category = JobCategory.FiberOptic;
        }

        /// <summary>
        /// Fiber tipi
        /// </summary>
        public FiberType FiberType { get; set; }

        /// <summary>
        /// Core sayısı
        /// </summary>
        public int CoreCount { get; set; }

        /// <summary>
        /// Uzunluk (metre)
        /// </summary>
        public double LengthMeters { get; set; }

        /// <summary>
        /// Konnektör tipi
        /// </summary>
        public ConnectorType ConnectorType { get; set; }

        /// <summary>
        /// Polish tipi
        /// </summary>
        public PolishType PolishType { get; set; }

        /// <summary>
        /// Sonlandırma kutusu tipi
        /// </summary>
        public TerminationBoxType TerminationBoxType { get; set; }
    }
}
