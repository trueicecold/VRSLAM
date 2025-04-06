using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VRSLAM.Libs;

namespace VRSLAM.Managers
{
    public class RCloneManager: Handler
    {
        static RClone rclone = new RClone(AppPath.VRSLAM_DIR + "/VRP.download.config"); // Initialize RClone with the config file path
        
        public static void InitHandlers() {
            rclone.Unmount(AppPath.RCLONE_MOUNT_DIR);
            rclone.Mount("VRP-mirror01", "", AppPath.RCLONE_MOUNT_DIR, "--read-only --rc --rc-no-auth"); // Sync the remote with the local directory
        }
    }
}