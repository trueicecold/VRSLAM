using Photino.NET;
using System.Drawing;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.IO.Compression;
using VRSLAM.Libs;
using VRSLAM.Managers;

namespace VRSLAM
{
    //NOTE: To hide the console window, go to the project properties and change the Output Type to Windows Application.
    // Or edit the .csproj file and change the <OutputType> tag from "WinExe" to "Exe".
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ADBManager.Start(); // Start the ADB manager to monitor devices

            // Window title declared here for visibility
            string windowTitle = "VSLAM";

            // Creating a new PhotinoWindow instance with the fluent API
            Shared.Window = new PhotinoWindow()
                .SetTitle(windowTitle)
                .SetSize(new Size(800, 600))
                .Center()
                .SetResizable(true)
                .SetMaxSize(1600, 900)
                .SetMinSize(800, 600);

            Shared.Window.RegisterWebMessageReceivedHandler((object sender, string messageStr) =>
                {
                    dynamic message = JSON.Parse(messageStr);

                    switch (message.action.ToString())
                    {
                        case "download_dependencies":
                            DependencyDownloader.Download();
                            break;
                        case "choose_apk":
                            string[] files = Shared.Window.ShowOpenFile(
                                title: "Open a file",
                                defaultPath: Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                multiSelect: false,
                                filters: [
                                    ("APK Files", new [] {"*.apk"})
                                ]
                            );
                            Shared.Window.SendWebMessage(JSON.Stringify(new {
                                type = "choose_apk",
                                files = files
                            }));
                            break;
                        case "fix_apk":
                            APKFixer.Fix(message.filePath.ToString());
                            break;
                        default:
                            break;
                    }
                })
                .Load("wwwroot/index.html"); // Can be used with relative path strings or "new URI()" instance to load a website.
                Shared.Window.WaitForClose(); // Starts the application event loop
        }
    }

    class Utils {

    }
}
