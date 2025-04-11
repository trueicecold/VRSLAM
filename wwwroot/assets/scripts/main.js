$(document).ready(function () {
    Logger.init();
    ADBManager.init();
    RClone.init();

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

let webMessaageHandlers = [];
window.external.receiveMessage((message) => {
    webMessaageHandlers.forEach(handler => {
        if (!handler.is_page || handler.is_page == PageManager.current_name) {
            handler.callback(message);
        }
    });
});

const receiveMessage = (callback, page) => {
    if (!webMessaageHandlers.find(clb => clb.callback.toString() == callback.toString())) {
        webMessaageHandlers.push({
            callback: callback,
            is_page: (page ? PageManager.current_name : null)
        });
    }
}