using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using System.Text;
using Windows.ApplicationModel.Background;
using System.Threading.Tasks;
using FarmControllerSpace;


// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace FarmControllerCombined
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        
        private MyWebserver webserver;
        private FarmController MyFarmController;
        private SolidColorBrush redBrush = new SolidColorBrush(Windows.UI.Colors.Red);
        private SolidColorBrush grayBrush = new SolidColorBrush(Windows.UI.Colors.LightGray);
        private SolidColorBrush greenBrush = new SolidColorBrush(Windows.UI.Colors.LightGreen);

        public MainPage()
        {
            this.InitializeComponent();

            MyFarmController = new FarmController();
            MyFarmController.Start();

            webserver = new MyWebserver();

            Task.WaitAll(webserver.Start(MyFarmController));
    

        }

        private void UpdateStatus()
        {
            if (MyFarmController.isPumpRunning() == true)
            {
                LED.Fill = greenBrush;
                txt_Info.Text = "Started";
                txt_StartTime.Text = "Start Time: " + MyFarmController.PumpStartTime.ToString();
                txt_EndTime.Text = "End Time: " +  MyFarmController.PumpEndTime.ToString();
            }
            else
            {
                LED.Fill = grayBrush;
                txt_Info.Text = "Stopped";
                txt_StartTime.Text = "Start Time:";
                txt_EndTime.Text = "End Time";
            }
        }


        private void btn_Start_Click(object sender, RoutedEventArgs e)
        {
            int theduration = 0;
            theduration = Convert.ToInt16(this.txt_Duration.Text);
            txt_Info.Text = "Starting";
            if (theduration > 0)
            {
                MyFarmController.StartPump(theduration);

            }
            UpdateStatus();

        }


        private void btn_Stop_Click(object sender, RoutedEventArgs e)
        {
            MyFarmController.StopPump();
            txt_Info.Text = "Stopping";
            UpdateStatus();
        }
    }

    internal class MyWebserver
        {
            private const uint BufferSize = 8192;
            private FarmController webFarmController;
            private string pumpname;
            private int pumpduration;
            private string pumpaction;

  
            public async Task Start(FarmController theFarmController)
            {
                webFarmController = theFarmController;
                var listener = new StreamSocketListener();

                await listener.BindServiceNameAsync("80");

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
                    Parse(query);
                    PerformActions();

                    //now respond to the request and display the current status
                    string myhtml = BuildMyHTMLResponse(query);

                    using (IOutputStream output = args.Socket.OutputStream)
                    {
                        using (Stream response = output.AsStreamForWrite())
                        {
                            byte[] html = Encoding.GetEncoding("iso-8859-1").GetBytes(myhtml);
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

            private void PerformActions()
            {
                switch (pumpaction)
                {
                    case "Start":
                        webFarmController.StartPump(pumpduration);
                        break;
                    case "Stop":
                        webFarmController.StopPump();
                        break;
                    default:
                        break;
                }
            }

            private string BuildMyHTMLResponse(string thequery)
            {
                string htmlresponse = $"<html><head><title>Farm Controller</title></head><body>"; //$"<html><head><title>Farm Controller</title></head><body>Hello from the background process!<br/>{thequery}<br>";
                htmlresponse += PumptStatusHTMLResponse();
                htmlresponse +=
                @"
                <form action=""/"">
                    <fieldset>
                        <legend>Pump Start Controls</legend>
                            Pump: <input type = ""readonly"" name = ""PumpName"" value = ""Pump1"">
                            Duration in Minutes: <input type = ""text"" name = ""Duration"" value = ""20"">
                            <br><br>
                            <input type=""hidden"" name= ""Action"" value=""Start"">
                            <input type = ""submit"" value = ""Start"">
                    </fieldset>
                </form>
                <br>
                <form action=""/"">
                    <fieldset>
                        <legend>Pump Stop Controls</legend>
                            Pump: <input type = ""readonly"" name = ""PumpName"" value = ""Pump1"">
                            <input type=""hidden"" name= ""Action"" value=""Stop"">
                            <br><br>
                            <input type = ""submit"" value = ""Stop"">
                    </fieldset>
               </form>

                ";

                htmlresponse += "</H1></body></html>";
                return htmlresponse;
            }

            private string PumptStatusHTMLResponse()
            {
                string pumptstatus = "";
                pumptstatus =  @"<H1>Pump Status: ";
                if (webFarmController.isPumpRunning() == true)
                {
                    pumptstatus += "Running, Start Time : ";
                    pumptstatus += webFarmController.PumpStartTime.ToString();
                    pumptstatus += ",End Time : ";
                    pumptstatus += webFarmController.PumpEndTime.ToString();
                    pumptstatus += "<br><br>";
                }
                else
                {
                    pumptstatus += "Stopped<br><br>";
                }
                
                    return pumptstatus;
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

            private void Parse(string stream)
            {

                string content = stream;

                string name = string.Empty;
                string value = string.Empty;
                bool lookForValue = false;
                int charCount = 0;

                foreach (var c in content)
                {
                    if (c == '=')
                    {
                        lookForValue = true;
                    }
                    else if (c == '&')
                    {
                        lookForValue = false;
                        AddParameter(name, value);
                        name = string.Empty;
                        value = string.Empty;
                    }
                    else if (!lookForValue)
                    {
                        name += c;
                    }
                    else
                    {
                        value += c;
                    }

                    if (++charCount == content.Length)
                    {
                        AddParameter(name, value);
                        break;
                    }
                }


                // If some data has been successfully received, set success to true
                //
                //      this.Success = true;
            }

            private void AddParameter(string name, string value)
            {
                switch (name)
                {
                    case "PumpName":
                        pumpname = value;
                        break;
                    case "Duration":
                        pumpduration = Convert.ToInt32(value);
                        break;
                    case "Action":
                        pumpaction = value;
                        break;
                    default:
                        break;

                 }


              }
   
        }



}
