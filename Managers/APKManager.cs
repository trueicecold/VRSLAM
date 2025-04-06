using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VRSLAM.Libs;

namespace VRSLAM.Managers
{
    public class APKManager: Handler
    {
        public static void InitHandlers() {
            Shared.Window.RegisterWebMessageReceivedHandler((object sender, string messageStr) =>
                {
                    dynamic message = JSON.Parse(messageStr);

                    switch (message.action.ToString())
                    {
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
                        case "rename_apk":
                            APKFixer.Rename(message.filePath.ToString());
                            break;
                        default:
                            break;
                    }
                });
        }
    }
}