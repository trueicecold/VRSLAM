$(document).ready(function () {
    DynamicPageManager.init('page_content');
    DynamicPageManager.loadPage(location.hash.substring(1));
    //drawPage(location.hash.substring(1));

    receiveMessage(message => {
        const data = JSON.parse(message);
        switch (data.type) {
            case "check_dependencies":
                onCheckDependencies(data.dependencies);
                break;
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
});

let selectedFile = null;
function chooseAPK() {
    sendMessage(JSON.stringify({ 
        action: "choose_apk"
    }));
}

function fixAPK() {
    if (selectedFile) {
        sendMessage(JSON.stringify({
            action: "fix_apk",
            filePath: selectedFile
        }));
    }
    else {
        alert("Please select an APK file first.");
    }
}

onhashchange = () => {
    DynamicPageManager.loadPage(location.hash.substring(1));
};

const sendMessage = (message) => {
    if (window.external && window.external.sendMessage) {
        window.external.sendMessage(message);
    }
    else {
        console.warn("sendMessage is not defined");
    }
}

const receiveMessage = (callback) => {
    if (window.external && window.external.receiveMessage) {
        window.external.receiveMessage(callback);
    }
    else {
        console.warn("receiveMessage is not defined");
    }
}