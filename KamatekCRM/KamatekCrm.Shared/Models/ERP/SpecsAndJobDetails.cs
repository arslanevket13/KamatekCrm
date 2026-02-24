namespace KamatekCrm.Shared.Models.Specs
{
    public class ProductSpecBase { public int Id { get; set; } public string Notes { get; set; } = string.Empty; }
    public class CameraSpecs : ProductSpecBase { public string Resolution { get; set; } = ""; public string LensType { get; set; } = ""; public string CameraType { get; set; } = ""; public string IRDistance { get; set; } = ""; public string IpRating { get; set; } = ""; public string Compression { get; set; } = ""; public bool IsPoE { get; set; } public bool HasAudio { get; set; } public bool HasColorVu { get; set; } }
    public class IntercomSpecs : ProductSpecBase { public string ScreenSize { get; set; } = ""; public string ConnectionType { get; set; } = ""; public string MountingType { get; set; } = ""; public bool HasWiFi { get; set; } public bool HasMobileApp { get; set; } public bool HasMemory { get; set; } }
    public class FireAlarmSpecs : ProductSpecBase { public string DetectorType { get; set; } = ""; public string SystemType { get; set; } = ""; public string ComponentType { get; set; } = ""; public bool IsWireless { get; set; } public bool HasRelay { get; set; } }
    public class BurglarAlarmSpecs : ProductSpecBase { public string ConnectionType { get; set; } = ""; public string ComponentType { get; set; } = ""; public string DetectionType { get; set; } = ""; public bool HasGSM { get; set; } public bool HasWiFi { get; set; } public bool IsPetImmune { get; set; } }
    public class SmartHomeSpecs : ProductSpecBase { public string Protocol { get; set; } = ""; public string ModuleType { get; set; } = ""; public string LoadType { get; set; } = ""; public bool RequiresHub { get; set; } public bool HasSceneSupport { get; set; } }
    public class AccessControlSpecs : ProductSpecBase { public string ReaderFrequency { get; set; } = ""; public string ComponentType { get; set; } = ""; public string CommunicationType { get; set; } = ""; public bool IsWaterproof { get; set; } public bool HasFingerprint { get; set; } public bool HasFaceRecognition { get; set; } }
    public class SatelliteSpecs : ProductSpecBase { public string ComponentType { get; set; } = ""; public string DishDiameter { get; set; } = ""; public string LnbOutputs { get; set; } = ""; public string Material { get; set; } = ""; public bool IsMotorized { get; set; } public bool HasCardSlot { get; set; } }
    public class FiberSpecs : ProductSpecBase { public string FiberMode { get; set; } = ""; public string CoreCount { get; set; } = ""; public string CableType { get; set; } = ""; public string ConnectorType { get; set; } = ""; public bool IsArmored { get; set; } }
    public class GeneralSpecs : ProductSpecBase { public string Material { get; set; } = ""; public string Color { get; set; } = ""; public string Warranty { get; set; } = ""; }
}

namespace KamatekCrm.Shared.Models.JobDetails
{
    public class CctvJobDetail : KamatekCrm.Shared.Models.JobDetailBase { }
    public class VideoIntercomJobDetail : KamatekCrm.Shared.Models.JobDetailBase { }
    public class FireAlarmJobDetail : KamatekCrm.Shared.Models.JobDetailBase { }
    public class BurglarAlarmJobDetail : KamatekCrm.Shared.Models.JobDetailBase { }
    public class SmartHomeJobDetail : KamatekCrm.Shared.Models.JobDetailBase { }
    public class AccessControlJobDetail : KamatekCrm.Shared.Models.JobDetailBase { }
    public class SatelliteJobDetail : KamatekCrm.Shared.Models.JobDetailBase { }
    public class FiberOpticJobDetail : KamatekCrm.Shared.Models.JobDetailBase { }
}
