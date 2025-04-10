window.__receiveMessageCallbacks = [];
window.external.event_id = 0;
window.__dispatchMessageCallback = (message) => {
    window.__receiveMessageCallbacks.forEach((callback) => {
        callback.callback(message);
    });
}

window.external.receiveMessage = (callback, page) => {
    if (!window.__receiveMessageCallbacks.find(clb => clb.toString() == callback.toString())) {
        window.__receiveMessageCallbacks.push({
            callback: callback,
            id: window.external.event_id++,
            is_page: page
        });
    }
}

window.external.clearPageEvents = () => {
    window.__receiveMessageCallbacks = window.__receiveMessageCallbacks.filter(clb => !clb.is_page);
}

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

const receiveMessage = (callback, page) => {
    if (window.external && window.external.receiveMessage) {
        window.external.receiveMessage(callback, page);
    }
    else {
        console.warn("receiveMessage is not defined");
    }   
}