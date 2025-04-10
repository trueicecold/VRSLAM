const Logger = {
    log: function (message, logId) {
        const logElement = $("#logger");
        if (logId && $(`#log_line_${logId}`).length > 0) {
            $(`#log_line_${logId}`).html(message);
        }
        else {
            const logLine = $("<div></div>").attr("id", "log_line_" +logId).html(message);
            logElement.append(logLine);
        }
    },
    init: () => {
        receiveMessage(message => {
            const data = JSON.parse(message);
            switch (data.type) {
                case "html_log":
                    Logger.log(data.message, data.logId);
                    break;
            }
        });
    }
}