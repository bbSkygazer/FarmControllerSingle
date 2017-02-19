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
using System.Text.RegularExpressions;


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
        private DispatcherTimer timer;
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

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += Timer_Tick;
            timer.Start();
 
        }


        private void Timer_Tick(object sender, object e)
        {
            MyFarmController.UpdatePumpControls();
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (MyFarmController.isPumpRunning() == true)
            {
                LED.Fill = greenBrush;
                txt_Info.Text = "Running";
                txt_StartTime.Text = "Start Time: " + MyFarmController.PumpStartTime.ToString("dd/MM/yyyy hh:mm:ss tt");
                txt_EndTime.Text = "End Time: " +  MyFarmController.PumpEndTime.ToString("dd/MM/yyyy hh:mm:ss tt");
            }
            else
            {
                LED.Fill = grayBrush;
                txt_Info.Text = "Stopped";
                txt_StartTime.Text = "Start Time:";
                txt_EndTime.Text = "End Time";
            }
            txt_CurrentTime.Text = "Current Time: " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss tt");
        }


        private void btn_Start_Click(object sender, RoutedEventArgs e)
        {
            int theduration = 0;

            try
            {
                theduration = Convert.ToInt16(this.txt_Duration.Text);
                if (theduration > 0)
                {
                    MyFarmController.StartPump(theduration);
                    txt_Info.Text = "Starting";

                }

            }
            catch
            {
                theduration = 0;
                txt_Info.Text = "Stopped Incorrect Value";
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
                            //byte[] html = Encoding.GetEncoding("iso-8859-1").GetBytes(myhtml);
                            byte[] html = Encoding.UTF8.GetBytes(myhtml);
                            MemoryStream bodyStream = new MemoryStream(html);

                            string header = "HTTP/1.1 200 OK\r\n" +
                                        $"Content-Length: {bodyStream.Length}\r\n" +
                                            "Connection: close\r\n\r\n";                            //var headerArray = Encoding.UTF8.GetBytes(header);
                            //byte[] headerArray = Encoding.GetEncoding("iso-8859-1").GetBytes(header);
                            byte[] headerArray = Encoding.UTF8.GetBytes(header);
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
                pumpaction = String.Empty;
            }

            private string BuildMyHTMLResponse(string thequery)
            {
                string htmlresponse = String.Empty;
            htmlresponse += @"<html><head><title>Farm Controller</title><meta http-equiv=""refresh"" content=""30;URL='/home.htm'""></head><body>";
            htmlresponse += PumptStatusHTMLResponse();


                if (webFarmController.isPumpRunning() == false)
                {
                //Duration in Minutes: <input type = ""text"" name = ""Duration"" value = ""20"" style=""height: 50px; width: 100px; font-size: 30px"">
                    htmlresponse +=
                    @"
                    <form action=""/Start?"" method=""post"">
                           <input type = ""hidden"" name = ""PumpName"" value = ""Pump1"" style=""height: 50px; width: 100px; font-size: 25px""><br>
                            <font color=""black"" size=""7"">Duration : </font>
                            <Select name =""Duration"" value=""20"" style=""height: 50px; width: 250px; font-size: 30px"">
                                <option value=""20"">20 Minuties</option>
                                <option value=""40"">40 Minuties</option>
                                <option value=""60"">60 Minuties</option>
                            </Select>
                            <input type=""hidden"" name= ""Action"" value=""Start"">
                            <input type = ""submit"" value = ""Start"" style=""height: 75px; width: 300px; font-size: 50px"">
                    </form>
                    ";
                }
         
                htmlresponse += @"<br>
                <form action=""/Stop?"" method=""post"">
                            <input type = ""hidden"" name = ""PumpName"" value = ""Pump1"" style=""height: 50px; width: 100px; font-size: 30px"">
                            <input type=""hidden"" name= ""Action"" value=""Stop"">
                            <input type = ""submit"" value = ""Stop"" style=""height: 75px; width: 300px; font-size: 50px"">
               </form>

                <br>
                <br>

                <form action=""/"" method=""GET"">
                            <input type = ""submit"" value = ""Refresh"" style=""height: 75px; width: 300px; font-size: 50px"">
                </form>
                ";
         
                htmlresponse += "</body></html>";
                return htmlresponse;
            }

            private string PumptStatusHTMLResponse()
            {
                string pumptstatus = "";
                pumptstatus = @"<font color=""black"" size=""7"">Current Time : " + DateTime.Now.ToString("h:mm:ss tt  dd/MM/yyyy") + "<br><br>";

            pumptstatus += pumptstatus = @"Pump Status: ";

            if (webFarmController.isPumpRunning() == true)
                {
                    pumptstatus += @"</font><font color=""green"" size=""7"">Runing for " + webFarmController.PumpDuration.ToString() + " minutes</font><br><br>";
                    pumptstatus += @"<font color=""black"" size=""7"">";
                    pumptstatus += "Start Time : ";
                    pumptstatus += webFarmController.PumpStartTime.ToString("h:mm:ss tt  dd/MM/yyyy")  + "<br>";
                    pumptstatus += "End Time  : ";
                    pumptstatus += webFarmController.PumpEndTime.ToString("h:mm:ss tt  dd/MM/yyyy") + "<br>";
                    pumptstatus += "<br>";
                }
                else
                {
                    pumptstatus += "Stopped<br><br>";
                }

                pumptstatus += "</font>";
                return pumptstatus;
            }

            private string GetQuery(StringBuilder request)
            {
            string url;
            var testGet = request.ToString().Split('\n')[0];

            if (testGet.StartsWith("GET") == true)
            {
                url = string.Empty;
            }
            else
            {
                var b = Regex.Matches(request.ToString(), Environment.NewLine).Count;
                string[] requestMethod = request.ToString().Split('\n');
                string c = requestMethod[b];
                string requestParts = c.Split('\0')[0];

                if (requestParts.Length > 1)
                {
                    url = "/?" + requestParts;
                }
                else
                {
                    url = string.Empty;
                    pumpaction = string.Empty;
                    pumpname = string.Empty;
                    pumpduration = 0;

                }
            

            }

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
                pumpaction = String.Empty;

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

             if (name == "PumpName")
             {
                   pumpname = value;
             }
                else if (name == "Action")
             {
                pumpaction = value;
             }
             else if (name == "Duration")
             {
                 try
                 {
                    pumpduration = Convert.ToInt32(value);
                }
                catch
                {
                    pumpduration = 0;
                }

             }
             else
             {

                pumpduration = 0;
                pumpname = String.Empty;
                pumpaction = String.Empty;
            }


         }
     }



}
