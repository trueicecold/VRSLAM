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
            // Window title declared here for visibility
            string windowTitle = "VSLAM";

            // Creating a new PhotinoWindow instance with the fluent API
            Shared.Window = new PhotinoWindow()
                .SetWebSecurityEnabled(false)
                .SetTitle(windowTitle)
                .SetSize(new Size(800, 600))
                .Center()
                .SetResizable(true)
                .SetMaxSize(1600, 900)
                .SetMinSize(800, 600);

            Shared.Window.RegisterCustomSchemeHandler("html", (object sender, string scheme, string url, out string contentType) =>
            {
                contentType = "text/html";
                if (File.Exists(url.Replace("html://", "wwwroot/")))
                {
                    return new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(url.Replace("html://", "wwwroot/"))));
                }
                else
                {
                    return new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText("wwwroot/pages/home.html")));
                }
            });

            Shared.Window.RegisterCustomSchemeHandler("js", (object sender, string scheme, string url, out string contentType) =>
            {
                contentType = "text/javascript";
                if (File.Exists(url.Replace("js://", "wwwroot/")))
                {
                    return new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(url.Replace("js://", "wwwroot/"))));
                }
                else
                {
                    return new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText("wwwroot/assets/scripts/pages/home.js")));
                }
            });

            Shared.Window.RegisterCustomSchemeHandler("css", (object sender, string scheme, string url, out string contentType) =>
            {
                contentType = "text/css";
                if (File.Exists(url.Replace("css://", "wwwroot/")))
                {
                    return new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(url.Replace("css://", "wwwroot/"))));
                }
                else
                {
                    return new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText("wwwroot/assets/styles/pages/home.css")));
                }
            });

            Shared.Window.Load("wwwroot/index.html"); // Can be used with relative path strings or "new URI()" instance to load a website.

            // Init managers interop handlers
            DependencyManager.InitHandlers();
            APKManager.InitHandlers();
            ADBManager.InitHandlers();
            RCloneManager.InitHandlers();

            Shared.Window.WaitForClose(); // Starts the application event loop
        }
    }
}
