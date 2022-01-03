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

using GHIElectronics.TinyCLR.Drivers.BasicGraphics;
using GHIElectronics.TinyCLR.Drivers.Worldsemi.WS2812;


namespace BMC.Oximeter
{
    class Program
    {
        static LedMatrix screen;
        static Random rnd;
        const int cols = 32;
        const int rows = 8;


        private static ST7735Controller st7735;
        private const int SCREEN_WIDTH = 160;
        private const int SCREEN_HEIGHT = 128;
        static Graphics screenLcd;
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
            screenLcd = Graphics.FromImage(new Bitmap(SCREEN_WIDTH, SCREEN_HEIGHT));

            //var image = Resources.GetBitmap(Resources.BitmapResources.
            //    smallJpegBackground);

            var font = Resources.GetFont(Resources.FontResources.NinaB);

            screenLcd.Clear();

            //screen.DrawImage(image, 56, 50);

            //screen.DrawRectangle(new Pen(Color.Yellow), 10, 80, 40, 25);
            //screen.DrawEllipse(new Pen(Color.Purple), 60, 80, 40, 25);
            //screen.FillRectangle(new SolidBrush(Color.Teal), 110, 80, 40, 25);

            //screen.DrawLine(new Pen(Color.White), 10, 127, 150, 127);
            //screen.SetPixel(80, 92, Color.White);

            screenLcd.DrawString("Ukur kadar oxigen!", font, new SolidBrush(Color.Blue), 10, 20);
            screenLcd.DrawString("Masukin jari kamu.", font, new SolidBrush(Color.Blue), 10, 50);
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
            screenLcd.Flush();
            var brush = new SolidBrush(Color.Red);
            var pen = new Pen(brush);
            var brush2 = new SolidBrush(Color.Yellow);
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

                screenLcd.Clear();
                /*
                screen.FillEllipse(new SolidBrush(System.Drawing.Color.FromArgb
               (255, 255, 0, 0)), 0, 0, 80, 64);

                screen.FillEllipse(new SolidBrush(System.Drawing.Color.FromArgb
                    (255, 0, 0, 255)), 80, 0, 80, 64);

                screen.FillEllipse(new SolidBrush(System.Drawing.Color.FromArgb
                    (128, 0, 255, 0)), 40, 0, 80, 64);
                */
                
                screenLcd.DrawRectangle(pen,0,0,160,2);
                screenLcd.DrawRectangle(pen2, 0, 4, 160, 2);
                screenLcd.DrawRectangle(pen3, 0, 7, 160, 2);

                if (SPO2 < 90)
                    Hasil = "Kamu Covid";
                else if (SPO2 <= 95)
                    Hasil = "Ga Sehat!";
                else
                    Hasil = "Sehat Jos!";
                screenLcd.DrawString($"Oksigen: {SPO2} %", font, new SolidBrush(Color.Blue), 10, 20);
                screenLcd.DrawString($"Pulse Rate: {PulseRate}", font, new SolidBrush(Color.Yellow), 10, 40);
                screenLcd.DrawString($"Signal: {SignalStrength}", font, new SolidBrush(Color.Red), 10, 60);
                screenLcd.DrawString($"Attached: {(oxi.IsProbeAttached?"Ya":"Tidak")}", font, new SolidBrush(Color.White), 10, 80);
                screenLcd.DrawString($"Hasil: {Hasil}", font, new SolidBrush(Color.Yellow), 10, 100);
                screenLcd.Flush();
            }, null, 1000, 1000);
            #region matrix
            rnd = new Random();
            var pin = GpioController.GetDefault().OpenPin(SC20100.GpioPin.PA8);
            var led = GpioController.GetDefault().OpenPin(SC20100.GpioPin.PE11);
            led.SetDriveMode(GpioPinDriveMode.Output);
            screen = new LedMatrix(pin, cols, rows);

            Thread th2 = new Thread(new ThreadStart(Animation));
            th2.Start();
            while (true)
            {
                led.Write(GpioPinValue.High);
                Thread.Sleep(200);
                led.Write(GpioPinValue.Low);
                Thread.Sleep(200);
            }
            #endregion
            //Thread.Sleep(Timeout.Infinite);
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
        static void Animation()
        {
            string[] words = { "THIS", "IS", "A", "PLACE", "OF", "THE", "CHAMP" };
            string[] words2 = { "READ", "QURAN", "ALL", "THE", "TIME" };
            string[] words3 = { "SALAT", "ON", "TIME", };
            string[] words4 = { "GOD", "WILL", "LOVE", "YOU" };
            while (true)
            {
                CountDownAnimation(0, 100, 1);
                BrickAnimation();
                CharAnimation(words);
                LineAnimation();
                CharAnimation(words2);
                LineAnimation2();
                CharAnimation(words3);
                LineAnimation();
                CharAnimation(words4);
                LineAnimation2();
                BallAnimation(200);
            }
        }
        static void BrickAnimation(int Moves = 16 * 4, int Delay = 100)
        {
            screen.Clear();
            var col = LedMatrix.ColorFromRgb((byte)rnd.Next(255), (byte)rnd.Next(255), (byte)rnd.Next(255));
            var MaxX = cols / 2;
            var MaxY = rows / 2;
            int x, y;
            for (int i = 0; i < Moves; i++)
            {
                x = rnd.Next(MaxX);
                y = rnd.Next(MaxY);
                screen.DrawRectangle(col, x * 2, y * 2, 2, 2);
                screen.Flush();
                Thread.Sleep(Delay);
                col = LedMatrix.ColorFromRgb((byte)rnd.Next(255), (byte)rnd.Next(255), (byte)rnd.Next(255));

            }
        }
        static void BallAnimation(int Moves = 1000, int Delay = 50)
        {
            var x = rnd.Next(cols);
            var y = rnd.Next(rows);
            var ax = 1 + rnd.Next(2);
            var ay = 1 + rnd.Next(2);
            screen.Clear();
            var col = LedMatrix.ColorFromRgb((byte)rnd.Next(255), (byte)rnd.Next(255), (byte)rnd.Next(255));
            var current = 0;
            while (current < Moves)
            {

                screen.Clear();
                screen.DrawCircle(col, x, y, 1);
                screen.Flush();
                Thread.Sleep(Delay);
                x += ax;
                y += ay;
                if (x + ax > cols || x < 0)
                {
                    ax = -ax;
                }
                if (ay + y > rows || y < 0)
                {
                    ay = -ay;
                }
                current++;
            }
        }
        static void LineAnimation2(int Delay = 10)
        {
            screen.Clear();
            var col = LedMatrix.ColorFromRgb(0, 20, 50);
            var rnd = new Random();
            for (int x = 0; x < cols; x++)
            {
                col = LedMatrix.ColorFromRgb((byte)rnd.Next(255), (byte)rnd.Next(255), (byte)rnd.Next(255));
                if (x % 2 == 0)
                {
                    for (int y = 0; y < rows; y++)
                    {
                        screen.SetPixel(x, y, col);
                        screen.Flush();
                        Thread.Sleep(Delay);
                    }
                }
                else
                {
                    for (int y = rows - 1; y >= 0; y--)
                    {
                        screen.SetPixel(x, y, col);
                        screen.Flush();
                        Thread.Sleep(Delay);
                    }
                }
            }
        }
        static void LineAnimation(int Delay = 10)
        {
            screen.Clear();
            var col = LedMatrix.ColorFromRgb(0, 20, 50);
            var rnd = new Random();
            for (int y = 0; y < rows; y++)
            {
                col = LedMatrix.ColorFromRgb((byte)rnd.Next(255), (byte)rnd.Next(255), (byte)rnd.Next(255));
                if (y % 2 == 0)
                {
                    for (int x = 0; x < cols; x++)
                    {
                        screen.SetPixel(x, y, col);
                        screen.Flush();
                        Thread.Sleep(Delay);
                    }
                }
                else
                {
                    for (int x = cols - 1; x >= 0; x--)
                    {
                        screen.SetPixel(x, y, col);
                        screen.Flush();
                        Thread.Sleep(Delay);
                    }
                }
            }
        }
        static void CharAnimation(string[] Words, int Delay = 500)
        {
            screen.Clear();
            var col = LedMatrix.ColorFromRgb(0, 20, 50);


            foreach (var word in Words)
            {
                col = LedMatrix.ColorFromRgb((byte)rnd.Next(255), (byte)rnd.Next(255), (byte)rnd.Next(255));
                var statement = string.Empty;
                for (int i = 0; i < word.Length; i++)
                {
                    statement += word[i];
                    screen.Clear();
                    screen.DrawString(statement.ToString(), col, 0, 0);
                    screen.Flush();
                    Thread.Sleep(Delay);
                }
            }
        }
        static void CountDownAnimation(int From, int To, int Incr)
        {
            screen.Clear();
            var col = LedMatrix.ColorFromRgb(0, 20, 50);


            int current = From;
            while (true)
            {
                screen.Clear();
                screen.DrawString(current.ToString(), col, 0, 0);
                screen.Flush();
                Thread.Sleep(10);
                if (current % 10 == 0)
                {
                    col = LedMatrix.ColorFromRgb((byte)rnd.Next(255), (byte)rnd.Next(255), (byte)rnd.Next(255));
                }
                if (current >= Int32.MaxValue) current = 0;
                if (current == To) break;
                current += Incr;
            }
        }
    }
    class LedMatrix : BasicGraphics
    {
        private uint row, column;
        WS2812Controller leds;

        public LedMatrix(GpioPin pin, uint column, uint row)
        {
            this.row = row;
            this.column = column;
            this.leds = new WS2812Controller(pin, this.row * this.column, WS2812Controller.DataFormat.rgb565);

            Clear();
        }

        public override void Clear()
        {
            leds.Clear();
        }

        public override void SetPixel(int x, int y, uint color)
        {
            if (x < 0 || x >= this.column) return;
            if (y < 0 || y >= this.row) return;

            // even columns are inverted
            if ((x & 0x01) != 0)
            {
                y = (int)(this.row - 1 - y);
            }

            var index = x * this.row + y;

            leds.SetColor((int)index, (byte)(color >> 16), (byte)(color >> 8), (byte)(color >> 0));
        }
        public void Flush()
        {
            leds.Flush();
        }
    }
}
