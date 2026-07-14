using System;
using System.ComponentModel;
using System.Globalization;

namespace OpenTyping
{
    [TypeConverter(typeof(KeyPosConverter))]
    public sealed class KeyPosConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string s)
            {
                string[] splitedValue = s.Split(',');

                if (splitedValue.Length != 2 ||
                    !Int32.TryParse(splitedValue[0], out int row) ||
                    !Int32.TryParse(splitedValue[1], out int column))
                {
                    throw new FormatException("키 위치 값 \"" + s + "\" 이 올바른 형식(\"행,열\")이 아닙니다.");
                }

                return new KeyPos(row, column);
            }

            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                var keyValue = (KeyPos)value;
                return keyValue.Row + "," + keyValue.Column;
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
