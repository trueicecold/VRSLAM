const init = () => {
    receiveMessage(message => {
        const data = JSON.parse(message);
        switch (data.type) {
            case "list_files":
                onFileList(data.directories, data.files, data.path);
                break;
        }
    }, true);

    sendMessage(JSON.stringify({
        action: "list_files",
        //home: true
        path: "/Users/trueicecold/Documents/VR Stuff/temp/The Thrill of the Fight v241118000+241118.0 -VRP"
    }));

    filePath = "";
    onFileList = (directories, files, file_path) => {
        $("#files_list").scrollTop(0);
        $("#files_list").empty();
        filePath = file_path;
        $("#path").html(file_path);
        let template_item = $("#file_list_item_template").html();
        
        for (const directory of directories) {
            let item = template_item.replace(/{{name}}/g, directory);
            item = item.replace(/{{type}}/g, "directory");
            item = item.replace(/{{icon}}/g, "mif-folder");
            item = item.replace(/{{path}}/g, directory);
            $("#files_list").append(item);
        };

        for (const file of files) {
            let item = template_item.replace(/{{name}}/g, file);
            item = item.replace(/{{type}}/g, "file");
            item = item.replace(/{{path}}/g, file);
            if (file.endsWith(".apk")) {
                item = item.replace(/{{icon}}/g, "mif-file-android");
            }
            else {
                item = item.replace(/{{icon}}/g, "mif-file_present");
                item = $(item).attr("disabled", "disabled");
            }
            $("#files_list").append(item);
        };
    }

    let selected_file_name;
    fileAction = (obj, path_name) => {
        if ($(obj).data("type") == "directory") {
            sendMessage(JSON.stringify({
                action: "list_files",
                path: filePath + "/" + path_name
            }));
        }
        else {
            if (path_name.endsWith(".apk")) {
                let file = path_name.split("/");
                let file_name = file[file.length - 1];
                $("#install_apk_name").html(file_name);
                Metro.dialog.open('#install_dialog');
            }
            else {
                alert("This is not an APK file.");
            }
        }
    }

    goBack = () => {
        let newPath = filePath.substring(0, filePath.lastIndexOf("/"));
        if (newPath.length == 0) {
            newPath = "/";
        }
        sendMessage(JSON.stringify({
            action: "list_files",
            path: newPath
        }));
    };

    installAPK = () => {
        sendMessage(JSON.stringify({
            action: "install_apk",
            deviceId: ADBManager.deviceId,
            filePath: filePath + "/" + selected_file_name
        }));
        Metro.dialog.close('#install_dialog');
    }
}