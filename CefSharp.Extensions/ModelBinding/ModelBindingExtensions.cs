// Copyright © 2020 The CefSharp Authors. All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace CefSharp.Extensions.ModelBinding
{
    internal static class ModelBindingExtensions
    {
        /// <summary>
        /// The custom parser below supports converting strings into a <see cref="Enum"/> that has the <see cref="FlagsAttribute"/> defined.
        /// These are commonly used separators to assist that process.
        /// </summary>
        private static readonly char[] EnumSeparators = { '|', ',', ';', '+', ' ' };

        /// <summary>
        /// ReadOnly Dictionary that maps <see cref="TypeCode"/> members to their corresponding <see cref="Type"/> instance.
        /// </summary>
        private static readonly IReadOnlyDictionary<TypeCode, Type> TypeCodeToTypeMap = new Dictionary<TypeCode, Type>
        {
            { TypeCode.Boolean, typeof(bool) },
            { TypeCode.Byte, typeof(byte) },
            { TypeCode.Char, typeof(char) },
            { TypeCode.DateTime, typeof(DateTime) },
            { TypeCode.DBNull, typeof(DBNull) },
            { TypeCode.Decimal, typeof(decimal) },
            { TypeCode.Double, typeof(double) },
            { TypeCode.Empty, null },
            { TypeCode.Int16, typeof(short) },
            { TypeCode.Int32, typeof(int) },
            { TypeCode.Int64, typeof(long) },
            { TypeCode.Object, typeof(object) },
            { TypeCode.SByte, typeof(sbyte) },
            { TypeCode.Single, typeof(float) },
            { TypeCode.String, typeof(string) },
            { TypeCode.UInt16, typeof(ushort) },
            { TypeCode.UInt32, typeof(uint) },
            { TypeCode.UInt64, typeof(ulong) }
        };

        /// <summary>
        /// Returns all the public properties of an underlying type that can be read and written to.
        /// </summary>
        /// <param name="source">The type where properties will be pulled from.</param>
        /// <returns>A collection of all the valid properties found on the <paramref name="source"/> type.</returns>
        public static IEnumerable<PropertyInfo> GetValidProperties(this Type source)
        {
            return source.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.CanRead && p.CanWrite).Where(p => p.GetIndexParameters().Length == 0);
        }

        /// <summary>
        /// Checks if a type is an array or not
        /// </summary>
        /// <param name="source">The type to check.</param>
        /// <returns><see langword="true" /> if the type is an array, otherwise <see langword="false" />.</returns>
        public static bool IsArray(this Type source)
        {
            return source.GetTypeInfo().BaseType == typeof(Array);
        }

        /// <summary>
        /// Checks if a type is an collection or not
        /// </summary>
        /// <param name="source">The type to check.</param>
        /// <returns><see langword="true" /> if the type is a collection, otherwise <see langword="false" />.</returns>
        public static bool IsCollection(this Type source)
        {
            var collectionType = typeof(ICollection<>);

            return source.GetTypeInfo().IsGenericType && source
                .GetInterfaces()
                .Any(i => i.GetTypeInfo().IsGenericType && i.GetGenericTypeDefinition() == collectionType);
        }

        /// <summary>
        /// Checks if a type is enumerable or not
        /// </summary>
        /// <param name="source">The type to check.</param>
        /// <returns><see langword="true" /> if the type is an enumerable, otherwise <see langword="false" />.</returns>
        public static bool IsEnumerable(this Type source)
        {
            var enumerableType = typeof(IEnumerable<>);

            return source.GetTypeInfo().IsGenericType && source.GetGenericTypeDefinition() == enumerableType;
        }

        /// <summary>
        /// Attempts to convert a string or number representation of an <see cref="Enum"/> field to an actual instance. 
        /// <br>Please note, this method will NOT fallback to the default value of the destination enum.</br>
        /// <br>If the source object cannot be marshaled, then this method will throw exceptions to prevent undefined behavior.</br>
        /// </summary>
        /// <param name="destinationType">The type of the <see cref="Enum"/> to create an instance of.</param>
        /// <param name="javaScriptObject">The object that will be marshaled into the destination type.</param>
        /// <returns>
        /// The a member on the destination <see cref="Enum"/> the corresponds to the source object.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the destination type or source object are null.</exception>
        /// <exception cref="ModelBindingException">Thrown when the source object cannot be bound to the destination enum.</exception>
        /// <remarks>
        /// This method does not rely on Enum.Parse and therefore will never raise any first or second chance exception.
        /// </remarks>
        public static object CreateEnumMember(this Type destinationType, object javaScriptObject)
        {
            if (destinationType == null)
            {
                throw new ArgumentNullException(nameof(destinationType));
            }

            if (javaScriptObject == null)
            {
                throw new ArgumentNullException(nameof(javaScriptObject));
            }

            if (!destinationType.IsEnum)
            {
                throw new ModelBindingException(javaScriptObject.GetType(), destinationType, BindingFailureCode.NoEnumAtDestinationType);
            }

            if (!(javaScriptObject is string) && !javaScriptObject.IsEnumIntegral())
            {
                throw new ModelBindingException(javaScriptObject.GetType(), destinationType, BindingFailureCode.SourceNotAssignable);
            }

            // if the source object is a number, then these is the only steps we need to run
            if (javaScriptObject.IsEnumIntegral())
            {
                // checks if the number exist within the destination enumeration 
                if (Enum.IsDefined(destinationType, javaScriptObject))
                {
                    // and if it does convert and return it.
                    return Enum.ToObject(destinationType, javaScriptObject);
                }
                // we're throwing because the number is not defined in the enumeration and defaulting to the first member
                // can cause some serious unintended side effects if a method relies on the enum member to be accurate.
                throw new ModelBindingException(javaScriptObject.GetType(), destinationType, BindingFailureCode.NumberNotDefinedInEnum);
            }

            var javaScriptString = ((string)javaScriptObject).Trim();
            // empty strings are not supported 
            if (javaScriptString.Length == 0)
            {
                throw new ModelBindingException(javaScriptObject.GetType(), destinationType, BindingFailureCode.StringNotDefinedInEnum);
            }
            var destinationMembers = Enum.GetNames(destinationType);
            // make sure the enum is actually defined and has members
            if (destinationMembers.Length == 0)
            {
                throw new ModelBindingException(javaScriptObject.GetType(), destinationType, BindingFailureCode.DestinationEnumEmpty);
            }
            // the underlying integral type is important as enums can be things other than int
            var underlyingType = Enum.GetUnderlyingType(destinationType);
            // these are all the values of all the members, they have matching indexs
            var destinationValues = Enum.GetValues(destinationType);

            // it is expected that someone might improperly implement their enum with flags and not explicitly define it. 
            // so to prevent crashing or invalid values from being returned, we don't try to parse flags unless the type has the flags attribute. 
            if (!destinationType.IsDefined(typeof(FlagsAttribute), true) && javaScriptString.IndexOfAny(EnumSeparators) < 0)
            {
                return StringToEnumMember(destinationType, underlyingType, destinationMembers, destinationValues, javaScriptString);
            }

            // split the string by the default separators so we can parse the tokens 
            var tokens = javaScriptString.Split(EnumSeparators, StringSplitOptions.RemoveEmptyEntries);
            // the source string is malformed or doesn't contain entries, so we cannot continue.
            if (tokens.Length == 0)
            {
                throw new ModelBindingException(javaScriptString.GetType(), destinationType, BindingFailureCode.SourceObjectNullOrEmpty);
            }

            ulong ul = 0;
            foreach (var tok in tokens)
            {
                var token = tok.Trim();
                // we should never experience an empty token given the code above, but if we do skip.
                if (token.Length == 0)
                {
                    continue;
                }

                // either we're going to get back the enum because the string had separator but one token
                // or the integral representation of a token, which leads to us forming the actual enum member after all tokens are parsed.
                var tokenValue = StringToEnumMember(destinationType, underlyingType, destinationMembers, destinationValues, token);

                ulong tokenUl;
                // for flags we need to do bitwise operations on the found values.
                // to save effort we just use a ulong for storing the token since Enum.ToObject will internally cast.
                var typeCode = Convert.GetTypeCode(tokenValue);
                switch (Convert.GetTypeCode(tokenValue))
                {
                    case TypeCode.SByte:
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                    {
                        tokenUl = (ulong)Convert.ToInt64(tokenValue, CultureInfo.InvariantCulture);
                        break;
                    }
                    case TypeCode.Byte:
                    case TypeCode.UInt16:
                    case TypeCode.UInt32:
                    case TypeCode.UInt64:
                    {
                        tokenUl = Convert.ToUInt64(tokenValue, CultureInfo.InvariantCulture);
                        break;
                    }
                    default:
                    {
                        throw new ModelBindingException(typeCode.ToType(), destinationType, BindingFailureCode.EnumIntegralNotFound);
                    }
                }
                // append the token to the overall value
                ul |= tokenUl;
            }
            // convert the flag to the value of our destination enum
            return Enum.ToObject(destinationType, ul);
        }

        /// <summary>
        /// Attempts to convert a string into a valid value of one of the destination <see cref="Enum"/> fields.
        /// </summary>
        /// <param name="destinationType">The type that the source string will be bound to.</param>
        /// <param name="underlyingType">The underlying type of the enum as .NET allow explicit specification of other integral numeric besides int.</param>
        /// <param name="members">The human-readable members of the destination enum.</param>
        /// <param name="values">The integral values of each enum field.</param>
        /// <param name="sourceString">A string that contains values within the destination enum.</param>
        /// <returns>The value of the enum field that corresponds to the provided string.</returns>
        /// <exception cref="ModelBindingException">Thrown when the sourceString cannot be assigned to any of the destination enum fields.</exception>
        private static object StringToEnumMember(Type destinationType, Type underlyingType, IReadOnlyList<string> members, Array values, string sourceString)
        {
            // loop over the enums members
            for (var i = 0; i < members.Count; i++)
            {
                // if the source string does not match the member name skip.
                // casing is ignored for compatibility with Javascript and Typescript naming conventions
                if (string.Compare(members[i], sourceString, StringComparison.InvariantCulture) != 0)
                {
                    continue;
                }
                // return the value for the matching enum member. 
                return values.GetValue(i);
            }
            //  now try parsing the string for numbers. 
            //  500 --> 500
            //  +13230 --> 13230
            //  -5 --> -5
            if (char.IsDigit(sourceString[0]) || sourceString[0] == '-' || sourceString[0] == '+')
            {
                // can never be null or the underlying types default value because StringToEnumIntegral throws on failure
                return StringToEnumIntegral(underlyingType, sourceString);
            }
            // if the source string is not present in the enum, throw.
            throw new ModelBindingException(sourceString.GetType(), destinationType, BindingFailureCode.StringNotDefinedInEnum);
        }


        /// <summary>
        /// Attempts to convert a string to an Enum integral type.
        /// </summary>
        /// <param name="destinationType">the type the string should be converted into.</param>
        /// <param name="sourceString">a string that is parseable to a number.</param>
        /// <returns>the converted string as the integral destination type.</returns>
        /// <exception cref="ModelBindingException">Thrown when the destination type is not a number, or the source string cannot be parsed.</exception>
        private static object StringToEnumIntegral(Type destinationType, string sourceString)
        {
            // activate an instance of the the destination Type to verify it is a number.
            var instance = Activator.CreateInstance(destinationType);
            if (!instance.IsEnumIntegral())
            {
                throw new ModelBindingException(typeof(string), destinationType, BindingFailureCode.SourceNotAssignable);
            }
            if (instance is int)
            {
                if (int.TryParse(sourceString, out var s))
                {
                    return s;
                }
            }

            if (instance is uint)
            {
                if (uint.TryParse(sourceString, out var s))
                {
                    return s;
                }
            }

            if (instance is ulong)
            {
                if (ulong.TryParse(sourceString, out var s))
                {
                    return s;
                }
            }

            if (instance is long)
            {
                if (long.TryParse(sourceString, out var s))
                {
                    return s;
                }
            }

            if (instance is short)
            {
                if (short.TryParse(sourceString, out var s))
                {
                    return s;
                }
            }

            if (instance is ushort)
            {
                if (ushort.TryParse(sourceString, out var s))
                {
                    return s;
                }
            }

            if (instance is byte)
            {
                if (byte.TryParse(sourceString, out var s))
                {
                    return s;
                }
            }
            if (instance is sbyte)
            {
                if (sbyte.TryParse(sourceString, out var s))
                {
                    return s;
                }
            }
            throw new ModelBindingException(typeof(string), destinationType, BindingFailureCode.SourceNotAssignable);
        }

        /// <summary>
        /// Convert a TypeCode ordinal into it's corresponding <see cref="Type"/> instance.
        /// </summary>
        /// <param name="code">the type code to lookup</param>
        /// <returns>an instance of <see cref="Type"/> for the given <see cref="TypeCode"/></returns>
        /// <exception cref="IndexOutOfRangeException">Thrown when the <see cref="TypeCode"/> provided cannot be found in map.</exception>
        public static Type ToType(this TypeCode code)
        {
            if (TypeCodeToTypeMap.TryGetValue(code, out var type))
            {
                return type;
            }
            throw new IndexOutOfRangeException($"Cannot find TypeCode {code} in TypeCodeToTypeMap");
        }

        /// <summary>
        /// Checks if the underlying type of an object is valid for a <see cref="Enum"/>
        /// </summary>
        /// <param name="sourceObject">the object of an unknown type</param>
        /// <returns><see langword="true" /> if the type is a number which is assignable to an <see cref="Enum"/>, otherwise <see langword="false" />.</returns>
        public static bool IsEnumIntegral(this object sourceObject)
        {
            return sourceObject is sbyte
                || sourceObject is byte
                || sourceObject is short
                || sourceObject is ushort
                || sourceObject is int
                || sourceObject is uint
                || sourceObject is long
                || sourceObject is ulong;
        }

        /// <summary>
        /// if the underlying type of an object is a common numeric type
        /// </summary>
        /// <param name="sourceObject">the object of an unknown type</param>
        /// <returns><see langword="true" /> if the object is a number, otherwise <see langword="false" />.</returns>
        public static bool IsNumber(this object sourceObject)
        {
            return sourceObject.IsEnumIntegral()
                || sourceObject is float
                || sourceObject is double
                || sourceObject is decimal;
        }

        /// <summary>
        /// Supports ValueTypes even though CefSharp targets an older version
        /// </summary>
        /// <param name="source">The type to check.</param>
        /// <returns><see langword="true" /> if the type is a ValueTuple of any size, otherwise <see langword="false" />.</returns>
        public static bool IsValueTupleType(this Type source)
        {
            var definition = source?.GetGenericTypeDefinition();
            if (definition == null)
            {
                return false;
            }
            var definitionName = definition.FullName;
            if (string.IsNullOrWhiteSpace(definitionName))
            {
                return false;
            }
            return string.Equals(definitionName, "System.ValueTuple`1", StringComparison.InvariantCulture) ||
                string.Equals(definitionName, "System.ValueTuple`2", StringComparison.InvariantCulture) ||
                string.Equals(definitionName, "System.ValueTuple`3", StringComparison.InvariantCulture) ||
                string.Equals(definitionName, "System.ValueTuple`4", StringComparison.InvariantCulture) ||
                string.Equals(definitionName, "System.ValueTuple`5", StringComparison.InvariantCulture) ||
                string.Equals(definitionName, "System.ValueTuple`6", StringComparison.InvariantCulture) ||
                string.Equals(definitionName, "System.ValueTuple`7", StringComparison.InvariantCulture);
        }

        /// <summary>
        /// Checks if a type is a Struct that has been defined outside the standard library.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static bool IsCustomStruct(this Type source)
        {
            // not entirely foolproof.
            return source.Namespace != null && (source.IsValueType && !source.IsPrimitive && !source.IsEnum && !source.Namespace.StartsWith("System."));
        }

        /// <summary>
        /// Converts the name of a property/method into camelCase
        /// </summary>
        /// <param name="memberInfo">the property/method which will have it's name converted</param>
        /// <returns>the camel case version of the property name.</returns>
        public static string ConvertNameToCamelCase(this MemberInfo memberInfo)
        {
            return ConvertNameToCamelCase(memberInfo.Name);
        }

        /// <summary>
        /// Converts a string (usually .NET value of some sort) to a camelCase representation.
        /// </summary>
        /// <param name="sourceString"></param>
        /// <returns>the string converted to camelCase or preserved based on it's original structure.</returns>
        internal static string ConvertNameToCamelCase(this string sourceString)
        {
            // don't allow whitespace in property names.
            // because we use this in the actual binding process, we should be throwing and not allowing invalid entries.
            if (string.IsNullOrWhiteSpace(sourceString))
            {
                throw new ModelBindingException(typeof(string), typeof(string), BindingFailureCode.SourceObjectNullOrEmpty);
            }

            // camelCase says that if the string is only one character that it is preserved.
            if (sourceString.Length == 1)
            {
                return sourceString;
            }

            // camelCase says that if the entire string is uppercase to preserve it.
            //TODO: We need to cache these values to avoid the cost of validating this
            if (sourceString.All(char.IsUpper))
            {
                return sourceString;
            }

            var firstHalf = sourceString.Substring(0, 1);
            var remainingHalf = sourceString.Substring(1);

            return firstHalf.ToLowerInvariant() + remainingHalf;
        }
    }
}
