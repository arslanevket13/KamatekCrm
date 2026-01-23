using System.ComponentModel;

namespace KamatekCrm.Enums
{
    /// <summary>
    /// Müşteri tipi (Bireysel/Kurumsal)
    /// </summary>
    public enum CustomerType
    {
        [Description("Bireysel")]
        Individual = 0,

        [Description("Kurumsal")]
        Corporate = 1
    }
}
