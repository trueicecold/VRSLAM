const ADBManager  = {
    init: () => {
        try {
            receiveMessage(message => {
                const data = JSON.parse(message);
                switch (data.type) {
                    case "device_connected":
                        ADBManager.onDeviceConnected(data.device, data.info);
                        break;
                    case "device_disconnected":
                        ADBManager.onDeviceDisconnected();
                        break;
                }
            });
        }
        catch(e) {
        }
    },

    onDeviceConnected: (device, info) => {
        try {
            console.log(device);
            if (device) {
                if (info) {
                    info = JSON.parse(info);
                }
                $("#device_storage_container").addClass("d-flex").removeClass("d-none");
                $("#device_status").removeClass("bg-charcoal").addClass("bg-green").html("Connected");
                $("#device_storage").html(info?.storage.Available + " / " + info?.storage.Total);
            }
        }
        catch(e) {
        }
    },
    onDeviceDisconnected: () => {
        console.log("Disconnected");
        $("#device_status").removeClass("bg-green").addClass("bg-charcoal").html("Not Connected");
        $("#device_storage_container").addClass("d-none").removeClass("d-flex");
    }
}