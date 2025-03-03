﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform;

namespace UniHacker
{
    internal class PlatformUtils
    {
        public const string FontFamily = "Microsoft YaHei,Simsun,苹方-简,宋体-简";

        static Stream? s_IconStream;
        public static Stream IconStream
        {
            get
            {
                if (s_IconStream == null)
                {
#pragma warning disable CS8602
                    var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
                    s_IconStream = assets.Open(new Uri("avares://UniHacker/Assets/avalonia-logo.ico"));
#pragma warning restore CS8602
                }

                return s_IconStream;
            }
        }

        public static bool IsAdministrator =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
            new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator) :
            Mono.Unix.Native.Syscall.geteuid() == 0;

        public static string GetExtension(bool dot = true)
        {
            var extension = string.Empty;
            switch (GetPlatformType())
            {
                case PlatformType.Windows:
                    extension = "exe";
                    break;
                case PlatformType.MacOS:
                    extension = "app";
                    break;
                case PlatformType.Linux:
                    return string.Empty;
            }

            return (dot ? "." : "") + extension;
        }

        public static PlatformType GetPlatformTypeByArch(ArchitectureType type)
        {
            if ((type & ArchitectureType.Windows) != 0)
                return PlatformType.Windows;
            else if ((type & ArchitectureType.MacOS) != 0)
                return PlatformType.MacOS;
            else if ((type & ArchitectureType.Linux) != 0)
                return PlatformType.Linux;
            else
                return PlatformType.Unknown;
        }

        public static PlatformType GetPlatformType()
        {
            if (IsWindows())
                return PlatformType.Windows;
            else if (IsOSX())
                return PlatformType.MacOS;
            else if (IsLinux())
                return PlatformType.Linux;

            return PlatformType.Unknown;
        }

        public static bool IsWindows()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        public static bool IsOSX()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        }

        public static bool IsLinux()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        }

        public static (string rootPath, string filePath) GetRealFilePath(string filePath)
        {
            var realFilePath = filePath;
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var rootPath = Path.GetDirectoryName(filePath) ?? string.Empty;

            switch (GetPlatformType())
            {
                case PlatformType.MacOS:
                    rootPath = Path.Combine(filePath, "Contents");
                    realFilePath = Path.Combine(rootPath, $"MacOS/{fileName}");
                    break;
                case PlatformType.Linux:
                    if (fileName.Contains("unityhub"))
                        realFilePath = Path.Combine(rootPath, "unityhub-bin");
                    break;
            }

            return (rootPath, realFilePath);
        }

        public static (string fileVersion, int majorVersion, int minorVersion) GetFileVersionInfo(string filePath, ArchitectureType architectureType)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var rootPath = Path.GetDirectoryName(filePath);
            var fileVersion = string.Empty;
            var majorVersion = 0;
            var minorVersion = 0;

            switch (GetPlatformTypeByArch(architectureType))
            {
                case PlatformType.Windows:
                    var info = FileVersionInfo.GetVersionInfo(filePath);
                    fileVersion = !string.IsNullOrEmpty(info.ProductVersion) ? info.ProductVersion.Split('_')[0] : Language.GetString(PatchStatus.Unknown.ToString());
                    majorVersion = info.ProductMajorPart;
                    minorVersion = info.ProductMinorPart;
                    return (fileVersion, majorVersion, minorVersion);
                case PlatformType.MacOS:
                    rootPath = Path.Combine(filePath, "Contents");
                    var plistFile = Path.Combine(rootPath, $"Info.plist");
                    var content = File.ReadAllText(plistFile);
                    var match = Regex.Match(content, "<key>CFBundleVersion</key>.*?<string>(?<version>.*?)</string>", RegexOptions.Singleline);
                    if (match.Success)
                    {
                        fileVersion = match.Groups["version"].Value;
                        var versions = fileVersion.Split('.');
                        _ = int.TryParse(versions[0], out majorVersion);
                        _ = int.TryParse(versions[1], out minorVersion);
                        return (fileVersion, majorVersion, minorVersion);
                    }
                    break;
                case PlatformType.Linux:
                    if (fileName.Contains("unityhub", StringComparison.OrdinalIgnoreCase))
                    {
                        var asarPath = Path.Combine(rootPath!, "resources/app.asar");
                        var asarBakPath = Path.Combine(rootPath!, "resources/app.asar.bak");
                        if (File.Exists(asarPath) || File.Exists(asarBakPath))
                        {
                            var asarContent = string.Empty;

                            if (File.Exists(asarPath))
                                asarContent = File.ReadAllText(asarPath);
                            else
                                asarContent = File.ReadAllText(asarBakPath);

                            var infoMatch = Regex.Match(asarContent, @"""name"":\s""unityhub"",.*?""version"":\s""(?<version>.*?)"",", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                            if (infoMatch.Success)
                            {
                                fileVersion = infoMatch.Groups["version"].Value;
                                var versions = fileVersion.Split('.');
                                _ = int.TryParse(versions[0], out majorVersion);
                                _ = int.TryParse(versions[1], out minorVersion);
                                return (fileVersion, majorVersion, minorVersion);
                            }
                            else
                            {
                                MessageBox.Show(Language.GetString("Hub_patch_error2"));
                            }
                        }
                        else
                        {
                            MessageBox.Show(Language.GetString("Hub_patch_error1"));
                        }
                    }
                    else
                    {
                        fileVersion = TryGetVersionOfUnity(filePath);
                        if (!string.IsNullOrEmpty(fileVersion))
                        {
                            var versions = fileVersion.Split('.');
                            _ = int.TryParse(versions[0], out majorVersion);
                            _ = int.TryParse(versions[1], out minorVersion);
                            return (fileVersion, majorVersion, minorVersion);
                        }
                        else
                        {
                            MessageBox.Show(Language.GetString("Unity_patch_error1"));
                        }
                    }
                    break;
            }

            return (fileVersion, majorVersion, minorVersion);
        }

        public static async Task<bool> MacOSRemoveQuarantine(string appPath)
        {
            try
            {
                var attrStartInfo = new ProcessStartInfo("xattr", $"-rds com.apple.quarantine \"{appPath}\"");
                var attrProcess = Process.Start(attrStartInfo);
                await attrProcess!.WaitForExitAsync();
                return attrProcess.ExitCode == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string TryGetVersionOfUnity(string filePath)
        {
            var maxLength = 40;
            var fileBytes = File.ReadAllBytes(filePath);

            var regex1 = new Regex(@"\d+\.\d\.\d+[fb]\d_\w+", RegexOptions.Compiled | RegexOptions.Singleline);
            var regex2 = new Regex(@"\d+\.\d\.\d+[fb]\d\.git\.\w+", RegexOptions.Compiled | RegexOptions.Singleline);

            var counter = 0;
            var stringBytes = new List<byte>(maxLength);

            void Clear()
            {
                counter = 0;
                stringBytes.Clear();
            }

            for (int i = 0; i < fileBytes.Length; i++)
            {
                if (++counter >= maxLength)
                {
                    Clear();
                    continue;
                }

                stringBytes.Add(fileBytes[i]);
                if (fileBytes[i] == 0 && stringBytes.Count > 1)
                {
                    stringBytes.RemoveAt(stringBytes.Count - 1);
                    var versionName = Encoding.UTF8.GetString(stringBytes.ToArray());

                    if (regex1.IsMatch(versionName) || regex2.IsMatch(versionName))
                        return versionName;

                    Clear();
                }
            }

            return string.Empty;
        }
    }

    internal enum PlatformType
    {
        Unknown = 0,
        Windows,
        MacOS,
        Linux,
    }
}
