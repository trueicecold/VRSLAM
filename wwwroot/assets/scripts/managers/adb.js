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

    deviceId: null,
    
    onDeviceConnected: (device, info) => {
        try {
            if (device) {
                deviceId = device.id;
                if (info) {
                    info = JSON.parse(info);
                }
                $("#device_storage_container").addClass("d-flex").removeClass("d-none");
                $("#device_status").addClass("success").html("Device Connected");
                $("#device_storage").html(info?.storage.Available + " / " + info?.storage.Total);
            }
        }
        catch(e) {
        }
    },
    onDeviceDisconnected: () => {
        $("#device_status").removeClass("success").html("Device Disconnected");
        $("#device_storage_container").addClass("d-none").removeClass("d-flex");
    }
}