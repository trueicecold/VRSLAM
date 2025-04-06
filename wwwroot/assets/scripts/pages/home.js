function init() {
    sendMessage(JSON.stringify({
        action: "check_dependencies"
    }));
}

onCheckDependencies = (dependencies) => {
    $("#dependencies_results").empty();
    let foundAll = true;
    for (const dependency of dependencies) {
        if (dependency.found) {
            $("#dependencies_results").append("<div class='fg-green'>" + dependency.dependency + " Found</div>");
        }
        else {
            foundAll = false;
            $("#dependencies_results").append("<div class='fg-red'>" + dependency.dependency + " Not Found</div>");
        }
    }
    if (foundAll) {
        $("#dependencies_download").removeClass("d-flex").addClass("d-none");
    }
    else {
        $("#dependencies_download").removeClass("d-none").addClass("d-flex");
    }
}

downloadDependencies = () => {
    sendMessage(JSON.stringify({
        action: "download_dependencies"
    }));
}