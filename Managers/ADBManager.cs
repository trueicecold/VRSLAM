using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VRSLAM.Libs;

namespace VRSLAM.Managers
{
    public class ADBManager
    {
        static string DeviceID = null;
        static Dictionary<string, object> DeviceInfo = null;
        public static void Start() {
            Shared.AdbToolkit = new AdbToolkit(AppPath.PLATFORM_TOOLS_URL + "/adb", true, true);
            Shared.AdbToolkit.DeviceConnected += (sender, device) => {
                DeviceID = device.DeviceId;
                DeviceInfo = device.DeviceInfo;
            };

            Shared.AdbToolkit.DeviceDisconnected += (sender, device) => {
                if (device.DeviceId == DeviceID) {
                    DeviceID = null;
                    DeviceInfo = null;
                }
            };

            Task.Run(async () => {
                while (true) {
                    if (Shared.Window != null) {
                        if (DeviceID != null) {
                            Shared.Window.SendWebMessage(JSON.Stringify(new {
                                type = "device_connected",
                                device = DeviceID,
                                info = JsonSerializer.Serialize(DeviceInfo)
                            }));
                        }
                        else {
                            Shared.Window.SendWebMessage(JSON.Stringify(new {
                                type = "device_disconnected"
                            }));
                        }
                    }
                    await Task.Delay(1000);
                }
            });
            
        }
    }
}