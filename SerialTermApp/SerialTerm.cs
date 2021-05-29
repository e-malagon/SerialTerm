/*
SerialTerm - Simple serial port utility. Designed to be used with Windows Terminal.

Copyright © 2021 Edgar Malagón Calderón

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Serialization;

namespace SerialTerm
{
    public class SerialTerm
    {
        private readonly SerialPort serialPort;
        private readonly Thread serialPortThread;
        private readonly Dictionary<string, Port> ports;
        private bool running;
        private Port current;

        private readonly string[] BaudRates = new string[]
        {
            "300",
            "600",
            "1200",
            "2400",
            "4800",
            "9600",
            "14400",
            "19200",
            "38400",
            "57600",
            "115200"
        };

        private readonly string[] DataBits = new string[]
        {
            "5",
            "6",
            "7",
            "8"
        };

        private readonly string[] Parities = new string[]
        {
            "None",
            "Odd",
            "Even",
            "Mark",
            "Space"
        };

        private readonly string[] StopBits = new string[]
        {
            "None",
            "One",
            "Two",
            "OnePointFive"
        };

        private readonly string[] Handshakes = new string[]
        {
            "None",
            "XOnXOff",
            "RequestToSend",
            "RequestToSendXOnXOff"
        };

        public SerialTerm()
        {
            running = true;
            serialPort = new SerialPort();
            serialPortThread = new Thread(() =>
            {
                while (running)
                {
                    if (serialPort.IsOpen)
                    {
                        byte[] readBuffer = new byte[serialPort.ReadBufferSize + 1];
                        try
                        {
                            int count = serialPort.Read(readBuffer, 0, serialPort.ReadBufferSize);
                            lock (current)
                            {
                                Console.ForegroundColor = current.Receive;
                                if (current.Text)
                                    Console.Write(Encoding.ASCII.GetString(readBuffer, 0, count));
                                else
                                    Console.Write(BitConverter.ToString(readBuffer, 0, count).Replace("-", " "));
                                Console.ForegroundColor = current.Send;
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        Thread.Sleep(50);
                    }
                }
            });
            serialPortThread.Start();
            current = null;
            ports = new Dictionary<string, Port>();
            LoadConfig();
            foreach (string name in SerialPort.GetPortNames())
            {
                if (current == null)
                {
                    current = new Port(name);
                    ports.Add(name, current);
                }
                else if (!ports.ContainsKey(name))
                    ports.Add(name, new Port(name));
            }
            Console.Title = "Closed";
        }

        public void Run(string[] args)
        {
            Directory.SetCurrentDirectory(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            Console.ForegroundColor = current.Send;
            Console.WriteLine("Type '.help' for a list of commands\n");
            if (0 < args.Length)
                Open(args);
            while (running)
            {
                Console.ForegroundColor = current.Send;
                string line = Console.ReadLine();
                if (line.StartsWith("."))
                    ExecuteCommand(line.TrimEnd());
                else lock (current)
                        Send(line);
            }
            serialPortThread.Join();
        }

        private void LoadConfig()
        {
            string config = Properties.Settings.Default.Ports;
            if (config.Length != 0)
            {
                XmlSerializer serializer = new XmlSerializer(typeof(List<Port>));
                using (TextReader reader = new StringReader(config))
                {
                    List<Port> portsList = (List<Port>)serializer.Deserialize(reader);
                    foreach (Port p in portsList)
                    {
                        if (current == null)
                            current = p;
                        ports.Add(p.Name, p);
                    }
                }
            }
        }

        private void SaveConfig()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(List<Port>));
            using (MemoryStream ms = new MemoryStream())
            {
                List<Port> portList = new List<Port>();
                foreach (KeyValuePair<string, Port> port in ports)
                    portList.Add(port.Value);

                serializer.Serialize(ms, portList);
                ms.Position = 0;
                using (var reader = new StreamReader(ms))
                {
                    Properties.Settings.Default.Ports = reader.ReadToEnd();
                    Properties.Settings.Default.Save();
                }
            }
        }

        private void ExecuteCommand(string line)
        {
            string[] words = line.Split(' ');
            if (words.Length != 0)
            {
                switch (words[0].ToLower())
                {
                    case ".open":
                        if (words.Length == 1)
                        {
                            lock (current)
                                Open();
                        }
                        else
                        {
                            lock (current)
                                Open(line.Replace(".open", "").Trim().Split(' '));
                        }
                        break;
                    case ".close":
                        Close();
                        break;
                    case ".send":
                        SendFile(line);
                        break;
                    case ".help":
                        PrintHelp();
                        break;
                    case ".hex":
                    case ".bin":
                        lock (current)
                            current.Text = false;
                        SaveConfig();
                        break;
                    case ".asc":
                    case ".text":
                        lock (current)
                            current.Text = true;
                        SaveConfig();
                        break;
                    case ".color":
                        lock (current)
                            ChangeColor(words);
                        break;
                    case ".exit":
                        running = false;
                        SaveConfig();
                        break;
                }
            }
        }

        private void Open()
        {
            serialPort.Close();
            Console.Title = "Closed";

            Console.ForegroundColor = current.Send;
            string[] names = SerialPort.GetPortNames();
            if (names.Length == 0)
            {
                Console.WriteLine("No COM port detected");
                return;
            }

            foreach (string name in names)
            {
                if (current == null)
                {
                    current = new Port(name);
                    ports.Add(name, current);
                }
                else if (!ports.ContainsKey(name))
                    ports.Add(name, new Port(name));
            }

            bool ok;
            do
            {
                ok = false;
                int i = 1;
                Console.WriteLine("");
                foreach (string name in names)
                    Console.WriteLine("    " + i++ + "\t" + name);

                Console.Write("Port [" + current.Name + "]> ");
                string readedPort = Console.ReadLine().ToUpper();
                if (readedPort.Length == 0)
                    readedPort = current.Name;
                else if (int.TryParse(readedPort, out int number) && number < names.Length)
                    readedPort = names[number - 1];

                foreach (string name in names)
                {
                    if (readedPort.Equals(name))
                    {
                        ok = ports.TryGetValue(name, out current);
                        if (ok)
                        {
                            serialPort.PortName = name;
                            break;
                        }
                    }
                }
            } while (!ok);

            do
            {
                ok = false;
                int i = 1;
                Console.WriteLine("");
                foreach (string baudRate in BaudRates)
                    Console.WriteLine("    " + i++ + "\t" + baudRate);

                Console.Write("Baud rate [" + current.BaudRate + "]> ");
                string readedBaudRate = Console.ReadLine().ToUpper();
                if (readedBaudRate.Length == 0)
                    readedBaudRate = current.BaudRate;
                else if (int.TryParse(readedBaudRate, out int number) && number < 20)
                    readedBaudRate = BaudRates[number - 1];

                foreach (string baudRate in BaudRates)
                {
                    if (readedBaudRate.Equals(baudRate))
                    {
                        ok = Int32.TryParse(baudRate, out Int32 b);
                        if (ok)
                        {
                            current.BaudRate = baudRate;
                            serialPort.BaudRate = b;
                            break;
                        }
                    }
                }
            } while (!ok);

            do
            {
                ok = false;
                Console.WriteLine("");
                foreach (string dataBits in DataBits)
                    Console.WriteLine("\t" + dataBits);

                Console.Write("Data bits [" + current.DataBits + "]> ");
                string readedDataBits = Console.ReadLine().ToUpper();
                if (readedDataBits.Length == 0)
                    readedDataBits = current.DataBits;
                else if (int.TryParse(readedDataBits, out int number) && number < DataBits.Length)
                    readedDataBits = DataBits[number - 1];

                foreach (string dataBits in DataBits)
                {
                    if (readedDataBits.Equals(dataBits))
                    {
                        ok = Int32.TryParse(dataBits, out Int32 d);
                        if (ok)
                        {
                            current.DataBits = dataBits;
                            serialPort.DataBits = d;
                            break;
                        }
                    }
                }
            } while (!ok);

            do
            {
                ok = false;
                int i = 1;
                Console.WriteLine("");
                foreach (string parity in Parities)
                    Console.WriteLine("    " + i++ + "\t" + parity);

                Console.Write("Parity [" + current.Parity + "]> ");
                string readedParity = Console.ReadLine().ToUpper();
                if (readedParity.Length == 0)
                    readedParity = current.Parity;
                else if (int.TryParse(readedParity, out int number) && number < Parities.Length)
                    readedParity = Parities[number - 1];

                foreach (string parity in Parities)
                {
                    if (readedParity.ToUpper().Equals(parity.ToUpper()))
                    {
                        ok = Enum.TryParse<Parity>(parity, out Parity p);
                        if (ok)
                        {
                            current.Parity = parity;
                            serialPort.Parity = p;
                            break;
                        }
                    }
                }
            } while (!ok);

            do
            {
                ok = false;
                try
                {
                    int i = 1;
                    Console.WriteLine("");
                    foreach (string stopBits in StopBits)
                        Console.WriteLine("    " + i++ + "\t" + stopBits);

                    Console.Write("Stop bits [" + current.StopBits + "]> ");
                    string readedStopBits = Console.ReadLine().ToUpper();
                    if (readedStopBits.Length == 0)
                        readedStopBits = current.StopBits;
                    else if (int.TryParse(readedStopBits, out int number) && number < StopBits.Length)
                        readedStopBits = StopBits[number - 1];

                    foreach (string stopBits in StopBits)
                    {
                        if (readedStopBits.ToUpper().Equals(stopBits.ToUpper()))
                        {
                            ok = Enum.TryParse<StopBits>(stopBits, out StopBits s);
                            if (ok)
                            {
                                current.StopBits = stopBits;
                                serialPort.StopBits = s;
                                break;
                            }
                        }
                    }
                }
                catch { }
            } while (!ok);

            do
            {
                ok = false;
                try
                {
                    int i = 1;
                    Console.WriteLine("");
                    foreach (string handshake in Handshakes)
                        Console.WriteLine("    " + i++ + "\t" + handshake);

                    Console.Write("Handshake [" + current.Handshake + "]> ");
                    string readedHandshake = Console.ReadLine().ToUpper();
                    if (readedHandshake.Length == 0)
                        readedHandshake = current.Handshake;
                    else if (int.TryParse(readedHandshake, out int number) && number < Handshakes.Length)
                        readedHandshake = Handshakes[number - 1];

                    foreach (string handshake in Handshakes)
                    {
                        if (readedHandshake.ToUpper().Equals(handshake.ToUpper()))
                        {
                            ok = Enum.TryParse<Handshake>(handshake, out Handshake h);
                            if (ok)
                            {
                                current.Handshake = handshake;
                                serialPort.Handshake = h;
                                break;
                            }
                        }
                    }
                }
                catch { }
            } while (!ok);

            serialPort.ReadTimeout = 1000;
            serialPort.WriteTimeout = 1000;
            try
            {
                serialPort.Open();
                Console.Title = current.ToString();
                SaveConfig();
                Console.WriteLine("Connected...");
                Console.WriteLine("");

            }
            catch (Exception e) { Console.WriteLine(e.Message); }
        }

        private void Open(string[] words)
        {
            serialPort.Close();

            Console.Title = "Closed";

            Console.ForegroundColor = current.Send;
            string[] names = SerialPort.GetPortNames();
            if (names.Length == 0)
            {
                Console.WriteLine("No COM port detected");
                return;
            }

            foreach (string name in names)
            {
                if (current == null)
                {
                    current = new Port(name);
                    ports.Add(name, current);
                }
                else if (!ports.ContainsKey(name))
                    ports.Add(name, new Port(name));
            }

            if (0 < words.Length)
            {
                bool found = false;
                foreach (string name in names)
                {
                    if (words[0].ToUpper().Equals(name))
                    {
                        found = ports.TryGetValue(name, out current);
                        if (found)
                        {
                            serialPort.PortName = name;
                            break;
                        }
                        break;
                    }
                }
                if (!found)
                {
                    Console.WriteLine("Unknown port " + words[0].ToUpper());
                    return;
                }
            }
            else
                return;

            if (1 < words.Length)
            {
                bool found = false;
                foreach (string baudRate in BaudRates)
                {
                    if (words[1].ToUpper().Equals(baudRate))
                    {
                        found = Int32.TryParse(baudRate, out Int32 b);
                        if (found)
                        {
                            current.BaudRate = baudRate;
                            serialPort.BaudRate = b;
                            break;
                        }
                    }
                }
                if (!found)
                {
                    Console.WriteLine("Invali baud rate value " + words[1].ToUpper() + " [300, 600, 1200, 2400, 4800, 9600, 14400, 19200, 38400, 57600, 115200]");
                    return;
                }
            }
            else
            {
                if (Int32.TryParse(current.BaudRate, out Int32 b))
                    serialPort.BaudRate = b;
            }

            if (2 < words.Length)
            {
                bool found = false;
                foreach (string dataBits in DataBits)
                {
                    if (words[2].ToUpper().Equals(dataBits))
                    {
                        found = Int32.TryParse(dataBits, out Int32 d);
                        if (found)
                        {
                            current.DataBits = dataBits;
                            serialPort.DataBits = d;
                            break;
                        }
                    }
                }
                if (!found)
                {
                    Console.WriteLine("Invali data bits value " + words[2].ToUpper() + " [5, 6, 7, 8]");
                    return;
                }
            }
            else
            {
                if (Int32.TryParse(current.DataBits, out Int32 d))
                    serialPort.DataBits = d;
            }

            if (3 < words.Length)
            {
                bool found = false;
                foreach (string parity in Parities)
                {
                    if (words[3].ToUpper().Equals(parity.ToUpper()))
                    {
                        found = Enum.TryParse<Parity>(parity, out Parity p);
                        if (found)
                        {
                            current.Parity = parity;
                            serialPort.Parity = p;
                            break;
                        }
                    }
                }
                if (!found)
                {
                    Console.WriteLine("Invali parity value " + words[3] + " [None, Odd, Even, Mark, Space]");
                    return;
                }
            }
            else
            {
                if (Enum.TryParse<Parity>(current.Parity, out Parity p))
                    serialPort.Parity = p;
            }

            if (4 < words.Length)
            {
                bool found = false;
                foreach (string stopBits in StopBits)
                {
                    if (words[4].ToUpper().Equals(stopBits.ToUpper()))
                    {
                        found = Enum.TryParse<StopBits>(stopBits, out StopBits s);
                        if (found)
                        {
                            current.StopBits = stopBits;
                            serialPort.StopBits = s;
                            break;
                        }
                    }
                }
                if (!found)
                {
                    Console.WriteLine("Invali stop bits value " + words[4] + " [None, One, Two, OnePointFive]");
                    return;
                }
            }
            else
            {
                if (Enum.TryParse<StopBits>(current.StopBits, out StopBits s))
                    serialPort.StopBits = s;
            }

            if (5 < words.Length)
            {
                bool found = false;
                foreach (string handshake in Handshakes)
                {
                    if (words[5].ToUpper().Equals(handshake.ToUpper()))
                    {
                        found = Enum.TryParse<Handshake>(handshake, out Handshake h);
                        if (found)
                        {
                            current.Handshake = handshake;
                            serialPort.Handshake = h;
                            break;
                        }
                    }
                }
                if (!found)
                {
                    Console.WriteLine("Invali handshake value " + words[5] + " [None, XOnXOff, RequestToSend, RequestToSendXOnXOff]");
                    return;
                }
            }
            else
            {
                if (Enum.TryParse<Handshake>(current.Handshake, out Handshake h))
                    serialPort.Handshake = h;
            }

            serialPort.ReadTimeout = 1000;
            serialPort.WriteTimeout = 1000;
            try
            {
                serialPort.Open();
                Console.Title = current.ToString();
                SaveConfig();
                Console.WriteLine("Connected...");
                Console.WriteLine("");
            }
            catch (Exception e) { Console.WriteLine(e.Message); }
        }

        private void Close()
        {
            try
            {
                serialPort.Close();
            }
            catch (Exception e) { Console.WriteLine(e.Message); }

            Console.Title = "Closed";
        }

        private void Send(string line)
        {
            if (serialPort.IsOpen)
            {
                if (current.Text)
                {
                    try
                    {
                        line = Regex.Unescape(line);
                        serialPort.Write(line);
                    }
                    catch (Exception e) { Console.WriteLine(e.Message); }
                }
                else
                {
                    int i = 0;
                    byte[] readBuffer = new byte[line.Length / 2];
                    try
                    {
                        for (int j = 0; i < line.Length; i += 2, j++)
                        {
                            string hs = line.Substring(i, 2);
                            readBuffer[j] = (byte)Convert.ToUInt16(hs, 16);
                        }
                        try
                        {
                            serialPort.Write(readBuffer, 0, line.Length / 2);
                        }
                        catch (Exception e) { Console.WriteLine(e.Message); }
                    }
                    catch { Console.WriteLine("Invalid hex string on index " + i); }

                }
            }
        }

        private void SendFile(string line)
        {
            try
            {
                if (serialPort.IsOpen)
                {
                    line = line.Replace(".send", "").Replace('"', ' ').Trim();
                    using (BinaryReader reader = new BinaryReader(File.Open(line, FileMode.Open)))
                    {
                        int sent = 0;
                        byte[] readed;
                        do
                        {
                            readed = reader.ReadBytes(1024);
                            serialPort.Write(readed, 0, readed.Length);
                            sent += readed.Length;
                        } while (readed.Length != 0);
                        Console.WriteLine("" + sent + " bytes sent");
                    }
                }
            }
            catch (Exception e) { Console.WriteLine(e.Message); }
        }

        public void ChangeColor(string[] words)
        {
            if (1 < words.Length)
            {
                switch (words[1].ToLower())
                {
                    case "black":
                        current.Receive = ConsoleColor.Black;
                        break;
                    case "darkblue":
                        current.Receive = ConsoleColor.DarkBlue;
                        break;
                    case "darkgreen":
                        current.Receive = ConsoleColor.DarkGreen;
                        break;
                    case "darkcyan":
                        current.Receive = ConsoleColor.DarkCyan;
                        break;
                    case "darkred":
                        current.Receive = ConsoleColor.DarkRed;
                        break;
                    case "darkmagenta":
                        current.Receive = ConsoleColor.DarkMagenta;
                        break;
                    case "darkyellow":
                        current.Receive = ConsoleColor.DarkYellow;
                        break;
                    case "gray":
                        current.Receive = ConsoleColor.Gray;
                        break;
                    case "darkgray":
                        current.Receive = ConsoleColor.DarkGray;
                        break;
                    case "blue":
                        current.Receive = ConsoleColor.Blue;
                        break;
                    case "green":
                        current.Receive = ConsoleColor.Green;
                        break;
                    case "cyan":
                        current.Receive = ConsoleColor.Cyan;
                        break;
                    case "red":
                        current.Receive = ConsoleColor.Red;
                        break;
                    case "magenta":
                        current.Receive = ConsoleColor.Magenta;
                        break;
                    case "yellow":
                        current.Receive = ConsoleColor.Yellow;
                        break;
                    case "white":
                        current.Receive = ConsoleColor.White;
                        break;

                }
            }
            if (2 < words.Length)
            {
                switch (words[2].ToLower())
                {
                    case "black":
                        current.Send = ConsoleColor.Black;
                        break;
                    case "darkblue":
                        current.Send = ConsoleColor.DarkBlue;
                        break;
                    case "darkgreen":
                        current.Send = ConsoleColor.DarkGreen;
                        break;
                    case "darkcyan":
                        current.Send = ConsoleColor.DarkCyan;
                        break;
                    case "darkred":
                        current.Send = ConsoleColor.DarkRed;
                        break;
                    case "darkmagenta":
                        current.Send = ConsoleColor.DarkMagenta;
                        break;
                    case "darkyellow":
                        current.Send = ConsoleColor.DarkYellow;
                        break;
                    case "gray":
                        current.Send = ConsoleColor.Gray;
                        break;
                    case "darkgray":
                        current.Send = ConsoleColor.DarkGray;
                        break;
                    case "blue":
                        current.Send = ConsoleColor.Blue;
                        break;
                    case "green":
                        current.Send = ConsoleColor.Green;
                        break;
                    case "cyan":
                        current.Send = ConsoleColor.Cyan;
                        break;
                    case "red":
                        current.Send = ConsoleColor.Red;
                        break;
                    case "magenta":
                        current.Send = ConsoleColor.Magenta;
                        break;
                    case "yellow":
                        current.Send = ConsoleColor.Yellow;
                        break;
                    case "white":
                        current.Send = ConsoleColor.White;
                        break;
                }
            }
            SaveConfig();
        }


        static public void PrintHelp()
        {
            Console.WriteLine("Simple serial port utility. Designed to be used with Windows Terminal.");
            Console.WriteLine("Version 1.0.");
            Console.WriteLine("");
            Console.WriteLine("SerialTerm.exe [Port][Baud rate][Data bits][Parity][Stop bits][Handshake]");
            Console.WriteLine("");
            Console.WriteLine("Supported values");
            Console.WriteLine("  Baud rate: 300, 600, 1200, 2400, 4800, 9600*, 14400, 19200, 38400, 57600, 115200");
            Console.WriteLine("  Data bits: 5, 6, 7, 8*");
            Console.WriteLine("  Parity: None*, Odd, Even, Mark, Space");
            Console.WriteLine("  Stop bits: None, One*, Two, OnePointFive");
            Console.WriteLine("  Handshake: None*, XOnXOff, RequestToSend, RequestToSendXOnXOff");
            Console.WriteLine("");
            Console.WriteLine("  *Default values");
            Console.WriteLine("");
            Console.WriteLine("Interactive commands:");
            Console.WriteLine(".open [Port][Baud rate][Data bits][Parity][Stop bits][Handshake]");
            Console.WriteLine("  Open a new connection using provided values");
            Console.WriteLine("");
            Console.WriteLine(".close");
            Console.WriteLine("  Close current connection");
            Console.WriteLine("");
            Console.WriteLine(".send filename");
            Console.WriteLine("  Send a file");
            Console.WriteLine("");
            Console.WriteLine(".hex|.bin");
            Console.WriteLine("  Input/output in binary hexadecimal mode");
            Console.WriteLine("");
            Console.WriteLine(".asc|.text");
            Console.WriteLine("  Input/output in ASCII mode");
            Console.WriteLine("");
            Console.WriteLine(".color [received text color] [send text color]");
            Console.WriteLine("  Change color of send/received text available colors Black, DarkBlue, DarkGreen, DarkCyan, DarkRed, DarkMagenta, DarkYellow,");
            Console.WriteLine("  Gray, DarkGray, Blue, Green, Cyan, Red, Magenta, Yellow, White");
            Console.WriteLine("");
            Console.WriteLine(".exit");
            Console.WriteLine("  Exit from SerialTerm");
            Console.WriteLine("");
            Console.WriteLine(".help");
            Console.WriteLine("  Print this help");
            Console.WriteLine("");
            Console.WriteLine("Usage: ");
            Console.WriteLine("");
            Console.WriteLine("To send some data, type it followed by an 'Enter'.");
            Console.WriteLine("Use escape characters to send any special character like '\\n'(LF) or '\\r'(CR).");
            Console.WriteLine("To send data that starts with a dot scape it '\\.'.");
            Console.WriteLine("");
            Console.WriteLine("If you want to send some binary data, switch to hex/bin mode then you can type it in Hex formar like:");
            Console.WriteLine("");
            Console.WriteLine("900391239900");
            Console.WriteLine("");
        }

        static void Main(string[] args)
        {
            if (1 < args.Length && (args[0].Equals("/?") || args[0].ToUpper().Equals("/HELP")))
            {
                SerialTerm.PrintHelp();
            }
            else
            {
                SerialTerm serialTerm = new SerialTerm();
                serialTerm.Run(args);

            }
        }
    }

    public class Port
    {
        public Port() { }
        public Port(string name)
        {
            Name = name;
            BaudRate = "9600";
            DataBits = "8";
            Parity = "None";
            StopBits = "One";
            Handshake = "None";
            Text = true;
            Send = ConsoleColor.Gray;
            Receive = ConsoleColor.White;
        }

        public string Name { get; set; }
        public string BaudRate { get; set; }
        public string DataBits { get; set; }
        public string Parity { get; set; }
        public string StopBits { get; set; }
        public string Handshake { get; set; }
        public bool Text { get; set; }
        public ConsoleColor Send { get; set; }
        public ConsoleColor Receive { get; set; }

        public override string ToString()
        {
            Enum.TryParse<StopBits>(StopBits, out StopBits s);
            string title = Name;
            title += " " + BaudRate;
            title += " " + DataBits;
            title += Parity[0];
            title += (int)s;
            title += " " + Handshake;
            return title;
        }
    }
}
