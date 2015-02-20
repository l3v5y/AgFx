﻿// (c) Copyright Microsoft Corporation.
// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AgFx
{
    /// <summary>
    ///     Helper class to serialize an object to text in a simple way.
    ///     This class will emit text for all public read/write properties whose property type supports IConvertable.
    /// </summary>
    public class ReflectionSerializer
    {
        /// <summary>
        ///     Serialize the given object to a TextWriter.  Only top-level properties who's type supports IConvertable are
        ///     serialized.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="sw"></param>
        public static void Serialize(object obj, TextWriter sw)
        {
            var rwProps = from p in obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                where p.CanRead && p.CanWrite
                select p;

            sw.WriteLine(obj.GetType().FullName);

            foreach(var prop in rwProps)
            {
                var value = prop.GetValue(obj, null);

                if(typeof(IConvertible).IsAssignableFrom(prop.PropertyType))
                {
                    if(value != null)
                    {
                        var strValue = (string)Convert.ChangeType(value, typeof(string), CultureInfo.InvariantCulture);
                        if(strValue != null)
                        {
                            var escapedValue = Uri.EscapeDataString(strValue);
                            sw.WriteLine("{0}:{1}", prop.Name, escapedValue);
                        }
                    }
                    else
                    {
                        sw.WriteLine("{0}:", prop.Name);
                    }
                }
            }
            sw.WriteLine("::");
            sw.Flush();
        }

        /// <summary>
        ///     Deserialize an object's values from a TextReader.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="sr"></param>
        public static void Deserialize(object obj, TextReader sr)
        {
            var typeFullname = sr.ReadLine();

            if(obj.GetType().FullName != typeFullname)
            {
                throw new ArgumentException(String.Format("Trying to deserialize {0}, data is for {1}",
                    obj.GetType().Name, typeFullname));
            }

            var rwProps = from p in obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                where p.CanRead && p.CanWrite
                select p;

            var propHash = new Dictionary<string, PropertyInfo>();

            foreach(var prop in rwProps)
            {
                propHash[prop.Name] = prop;
            }

            for(var ln = sr.ReadLine();
                ln != null;
                ln = sr.ReadLine())
            {
                var separatorPos = ln.IndexOf(':');

                if(separatorPos == 0)
                {
                    break;
                }
                if(separatorPos != -1)
                {
                    var propName = ln.Substring(0, separatorPos);
                    var prop = propHash[propName];

                    if(separatorPos < ln.Length - 1)
                    {
                        var propValue = Uri.UnescapeDataString(ln.Substring(separatorPos + 1));

                        var value = Convert.ChangeType(propValue, prop.PropertyType, CultureInfo.InvariantCulture);

                        try
                        {
                            prop.SetValue(obj, value, null);
                        }
                        catch(Exception ex)
                        {
                            Debug.WriteLine(ex);
                        }
                    }
                }
            }
        }

        internal static void UpdateObject(object source, object destination, DateTime? updateTime)
        {
            if(source == null)
            {
                throw new ArgumentNullException("source");
            }
            if(destination == null)
            {
                throw new ArgumentNullException("destination");
            }

            var destType = destination.GetType();

            if(!destType.IsInstanceOfType(source))
            {
                throw new ArgumentException(String.Format("Can not copy values from type {0} to type {1}",
                    source.GetType().Name, destType.Name));
            }

            // for things that are IUpdatable, let them do the copy.
            //
            var updateable = destination as IUpdatable;
            if(updateable != null)
            {
                updateable.UpdateFrom(source);
                if(updateTime.HasValue)
                {
                    updateable.LastUpdated = updateTime.Value;
                }
            }
            else
            {
                CloneProperties(source, destination);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        public static void CloneProperties(object source, object destination)
        {
            foreach(var prop in destination.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                try
                {
                    if (prop.CanWrite && prop.CanRead)
                    {
                        var v = prop.GetValue(source, null);
                        prop.SetValue(destination, v, null);
                    }
                }
                catch(Exception ex)
                {
                    Debug.WriteLine("Failed to update property {0}.{1} on type {2} ({3})",
                        destination.GetType().Name,
                        prop.Name,
                        source.GetType().Name,
                        ex.Message);
                }
            }
        }
    }
}