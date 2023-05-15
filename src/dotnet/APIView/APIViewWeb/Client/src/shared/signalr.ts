$(() => {
  const notificationToast = $('#notification-toast');

  var signalR = require('@microsoft/signalr');

  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${location.origin}/hubs/notification`)
    .configureLogging(signalR.LogLevel.Information)
    .withAutomaticReconnect()
    .build();
    

  async function start() {
    try {
      await connection.start();
    }
    catch (err) {
      console.log(err);
      setTimeout(start, 5000);
    }
  }

  connection.onclose(async () => {
    await start();
  });

  connection.on("RecieveNotification", (notification) => {
    var newtoast = notificationToast.clone().removeAttr("id");
    switch (notification.level) {
      case 0:
        newtoast.find(".toast-header").prepend(`<i class="fa-solid fa-circle-info text-info me-1" ></i>`);
        newtoast.find(".toast-header strong").html("Information");
        break;
      case 1:
        newtoast.find(".toast-header").prepend(`<i class="fa-solid fa-triangle-exclamation text-warning me-1"></i>`);
        newtoast.find(".toast-header strong").html("Warning");
        break;
      case 2:
        newtoast.find(".toast-header").prepend(`<i class="fa-solid fa-circle-exclamation text-danger me-1"></i>`);
        newtoast.find(".toast-header strong").html("Error");
        break;
    }
    newtoast.find(".toast-body").html(notification.message);
    const toastBootstrap = bootstrap.Toast.getOrCreateInstance(newtoast[0]);
    $("#notification-container").append(newtoast);
    toastBootstrap.show();
  });

  // Start the connection.
  start();
});
