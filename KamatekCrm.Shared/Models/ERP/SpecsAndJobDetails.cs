using System.Text.Json.Serialization;

namespace KamatekCrm.Shared.Models.Specs
{
    /// <summary>
    /// Ürün teknik özelliklerinin polimorfik JSON (de)serializasyonu için temel sınıf.
    /// 
    /// Product.TechSpecsJson alanında JSONB olarak saklanır.
    /// [JsonDerivedType] attribute'ları System.Text.Json'un "$type" discriminator'ı
    /// ile doğru türü otomatik çözümlemesini sağlar.
    /// 
    /// Kullanım:
    ///   Serialize:  JsonSerializer.Serialize&lt;ProductSpecBase&gt;(specs, options)
    ///   Deserialize: JsonSerializer.Deserialize&lt;ProductSpecBase&gt;(json, options)
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    [JsonDerivedType(typeof(CameraSpecs), typeDiscriminator: "camera")]
    [JsonDerivedType(typeof(IntercomSpecs), typeDiscriminator: "intercom")]
    [JsonDerivedType(typeof(FireAlarmSpecs), typeDiscriminator: "fire_alarm")]
    [JsonDerivedType(typeof(BurglarAlarmSpecs), typeDiscriminator: "burglar_alarm")]
    [JsonDerivedType(typeof(SmartHomeSpecs), typeDiscriminator: "smart_home")]
    [JsonDerivedType(typeof(AccessControlSpecs), typeDiscriminator: "access_control")]
    [JsonDerivedType(typeof(SatelliteSpecs), typeDiscriminator: "satellite")]
    [JsonDerivedType(typeof(FiberSpecs), typeDiscriminator: "fiber")]
    [JsonDerivedType(typeof(GeneralSpecs), typeDiscriminator: "general")]
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    public class ProductSpecBase
    {
        public string Notes { get; set; } = string.Empty;
    }

    public class CameraSpecs : ProductSpecBase
    {
        public string Resolution { get; set; } = "";
        public string LensType { get; set; } = "";
        public string CameraType { get; set; } = "";
        public string IRDistance { get; set; } = "";
        public string IpRating { get; set; } = "";
        public string Compression { get; set; } = "";
        public bool IsPoE { get; set; }
        public bool HasAudio { get; set; }
        public bool HasColorVu { get; set; }
    }

    public class IntercomSpecs : ProductSpecBase
    {
        public string ScreenSize { get; set; } = "";
        public string ConnectionType { get; set; } = "";
        public string MountingType { get; set; } = "";
        public bool HasWiFi { get; set; }
        public bool HasMobileApp { get; set; }
        public bool HasMemory { get; set; }
    }

    public class FireAlarmSpecs : ProductSpecBase
    {
        public string DetectorType { get; set; } = "";
        public string SystemType { get; set; } = "";
        public string ComponentType { get; set; } = "";
        public bool IsWireless { get; set; }
        public bool HasRelay { get; set; }
    }

    public class BurglarAlarmSpecs : ProductSpecBase
    {
        public string ConnectionType { get; set; } = "";
        public string ComponentType { get; set; } = "";
        public string DetectionType { get; set; } = "";
        public bool HasGSM { get; set; }
        public bool HasWiFi { get; set; }
        public bool IsPetImmune { get; set; }
    }

    public class SmartHomeSpecs : ProductSpecBase
    {
        public string Protocol { get; set; } = "";
        public string ModuleType { get; set; } = "";
        public string LoadType { get; set; } = "";
        public bool RequiresHub { get; set; }
        public bool HasSceneSupport { get; set; }
    }

    public class AccessControlSpecs : ProductSpecBase
    {
        public string ReaderFrequency { get; set; } = "";
        public string ComponentType { get; set; } = "";
        public string CommunicationType { get; set; } = "";
        public bool IsWaterproof { get; set; }
        public bool HasFingerprint { get; set; }
        public bool HasFaceRecognition { get; set; }
    }

    public class SatelliteSpecs : ProductSpecBase
    {
        public string ComponentType { get; set; } = "";
        public string DishDiameter { get; set; } = "";
        public string LnbOutputs { get; set; } = "";
        public string Material { get; set; } = "";
        public bool IsMotorized { get; set; }
        public bool HasCardSlot { get; set; }
    }

    public class FiberSpecs : ProductSpecBase
    {
        public string FiberMode { get; set; } = "";
        public string CoreCount { get; set; } = "";
        public string CableType { get; set; } = "";
        public string ConnectorType { get; set; } = "";
        public bool IsArmored { get; set; }
    }

    public class GeneralSpecs : ProductSpecBase
    {
        public string Material { get; set; } = "";
        public string Color { get; set; } = "";
        public string Warranty { get; set; } = "";
    }
}

// NOT: Eski JobDetails namespace'i (CctvJobDetail, FireAlarmJobDetail, vb.) silindi.
// Bu sınıflar tamamen boştu (JobDetailBase'den hiçbir ek property devralımıyordu),
// hiçbir entity'de referansları yoktu ve veritabanında karşılıkları bulunmuyordu.
// Eğer gelecekte iş detayı yapıları gerekirse, ServiceJob.CategoriesJson JSONB
// alanı üzerinden yeni bir polimorfik yapı tasarlanmalıdır.

namespace KamatekCrm.Shared.Models.JobDetails
{
    [JsonDerivedType(typeof(CctvJobDetail), typeDiscriminator: "cctv")]
    [JsonDerivedType(typeof(VideoIntercomJobDetail), typeDiscriminator: "video_intercom")]
    [JsonDerivedType(typeof(FireAlarmJobDetail), typeDiscriminator: "fire_alarm")]
    [JsonDerivedType(typeof(BurglarAlarmJobDetail), typeDiscriminator: "burglar_alarm")]
    [JsonDerivedType(typeof(SmartHomeJobDetail), typeDiscriminator: "smart_home")]
    [JsonDerivedType(typeof(AccessControlJobDetail), typeDiscriminator: "access_control")]
    [JsonDerivedType(typeof(SatelliteJobDetail), typeDiscriminator: "satellite")]
    [JsonDerivedType(typeof(FiberOpticJobDetail), typeDiscriminator: "fiber_optic")]
    [JsonDerivedType(typeof(GeneralJobDetail), typeDiscriminator: "general")]
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    public class JobDetailBase
    {
        public string Notes { get; set; } = string.Empty;
    }

    public class CctvJobDetail : JobDetailBase { public int CameraCount { get; set; } }
    public class VideoIntercomJobDetail : JobDetailBase { public int ScreenCount { get; set; } }
    public class FireAlarmJobDetail : JobDetailBase { public int DetectorCount { get; set; } }
    public class BurglarAlarmJobDetail : JobDetailBase { public int SensorCount { get; set; } }
    public class SmartHomeJobDetail : JobDetailBase { public int ModuleCount { get; set; } }
    public class AccessControlJobDetail : JobDetailBase { public int DoorCount { get; set; } }
    public class SatelliteJobDetail : JobDetailBase { public int DishCount { get; set; } }
    public class FiberOpticJobDetail : JobDetailBase { public int CoreCount { get; set; } }
    public class GeneralJobDetail : JobDetailBase { }
}

