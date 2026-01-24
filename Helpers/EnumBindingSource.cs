using System;
using System.Windows.Markup;

namespace KamatekCrm.Helpers
{
    public class EnumBindingSource : MarkupExtension
    {
        public Type EnumType { get; set; }

        public EnumBindingSource() { }

        public EnumBindingSource(Type enumType)
        {
            EnumType = enumType;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (EnumType == null)
                throw new InvalidOperationException("The EnumType must be specified.");

            return Enum.GetValues(EnumType);
        }
    }
}
