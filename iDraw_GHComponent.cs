using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;

namespace iDraw_GH
{
    public class iDraw_GHComponent : GH_Component
    {
        private bool hasEverRun = false; // Stupid grasshopper button stuff...
        private string lastMessage = "Idle. Click the Run button to start.";

        // Keep track of a temporary path for the extracted stream.py
        private string extractedStreamPyPath = string.Empty;

        // Store the last used G-code file path, for the output.
        private string gcodeFilePath = string.Empty;

        // Cached port for iDraw
        private string cachedPort;

        // ASCII art to show in the Python console
        private readonly string[] asciiArt = new string[]
        {
            "                                                              ",
            "██╗██████╗ ██████╗  █████╗ ██╗    ██╗         ██████╗ ██╗  ██╗",
            "██║██╔══██╗██╔══██╗██╔══██╗██║    ██║        ██╔════╝ ██║  ██║",
            "██║██║  ██║██████╔╝███████║██║ █╗ ██║        ██║  ███╗███████║",
            "██║██║  ██║██╔══██╗██╔══██║██║███╗██║        ██║   ██║██╔══██║",
            "██║██████╔╝██║  ██║██║  ██║╚███╔███╔╝███████╗╚██████╔╝██║  ██║",
            "╚═╝╚═════╝ ╚═╝  ╚═╝╚═╝  ╚═╝ ╚══╝╚══╝ ╚══════╝ ╚═════╝ ╚═╝  ╚═╝",
            "                                                              ",
            "Authored by James Dalessandro.",
            "                                                              ",
        };

        public iDraw_GHComponent()
          : base("iDraw_GH", "iDraw_GH",
            "Stream GRBL and G-code commands to iDraw pen plotters.",
            "Extra", "iDraw_GH")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run", "R", "Connect a button here. Click to run.", GH_ParamAccess.item);
            pManager.AddTextParameter("Identifier", "I", "The text to identify the connected plotter with. For iDraw use \"DrawCore\", for DrawBot use \"DRAWBOT\". For other GRBL plotters, connect to your plotter through Universal Gcode Sender and send \"$I\", then pick out an identifiable part of the response.", GH_ParamAccess.item);
            pManager.AddTextParameter("Commands", "C", "The list of GRBL and G-code commands to stream to your iDraw. I recommend always starting with $H, and ending with $H and $SLP.", GH_ParamAccess.list);
            pManager.AddTextParameter("G-code Folder", "G", "The directory where G-code files should be saved.", GH_ParamAccess.item);
            pManager.AddTextParameter("Python Path", "P", "File location of your 3.11+ Python.exe.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Message", "M", "This will tell you where the last G-code file was saved.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool run = false;
            DA.GetData(0, ref run);

            string id = string.Empty;
            DA.GetData(1, ref id);

            List<string> commands = new List<string>();
            DA.GetDataList(2, commands);

            string gcodeDir = string.Empty;
            DA.GetData(3, ref gcodeDir);

            string pyPath = string.Empty;
            DA.GetData(4, ref pyPath);

            // If "Run" is false, either show "Idle" if we've never run, or show the last message
            if (!run)
            {
                if (!hasEverRun)
                {
                    // We've never run before, so it's indeed idle
                    DA.SetData(0, "Idle. Click the Run button to start.");
                }
                else
                {
                    // We've already run at least once, so preserve the last message
                    DA.SetData(0, lastMessage);
                }
                return;
            }

            //validate identifier string
            if (string.IsNullOrWhiteSpace(id))
            {
                lastMessage = "Please supply a valid identifier string.";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, lastMessage);
                DA.SetData(0, lastMessage);
                return;
            }

            // Validate Python path
            if (!File.Exists(pyPath))
            {
                lastMessage = $"Cannot find Python at: {pyPath}";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, lastMessage);
                DA.SetData(0, lastMessage);
                return;
            }

            // Validate G-code folder
            if (string.IsNullOrWhiteSpace(gcodeDir))
            {
                lastMessage = "Please supply a valid G-code folder path.";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, lastMessage);
                DA.SetData(0, lastMessage);
                return;
            }
            if (!Directory.Exists(gcodeDir))
            {
                try
                {
                    Directory.CreateDirectory(gcodeDir);
                }
                catch (Exception ex)
                {
                    lastMessage = $"Cannot create directory: {ex.Message}";
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, lastMessage);
                    DA.SetData(0, lastMessage);
                    return;
                }
            }

            // Extract the embedded stream.py if we haven't already
            try
            {
                if (string.IsNullOrEmpty(extractedStreamPyPath) || !File.Exists(extractedStreamPyPath))
                {
                    extractedStreamPyPath = ExtractEmbeddedStreamPy();
                }
            }
            catch (Exception ex)
            {
                lastMessage = $"Failed to extract stream.py: {ex.Message}";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, lastMessage);
                DA.SetData(0, lastMessage);
                return;
            }

            // Find the iDraw port if needed
            if (cachedPort == null)
            {
                cachedPort = FindiDrawPort(id);
                if (cachedPort == null)
                {
                    lastMessage = "No plotter found. Are you sure it's connected or that you input the correct identifier?";
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, lastMessage);
                    DA.SetData(0, lastMessage);
                    return;
                }
            }

            // Write G-code commands to a file
            try
            {
                // Use a time-based name
                string fileName = DateTime.Now.ToString("yyyyMMddHHmmss") + ".gcode";
                gcodeFilePath = Path.Combine(gcodeDir, fileName);

                // Write a comment line and then append all commands
                File.WriteAllText(gcodeFilePath, ";Created with iDraw_GH by James Dalessandro\n");
                File.AppendAllLines(gcodeFilePath, commands);
            }
            catch (Exception ex)
            {
                lastMessage = "Error writing G-code file: " + ex.Message;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, lastMessage);
                DA.SetData(0, lastMessage);
                return;
            }

            // Stream the G-code via Python
            try
            {
                GRBLPythonWrapper pyWrapper = new GRBLPythonWrapper(pyPath, extractedStreamPyPath);
                pyWrapper.RunPythonStreamInteractive(gcodeFilePath, cachedPort, asciiArt);

                // Update lastMessage so it persists even after "Run" is false again
                lastMessage = $"G-code saved to: {gcodeFilePath}";
                hasEverRun = true; //we have now run at least once
                DA.SetData(0, lastMessage);
            }
            catch (Exception ex)
            {
                lastMessage = ex.Message;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, lastMessage);
                DA.SetData(0, lastMessage);
            }
        }

        private string ExtractEmbeddedStreamPy()
        {
            // We'll drop stream.py into the user's temp directory
            string tempDir = Path.GetTempPath();
            string outPath = Path.Combine(tempDir, "stream.py");

            string resourceName = "iDraw_GH.stream.py";

            var assembly = Assembly.GetExecutingAssembly();
            using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
            {
                if (resourceStream == null)
                    throw new FileNotFoundException($"Embedded resource not found: {resourceName}");

                using (FileStream fileStream = new FileStream(outPath, FileMode.Create, FileAccess.Write))
                {
                    resourceStream.CopyTo(fileStream);
                }
            }

            return outPath;
        }

        // ---------------------------------------------------------------
        // 4) iDraw Port Detection automagically
        private string FindiDrawPort(string id)
        {
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                string response = HandleSerialCommunication(serial =>
                {
                    serial.WriteLine("$I");
                    System.Threading.Thread.Sleep(200);
                    return serial.ReadExisting();
                }, port);

                if (!string.IsNullOrEmpty(response) &&
                    (response.Contains(id)))
                {
                    return port; // Found a compatible GRBL device
                }

            }

            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No iDraw found.");
            return null;
        }

        // Wrapper for serial usage
        private string HandleSerialCommunication(Func<SerialPort, string> action, string portName)
        {
            try
            {
                using (SerialPort serial = new SerialPort(portName, 115200))
                {
                    serial.NewLine = "\r\n";
                    serial.ReadTimeout = 1000;
                    serial.WriteTimeout = 1000;
                    serial.Open();

                    string result = action(serial);
                    serial.Close();
                    return result;
                }
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Error: " + ex.Message);
                return null;
            }
        }

        // ---------------------------------------------------------------
        // 5) Inner GRBLPythonWrapper Class
        public class GRBLPythonWrapper
        {
            private readonly string _pyPath;
            private readonly string _streamPath;

            public GRBLPythonWrapper(string pyPath, string streamPath)
            {
                _pyPath = pyPath;
                _streamPath = streamPath;
            }

            public void RunPythonStreamInteractive(string gcodeFilePath, string devicePort, string[] asciiArt)
            {
                try
                {
                    // Prepare arguments for Python script
                    string arguments = $"\"{_streamPath}\" \"{gcodeFilePath}\" \"{devicePort}\"";

                    // Convert ASCII art to echo commands, hell yeah
                    string asciiArtCommands = string.Join(" && ", asciiArt.Select(line =>
                        string.IsNullOrWhiteSpace(line) ? "echo." : "echo " + EscapeForCmd(line)));

                    // Full command line
                    string cmdArguments =
                        $"/K \"title PLOTTING! Closing this window will stop the plot! && " +
                        $"{asciiArtCommands} && {_pyPath} {arguments}\"";

                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = cmdArguments,
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        RedirectStandardOutput = false,
                        RedirectStandardError = false
                    };

                    Process.Start(startInfo);
                }
                catch (Exception ex)
                {
                    throw new Exception("Error launching Python script in interactive mode: " + ex.Message);
                }
            }

            private string EscapeForCmd(string input)
            {
                return input.Replace("&", "^&")
                            .Replace("|", "^|")
                            .Replace(">", "^>")
                            .Replace("<", "^<");
            }
        }

        // Icon
        protected override System.Drawing.Bitmap Icon => Properties.Resources.idrawGHicon;

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("3CF3551B-1B59-4A0A-A83F-AA6EBA5FF818");
    }
}