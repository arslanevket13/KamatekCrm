using System.Text.Json.Serialization;

namespace KamatekCrm.Models.Specs
{
    /// <summary>
    /// Tüm ürün teknik özellik sınıfları için temel sınıf
    /// </summary>
    [JsonDerivedType(typeof(CameraSpecs), "camera")]
    [JsonDerivedType(typeof(IntercomSpecs), "intercom")]
    [JsonDerivedType(typeof(FireAlarmSpecs), "firealarm")]
    [JsonDerivedType(typeof(BurglarAlarmSpecs), "burglaralarm")]
    [JsonDerivedType(typeof(SmartHomeSpecs), "smarthome")]
    [JsonDerivedType(typeof(AccessControlSpecs), "accesscontrol")]
    [JsonDerivedType(typeof(SatelliteSpecs), "satellite")]
    [JsonDerivedType(typeof(FiberSpecs), "fiber")]
    [JsonDerivedType(typeof(GeneralSpecs), "general")]
    public abstract class ProductSpecBase
    {
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Güvenlik Kamera (CCTV) Teknik Özellikleri
    /// </summary>
    public class CameraSpecs : ProductSpecBase
    {
        public string? Resolution { get; set; } // 2MP, 4MP, 8MP, 4K
        public string? LensType { get; set; } // Fixed, Varifocal, Motorized
        public int IRDistance { get; set; } // Metre cinsinden
        public string? IpRating { get; set; } // IP66, IP67
        public bool IsPoE { get; set; } // Power over Ethernet
        public string? Compression { get; set; } // H.264, H.265, H.265+
        public string? CameraType { get; set; } // Dome, Bullet, PTZ, Turret
        public bool HasAudio { get; set; }
        public bool HasColorVu { get; set; } // Renkli gece görüşü
    }

    /// <summary>
    /// Görüntülü Diafon Teknik Özellikleri
    /// </summary>
    public class IntercomSpecs : ProductSpecBase
    {
        public string? ScreenSize { get; set; } // 4.3", 7", 10"
        public string? ConnectionType { get; set; } // IP, 2-Wire, 4-Wire, Analog
        public string? MountingType { get; set; } // Surface, Flush, Table
        public int MaxDoorStations { get; set; }
        public bool HasWiFi { get; set; }
        public bool HasMobileApp { get; set; }
        public bool HasMemory { get; set; } // Kayıt özelliği
    }

    /// <summary>
    /// Yangın Alarm Teknik Özellikleri
    /// </summary>
    public class FireAlarmSpecs : ProductSpecBase
    {
        public string? DetectorType { get; set; } // Smoke, Heat, Multi-Sensor, Beam
        public string? SystemType { get; set; } // Addressable, Conventional
        public bool IsWireless { get; set; }
        public string? ComponentType { get; set; } // Panel, Detector, Siren, Module
        public int ZoneCount { get; set; } // Panel için
        public bool HasRelay { get; set; }
    }

    /// <summary>
    /// Hırsız Alarm Teknik Özellikleri
    /// </summary>
    public class BurglarAlarmSpecs : ProductSpecBase
    {
        public string? ConnectionType { get; set; } // Wired, Wireless, Hybrid
        public string? ComponentType { get; set; } // Panel, PIR, Magnetic, Siren, Keypad
        public bool HasGSM { get; set; }
        public bool HasWiFi { get; set; }
        public int ZoneCount { get; set; }
        public string? DetectionType { get; set; } // Motion, Door/Window, Vibration, Glass Break
        public bool IsPetImmune { get; set; }
    }

    /// <summary>
    /// Akıllı Ev Teknik Özellikleri
    /// </summary>
    public class SmartHomeSpecs : ProductSpecBase
    {
        public string? Protocol { get; set; } // KNX, Zigbee, Z-Wave, WiFi, Matter
        public string? ModuleType { get; set; } // Switch, Dimmer, Relay, Sensor, Thermostat
        public int ChannelCount { get; set; }
        public string? LoadType { get; set; } // LED, Incandescent, Motor
        public int MaxLoad { get; set; } // Watt
        public bool RequiresHub { get; set; }
        public bool HasSceneSupport { get; set; }
    }

    /// <summary>
    /// Kartlı Geçiş (PDKS) Teknik Özellikleri
    /// </summary>
    public class AccessControlSpecs : ProductSpecBase
    {
        public string? ReaderFrequency { get; set; } // 125kHz, 13.56MHz, Dual
        public string? ComponentType { get; set; } // Controller, Reader, Lock, Button
        public bool IsWaterproof { get; set; }
        public string? IpRating { get; set; }
        public bool HasFingerprint { get; set; }
        public bool HasFaceRecognition { get; set; }
        public int UserCapacity { get; set; }
        public string? CommunicationType { get; set; } // Wiegand, RS485, TCP/IP
    }

    /// <summary>
    /// Uydu Sistemleri Teknik Özellikleri
    /// </summary>
    public class SatelliteSpecs : ProductSpecBase
    {
        public string? ComponentType { get; set; } // Dish, LNB, Receiver, Switch, Cable
        public int DishDiameter { get; set; } // cm
        public int LnbOutputs { get; set; } // 1, 2, 4, 8
        public string? Material { get; set; } // Steel, Aluminum, Fiberglass
        public bool IsMotorized { get; set; }
        public string? ReceiverType { get; set; } // SD, HD, 4K
        public bool HasCardSlot { get; set; }
    }

    /// <summary>
    /// Fiber Optik Teknik Özellikleri
    /// </summary>
    public class FiberSpecs : ProductSpecBase
    {
        public string? FiberMode { get; set; } // SingleMode, MultiMode
        public int CoreCount { get; set; } // 2, 4, 8, 12, 24, 48, 96
        public string? CableType { get; set; } // Indoor, Outdoor, ADSS, Direct Burial
        public string? ConnectorType { get; set; } // SC, LC, FC, ST
        public string? ComponentType { get; set; } // Cable, Patch Cord, ODF, Splice Closure
        public bool IsArmored { get; set; }
        public int CableDiameter { get; set; } // mm
    }

    /// <summary>
    /// Genel / Diğer Ürün Teknik Özellikleri
    /// </summary>
    public class GeneralSpecs : ProductSpecBase
    {
        public string? Material { get; set; }
        public string? Color { get; set; }
        public string? Dimensions { get; set; }
        public string? Weight { get; set; }
        public string? Voltage { get; set; }
        public string? Warranty { get; set; }
    }
}
