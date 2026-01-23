using System.ComponentModel;
using System.Reflection;

namespace Kpett.ChatApp.Helper
{
    public static class EnumHelper
    {
        /// <summary>
        /// Get the names of an enum as a list of strings
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static List<string> GetEnumNames<T>() where T : Enum
        {
            return Enum.GetNames(typeof(T)).ToList();
        }

        /// <summary>
        // / Get the values of an enum as a list of integers
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static List<int> GetEnumValues<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T)).Cast<int>().ToList();
        }

        /// <summary>
        /// Get the description of an enum value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string GetEnumDescription<T>(T value) where T : Enum
        {
            var field = typeof(T).GetField(value.ToString());
            if (field == null)
            {
                return value.ToString(); // Return the enum value as a string if the field is null
            }

            var attribute = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute))
                            as DescriptionAttribute;

            return attribute != null ? attribute.Description : value.ToString();
        }

        /// <summary>
        /// Get the descriptions of all enum values in a list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static List<string> GetEnumDescriptions<T>() where T : Enum
        {
            var values = typeof(T).GetEnumValues();
            if (values == null)
            {
                return new List<string>();
            }
            var descriptions = new List<string>();
            foreach (var value in values)
            {
                var field = typeof(T).GetField(value.ToString()!);
                if (field == null)
                {
                    continue; // Skip if the field is null
                }
                var attribute = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute))
                                as DescriptionAttribute;
                if (attribute != null)
                {
                    descriptions.Add(attribute.Description);
                }
            }

            return descriptions;

        }

        /// <summary>
        /// Convert tu kieu in sang gia tri enum
        /// </summary>
        /// <typeparam name="T">Enum type</typeparam>
        /// <param name="intValue">Integer value</param>
        /// <returns>Enum value of type T</returns>
        /// <exception cref="ArgumentException">If intValue is not defined in enum T</exception>
        public static T FromInt<T>(int intValue) where T : Enum
        {
            if (!Enum.IsDefined(typeof(T), intValue))
                throw new ArgumentException($"Value {intValue} is not defined in enum {typeof(T).Name}");

            return (T)Enum.ToObject(typeof(T), intValue);
        }

        /// <summary>
        /// extension method to get the description of an enum value
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string GetDescription(this Enum value)
        {
            FieldInfo fieldInfo = value.GetType().GetField(value.ToString())!;

            if (fieldInfo == null)
            {
                return value.ToString();
            }

            object[] attributes = fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);

            if (attributes.Length > 0)
            {
                return ((DescriptionAttribute)attributes[0]).Description;
            }

            return value.ToString();
        }
    }
}
