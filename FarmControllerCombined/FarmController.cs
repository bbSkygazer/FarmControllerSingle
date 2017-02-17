using System;
using Windows.Devices.Gpio;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml;

namespace FarmControllerSpace
{


    public class FarmController
    {
        private bool Pump1Running;
        public DateTime PumpEndTime { get; internal set; }
        public DateTime PumpStartTime { get; internal set; }
        private const int LED_PIN = 3;
        private GpioPin pin;
        private GpioPinValue pinValue;
        private DispatcherTimer timer;
        private SolidColorBrush redBrush = new SolidColorBrush(Windows.UI.Colors.Red);
        private SolidColorBrush grayBrush = new SolidColorBrush(Windows.UI.Colors.LightGray);
        private SolidColorBrush greenBrush = new SolidColorBrush(Windows.UI.Colors.LightGreen);


        public void Start()
        {
            Pump1Running = false;
            InitGPIO();


        }

        private void InitGPIO()
        {
            var gpio = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                pin = null;
                //GpioStatus.Text = "There is no GPIO controller on this device.";
                return;
            }

            pin = gpio.OpenPin(LED_PIN);
            pinValue = GpioPinValue.High;
            pin.Write(pinValue);
            pin.SetDriveMode(GpioPinDriveMode.Output);

            //GpioStatus.Text = "GPIO pin initialized correctly.";

        }


        public bool isPumpRunning()
        {
            return Pump1Running;
        }

        public void StopPump()
        {
            Pump1Running = false;
            pin.Write(GpioPinValue.High);
        }

       public void StartPump(int duration)
        {
            PumpStartTime = DateTime.Now;
            PumpEndTime = PumpStartTime.AddMinutes(duration);
            Pump1Running = true;
            pin.Write(GpioPinValue.Low);

        }



    }

}
