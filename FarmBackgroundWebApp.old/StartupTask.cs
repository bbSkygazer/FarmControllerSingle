using System;
using System.Text;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.AppService;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using System.IO;
using Windows.Foundation.Collections;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace FarmBackgroundWebApp
{
    public sealed class StartupTask : IBackgroundTask
    {
        private static BackgroundTaskDeferral _deferral = null;
        private AppServiceConnection _connection;
        private MyWebserver webserver;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();
            taskInstance.Canceled += OnTaskCanceled;

            var triggerDetails = taskInstance.TriggerDetails as AppServiceTriggerDetails;
            _connection = triggerDetails.AppServiceConnection;
            _connection.RequestReceived += Connection_RequestReceived;


            //webserver = new MyWebserver(_connection);
            webserver = new MyWebserver();
        
            
            await Windows.System.Threading.ThreadPool.RunAsync(workItem =>
            {
                webserver.Start();
            });
        }

        private async void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)

        {

            // if you are doing anything awaitable, you need to get a deferral

            var requestDeferral = args.GetDeferral();

            var returnMessage = new ValueSet();

            try

            {

                //obtain and react to the command passed in by the client

                var message = args.Request.Message["Request"] as string;

                switch (message)

                {

                    case "Turn Pump On":

                       // _ledPin.Write(GpioPinValue.High);

                        break;

                    case "Turn Pump Off":

                        //_ledPin.Write(GpioPinValue.Low);

                        break;

                }

                returnMessage.Add("Response", "OK");

            }

            catch (Exception ex)

            {

                returnMessage.Add("Response", "Failed: " + ex.Message);

            }

            await args.Request.SendResponseAsync(returnMessage);



            //let the OS know that the action is complete

            requestDeferral.Complete();

        }

        private void OnTaskCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {

            if (_deferral != null)

            {

                _deferral.Complete();

            }

        }
    }

    internal class MyWebserver
    {
        private const uint BufferSize = 8192;
        //private AppServiceConnection _connection;
        private string _name;

        /* public MyWebserver(AppServiceConnection connection)
         {
             _connection = connection;
             _connection.RequestReceived += ConnectionRequestReceived;
         }

         private void ConnectionRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
         {
             _name = (string)args.Request.Message.First().Value;
         }
         */
        public async void Start()
        {
            var listener = new StreamSocketListener();

            await listener.BindServiceNameAsync("8081");

            listener.ConnectionReceived += async (sender, args) =>
            {
                StringBuilder request = new StringBuilder();

                using (IInputStream input = args.Socket.InputStream)
                {
                    byte[] data = new byte[BufferSize];
                    IBuffer buffer = data.AsBuffer();
                    uint dataRead = BufferSize;

                    while (dataRead == BufferSize)
                    {
                        await input.ReadAsync(
                        buffer, BufferSize, InputStreamOptions.Partial);
                        request.Append(Encoding.UTF8.GetString(
                        data, 0, data.Length));
                        dataRead = buffer.Length;
                    }
                }

                string query = GetQuery(request);

                using (IOutputStream output = args.Socket.OutputStream)
                {
                    using (Stream response = output.AsStreamForWrite())
                    {
                        byte[] html = Encoding.GetEncoding("iso-8859-1").GetBytes($"<html><head><title>Background Message</title></head><body>Hello from the background process!<br/>{query}</body></html>");
                        MemoryStream bodyStream = new MemoryStream(html);

                        string header = "HTTP/1.1 200 OK\r\n" +
                                    $"Content-Length: {bodyStream.Length}\r\n" +
                                        "Connection: close\r\n\r\n";                            //var headerArray = Encoding.UTF8.GetBytes(header);
                        byte[] headerArray = Encoding.GetEncoding("iso-8859-1").GetBytes(header);
                        await response.WriteAsync(headerArray,
                        0, headerArray.Length);
                        await bodyStream.CopyToAsync(response);
                        await response.FlushAsync();

                    }
                }
            };
        }

        private static string GetQuery(StringBuilder request)
        {
            string requestMethod = request.ToString().Split('\n')[0];
            string[] requestParts = requestMethod.Split(' ');

            var url = requestParts.Length > 1
            ? requestParts[1] : string.Empty;

            var uri = new Uri("http://localhost" + url);
            var query = uri.Query;
            return query;
        }


    }
}
