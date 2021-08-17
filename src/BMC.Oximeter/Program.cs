using BMC.Oximeter.Properties;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.Spi;
using GHIElectronics.TinyCLR.Drivers.Sitronix.ST7735;
using GHIElectronics.TinyCLR.Pins;
using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Threading;

namespace BMC.Oximeter
{
    class Program
    {
        private static ST7735Controller st7735;
        private const int SCREEN_WIDTH = 160;
        private const int SCREEN_HEIGHT = 128;
        static Graphics screen;
        static PulseOximeter oxi;
        private static void Main()
        {
            var spi = SpiController.FromName(SC20100.SpiBus.Spi3);
            var gpio = GpioController.GetDefault();

            st7735 = new ST7735Controller(
                spi.GetDevice(ST7735Controller.GetConnectionSettings
                (SpiChipSelectType.Gpio, gpio.OpenPin(SC20100.GpioPin.PD10))), //CS pin.
                gpio.OpenPin(SC20100.GpioPin.PC4), //RS pin.
                gpio.OpenPin(SC20100.GpioPin.PE15) //RESET pin.
            );

            var backlight = gpio.OpenPin(SC20100.GpioPin.PE5);
            backlight.SetDriveMode(GpioPinDriveMode.Output);
            backlight.Write(GpioPinValue.High);

            st7735.SetDataAccessControl(true, true, false, false); //Rotate the screen.
            st7735.SetDrawWindow(0, 0, SCREEN_WIDTH - 1, SCREEN_HEIGHT - 1);
            st7735.Enable();

            // Create flush event
            Graphics.OnFlushEvent += Graphics_OnFlushEvent;

            // Create bitmap buffer
            screen = Graphics.FromImage(new Bitmap(SCREEN_WIDTH, SCREEN_HEIGHT));

            //var image = Resources.GetBitmap(Resources.BitmapResources.
            //    smallJpegBackground);

            var font = Resources.GetFont(Resources.FontResources.NinaB);

            screen.Clear();

            //screen.DrawImage(image, 56, 50);

            //screen.DrawRectangle(new Pen(Color.Yellow), 10, 80, 40, 25);
            //screen.DrawEllipse(new Pen(Color.Purple), 60, 80, 40, 25);
            //screen.FillRectangle(new SolidBrush(Color.Teal), 110, 80, 40, 25);

            //screen.DrawLine(new Pen(Color.White), 10, 127, 150, 127);
            //screen.SetPixel(80, 92, Color.White);

            screen.DrawString("Ukur kadar oxigen!", font, new SolidBrush(Color.Blue), 10, 20);
            screen.DrawString("Masukin jari kamu.", font, new SolidBrush(Color.Blue), 10, 50);
            var pin3 = GpioController.GetDefault().OpenPin(SC20100.GpioPin.PA1);
            pin3.SetDriveMode(GpioPinDriveMode.Output);
            pin3.Write(GpioPinValue.High);
            
            var pin4 = GpioController.GetDefault().OpenPin(SC20100.GpioPin.PA2);
            pin4.SetDriveMode(GpioPinDriveMode.Output);
            pin4.Write(GpioPinValue.High);

            oxi = new PulseOximeter(SC20100.UartPort.Uart3);
            
            oxi.ProbeAttached += Oxi_ProbeAttached;
            oxi.ProbeDetached += Oxi_ProbeDetached;
            oxi.Heartbeat += Oxi_Heartbeat;
            screen.Flush();
            var brush = new SolidBrush(Color.Red);
            var pen = new Pen(brush);
            var brush2 = new SolidBrush(Color.Purple);
            var pen2 = new Pen(brush2);
            var brush3 = new SolidBrush(Color.Blue);
            var pen3 = new Pen(brush3);
            Timer timer = new Timer((a)=> {
                if (PulseRate <= 0 || SPO2<=0 || SignalStrength <= 0)
                {
                    if (oxi.LastReading != null)
                    {
                        PulseRate = oxi.LastReading.PulseRate;
                        SPO2 = oxi.LastReading.SPO2;
                        SignalStrength = oxi.LastReading.SignalStrength;
                    }
                    else
                    {
                        return;
                    }
                }

                screen.Clear();
                /*
                screen.FillEllipse(new SolidBrush(System.Drawing.Color.FromArgb
               (255, 255, 0, 0)), 0, 0, 80, 64);

                screen.FillEllipse(new SolidBrush(System.Drawing.Color.FromArgb
                    (255, 0, 0, 255)), 80, 0, 80, 64);

                screen.FillEllipse(new SolidBrush(System.Drawing.Color.FromArgb
                    (128, 0, 255, 0)), 40, 0, 80, 64);
                */
                
                screen.DrawRectangle(pen,0,0,160,2);
                screen.DrawRectangle(pen2, 0, 4, 160, 2);
                screen.DrawRectangle(pen3, 0, 7, 160, 2);

                if (SPO2 < 90)
                    Hasil = "Kamu Covid";
                else if (SPO2 <= 95)
                    Hasil = "Ga Sehat!";
                else
                    Hasil = "Sehat Jos!";
                screen.DrawString($"Oksigen: {SPO2} %", font, new SolidBrush(Color.Blue), 10, 20);
                screen.DrawString($"Pulse Rate: {PulseRate}", font, new SolidBrush(Color.Yellow), 10, 40);
                screen.DrawString($"Signal: {SignalStrength}", font, new SolidBrush(Color.Red), 10, 60);
                screen.DrawString($"Attached: {(oxi.IsProbeAttached?"Ya":"Tidak")}", font, new SolidBrush(Color.White), 10, 80);
                screen.DrawString($"Hasil: {Hasil}", font, new SolidBrush(Color.White), 10, 100);
                screen.Flush();
            }, null, 1000, 1000);

            Thread.Sleep(Timeout.Infinite);
        }
        static string Hasil;
        static int PulseRate=-1, SignalStrength=-1, SPO2=-1;
        private static void Oxi_Heartbeat(PulseOximeter sender, PulseOximeter.Reading e)
        {
            PulseRate = e.PulseRate;
            SignalStrength = e.SignalStrength;
            SPO2 = e.SPO2;
            Debug.WriteLine("got data from sensor");
        }

        private static void Oxi_ProbeDetached(PulseOximeter sender, EventArgs e)
        {
            isProbeAttached = false;
            Debug.WriteLine("oxi detached");
        }

        private static void Oxi_ProbeAttached(PulseOximeter sender, EventArgs e)
        {
            isProbeAttached = true;
            Debug.WriteLine("oxi attached");
        }

        static bool isProbeAttached;

        private static void Graphics_OnFlushEvent(Graphics sender, byte[] data, int x, int y, int width, int height, int originalWidth)
        {
            try
            {
                st7735.DrawBuffer(data);
            }
            catch (Exception)
            {
            }
        }
    }
}
