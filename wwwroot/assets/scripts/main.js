$(document).ready(function () {
    window.external.sendMessage(JSON.stringify({
        action: "download_dependencies"
    }));
});

let selectedFile = null;
function chooseAPK() {
    window.external.sendMessage(JSON.stringify({ 
        action: "choose_apk"
    }));
}

function fixAPK() {
    if (selectedFile) {
        window.external.sendMessage(JSON.stringify({
            action: "fix_apk",
            filePath: selectedFile
        }));
    }
    else {
        alert("Please select an APK file first.");
    }
}

window.external.receiveMessage(message => {
    const data = JSON.parse(message);
    switch (data.type) {
        case "html_log":
            Logger.log(data.message, data.logId);
            break;
        case "choose_apk":
            if (data.files && data.files.length > 0) {
                selectedFile = data.files[0];
                $("#selectedFile").val(selectedFile || "");
            }
            break;
        case "device_connected":
            ADBManager.onDeviceConnected(data.device, data.info);
            break;
        case "device_disconnected":
            ADBManager.onDeviceDisconnected();
            break;
    }
});