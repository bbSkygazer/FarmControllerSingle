using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Windows.ApplicationModel.Background;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.System.Threading;
using System.Runtime.InteropServices.WindowsRuntime;
using System.IO;
//using Windows.ApplicationModel.AppService;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace FarmBackgroundWebApp
{
    public sealed class StartupTask : IBackgroundTask
    {
        private static BackgroundTaskDeferral _Deferral = null;
      //  private AppServiceConnection _connection;
        private MyWebserver webserver;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            _Deferral = taskInstance.GetDeferral();

             //AppServiceTriggerDetails triggerDetails = taskInstance.TriggerDetails as AppServiceTriggerDetails;
            // _connection = triggerDetails.AppServiceConnection;



            //webserver = new MyWebserver(_connection);
            webserver = new MyWebserver();


            await ThreadPool.RunAsync(workItem =>
            {
                webserver.Start();
            });
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
