using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace extracTOR.Helper.Constants
{
    public static class Constants
    {
        public const string AppSettingsFileName = "appSettings.json";
        // Folder the executable lives in. Used instead of the current working directory so that
        // settings and the hash list are found no matter where the app is launched from
        // (on Linux, desktop launchers usually start apps with $HOME as the working directory).
        // When packaged as a Linux AppImage the executable runs from a read-only mount, so use the folder
        // containing the .AppImage file instead ($APPIMAGE is set by the AppImage runtime).
        public static readonly string AppDirectory =
            Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 } appImage
                ? Path.GetDirectoryName(appImage)!
                : AppContext.BaseDirectory;
        public static readonly string AppSettingsPath = Path.Combine(AppDirectory, AppSettingsFileName);
        public static readonly string HashesFilenamePath = Path.Combine(AppDirectory, "filenames-all.txt");
        public static readonly string HashesBinPath = Path.Combine(AppDirectory, "filenames-all.txt");

        public static class ConfigKeys
        {
            public const string AssetsPath = "Paths:Assets";
            public const string OutputPath = "Paths:Output";
        }

        public static class URLs
        {
            public const string Jedipedia = "https://swtor.jedipedia.net";
            public const string Blender = "https://blender.org";
            public const string Discord = "https://discord.gg/XfHFjSN";
            public const string Github = "https://github.com/SWTOR-Slicers";
            public const string TorCommunity = "https://torcommunity.com";
        }

        public static class Paths
        {
            public static readonly string PublicTestClient = Path.Combine("publictest", "retailclient");
            public static readonly string LiveClient = Path.Combine("swtor", "retailclient");
        }

        public static class Files
        {
            public const string MainGfx = "main_gfx_1.tor";
        }
    }
}
