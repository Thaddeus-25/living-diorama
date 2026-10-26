using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace LivingDiorama.Persistence
{
    public readonly struct SaveFileInfo
    {
        public readonly string Name;
        public readonly DateTime LastWriteTimeUtc;

        public SaveFileInfo(string name, DateTime lastWriteTimeUtc)
        {
            Name = name;
            LastWriteTimeUtc = lastWriteTimeUtc;
        }
    }

    /// <summary>Scene files on disk: one JSON file per scene, named after the scene.</summary>
    public sealed class SaveFileStore
    {
        public const string Extension = ".json";
        private const int MaxNameLength = 60;

        private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>…/AppData/LocalLow/&lt;company&gt;/&lt;product&gt;/Scenes on Windows.</summary>
        public static string DefaultDirectory => Path.Combine(Application.persistentDataPath, "Scenes");

        public string DirectoryPath { get; }

        public SaveFileStore(string directoryPath) => DirectoryPath = directoryPath;

        /// <summary>
        /// A safe file name for a scene name: characters Windows doesn't allow become '_', and
        /// reserved names get a prefix. Applying it twice gives the same result.
        /// </summary>
        public static string ToFileName(string sceneName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder();
            foreach (char c in (sceneName ?? string.Empty).Trim())
                builder.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);

            string name = builder.ToString().Trim().TrimEnd('.');
            if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength).Trim();
            if (name.Length == 0) name = "Untitled";
            if (ReservedWindowsNames.Contains(name)) name = "_" + name;
            return name;
        }

        public string PathFor(string sceneName) => Path.Combine(DirectoryPath, ToFileName(sceneName) + Extension);

        public bool Exists(string sceneName) => File.Exists(PathFor(sceneName));

        /// <summary>Writes via a temporary file so a crash mid-save never leaves a half-written scene.</summary>
        public void Write(string sceneName, string json)
        {
            Directory.CreateDirectory(DirectoryPath);
            string path = PathFor(sceneName);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, json, Encoding.UTF8);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        public string Read(string sceneName) => File.ReadAllText(PathFor(sceneName), Encoding.UTF8);

        /// <summary>Saved scenes, newest first.</summary>
        public List<SaveFileInfo> List()
        {
            if (!Directory.Exists(DirectoryPath)) return new List<SaveFileInfo>();
            return Directory.GetFiles(DirectoryPath, "*" + Extension)
                .Select(path => new SaveFileInfo(Path.GetFileNameWithoutExtension(path), File.GetLastWriteTimeUtc(path)))
                .OrderByDescending(info => info.LastWriteTimeUtc)
                .ToList();
        }
    }
}
