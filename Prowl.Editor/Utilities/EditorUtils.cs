﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Prowl.Editor.Utilities
{
    public static class EditorUtils
    {

        public static void GetSafeName(ref DirectoryInfo dir)
        {
            string name = dir.Name;
            if (dir.Exists)
            {
                int counter = 1;
                while (dir.Exists)
                {
                    dir = new DirectoryInfo(Path.Combine(dir.Parent.FullName, $"{name} ({counter})"));
                    counter++;
                }
            }
        }

        public static void GetSafeName(ref FileInfo file)
        {
            string name = Path.GetFileNameWithoutExtension(file.FullName);
            string ext = file.Extension;
            if (File.Exists(file.FullName))
            {
                int counter = 1;
                while (File.Exists(file.FullName))
                {
                    file = new FileInfo(Path.Combine(file.Directory.FullName, $"{name} ({counter}){ext}"));
                    counter++;
                }
            }
        }

        public static string FilterAlpha(string input) => new string(input.Where(char.IsLetter).ToArray());

        public static List<Type> GetDerivedTypes(Type baseType, Assembly assembly)
        {
            // Get all types from the given assembly
            Type[] types = assembly.GetTypes();
            List<Type> derivedTypes = new List<Type>();

            for (int i = 0, count = types.Length; i < count; i++)
            {
                Type type = types[i];
                if (IsSubclassOf(type, baseType))
                {
                    // The current type is derived from the base type,
                    // so add it to the list
                    derivedTypes.Add(type);
                }
            }

            return derivedTypes;
        }

        public static bool IsSubclassOf(Type type, Type baseType)
        {
            if (type == null || baseType == null || type == baseType)
                return false;

            if (baseType.IsGenericType == false)
            {
                if (type.IsGenericType == false)
                    return type.IsSubclassOf(baseType);
            }
            else
            {
                baseType = baseType.GetGenericTypeDefinition();
            }

            type = type.BaseType;
            Type objectType = typeof(object);

            while (type != objectType && type != null)
            {
                Type curentType = type.IsGenericType ?
                    type.GetGenericTypeDefinition() : type;
                if (curentType == baseType)
                    return true;

                type = type.BaseType;
            }
            return false;
        }

        /// <summary>Calculate a unique file path for the given directory, file name and extension with period '.mat'</summary>
        /// <returns>
        /// Path.Combine(dir.FullName, $"{fileName}.{ext}") If that path exists, 
        /// we add an incrementing number to the end of the file name and try again.
        /// </returns>
        public static FileInfo GetUniqueFilePath(DirectoryInfo dir, string fileName, string ext)
        {
            FileInfo file = new(Path.Combine(dir.FullName, $"{fileName}.{ext}"));
            int matAttempt = 0;
            while (File.Exists(file.FullName))
                file = new(Path.Combine(dir.FullName, $"{fileName}-{matAttempt++}.ext"));
            return file;
        }
    }
}
