﻿namespace Unosquare.Swan.Formatters
{
    using Reflection;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    /// <summary>
    /// A very simple JSON library written by Mario
    /// to teach Geo how things are done
    /// </summary>
    public class JsonEx
    {
        #region Private Declarations

        private static readonly Dictionary<int, string> IndentStrings = new Dictionary<int, string>();
        private static readonly PropertyTypeCache TypeCache = new PropertyTypeCache();

        private readonly string Result = null;
        private readonly Type TargetType;
        private readonly StringBuilder Builder;
        private readonly bool Format = true;
        private readonly List<string> ExcludeProperties = new List<string>();
        private readonly List<string> IncludeProperties = new List<string>();

        #endregion

        #region Constructors

        private JsonEx(object obj, int depth, bool format, string[] includeProperties, string[] excludeProperties)
        {
            if (includeProperties != null && includeProperties.Length > 0)
                IncludeProperties.AddRange(includeProperties);

            if (excludeProperties != null && excludeProperties.Length > 0)
                ExcludeProperties.AddRange(excludeProperties);

            Format = format;

            #region Basic Type Handling

            if (obj == null)
            {
                Result = depth == 0 ? "{ }" : "null";

                return;
            }

            var target = obj;
            TargetType = obj.GetType();

            if (obj is string || Constants.BasicTypesInfo.ContainsKey(TargetType))
            {
                var value = Escape(Constants.BasicTypesInfo[TargetType].ToStringInvariant(target));
                decimal val;
                bool boolVal;

                if (decimal.TryParse(value, out val))
                    Result = $"{value}";
                else if (bool.TryParse(value, out boolVal))
                    Result = boolVal.ToString().ToLowerInvariant();
                else
                    Result = $"\"{Escape(value)}\"";

                return;
            }

            #endregion

            Builder = new StringBuilder();

            #region Dictionaries

            if (target is IDictionary)
            {
                var items = target as IDictionary;

                Append("{", depth);

                if (items.Count > 0)
                    AppendLine();

                foreach (DictionaryEntry entry in items)
                {
                    Append($"\"{Escape(entry.Key.ToString())}\": ", depth + 1);

                    var serializedValue = Serialize(entry.Value, depth + 1, Format, includeProperties, excludeProperties);
                    if (IsSetOpening(serializedValue))
                        AppendLine();

                    Append(Serialize(entry.Value, depth + 1, Format, includeProperties, excludeProperties), 0);

                    Append(",", 0);
                    AppendLine();
                }

                RemoveLastComma(format);

                Append("}", items.Count > 0 ? depth : 0);
                Result = Builder.ToString();
                return;
            }

            #endregion

            #region Enumerables

            if (target is IEnumerable)
            {
                if (target is byte[])
                {
                    Result = Serialize((target as byte[]).ToBase64(), depth, Format, includeProperties, excludeProperties);
                    return;
                }

                Append("[", depth);
                var items = (target as IEnumerable).Cast<object>().ToArray();

                if (items.Length > 0)
                    AppendLine();

                foreach (var entry in items)
                {
                    var serializedValue = Serialize(entry, depth + 1, Format, includeProperties, excludeProperties);

                    if (IsSetOpening(serializedValue))
                    {
                        Append(serializedValue, 0);
                    }
                    else
                    {
                        Append(serializedValue, depth + 1);
                    }

                    Append(",", 0);
                    AppendLine();
                }

                RemoveLastComma(format);

                Append("]", items.Length > 0 ? depth : 0);
                Result = Builder.ToString();
                return;
            }

            #endregion

            #region Other Object Types

            // Handle all other object types
            var objectDictionary = new Dictionary<string, object>();
            var properties = TypeCache.Retrieve(TargetType, () =>
            {
                return
                TargetType.GetTypeInfo().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead).ToArray();
            });

            // Only include selected properties
            if (IncludeProperties.Count > 0)
                properties = properties.Where(p => IncludeProperties.Contains(p.Name)).ToArray();

            foreach (var property in properties)
            {
                // Remove excluded properties
                if (ExcludeProperties.Contains(property.Name))
                    continue;

                try { objectDictionary[property.Name] = property.GetValue(target); }
                catch
                {
                    // ignored
                }
            }

            // Multi-property 
            if (objectDictionary.Count > 0)
            {
                Result = Serialize(objectDictionary, depth, Format, includeProperties, excludeProperties);
            }
            else
            {
                Result = Serialize(target.ToString(), 0, Format, includeProperties, excludeProperties);
            }

            #endregion
        }

        private static string Serialize(object obj, int depth, bool format, string[] includeProperties, string[] excludeProperties)
        {
            var serializer = new JsonEx(obj, depth, format, includeProperties, excludeProperties);
            return serializer.Result;
        }

        #endregion

        #region Helper Methods

        private string GetIndent(int depth)
        {
            if (Format == false) return string.Empty;

            if (depth > 0 && IndentStrings.ContainsKey(depth) == false)
                IndentStrings[depth] = new string(' ', depth * 4);

            var indent = depth > 0 ? IndentStrings[depth] : string.Empty;
            return indent;
        }

        private static bool IsSetOpening(string serialized)
        {
            var startTextIndex = serialized.TakeWhile(c => c == ' ').Count();

            var indent = startTextIndex > 0 ? new string(' ', startTextIndex) : string.Empty;

            var openingObject = indent + "{";
            var openingArray = indent + "[";

            return serialized.StartsWith(openingObject) || serialized.StartsWith(openingArray);
        }

        private void  RemoveLastComma(bool format)
        {
            var search = "," + (format ? Environment.NewLine : "");

            if (Builder.Length < search.Length)
                return;

            for (var i = 0; i < search.Length; i++)
                if (Builder[Builder.Length - search.Length + i] != search[i])
                    return;

            Builder.Remove(Builder.Length - search.Length, 1);
        }

        private void Append(string text, int depth)
        {
            Builder.Append($"{GetIndent(depth)}{text}");
        }

        private void AppendLine()
        {
            if (Format == false) return;
            Builder.Append(Environment.NewLine);
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "";
            }

            var builder = new StringBuilder(s.Length * 2);

            foreach (var currentChar in s)
            {
                switch (currentChar)
                {
                    case '\\':
                    case '"':
                        builder.Append('\\');
                        builder.Append(currentChar);
                        break;
                    case '/':
                        builder.Append('\\');
                        builder.Append(currentChar);
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    default:
                        if (currentChar < ' ')
                        {
                            var escapeSequence = ((int)currentChar).ToString("X");
                            builder.Append("\\u" + escapeSequence.PadLeft(4, '0'));
                        }
                        else
                        {
                            builder.Append(currentChar);
                        }
                        break;
                }
            }

            return builder.ToString();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Serializes the specified object. All properties are serialized
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="format">if set to <c>true</c> it formats and indents the output.</param>
        /// <returns></returns>
        public static string Serialize(object obj, bool format = false)
        {
            return Serialize(obj, 0, format, null, null);
        }

        /// <summary>
        /// Serializes the specified object only including the specified property names.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="format">if set to <c>true</c> it formats and indents the output.</param>
        /// <param name="includeNames">The include names.</param>
        /// <returns></returns>
        public static string SerializeOnly(object obj, bool format, params string[] includeNames)
        {
            return Serialize(obj, 0, format, includeNames, null);
        }

        /// <summary>
        /// Serializes the specified object excluding the specified property names.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="format">if set to <c>true</c> it formats and indents the output.</param>
        /// <param name="excludeNames">The exclude names.</param>
        /// <returns></returns>
        public static string SerializeExcluding(object obj, bool format, params string[] excludeNames)
        {
            return Serialize(obj, 0, format, null, excludeNames);
        }

        #endregion

    }
}