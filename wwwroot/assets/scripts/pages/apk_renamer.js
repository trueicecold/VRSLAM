const init = () => {
    receiveMessage(message => {
        const data = JSON.parse(message);
        switch (data.type) {
            case "choose_apk":
                if (data.files && data.files.length > 0) {
                    selectedFile = data.files[0];
                    $("#selectedFile").val(selectedFile || "");
                }
                break;
        }
    });
}

chooseAPK = () => {
    sendMessage(JSON.stringify({ 
        action: "choose_apk"
    }));
}

selectedFile = null;

fixAPK = () => {
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