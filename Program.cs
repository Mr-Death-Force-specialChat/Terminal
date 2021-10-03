using System;
using System.Collections.Generic;
using System.IO;

namespace Terminal
{
    class Program
    {
        public static string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EXO-Terminal");
        public static INIFile Config = new INIFile(Path.Combine(path, "Config.ini"));
        public static Run run = new Run();
        public static List<double> sessions = new List<double>();
        static void Main(string[] args)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            if (!File.Exists(Config.path))
            {
                setConfig();
            }
            if (Config.IniReadValue("keConfig", "SPath") == "keTrue")
            {
                path = Config.IniReadValue("keConfig", "SPathV");
                Config = new INIFile(Path.Combine(path, "Config.ini"));
            }
            if (Config.IniReadValue("keConfig", "Debug") == "keTrue")
                run.StartTerminal(true);
            else
                run.StartTerminal(false);
        }
        public static string GetPath()
        {
            if (Config.IniReadValue("keConfig", "SPath") == "keTrue")
                return new INIFile(Path.Combine(path, "Config.ini")).IniReadValue("keConfig", "SPathV");
            else
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EXO-Terminal");
        }
        public static void setConfig()
        {
            try
            {
                Console.WriteLine("Setting up the configuration file <" + Config.path + ">");
                File.Delete(Config.path);
                Config.IniWriteValue("Commands", "echo", "echo");
                Config.IniWriteValue("Commands", "prmpt", "prompt");
                Config.IniWriteValue("Commands", "prompt", "prompt");
                Config.IniWriteValue("Commands", "clear", "cls");
                Config.IniWriteValue("Commands", "cls", "cls");
                Config.IniWriteValue("Commands", "title", "title");
                Config.IniWriteValue("Commands", "exit", "ext");
                Config.IniWriteValue("Commands", "read", "read");
                Config.IniWriteValue("Commands", "readln", "readln");
                Config.IniWriteValue("Commands", "terminal", "terminal");
                Config.IniWriteValue("Commands", "readreg", "readreg");
                Config.IniWriteValue("Commands", "writereg", "writereg");
                Config.IniWriteValue("Commands", "run", "run");
                Config.IniWriteValue("Commands", "start", "newrun");
                Config.IniWriteValue("Commands", "cd", "cd");
                Config.IniWriteValue("Commands", "ls", "ls");
                Config.IniWriteValue("Commands", "dir", "ls");
                Config.IniWriteValue("Commands", "create", "create");
                Config.IniWriteValue("Commands", "delete", "delete");
                Config.IniWriteValue("Commands", "mkdir", "mkdir");
                Config.IniWriteValue("Commands", "md", "mkdir");
                Config.IniWriteValue("Commands", "rmdir", "rmdir");
                Config.IniWriteValue("Commands", "rd", "rmdir");
                Config.IniWriteValue("Commands", "cp", "copy");
                Config.IniWriteValue("Commands", "copy", "copy");
                Config.IniWriteValue("Commands", "help", "hlp");
                Config.IniWriteValue("Commands", "hlp", "hlp");
                Config.IniWriteValue("Commands", "Error", "ERR");
                Config.IniWriteValue("Commands", "Warn", "WRN");
                Config.IniWriteValue("Commands", "Success", "SUC");
                Config.IniWriteValue("Commands", "Lock", "LCK");
                Config.IniWriteValue("Commands", "Unlock", "ULCK");
                Config.IniWriteValue("Commands", "ToggleDebug", "SDBG");
                Config.IniWriteValue("Commands", "VirtualTerminal", "VTERM");
                Config.IniWriteValue("Commands", "FSCon", "FSC");
                Config.IniWriteValue("keConfig", "Debug", "keFalse");
                Config.IniWriteValue("keConfig", "SPath", "keFalse");
                Config.IniWriteValue("keConfig", "SPathV", "File shouldn't exit with SPath=keFalse");
                run.Success("Rewritten the config file");
            }
            catch (Exception e)
            {
                Console.WriteLine("Failing config system: \n\n{0}", e.Message);
                run.failingCmdSystem = true;
            }
        }
        public static bool CheckLock()
        {
            return Config.IniReadValue("Data", "Password") != "";
        }
    }
}