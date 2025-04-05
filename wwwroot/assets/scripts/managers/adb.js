const ADBManager  = {
    onDeviceConnected: function (device, info) {
        if (device) {
            if (info) {
                info = JSON.parse(info);
            }
            $("#device_storage_container").addClass("d-flex").removeClass("d-none");
            $("#device_status").html("<span class='fg-green'>Connected</span> (" + device + ")");
            $("#device_storage").html(info?.storage.Available + " / " + info?.storage.Total);
        }
    },
    onDeviceDisconnected: function () {
        $("#device_status").html("Not Connected");
        $("#device_storage_container").addClass("d-none").removeClass("d-flex");
    }
}