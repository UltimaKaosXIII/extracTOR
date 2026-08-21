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
        public static readonly string AppSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), AppSettingsFileName);
        public static readonly string HashesFilenamePath = Path.Combine(Directory.GetCurrentDirectory(), "filenames-all.txt");
        public static readonly string HashesBinPath = Path.Combine(Directory.GetCurrentDirectory(), "filenames-all.txt");

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
            public const string PublicTestClient = "publictest\\retailclient";
            public const string LiveClient = "swtor\\retailclient";
        }

        public static class Files
        {
            public const string MainGfx = "main_gfx_1.tor";
        }
    }
}
