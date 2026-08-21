/*
 * EvidenciasSQA - a free and open source screenshot tool
 * Copyright (C) 2004-2026 Thomas Braun, Jens Klingen, Robin Krom
 * 
 * For more information see: https://evidenciassqa.com/
 * The EvidenciasSQA project is hosted on GitHub https://github.com/evidenciassqa/evidenciassqa
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 1 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace EvidenciasSQA.Base.Core
{
    /// <summary>
    /// Extension methods which work for objects
    /// </summary>
    public static class ObjectExtensions
    {
        /// <summary>
        /// Deep-copy del objeto por reflexion (sin BinaryFormatter, que no existe
        /// en .NET Core). Grafo soportado: listas, arrays, ICloneable, campos.
        /// Los valores no clonables (p.ej. Brush) se comparten por referencia.
        /// </summary>
        public static T Clone<T>(this T source)
        {
            return (T)DeepClone(source);
        }

        private static object DeepClone(object source)
        {
            if (source == null)
            {
                return null;
            }

            Type type = source.GetType();
            if (type.IsValueType || type == typeof(string))
            {
                return source;
            }

            if (type.IsArray)
            {
                Array sourceArray = (Array) source;
                Array cloneArray = Array.CreateInstance(type.GetElementType(), sourceArray.Length);
                for (int i = 0; i < sourceArray.Length; i++)
                {
                    cloneArray.SetValue(DeepClone(sourceArray.GetValue(i)), i);
                }
                return cloneArray;
            }

            if (source is System.Collections.IList sourceList)
            {
                var cloneList = (System.Collections.IList) Activator.CreateInstance(type);
                foreach (object item in sourceList)
                {
                    cloneList.Add(DeepClone(item));
                }
                return cloneList;
            }

            if (source is ICloneable cloneable)
            {
                return cloneable.Clone();
            }

            try
            {
                object result = Activator.CreateInstance(type);
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (typeof(Delegate).IsAssignableFrom(field.FieldType))
                    {
                        continue;
                    }
                    field.SetValue(result, DeepClone(field.GetValue(source)));
                }
                return result;
            }
            catch (Exception)
            {
                // Tipo sin constructor o sin campos clonables: compartir la referencia.
                return source;
            }
        }

        /// <summary>
        /// Clone the content from source to destination
        /// </summary>
        /// <typeparam name="T">Type to clone</typeparam>
        /// <param name="source">Instance to copy from</param>
        /// <param name="destination">Instance to copy to</param>
        public static void CloneTo<T>(this T source, T destination)
        {
            var type = typeof(T);
            var myObjectFields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            foreach (var fieldInfo in myObjectFields)
            {
                fieldInfo.SetValue(destination, fieldInfo.GetValue(source));
            }

            var myObjectProperties = type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            foreach (var propertyInfo in myObjectProperties)
            {
                if (propertyInfo.CanWrite)
                {
                    propertyInfo.SetValue(destination, propertyInfo.GetValue(source, null), null);
                }
            }
        }

        /// <summary>
        /// Compare two lists
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="l1">IList</param>
        /// <param name="l2">IList</param>
        /// <returns>true if they are the same</returns>
        public static bool CompareLists<T>(IList<T> l1, IList<T> l2)
        {
            if (l1.Count != l2.Count)
            {
                return false;
            }

            int matched = 0;
            foreach (T item in l1)
            {
                if (!l2.Contains(item))
                {
                    return false;
                }

                matched++;
            }

            return matched == l1.Count;
        }
    }
}