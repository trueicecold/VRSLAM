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
            bool isInited = false;
            // Window title declared here for visibility
            string windowTitle = "VSLAM";

            // Creating a new PhotinoWindow instance with the fluent API
            Shared.Window = new PhotinoWindow()
                .SetWebSecurityEnabled(false)
                .SetFileSystemAccessEnabled(true)
                .SetTitle(windowTitle)
                .SetSize(new Size(1600, 600))
                .Center()
                .SetResizable(true)
                .SetMaxSize(1600, 900)
                .SetMinSize(1600, 600);

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

            Shared.Window.RegisterWebMessageReceivedHandler((object sender, string messageStr) =>
                {
                    dynamic message = JSON.Parse(messageStr);
                    string htmlContent = string.Empty;
                    string jsContent = string.Empty;
                    string cssContent = string.Empty;

                    switch (message.action.ToString()) {
                        case "init_managers":
                            if (isInited) return;
                            isInited = true;
                            // Init managers interop handlers
                            DependencyManager.InitHandlers();
                            APKManager.InitHandlers();
                            ADBManager.InitHandlers();
                            RCloneManager.InitHandlers();
                            FileManager.InitHandlers();
                            break;
                        case "page_files":
                            if (File.Exists("wwwroot/pages/" + message.pageName + ".html"))
                            {
                                htmlContent = File.ReadAllText("wwwroot/pages/" + message.pageName + ".html");
                            }
                            else
                            {
                                htmlContent = "Page Not Found";
                            }
                            if (File.Exists("wwwroot/assets/scripts/pages/" + message.pageName + ".js"))
                            {
                                jsContent = File.ReadAllText("wwwroot/assets/scripts/pages/" + message.pageName + ".js");
                            }
                            else
                            {
                                htmlContent = "";
                            }
                            if (File.Exists("wwwroot/assets/styles/pages/" + message.pageName + ".css"))
                            {
                                cssContent = File.ReadAllText("wwwroot/assets/styles/pages/" + message.pageName + ".css");
                            }
                            else
                            {
                                cssContent = "";
                            }
                            Shared.Window.SendWebMessage(JSON.Stringify(new {
                                type = "page_files",
                                pageName = message.pageName,
                                html = htmlContent,
                                js = jsContent,
                                css = cssContent
                            }));
                            break;

                    }
                });

            Shared.Window.WaitForClose(); // Starts the application event loop
        }
    }
}
