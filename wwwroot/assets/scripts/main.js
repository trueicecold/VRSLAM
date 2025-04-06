$(document).ready(function () {
    Logger.init();
    ADBManager.init();

    // Initialize the page manager
    PageManager.init('page_content');
    PageManager.loadPage(location.hash.substring(1) || "home");

    sendMessage(JSON.stringify({
        action: "init_managers"
    }));
});

onhashchange = () => {
    PageManager.loadPage(location.hash.substring(1));
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