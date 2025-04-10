const RClone = {
    init: () => {
        try {
            receiveMessage(message => {
                const data = JSON.parse(message);
                switch (data.type) {
                    case "rclone_mount":
                        RClone.onRCloneMount(data.success);
                        break;
                }
            });
        }
        catch(e) {
        }
    },

    onRCloneMount: (success) => {
        try {
            if (success) {
                $("#mount_status").addClass("success").html("Mount Connected");
            }
            else {
                $("#mount_status").removeClass("success").html("Mount Disconnected");
            }
        }
        catch(e) {
        }
    }
}