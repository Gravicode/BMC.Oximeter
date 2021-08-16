using GHIElectronics.TinyCLR.Devices.Uart;
using GHIElectronics.TinyCLR.Pins;
using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace BMC.Oximeter
{
    public class PulseOximeter 
    {
        /*
         txBuffer = new byte[] { 0x41, 0x42, 0x43, 0x44, 0x45, 0x46 }; //A, B, C, D, E, F
    rxBuffer = new byte[txBuffer.Length];

    myUart = UartController.FromName(SC20100.UartPort.Uart7);
    var uartSetting = new UartSetting()
            {
                BaudRate = 115200,
                DataBits = 8,
                Parity = UartParity.None,
                StopBits = UartStopBitCount.One,
                Handshaking = UartHandshake.None,
            };
    myUart.SetActiveSettings(uartSetting);
    myUart.Enable();
    myUart.DataReceived += MyUart_DataReceived;
    myUart.Write(txBuffer, 0, txBuffer.Length);
    while (true)
        Thread.Sleep(20);

         */
        private Thread workerThread;
        private UartController serialPort;

        private HeartbeatEventHandler onHeartbeat;

        private ProbeAttachedEventHandler onProbeAttached;

        private ProbeDetachedEventHandler onProbeDetached;

        /// <summary>Represents the delegate used for the Heartbeat event.</summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The event arguments.</param>
        public delegate void HeartbeatEventHandler(PulseOximeter sender, Reading e);

        /// <summary>Represents the delegate used for the ProbeAttached event.</summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The event arguments.</param>
        public delegate void ProbeAttachedEventHandler(PulseOximeter sender, EventArgs e);

        /// <summary>Represents the delegate used for the ProbeDetached event.</summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The event arguments.</param>
        public delegate void ProbeDetachedEventHandler(PulseOximeter sender, EventArgs e);

        /// <summary>Raised when the module detects a heartbeat.</summary>
        public event HeartbeatEventHandler Heartbeat;

        /// <summary>Raised when the module detects that the probe is placed on a finger.</summary>
        public event ProbeAttachedEventHandler ProbeAttached;

        /// <summary>Raised when the module detects that the probe is removed from a finger.</summary>
        public event ProbeDetachedEventHandler ProbeDetached;

        /// <summary>Whether the PulseOximeter's probe is attached to a finger.</summary>
        public bool IsProbeAttached { get; private set; }

        /// <summary>The most recent valid reading from the pulse oximeter</summary>
        public Reading LastReading { get; private set; }

        /// <summary>Constructs a new instance.</summary>
        /// <param name="socketNumber">The socket that this module is plugged in to.</param>
        public PulseOximeter(string UartName = SC20100.UartPort.Uart1)
        {
            
            this.IsProbeAttached = false;
            this.LastReading = null;

            //Socket socket = Socket.GetSocket(socketNumber, true, this, null);

            //this.serialPort = GTI.SerialFactory.Create(socket, 4800, GTI.SerialParity.Even, GTI.SerialStopBits.One, 8, GTI.HardwareFlowControl.NotRequired, this);
            //this.serialPort.Open();
            this.serialPort = UartController.FromName(UartName);
            var uartSetting = new UartSetting()
            {
                BaudRate = 4800,
                DataBits = 8,
                Parity = UartParity.Even,
                StopBits = UartStopBitCount.One,
                Handshaking = UartHandshake.None,
            };
            this.serialPort.SetActiveSettings(uartSetting);
            this.serialPort.Enable();
           

            this.workerThread = new Thread(this.DoWork);
            this.workerThread.Start();
        }
        static byte[] tmp = new byte[1];
        private void DoWork()
        {
            bool sync = false;
            byte[] data = new byte[5];

            while (true)
            {
                int totalRead = 0;
                if (!sync)
                {
                    
                    int b = this.serialPort.Read(tmp);
                    if (b < 0)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    if (((b >> 7) & 0x1) != 1)
                        continue;

                    data[0] = (byte)b;
                    totalRead = 1;
                    sync = true;
                }

                while (totalRead < 5)
                {
                    int read = this.serialPort.Read(data, totalRead, 5 - totalRead);
                    if (read < 0)
                    {
                        this.DebugPrint("Serial error");
                        sync = false;

                        if (this.IsProbeAttached)
                        {
                            this.IsProbeAttached = false;

                            this.OnProbeDetached(this, null);
                        }

                        continue;
                    }

                    totalRead += read;
                }

                if (((data[0] >> 7) & 0x1) != 1)
                {
                    this.DebugPrint("Lost sync");
                    sync = false;

                    if (this.IsProbeAttached)
                    {
                        this.IsProbeAttached = false;

                        this.OnProbeDetached(this, null);
                    }

                    continue;
                }

                bool probeAttached = ((data[2] >> 4) & 0x1) == 0;

                if (!probeAttached && this.IsProbeAttached)
                {
                    this.IsProbeAttached = false;
                    this.OnProbeDetached(this, null);
                }

                if (!probeAttached || ((data[0] >> 6) & 0x1) != 1)
                    continue;

                int signalStrength = data[0] & 0xF;
                int pulseRate = ((data[2] << 1) & 0x80) + (data[3] & 0x7F);
                int spO2 = data[4] & 0x7F;

                if (pulseRate == 255 || spO2 == 127)
                    continue;

                this.LastReading = new Reading(pulseRate, spO2, signalStrength);

                if (probeAttached && !this.IsProbeAttached)
                {
                    this.IsProbeAttached = true;
                    this.OnProbeAttached(this, null);
                }

                this.OnHeartbeat(this, this.LastReading);
            }
        }
        void DebugPrint(string message)
        {
            Debug.WriteLine(message);
        }
        private void OnHeartbeat(PulseOximeter sender, Reading e)
        {
            if (this.onHeartbeat == null)
                this.onHeartbeat = this.OnHeartbeat;
            this.Heartbeat?.Invoke(sender, e);
            /*
            if (Program.CheckAndInvoke(this.Heartbeat, this.onHeartbeat, sender, e))
                this.Heartbeat(sender, e);
            */
        }

        private void OnProbeAttached(PulseOximeter sender, EventArgs e)
        {
            if (this.onProbeAttached == null)
                this.onProbeAttached = this.OnProbeAttached;
            /*
            if (Program.CheckAndInvoke(this.ProbeAttached, this.onProbeAttached, sender, e))
                this.ProbeAttached(sender, e);
            */
            this.ProbeAttached?.Invoke(sender, e);
        }

        private void OnProbeDetached(PulseOximeter sender, EventArgs e)
        {
            if (this.onProbeDetached == null)
                this.onProbeDetached = this.OnProbeDetached;
            /*
            if (Program.CheckAndInvoke(this.ProbeDetached, this.onProbeDetached, sender, e))
                this.ProbeDetached(sender, e);*/
            this.ProbeDetached?.Invoke(sender, e);
        }
        /// <summary>A class representing a pulse oximeter reading</summary>
        public class Reading
        {

            /// <summary>The pulse rate automatically averaged over time.</summary>
            public int PulseRate { get; private set; }

            /// <summary>The oxygen saturation between 0 and 100.</summary>
            public int SPO2 { get; private set; }

            /// <summary>The signal strength between 0 and 15.</summary>
            public int SignalStrength { get; private set; }

            internal Reading(int pulseRate, int spo2, int signalStrength)
            {
                this.PulseRate = pulseRate;
                this.SignalStrength = signalStrength;
                this.SPO2 = spo2;
            }
        }
    }

}
