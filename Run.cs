using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;

namespace Terminal
{
    public class Run
    {
        /*
         * TODO
         * -  2.0.32.7 QOL
         *      custom password attempts
         * -  3.0.0.0 QualUI
         *      gui mode
         */

        /*
         * CHANGE LOG
         *  - 2.0.32.6
         *      minor bug fix - 2.0.32.6
         *      allowed multi word files to be made using FSCon - 2.0.32.6
         *      added FSCon to broken cmd system - 2.0.32.6
         *      added DISABLE LOG function
         *      alias feature
         *  - 2.0.33.0
         *      fixed fscon
         *          fix details: in "fscon", out "WARNING: 'fscon' is not a valid internal or external"
         *      fixed max password attempts being int.MaxValue
         *      added delay before exiting the terminal when entered an invalid password with 0 attempts left
         *      
         *      added color
         */
        // dllimports
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        // vars
        readonly string defaultPrompt = "$V$S$P$G";
        string prompt;
        string currentPath;
        string currentVersion;
        string command;
        string startUpMessage;
        string[] registers;
        string trwaFile;
        int inputReg = 0;
        int keyInputReg = 1;
        double sessionID = 0;
        bool ext = false;
        bool dbg = false;
        bool lck = false;
        bool dlc = false;
        public static string title = "EXO-Terminal";
        public static string path = Program.GetPath();
        public bool failingCmdSystem = false;
        INIFile Config = new INIFile(Path.Combine(path, "Config.ini"));
        string tmpPath = Path.Combine(path, "tmp");
        string LV = "2.0.33.0";
        public void StartTerminal(bool debug)
        {
            WriteToLog("--------------------------------------------------new session started--------------------------------------------------");
            sessionID = GenSessionID();
            dbg = debug;
            registers = new string[20];
            prompt = defaultPrompt;
            currentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
#if RELEASE
            currentVersion = "[TKRNL " + LV + " Release]";
#else
            currentVersion = "[TKRNL " + LV + " Debug]";
#endif
            startUpMessage = "EXO-Terminal " + currentVersion + " [CLI] [" + sessionID.ToString() + "]";
            trwaFile = "test-of-read-write-access";
            Console.Title = title;
            Console.Write(startUpMessage);
            lck = Program.CheckLock();
            if (lck)
            {
                Console.Write(" [");
                ConsoleColor clr = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Write("LOCKED");
                Console.ForegroundColor = clr;
                Console.WriteLine("]");
                int attemptsLeft = 10;
                attemptsLeft++;
                while (true)
                {
                    try
                    {
                        attemptsLeft--;
                        Console.Write("Password :>");
                        string input = Console.ReadLine();
                        Console.CursorTop--;
                        if (ComputeSha512Hash(input) == Config.IniReadValue("Data", "Password"))
                        {
                            Console.Write("\r                                                                                               \r");
                            break;
                        }
                        else
                            Console.WriteLine("Wrong password {0} attempts left", attemptsLeft);
                        if (attemptsLeft <= 0)
                        {
                            ext = true;
                            Thread.Sleep(10000);
                            break;
                        }
                    }
                    catch (Exception)
                    {
                        if (attemptsLeft > 1)
                        {
                            attemptsLeft = 1;
                        }
                        Error("Something went wrong");
                        Console.WriteLine("Wrong password {0} attempts left", attemptsLeft);
                        if (attemptsLeft <= 0)
                        {
                            ext = true;
                            break;
                        }
                    }
                }
            }
            else
            {
                Console.Write(" [");
                ConsoleColor clr = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("UNLOCKED");
                Console.ForegroundColor = clr;
                Console.WriteLine("]");
            }
            while (ext)
            {
                Console.Write(parsePrompt());
                command = readCommand();
                parseCommand();
            }
            WriteToLog("--------------------------------------------------session ended--------------------------------------------------");
        }
        // reads the command
        string readCommand()
        {
            string data = Console.ReadLine();
            return data;
        }
        // hides the terminal window (unused)
        void HIDE()
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);
        }
        // shows the terminal window (unused)
        void SHOW()
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_SHOW);
        }
        #region parsers ||||        parsers used by the terminal (its stupid and not good)
        /*
         * parse prompt
         * $P path
         * $S space
         * $V version
         * $B |
         * C (
         * D date
         * T time
         * F )
         * G >
         * L <
         * Q =
         * $ $
         */
        string parsePrompt()
        {
            // parses the prompt [help prompt] for more info
            string data = "";

            for (int i = 0; i < prompt.Length; i++)
            {
                if (prompt[i] == '$')
                {
                    i++;
                    if (prompt.Length == i)
                    {
                        Console.WriteLine("Parsing error : prompt");
                        prompt = defaultPrompt;
                        data = parsePrompt();
                        return data;
                    }
                    else if (prompt[i] == 'P')
                    {
                        data += currentPath;
                    }
                    else if (prompt[i] == 'S')
                    {
                        data += " ";
                    }
                    else if (prompt[i] == 'V')
                    {
                        data += currentVersion;
                    }
                    else if (prompt[i] == 'B')
                    {
                        data += "|";
                    }
                    else if (prompt[i] == 'C')
                    {
                        data += "(";
                    }
                    else if (prompt[i] == 'D')
                    {
                        data += DateTime.Now.ToString("ddd dd/MM/yyyy");
                    }
                    else if (prompt[i] == 'T')
                    {
                        data += DateTime.Now.ToString("ff");
                    }
                    else if (prompt[i] == 'F')
                    {
                        data += ")";
                    }
                    else if (prompt[i] == 'G')
                    {
                        data += ">";
                    }
                    else if (prompt[i] == 'L')
                    {
                        data += "<";
                    }
                    else if (prompt[i] == 'Q')
                    {
                        data += "=";
                    }
                    else if (prompt[i] == '$')
                    {
                        data += "$";
                    }
                }
                else
                {
                    data += prompt[i];
                }
            }

            return data;
        }
        /*
         * parse command
         * takes the command
         * checks the built-in command registry
         * checks the external command registry
         * displays an error if it didn't find a command
         */
        void parseCommand()
        {
            // get arguments and stuff
            string[] commandArray;
            commandArray = Regex.Split(command, @" |,");
            string cmd = commandArray[0];
            string BuiltCmdReg = Config.IniReadValue("Commands", cmd);
            string CmdReg = Config.IniReadValue("CommandRegistry", cmd);
            string argstr = "";
            string[] argsa = new string[commandArray.Length - 1];
            if (BuiltCmdReg == "")
                BuiltCmdReg = cmd; // user may use the config command
            for (int i = 0; i < commandArray.Length - 1; i++)
            {
                argstr += commandArray[i + 1] + ((commandArray.Length - 2) != i ? " " : "");
            }
            argsa = argstr.Split(' ');
            // parsin is fun (i need help)
            // normal functional command system
            if (!failingCmdSystem)
            {
                try
                {
                    // READ HELP
                    // if the config file doesn't exist ERROR
                    if (!File.Exists(Path.Combine(path, "Config.ini")))
                    {
                        failingCmdSystem = true;
                        Console.WriteLine("Failing command system.");
                        double id = GenSessionID();
                        Run rn = new Run();
                        rn.failingCmdSystem = true;
                        rn.StartTerminal(true);
                        while (!rn.ext)
                        {

                        }
                        return;
                    }
                    if (BuiltCmdReg == "echo")
                    {
                        for (int i = 0; i < commandArray.Length - 1; i++)
                        {
                            Console.Write(commandArray[i + 1] + " ");
                        }
                        Console.Write("\n");
                    }
                    else if (BuiltCmdReg == "cls")
                    {
                        Console.Clear();
                    }
                    else if (BuiltCmdReg == "ext")
                    {
                        ext = true;
                    }
                    else if (BuiltCmdReg == "title")
                    {
                        title = "";
                        for (int i = 0; i < commandArray.Length - 1; i++)
                        {
                            title += commandArray[i + 1] + " ";
                        }
                        Console.Title = title;
                    }
                    else if (BuiltCmdReg == "readln")
                    {
                        registers[inputReg] = Console.ReadLine();
                        DebugLog(true, "INPUTMGR", "KeysPressed {0} register {1}\n", registers[inputReg], inputReg.ToString());
                    }
                    else if (BuiltCmdReg == "terminal")
                    {
                        for (int i = 0; i < commandArray.Length - 1; i++)
                        {
                            if (commandArray[i + 1] == "-v" || commandArray[i + 1] == "--version")
                            {
                                Console.WriteLine("the terminal version is : " + currentVersion);
                            }
                            else if (commandArray[i + 1] == "-r" || commandArray[i + 1] == "--reset")
                            {
                                if (File.Exists(Config.path))
                                    File.Delete(Config.path);
                                Program.setConfig();
                            }
                            else if (commandArray[i + 1] == "-rs" || commandArray[i + 1] == "--restart")
                            {
                                Console.Clear();
                                StartTerminal(false);
                                ext = true;
                            }
                            else if (commandArray[i + 1] == "-trwa" || commandArray[i + 1] == "--test-read-write-access")
                            {
                                try
                                {
                                    if (!Directory.Exists(tmpPath))
                                    {
                                        Warning("temp folder not found creating a new one");
                                        Directory.CreateDirectory(tmpPath);
                                    }
                                    StreamWriter sw = new StreamWriter(trwaFile, false);
                                    sw.Write("long very very long text very very VERY long text file that should be correct");
                                    sw.Flush();
                                    sw.Dispose();
                                    sw.Close();
                                    StreamReader sr = new StreamReader(trwaFile);
                                    string readText = sr.ReadToEnd();
                                    sr.Dispose();
                                    sr.Close();
                                    if (readText != "long very very long text very very VERY long text file that should be correct")
                                    {
                                        Error("Invalid string written or read try again");
                                    }
                                    else
                                    {
                                        Success("able to read and write");
                                    }
                                    File.Delete(trwaFile);
                                }
                                catch (Exception e)
                                {
                                    Error("Unable to write and read the temp file");
                                    if (File.Exists(Path.Combine(tmpPath, trwaFile)))
                                    {
                                        File.Delete(Path.Combine(tmpPath, trwaFile));
                                    }
                                    DebugLog(false, "terminal.test-read-write-access", e.Message + "\n");
                                }
                            }
                            else if (commandArray[i + 1] == "-ac" || commandArray[i + 1] == "--aliascreate")
                            {
                                if (!(commandArray.Length >= i + 3))
                                {
                                    Error("alias creation needs more arguments. eg <alias name> <alias command(in config.ini)>");
                                    return;
                                }
                                string an = commandArray[i + 2];
                                string ac = commandArray[i + 3];
                                if (Config.IniReadValue("Commands", an) == ac)
                                {
                                    Warning("Alias already exists");
                                    return;
                                }
                                else if ((Config.IniReadValue("Commands", an) != ac) && (Config.IniReadValue("Commands", an) != ""))
                                {
                                    Warning("Changing an existing alias");
                                }
                                Config.IniWriteValue("Commands", an, ac);
                                Success("Changed");
                                return;
                            }
                            else if (commandArray[i + 1] == "-h" || commandArray[i + 1] == "--help")
                            {
                                Console.WriteLine("Syntax Syntax: <...> required, {...} optional");
                                Console.WriteLine("Syntax: terminal <argument> {argument.arguments[list]}");
                                Console.WriteLine("-v or --version: prints the terminal version to the screen");
                                Console.Write("-r or --reset: resets the configurations ");
                                Warning("this will delete all aliases and external comands and the currently set password");
                                Console.WriteLine("-rs or --restart: restarts the terminal");
                                Console.WriteLine("-trwa or --test-read-write-access: read and write to test if it is possible");
                                Console.WriteLine("-ac or --aliascreate: add a new alias syn:<alias name> <alias command(in config.ini)>");
                                Console.WriteLine("-h or --help: help msg");
                            }
                            else
                            {
                                Warning("'" + commandArray[i + 1] + "' is an unknown argument");
                            }
                        }
                        if (commandArray.Length <= 1)
                        {
                            Error("'terminal' neads arguments");
                        }
                    }
                    else if (BuiltCmdReg == "read")
                    {
                        registers[keyInputReg] = Console.ReadKey().KeyChar.ToString();
                        DebugLog(true, "INPUTMGR", "KeyPressed {0} register {1}\n", registers[keyInputReg], keyInputReg.ToString());
                    }
                    else if (BuiltCmdReg == "readreg")
                    {
                        if (commandArray.Length <= 1)
                        {
                            Error("'readreg' needs arguments");
                        }
                        else
                        {
                            Console.WriteLine(registers[int.Parse(commandArray[0 + 1])]);
                        }
                    }
                    else if (BuiltCmdReg == "writereg")
                    {
                        if (commandArray.Length <= 2)
                        {
                            Error("'writereg' needs arguments");
                        }
                        else
                        {
                            string data = "";
                            for (int i = 1; i < commandArray.Length - 1; i++)
                            {
                                data += commandArray[i + 1] + " ";
                            }
                            registers[int.Parse(commandArray[0 + 1])] = data;
                            DebugLog(true, "HDLR", "reg {0} is now {1}\n", commandArray[0 + 1], data);
                        }
                    }
                    else if (BuiltCmdReg == "run")
                    {
                        if (commandArray.Length <= 1)
                        {
                            Error("Could not run a non-specified file");
                        }
                        else
                        {
                            string fileLocation = "";
                            for (int i = 1; i < commandArray.Length; i++)
                            {
                                if (i != commandArray.Length - 1)
                                {
                                    fileLocation += commandArray[i] += " ";
                                }
                                else
                                {
                                    fileLocation += commandArray[i];
                                }
                            }
                            if (File.Exists(fileLocation) && commandArray.Length >= 2)
                            {
                                try
                                {
                                    Process tmp = new Process();
                                    tmp.StartInfo.FileName = fileLocation;
                                    tmp.StartInfo.Arguments = commandArray[2];
                                    tmp.Start();
                                    Console.Title = title;
                                    tmp.WaitForExit();
                                }
                                catch (Exception)
                                {
                                    Warning("Could not excute: not a valid excutable");
                                }
                            }
                            else if (File.Exists(fileLocation))
                            {
                                try
                                {
                                    Process tmp = new Process();
                                    tmp.StartInfo.FileName = fileLocation;
                                    tmp.StartInfo.Arguments = "";
                                    tmp.Start();
                                    Console.Title = title;
                                    tmp.WaitForExit();
                                }
                                catch (Exception)
                                {
                                    Warning("Could not excute: not a valid excutable");
                                }
                            }
                            else if (File.Exists(Path.Combine(currentPath, fileLocation)))
                            {
                                try
                                {
                                    Process tmp = new Process();
                                    tmp.StartInfo.FileName = "explorer";
                                    tmp.StartInfo.Arguments = "\"" + Path.Combine(currentPath, fileLocation) + "\"";
                                    tmp.Start();
                                    Console.Title = title;
                                    tmp.WaitForExit();
                                }
                                catch (Exception)
                                {
                                    Console.WriteLine("Could not excute: not a valid excutable");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Could not excute: not found");
                            }
                        }
                    }
                    else if (BuiltCmdReg == "newrun")
                    {
                        if (commandArray.Length <= 1)
                        {
                            Error("Could not run a non-specified file");
                        }
                        else
                        {
                            string fileLocation = "";
                            for (int i = 1; i < commandArray.Length; i++)
                            {
                                if (i != commandArray.Length - 1)
                                {
                                    fileLocation += commandArray[i] += " ";
                                }
                                else
                                {
                                    fileLocation += commandArray[i];
                                }
                            }
                            if (File.Exists(fileLocation))
                            {
                                try
                                {
                                    Process tmp = new Process();
                                    tmp.StartInfo.FileName = "explorer";
                                    tmp.StartInfo.Arguments = "\"" + fileLocation + "\"";
                                    tmp.Start();
                                    Console.Title = title;
                                    tmp.WaitForExit();
                                }
                                catch (Exception)
                                {
                                    Warning("Could not excute: not a valid excutable");
                                }
                            }
                            else if (File.Exists(Path.Combine(currentPath, fileLocation)))
                            {
                                try
                                {
                                    Process tmp = new Process();
                                    tmp.StartInfo.FileName = "explorer";
                                    tmp.StartInfo.Arguments = "\"" + Path.Combine(currentPath, fileLocation) + "\"";
                                    tmp.Start();
                                    Console.Title = title;
                                    tmp.WaitForExit();
                                }
                                catch (Exception)
                                {
                                    Console.WriteLine("Could not excute: not a valid excutable");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Could not excute: not found");
                            }
                        }
                    }
                    else if (BuiltCmdReg == "cd")
                    {
                        string dir = "";
                        List<string> list = commandArray.OfType<string>().ToList(); ;
                        list.Add("");
                        commandArray = list.ToArray();
                        for (int i = 1; i < commandArray.Length - 1; i++)
                        {
                            if (i == commandArray.Length - 2)
                            {
                                dir += commandArray[i] + " ";
                            }
                            else
                            {
                                dir += commandArray[i];
                            }
                        }
                        List<char> lst = dir.ToCharArray().OfType<char>().ToList();
                        lst.RemoveAt(dir.Length - 1);
                        dir = new string(lst.ToArray());
                        if (dir == "..")
                        {
                            currentPath = Directory.GetParent(currentPath).FullName;
                        }
                        else if (Directory.Exists(Path.Combine(currentPath, dir)))
                        {
                            currentPath = Path.Combine(currentPath, dir);
                        }
                        else if (Directory.Exists(dir))
                        {
                            currentPath = dir;
                        }
                        else
                        {
                            Warning("Directory not found");
                        }
                        // automaticaly fix broken currentPath
                        Directory.SetCurrentDirectory(currentPath);
                        currentPath = Directory.GetCurrentDirectory();
                    }
                    else if (BuiltCmdReg == "ls")
                    {
                        string[] dirs = Directory.GetDirectories(currentPath);
                        for (int i = 0; i < dirs.Length - 1; i++)
                        {
                            Console.WriteLine(dirs[i]);
                        }
                        dirs = Directory.GetFiles(currentPath);
                        for (int i = 0; i < dirs.Length - 1; i++)
                        {
                            Console.WriteLine(dirs[i]);
                        }
                    }
                    else if (BuiltCmdReg == "create")
                    {
                        if (commandArray.Length <= 1)
                        {
                            Error("'create' nead arguments");
                        }
                        else
                        {
                            string args = "";
                            for (int i = 0; i < commandArray.Length - 1; i++)
                            {
                                if (i != commandArray.Length - 1)
                                {
                                    args += commandArray[i + 1] + " ";
                                }
                                else
                                {
                                    args += commandArray[i];
                                }
                            }
                            Directory.SetCurrentDirectory(currentPath);
                            if (!File.Exists(args))
                            {
                                File.Create(args);
                                DebugLog(true, "FSHL", "Created file {0}\n", args);
                            }
                            else
                            {
                                Warning("File already exists");
                            }
                        }
                    }
                    else if (BuiltCmdReg == "delete")
                    {
                        if (commandArray.Length <= 1)
                        {
                            Error("'delete' nead arguments");
                        }
                        else
                        {
                            string args = "";
                            for (int i = 0; i < commandArray.Length - 1; i++)
                            {
                                if (i != commandArray.Length - 1)
                                {
                                    args += commandArray[i + 1] + " ";
                                }
                                else
                                {
                                    args += commandArray[i];
                                }
                            }
                            Directory.SetCurrentDirectory(currentPath);
                            if (File.Exists(args))
                            {
                                File.Delete(args);
                                DebugLog(true, "FSHL", "Deleted file {0}\n", args);
                            }
                            else
                            {
                                Warning("File not found");
                            }
                        }
                    }
                    else if (BuiltCmdReg == "mkdir")
                    {
                        if (commandArray.Length <= 1)
                        {
                            Error("'mkdir' nead arguments");
                        }
                        else
                        {
                            string args = "";
                            for (int i = 0; i < commandArray.Length - 1; i++)
                            {
                                if (i != commandArray.Length - 1)
                                {
                                    args += commandArray[i + 1] + " ";
                                }
                                else
                                {
                                    args += commandArray[i];
                                }
                            }
                            Directory.SetCurrentDirectory(currentPath);
                            if (!Directory.Exists(args))
                            {
                                Directory.CreateDirectory(args);
                                DebugLog(true, "FSHL", "Created directory {0}\n", args);
                            }
                            else
                            {
                                Warning("Directory already exists");
                            }
                        }
                    }
                    else if (BuiltCmdReg == "rmdir")
                    {
                        if (commandArray.Length <= 1)
                        {
                            Error("'mkdir' nead arguments");
                        }
                        else
                        {
                            string args = "";
                            for (int i = 0; i < commandArray.Length - 1; i++)
                            {
                                if (i != commandArray.Length - 1)
                                {
                                    args += commandArray[i + 1] + " ";
                                }
                                else
                                {
                                    args += commandArray[i];
                                }
                            }
                            Directory.SetCurrentDirectory(currentPath);
                            if (Directory.Exists(args))
                            {
                                Directory.Delete(args);
                                DebugLog(true, "FSHL", "Deleted directory {0}\n", args);
                            }
                            else
                            {
                                Warning("Directory not found");
                            }
                        }
                    }
                    else if (BuiltCmdReg == "copy")
                    {
                        if (commandArray.Length == 2)
                        {
                            Error("Destination not specified");
                        }
                        else if (commandArray.Length == 1)
                        {
                            Error("Source not specified");
                        }
                        else if (commandArray.Length == 3)
                        {
                            if (File.Exists(commandArray[0 + 1]))
                            {
                                if (File.Exists(commandArray[0 + 2]))
                                {
                                    Console.WriteLine("Do you want to override '" + commandArray[0 + 2] + "'?");
                                    Console.Write(">");
                                    char ans = Console.ReadKey().KeyChar;
                                    if (ans.ToString().ToLower().ToCharArray()[0] == 'y')
                                    {
                                        File.Copy(commandArray[0 + 1], commandArray[0 + 2]);
                                        DebugLog(true, "FSHL", "Copied {0} to {1}\n", commandArray[0 + 1], commandArray[0 + 2]);
                                    }
                                    else
                                    {
                                        Success("Canceled operation");
                                    }
                                }
                                else
                                {
                                    File.Copy(commandArray[0 + 1], commandArray[0 + 2]);
                                    DebugLog(true, "FSHL", "Copied {0} to {1}\n", commandArray[0 + 1], commandArray[0 + 2]);
                                }
                            }
                            else
                            {
                                Error("Source not found");
                            }
                        }
                        else
                        {
                            Error("Unknown arguments");
                        }
                    }
                    else if (BuiltCmdReg == "prompt")
                    {
                        string prmpt = "";
                        for (int i = 0; i < commandArray.Length - 1; i++)
                        {
                            prmpt += commandArray[i + 1];
                        }
                        if (prmpt == "")
                        {
                            prompt = defaultPrompt;
                        }
                        else
                        {
                            prompt = prmpt;
                        }
                    }
                    else if (BuiltCmdReg == "hlp")
                    {
                        string data = "";
                        for (int i = 0; i < commandArray.Length - 1; i++)
                        {
                            data += commandArray[i + 1];
                        }
                        if (data == "")
                        {
                            Console.WriteLine("commands:\ncls|clear\nexit\ntitle\nreadln\nterminal\nread\nreadreg\nwritereg\nrun\nstart\ncd\nls|dir\ncreate\ndelete\nmkdir|md\nrmdir|rd\ncopy|cp\nprmpt|prompt\nhelp\nerror\nwarn\nsuccess\nFSCon\nLock\nUnlock\nToggleDebug\nVirtualTerminal\ndisablelc\ncolor\nRun help [command] for more info");
                        }
                        else
                        {
                            help(data);
                        }
                    }
                    else if (BuiltCmdReg == "ERR")
                    {
                        Error("Success???");
                    }
                    else if (BuiltCmdReg == "WRN")
                    {
                        Warning("Success???");
                    }
                    else if (BuiltCmdReg == "SUC")
                    {
                        Success("Success");
                    }
                    else if (BuiltCmdReg == "fscon")
                    {
                        string fn = "";
                        string word = "";
                        bool mode = false;
                        bool ov = true;
                        bool createNew = false;
                        bool es = false;
                        bool el = false;
                        Directory.SetCurrentDirectory(currentPath);
                        try
                        {
                            for (int i = 0; i < argsa.Length; i++)
                            {
                                if (argsa[i] == "file")
                                {
                                    fn = argsa[i + 1];
                                }
                                if (argsa[i] == "in")
                                {
                                    mode = false;
                                }
                                if (argsa[i] == "out")
                                {
                                    mode = true;
                                }
                                if (argsa[i] == "word")
                                {
                                    word = argsa[i + 1];
                                }
                                if (argsa[i] == "overwrite:true")
                                {
                                    ov = true;
                                }
                                if (argsa[i] == "overwrite:false")
                                {
                                    ov = false;
                                }
                                if (argsa[i] == "create:true")
                                {
                                    createNew = true;
                                }
                                if (argsa[i] == "create:false")
                                {
                                    createNew = false;
                                }
                                if (argsa[i] == "es:true")
                                {
                                    es = true;
                                }
                                if (argsa[i] == "el:true")
                                {
                                    el = true;
                                }
                            }
                            if (createNew)
                            {
                                File.Create(fn);
                            }
                            if (mode)
                            {
                                StreamReader sr = new StreamReader(fn);
                                Console.WriteLine(sr.ReadToEnd());
                                sr.Close();
                                sr.Dispose();
                            }
                            else
                            {
                                StreamWriter sw = new StreamWriter(fn, !ov);
                                if (el)
                                    sw.Write("\n");
                                if (es)
                                    sw.Write(" ");
                                sw.Write(word);
                                sw.Flush();
                                sw.Close();
                                sw.Dispose();
                            }
                        }
                        catch (Exception e)
                        {
                            Error("Something went wrong [" + e.Message + "]");
                        }
                    }
                    else if (BuiltCmdReg == "LCK")
                    {
                        string res;
                        if (Program.CheckLock())
                        {
                            Warning("Can't lock while it is locked");
                        }
                        else
                        {
                            Console.Write("Password :>");
                            string pswrd = Console.ReadLine();
                            ComputeSha512Hash(pswrd, out res);
                            Config.IniWriteValue("Data", "Password", res);
                            Config.IniWriteValue("keConfig", "LCK", "keTrue");
                            Success("Locked");
                            DebugLog(true, "Lock", "Please wait {0} seconds", "5");
                            Thread.Sleep(1000);
                            DebugLog(true, "Lock", "Please wait {0} seconds", "4");
                            Thread.Sleep(1000);
                            DebugLog(true, "Lock", "Please wait {0} seconds", "3");
                            Thread.Sleep(1000);
                            DebugLog(true, "Lock", "Please wait {0} seconds", "2");
                            Thread.Sleep(1000);
                            DebugLog(true, "Lock", "Please wait {0} seconds", "1");
                            Thread.Sleep(1000);
                            DebugLog(true, "Lock", "Please wait {0} seconds", "0");
                            StartTerminal(dbg);
                        }
                    }
                    else if (BuiltCmdReg == "ULCK")
                    {
                        if (!Program.CheckLock())
                        {
                            Warning("Can't unlock while it is unlocked");
                        }
                        else
                        {
                            bool e = true;
                            int attemptsLeft = 3;
                            attemptsLeft++;
                            while (true)
                            {
                                attemptsLeft--;
                                Console.Write("Password :>");
                                string input = Console.ReadLine();
                                if (ComputeSha512Hash(input) == Config.IniReadValue("Data", "Password"))
                                {
                                    break;
                                }
                                else if (!(attemptsLeft <= 0))
                                    Console.Write("Wrong password {0} attempts left", attemptsLeft);
                                if (attemptsLeft <= 0)
                                {
                                    e = false;
                                    break;
                                }
                            }
                            if (e)
                            {
                                Config.IniWriteValue("Data", "Password", "");
                                Success("Unlocked");
                                DebugLog(true, "Unlock", "Please wait {0} seconds", "5");
                                Thread.Sleep(1000);
                                DebugLog(true, "Unlock", "Please wait {0} seconds", "4");
                                Thread.Sleep(1000);
                                DebugLog(true, "Unlock", "Please wait {0} seconds", "3");
                                Thread.Sleep(1000);
                                DebugLog(true, "Unlock", "Please wait {0} seconds", "2");
                                Thread.Sleep(1000);
                                DebugLog(true, "Unlock", "Please wait {0} seconds", "1");
                                Thread.Sleep(1000);
                                DebugLog(true, "Unlock", "Please wait {0} seconds", "0");
                                StartTerminal(dbg);
                                ext = true;
                            }
                            Console.Write("\n");
                        }
                    }
                    else if (BuiltCmdReg == "SDBG")
                    {
                        if (Config.IniReadValue("keConfig", "Debug") == "keTrue")
                        {
                            Config.IniWriteValue("keConfig", "Debug", "keFalse");
                            dbg = false;
                        }
                        else if (Config.IniReadValue("keConfig", "Debug") == "keFalse")
                        {
                            Config.IniWriteValue("keConfig", "Debug", "keTrue");
                            dbg = false;
                        }
                        else
                        {
                            Config.IniWriteValue("keConfig", "Debug", "keFalse");
                            dbg = false;
                        }
                        Warning("Requires restarting the terminal. virtualterminal command will help");
                    }
                    else if (BuiltCmdReg == "VTERM")
                    {
                        double id = GenSessionID();
                        Run rn = new Run();
                        rn.StartTerminal(dbg);
                        while (!rn.ext)
                        {

                        }
                        Console.WriteLine("Virtual terminal session ended <{0}>", id);
                        DebugLog(true, "VMGR", "Virtual terminal session ended. session id <{0}>\n", id.ToString());

                    }
                    else if (BuiltCmdReg == "DLC")
                    {
                        dlc = !dlc;
                        Config.IniWriteValue("keConfig", "DLog", dlc ? "keTrue" : "keFlase");
                        Console.WriteLine("Log: {0}", dlc);
                    }
                    else if (BuiltCmdReg == "CHKR")
                    {
                        string op = "";
                        if (argsa.Length >= 1)
                        {
                            op = argsa[0];
                            if (op == "safe")
                            {
                                bool fail = false;
                                try
                                {
                                    Config.IniWriteValue("keConfig", "DLC", Config.IniReadValue("keConfig", "DLC"));
                                }
                                catch (Exception)
                                {
                                    Error("Config failing", true);
                                    failingCmdSystem = true;
                                    fail = true;
                                }
                                try
                                {
                                    WriteToLog("check log", true);
                                }
                                catch (Exception)
                                {
                                    Error("Log failing", true);
                                    fail = true;
                                }
                                try
                                {
                                    if (!Directory.Exists(tmpPath))
                                    {
                                        throw new DirectoryNotFoundException("temp not found");
                                    }
                                }
                                catch (Exception)
                                {
                                    Warning("Temp dir not found please run: terminal -trwa", true);
                                    fail = true;
                                }
                                if (!fail)
                                {
                                    Success("Safe check");
                                }
                            }
                        }
                        else
                        {
                            Error("Arguments not found");
                        }
                    }
                    else if (BuiltCmdReg == "CDD")
                    {
                        string p = "";
                        foreach (string arg in argsa)
                        {
                            p += arg + " ";
                        }
                        currentPath = p;
                        Directory.SetCurrentDirectory(currentPath);
                        currentPath = Directory.GetCurrentDirectory();
                    }
                    else if (BuiltCmdReg == "CLR")
                    {
                        if (argsa.Length == 2)
                        {
                            int bg = int.Parse(argsa[0]);
                            int fg = int.Parse(argsa[1]);
                            Console.BackgroundColor = (ConsoleColor)bg;
                            Console.ForegroundColor = (ConsoleColor)fg;
                        }
                        else if (argsa.Length <= 1 && (argsa[0] == ""))
                        {
                            Console.BackgroundColor = ConsoleColor.Black;
                            Console.ForegroundColor = ConsoleColor.White;
                        }
                        else
                        {
                            Error("Where are my 2 arguments? you can give me nothing. gonna eat the config and remove all the alia-*gets shot*");
                        }
                    }
                    else if (cmd == "")
                    {

                    }
                    else if (File.Exists(CmdReg))
                    {
                        try
                        {
                            Process tmp = new Process();
                            tmp.StartInfo.FileName = CmdReg;
                            tmp.StartInfo.Arguments = "";
                            tmp.Start();
                            Console.Title = title;
                            tmp.WaitForExit();
                        }
                        catch (Exception)
                        {
                            Error("Could not excute: not a valid excutable");
                        }
                    }
                    else
                    {
                        Warning("'" + cmd + "' is not a valid internal or external command");
                    }
                }
                catch (Exception e)
                {
                    Error("Something went wrong: " + e.Message);
                }
            }
            // broken command system (missing config.ini) can be caused by an incorrect config.ini file (don't remeber how that happened)
            else if (failingCmdSystem)
            {
                if (cmd == "exit")
                    ext = true;
                else if (cmd == "clear" || cmd == "cls")
                    Console.Clear();
                else if (cmd == "terminal")
                {
                    for (int i = 0; i < commandArray.Length - 1; i++)
                    {
                        if (commandArray[i + 1] == "-v" || commandArray[i + 1] == "--version")
                        {
                            Console.WriteLine("the terminal version is : " + currentVersion);
                        }
                        else if (commandArray[i + 1] == "-r" || commandArray[i + 1] == "--reset")
                        {
                            if (File.Exists(Config.path))
                                File.Delete(Config.path);
                            Program.setConfig();
                        }
                        else if (commandArray[i + 1] == "-rs" || commandArray[i + 1] == "--restart")
                        {
                            Console.Clear();
                            StartTerminal(false);
                            ext = true;
                        }
                        else if (commandArray[i + 1] == "-h" || commandArray[i + 1] == "--help")
                        {
                            Console.WriteLine("-v or --version: prints the terminal version to the screen");
                            Console.Write("-r or --reset: resets the configurations");
                            Console.WriteLine("-rs or --restart: restarts the terminal");
                            Console.WriteLine("-h or --help: help msg");
                        }
                        else
                        {
                            Warning("'" + commandArray[i + 1] + "' is an unknown argument");
                        }
                    }
                    if (commandArray.Length <= 1)
                    {
                        Error("'terminal' neads arguments");
                    }
                }
                else if (cmd == "run")
                {
                    if (commandArray.Length <= 1)
                    {
                        Error("Could not run a non-specified file");
                    }
                    else
                    {
                        string fileLocation = "";
                        for (int i = 1; i < commandArray.Length; i++)
                        {
                            if (i != commandArray.Length - 1)
                            {
                                fileLocation += commandArray[i] += " ";
                            }
                            else
                            {
                                fileLocation += commandArray[i];
                            }
                        }
                        if (File.Exists(fileLocation) && commandArray.Length >= 2)
                        {
                            try
                            {
                                Process tmp = new Process();
                                tmp.StartInfo.FileName = fileLocation;
                                tmp.StartInfo.Arguments = commandArray[2];
                                tmp.Start();
                                Console.Title = title;
                                tmp.WaitForExit();
                            }
                            catch (Exception)
                            {
                                Warning("Could not excute: not a valid excutable");
                            }
                        }
                        else if (File.Exists(fileLocation))
                        {
                            try
                            {
                                Process tmp = new Process();
                                tmp.StartInfo.FileName = fileLocation;
                                tmp.StartInfo.Arguments = "";
                                tmp.Start();
                                Console.Title = title;
                                tmp.WaitForExit();
                            }
                            catch (Exception)
                            {
                                Warning("Could not excute: not a valid excutable");
                            }
                        }
                        else if (File.Exists(Path.Combine(currentPath, fileLocation)))
                        {
                            try
                            {
                                Process tmp = new Process();
                                tmp.StartInfo.FileName = "explorer";
                                tmp.StartInfo.Arguments = "\"" + Path.Combine(currentPath, fileLocation) + "\"";
                                tmp.Start();
                                Console.Title = title;
                                tmp.WaitForExit();
                            }
                            catch (Exception)
                            {
                                Console.WriteLine("Could not excute: not a valid excutable");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Could not excute: not found");
                        }
                    }
                }
                else if (cmd == "newrun")
                {
                    if (commandArray.Length <= 1)
                    {
                        Error("Could not run a non-specified file");
                    }
                    else
                    {
                        string fileLocation = "";
                        for (int i = 1; i < commandArray.Length; i++)
                        {
                            if (i != commandArray.Length - 1)
                            {
                                fileLocation += commandArray[i] += " ";
                            }
                            else
                            {
                                fileLocation += commandArray[i];
                            }
                        }
                        if (File.Exists(fileLocation))
                        {
                            try
                            {
                                Process tmp = new Process();
                                tmp.StartInfo.FileName = "explorer";
                                tmp.StartInfo.Arguments = "\"" + fileLocation + "\"";
                                tmp.Start();
                                Console.Title = title;
                                tmp.WaitForExit();
                            }
                            catch (Exception)
                            {
                                Warning("Could not excute: not a valid excutable");
                            }
                        }
                        else if (File.Exists(Path.Combine(currentPath, fileLocation)))
                        {
                            try
                            {
                                Process tmp = new Process();
                                tmp.StartInfo.FileName = "explorer";
                                tmp.StartInfo.Arguments = "\"" + Path.Combine(currentPath, fileLocation) + "\"";
                                tmp.Start();
                                Console.Title = title;
                                tmp.WaitForExit();
                            }
                            catch (Exception)
                            {
                                Console.WriteLine("Could not excute: not a valid excutable");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Could not excute: not found");
                        }
                    }
                }
                else if (cmd == "cd")
                {
                    string dir = "";
                    List<string> list = commandArray.OfType<string>().ToList(); ;
                    list.Add("");
                    commandArray = list.ToArray();
                    for (int i = 1; i < commandArray.Length - 1; i++)
                    {
                        if (i == commandArray.Length - 2)
                        {
                            dir += commandArray[i] + " ";
                        }
                        else
                        {
                            dir += commandArray[i];
                        }
                    }
                    List<char> lst = dir.ToCharArray().OfType<char>().ToList();
                    lst.RemoveAt(dir.Length - 1);
                    dir = new string(lst.ToArray());
                    if (dir == "..")
                    {
                        currentPath = Directory.GetParent(currentPath).FullName;
                    }
                    else if (Directory.Exists(Path.Combine(currentPath, dir)))
                    {
                        currentPath = Path.Combine(currentPath, dir);
                    }
                    else if (Directory.Exists(dir))
                    {
                        currentPath = dir;
                    }
                    else
                    {
                        Warning("Directory not found");
                    }
                }
                else if (cmd == "ls")
                {
                    string[] dirs = Directory.GetDirectories(currentPath);
                    for (int i = 0; i < dirs.Length - 1; i++)
                    {
                        Console.WriteLine(dirs[i]);
                    }
                    dirs = Directory.GetFiles(currentPath);
                    for (int i = 0; i < dirs.Length - 1; i++)
                    {
                        Console.WriteLine(dirs[i]);
                    }
                }
                else if (cmd == "create")
                {
                    if (commandArray.Length <= 1)
                    {
                        Error("'create' nead arguments");
                    }
                    else
                    {
                        string args = "";
                        for (int i = 0; i < commandArray.Length - 1; i++)
                        {
                            if (i != commandArray.Length - 1)
                            {
                                args += commandArray[i + 1] + " ";
                            }
                            else
                            {
                                args += commandArray[i];
                            }
                        }
                        Directory.SetCurrentDirectory(currentPath);
                        if (!File.Exists(args))
                        {
                            File.Create(args);
                            DebugLog(true, "FSHL", "Created file {0}\n", args);
                        }
                        else
                        {
                            Warning("File already exists");
                        }
                    }
                }
                else if (cmd == "delete")
                {
                    if (commandArray.Length <= 1)
                    {
                        Error("'delete' nead arguments");
                    }
                    else
                    {
                        string args = "";
                        for (int i = 0; i < commandArray.Length - 1; i++)
                        {
                            if (i != commandArray.Length - 1)
                            {
                                args += commandArray[i + 1] + " ";
                            }
                            else
                            {
                                args += commandArray[i];
                            }
                        }
                        Directory.SetCurrentDirectory(currentPath);
                        if (File.Exists(args))
                        {
                            File.Delete(args);
                            DebugLog(true, "FSHL", "Deleted file {0}\n", args);
                        }
                        else
                        {
                            Warning("File not found");
                        }
                    }
                }
                else if (cmd == "mkdir")
                {
                    if (commandArray.Length <= 1)
                    {
                        Error("'mkdir' nead arguments");
                    }
                    else
                    {
                        string args = "";
                        for (int i = 0; i < commandArray.Length - 1; i++)
                        {
                            if (i != commandArray.Length - 1)
                            {
                                args += commandArray[i + 1] + " ";
                            }
                            else
                            {
                                args += commandArray[i];
                            }
                        }
                        Directory.SetCurrentDirectory(currentPath);
                        if (!Directory.Exists(args))
                        {
                            Directory.CreateDirectory(args);
                            DebugLog(true, "FSHL", "Created directory {0}\n", args);
                        }
                        else
                        {
                            Warning("Directory already exists");
                        }
                    }
                }
                else if (cmd == "rmdir")
                {
                    if (commandArray.Length <= 1)
                    {
                        Error("'mkdir' nead arguments");
                    }
                    else
                    {
                        string args = "";
                        for (int i = 0; i < commandArray.Length - 1; i++)
                        {
                            if (i != commandArray.Length - 1)
                            {
                                args += commandArray[i + 1] + " ";
                            }
                            else
                            {
                                args += commandArray[i];
                            }
                        }
                        Directory.SetCurrentDirectory(currentPath);
                        if (Directory.Exists(args))
                        {
                            Directory.Delete(args);
                            DebugLog(true, "FSHL", "Deleted directory {0}\n", args);
                        }
                        else
                        {
                            Warning("Directory not found");
                        }
                    }
                }
                else if (cmd == "copy")
                {
                    if (commandArray.Length == 2)
                    {
                        Error("Destination not specified");
                    }
                    else if (commandArray.Length == 1)
                    {
                        Error("Source not specified");
                    }
                    else if (commandArray.Length == 3)
                    {
                        if (File.Exists(commandArray[0 + 1]))
                        {
                            if (File.Exists(commandArray[0 + 2]))
                            {
                                Console.WriteLine("Do you want to override '" + commandArray[0 + 2] + "'?");
                                Console.Write(">");
                                char ans = Console.ReadKey().KeyChar;
                                if (ans.ToString().ToLower().ToCharArray()[0] == 'y')
                                {
                                    File.Copy(commandArray[0 + 1], commandArray[0 + 2]);
                                    DebugLog(true, "FSHL", "Copied {0} to {1}\n", commandArray[0 + 1], commandArray[0 + 2]);
                                }
                                else
                                {
                                    Success("Canceled operation");
                                }
                            }
                            else
                            {
                                File.Copy(commandArray[0 + 1], commandArray[0 + 2]);
                                DebugLog(true, "FSHL", "Copied {0} to {1}\n", commandArray[0 + 1], commandArray[0 + 2]);
                            }
                        }
                        else
                        {
                            Error("Source not found");
                        }
                    }
                    else
                    {
                        Error("Unknown arguments");
                    }
                }
                else if (BuiltCmdReg == "fscon")
                {
                    string fn = "";
                    string word = "";
                    bool mode = false;
                    bool ov = true;
                    bool createNew = false;
                    bool es = false;
                    bool el = false;
                    Directory.SetCurrentDirectory(currentPath);
                    try
                    {
                        for (int i = 0; i < argsa.Length; i++)
                        {
                            if (argsa[i] == "file")
                            {
                                fn = argsa[i + 1];
                            }
                            if (argsa[i] == "in")
                            {
                                mode = false;
                            }
                            if (argsa[i] == "out")
                            {
                                mode = true;
                            }
                            if (argsa[i] == "word")
                            {
                                word = argsa[i + 1];
                            }
                            if (argsa[i] == "overwrite:true")
                            {
                                ov = true;
                            }
                            if (argsa[i] == "overwrite:false")
                            {
                                ov = false;
                            }
                            if (argsa[i] == "create:true")
                            {
                                createNew = true;
                            }
                            if (argsa[i] == "create:false")
                            {
                                createNew = false;
                            }
                            if (argsa[i] == "es:true")
                            {
                                es = true;
                            }
                            if (argsa[i] == "el:true")
                            {
                                el = true;
                            }
                        }
                        if (createNew)
                        {
                            File.Create(fn);
                        }
                        if (mode)
                        {
                            StreamReader sr = new StreamReader(fn);
                            Console.WriteLine(sr.ReadToEnd());
                            sr.Close();
                            sr.Dispose();
                        }
                        else
                        {
                            StreamWriter sw = new StreamWriter(fn, !ov);
                            if (el)
                                sw.Write("\n");
                            if (es)
                                sw.Write(" ");
                            sw.Write(word);
                            sw.Flush();
                            sw.Close();
                            sw.Dispose();
                        }
                    }
                    catch (Exception e)
                    {
                        Error("Something went wrong [" + e.Message + "]");
                    }
                }
                else
                    Console.WriteLine("Failing command system.");
            }
            else
            {
                Warning("'" + cmd + "' is not a valid internal or external command");
            }
        }
        #endregion
        #region functions |||| functions used by the terminal (its stupid and not good)
        // help message when running (help [X]) x is a command
        void help(string data)
        {
            data = data.ToLower();
            if (data == "echo")
            {
                Console.WriteLine("write arguments to the console\nexample: echo Hello, World!");
            }
            else if (data == "clear" || data == "cls")
            {
                Console.WriteLine("Clears the screen. two commands available : {cls, clear}");
            }
            else if (data == "exit")
            {
                Console.WriteLine("Exits the terminal");
            }
            else if (data == "title")
            {
                Console.WriteLine("changes the title\nexample: title a random title");
            }
            else if (data == "readln")
            {
                Console.WriteLine("sets register : {" + inputReg + "} to the line input");
            }
            else if (data == "read")
            {
                Console.WriteLine("sets register : {" + keyInputReg + "} to the input char");
            }
            else if (data == "terminal")
            {
                Console.WriteLine("Please use {terminal -h} or {terminal --help}");
            }
            else if (data == "readreg")
            {
                Console.WriteLine("puts the value of the register specified by the first argument into the console");
            }
            else if (data == "writereg")
            {
                Console.WriteLine("sets the value of the register specified by the first argument into the console to the seconds argument");
            }
            else if (data == "run")
            {
                Console.WriteLine("Runs the file specified into the first argument. if the file is a console app it will run in the console");
            }
            else if (data == "start")
            {
                Console.WriteLine("Runs the file specified into the first argument in a new window");
            }
            else if (data == "cd")
            {
                Console.WriteLine("Changes the Directory to the directory specified in the first argument\nexample: cd directory");
            }
            else if (data == "ls" || data == "dir")
            {
                Console.WriteLine("lists the files and folders in the current directory. two commands available : {ls, dir}");
            }
            else if (data == "create")
            {
                Console.WriteLine("Creates a file with the name specified in the first argument");
            }
            else if (data == "delete")
            {
                Console.WriteLine("Deletes a file with the name specified in the first argument");
            }
            else if (data == "mkdir" || data == "md")
            {
                Console.WriteLine("Creates a directory with the name specified in the first argument");
            }
            else if (data == "rmdir" || data == "rd")
            {
                Console.WriteLine("Deletes a directory with the name specified in the first argument");
            }
            else if (data == "copy" || data == "cp")
            {
                Console.WriteLine("copies the file specified in the first argument to a file specified in the seconds argument. if dest file exists it will ask to overwrite");
            }
            else if (data == "prompt" || data == "prmpt")
            {
                Console.WriteLine("sets the prompt");
                Console.WriteLine("$P path");
                Console.WriteLine("$S space");
                Console.WriteLine("$V version");
                Console.WriteLine("$B |");
                Console.WriteLine("$C (");
                Console.WriteLine("$D date");
                Console.WriteLine("$T time in milliseconds");
                Console.WriteLine("$F )");
                Console.WriteLine("$G >");
                Console.WriteLine("$L <");
                Console.WriteLine("$Q =");
                Console.WriteLine("$$ $");
            }
            else if (data == "error")
            {
                Console.WriteLine("Shows a fake error message");
            }
            else if (data == "warn")
            {
                Console.WriteLine("Shows a fake warning message");
            }
            else if (data == "success")
            {
                Console.WriteLine("Shows a fake (real?) success message");
            }
            else if (data == "fscon")
            {
                Console.WriteLine("a file system altering program (command)\narg file (requires a word after it): specifies a file\narg in: read the file\narg out: write the file (one word currenlt supported)\narg overwrite:(true/false): does it overwrite or in other words append?\narg create:(true/false): not supported yet also useless\narg es:true: adds a space before writing (allows multi words in files)\narg el:true: adds an extra line before writing");
            }
            else if (data == "lock")
            {
                Console.WriteLine("LOCKS the terminal startup with a password can be bypassed but the password can't be read");
            }
            else if (data == "unlock")
            {
                Console.WriteLine("removes the lock otherwise usless");
            }
            else if (data == "toggledebug")
            {
                Console.WriteLine("if it is not debug it makes it debug and vise versa (i think thats how to use it)");
            }
            else if (data == "virtualterminal")
            {
                Console.WriteLine("creates a new terminal");
            }
            else if (data == "emptyprogramssuck")
            {
                Console.WriteLine("agree");
            }
            else if (data == "disablelc")
            {
                Console.WriteLine("toggles logging");
            }
            else
            {
                Console.WriteLine("Command doesn't exist");
            }
        }
        // if something went wrong
        public void Warning(string message, bool dl = false)
        {
            ConsoleColor clr = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write("WARNING: ");
            Console.WriteLine(message);
            Console.ForegroundColor = clr;
            if (!dl)
                WriteToLog("[LOG module] WARNING: " + message);
        }
        // if something almost went right
        public void Error(string message, bool dl = false)
        {
            ConsoleColor clr = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Write("ERROR: ");
            Console.WriteLine(message);
            Console.ForegroundColor = clr;
            if (!dl)
                WriteToLog("[LOG module] ERROR: " + message);
        }
        // if something went right
        public void Success(string message, bool dl = false)
        {
            ConsoleColor clr = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write("SUCCESS: ");
            Console.WriteLine(message);
            Console.ForegroundColor = clr;
            if (!dl)
                WriteToLog("[LOG module] SUCCESS: " + message);
        }
        // logs a debug message
        public void DebugLog(bool returnChar, string module, string msg, string ca = "{0}", string cb = "{1}", string cc = "{2}", string cd = "{3}", string ce = "{4}", string cf = "{5}", string cg = "{6}", string ch = "{7}", string ci = "{8}", string cj = "{9}", string ck = "{10}")
        {
            msg = msg.Replace("{0}", ca);
            msg = msg.Replace("{1}", cb);
            msg = msg.Replace("{2}", cc);
            msg = msg.Replace("{3}", cd);
            msg = msg.Replace("{4}", ce);
            msg = msg.Replace("{5}", cf);
            msg = msg.Replace("{6}", cg);
            msg = msg.Replace("{7}", ch);
            msg = msg.Replace("{8}", ci);
            msg = msg.Replace("{9}", cj);
            msg = msg.Replace("{10}", ck);
            string write = "[DBG [" + module + "]: " + msg;
            if (dbg)
            {
                string lng = "";
                for (int i = 0; i < msg.Length; i++)
                {
                    lng += " ";
                }
                if (returnChar)
                {
                    Console.Write("{2}\r[DBG [{0}]]: {1}", module, msg, lng);
                }
                else
                {
                    Console.Write("[DBG [{0}]]: {1}", module, msg);
                }
                WriteToLog(write);
            }
        }
        // Forces the log of a debug log
        public void ForceDebugLog(bool returnChar, string module, string msg, string ca = "{0}", string cb = "{1}", string cc = "{2}", string cd = "{3}", string ce = "{4}", string cf = "{5}", string cg = "{6}", string ch = "{7}", string ci = "{8}", string cj = "{9}", string ck = "{10}")
        {
            msg = msg.Replace("{0}", ca);
            msg = msg.Replace("{1}", cb);
            msg = msg.Replace("{2}", cc);
            msg = msg.Replace("{3}", cd);
            msg = msg.Replace("{4}", ce);
            msg = msg.Replace("{5}", cf);
            msg = msg.Replace("{6}", cg);
            msg = msg.Replace("{7}", ch);
            msg = msg.Replace("{8}", ci);
            msg = msg.Replace("{9}", cj);
            msg = msg.Replace("{10}", ck);
            string write = "[FDBG [" + module + "]: " + msg;
            string lng = "";
            for (int i = 0; i < msg.Length; i++)
            {
                lng += " ";
            }
            if (returnChar)
            {
                Console.Write("{2}\r[FDBG [{0}]]: {1}", module, msg, lng);
            }
            else
            {
                Console.Write("[FDBG [{0}]]: {1}", module, msg);
            }
            WriteToLog(write);
        }
        // computes a hash and outputs the result
        public void ComputeSha512Hash(string Input, out string res)
        {
            res = "";
            byte[] resHashInBytes;
            byte[] InpInBytes = new byte[Input.Length];
            for (int i = 0; i < Input.Length; i++)
            {
                InpInBytes[i] = (byte)Input[i];
            }
            SHA256 hasher = SHA256.Create();
            resHashInBytes = hasher.ComputeHash(InpInBytes);
            hasher.Clear();
            for (int i = 0; i < Input.Length; i++)
            {
                res += (char)resHashInBytes[i];
            }
            DebugLog(true, "SECURITYSYS", "Computed hash: {0}\n", res);
        }
        // -m-a-k-e-s- compute a hash and returns the result
        public string ComputeSha512Hash(string Input)
        {
            string res = "";
            byte[] resHashInBytes;
            byte[] InpInBytes = new byte[Input.Length];
            for (int i = 0; i < Input.Length; i++)
            {
                InpInBytes[i] = (byte)Input[i];
            }
            SHA256 hasher = SHA256.Create();
            resHashInBytes = hasher.ComputeHash(InpInBytes);
            hasher.Clear();
            for (int i = 0; i < Input.Length; i++)
            {
                res += (char)resHashInBytes[i];
            }
            DebugLog(true, "SECURITYSYS", "Computed hash: {0}\n", res);
            return res;
        }
        // writes to the log file
        public void WriteToLog(string write, bool throwE = false)
        {
            if (dlc)
                return;
            try
            {
                StreamWriter sw = new StreamWriter(Path.Combine(path, "Log.log"), true);
                sw.WriteLine("[{0}] " + write, DateTime.Now.ToString("yyyy/(hh/HH)/mm/ss/fffffff"));
                sw.Flush();
                sw.Close();
                sw.Dispose();
            }
            catch (Exception e)
            {
                if (!throwE)
                    Console.WriteLine("Failing log system: {0}", e.Message);
                else
                    throw e;
            }
        }
        // A function to generate a session id (sid). used when creating terminal sessions [help virtualterminal] for more info about virtual terminals
        public double GenSessionID()
        {
            double superTempNobodyUses = new Random().NextDouble();
            DebugLog(true, "CORE", "Generated session {0}\n", superTempNobodyUses.ToString());
            if (Program.sessions.Contains(superTempNobodyUses))
            {
                superTempNobodyUses = GenSessionID();
            }
            Program.sessions.Add(superTempNobodyUses);
            return superTempNobodyUses;
        }
        // A function that returns the index of a session id (sid). used when using DelSessionID(int idx)
        public int GetSessionIDIndex(double sid)
        {
            int sidx = Program.sessions.IndexOf(sid);
            return sidx;
        }
        // A function to delete a session id from sessions list using an idx. used when exiting a terminal session
        public void DelSessionID(int idx)
        {
            string sid = Program.sessions[idx].ToString(); ;
            Program.sessions.RemoveAt(idx);
            DebugLog(true, "CORE", "Session id {0} at {1} removed", sid, idx.ToString());
        }
        // A function to delete a session id from sessions list. used when exiting a terminal session
        public void DelSessionID(double sid)
        {
            Program.sessions.Remove(sid);
            DebugLog(true, "CORE", "Session id {0} removed", sid.ToString());
        }
        // Unused function
        public double ClampVal(double min, double max, double val)
        {
            if (val < min)
                val = min;
            if (val > max)
                val = max;
            return val;
        }
        // Unused function
        public long ClampVal(long min, long max, long val)
        {
            if (val < min)
                val = min;
            if (val > max)
                val = max;
            return val;
        }
        // Unused function
        public int ClampVal(int min, int max, int val)
        {
            if (val < min)
                val = min;
            if (val > max)
                val = max;
            return val;
        }
        #endregion;
    }
}
