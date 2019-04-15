using System;
using System.Threading;
using System.Threading.Tasks;
using Unosquare.RaspberryIO;
using Unosquare.RaspberryIO.Abstractions;
using Unosquare.WiringPi;

namespace PiConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            Pi.Init<BootstrapWiringPi>();

            Console.WriteLine("Hello World!");

            var tcs = new CancellationTokenSource();

            var touchSensorTask = PollTouchSensor(tcs.Token);
            var humidityTempatureTask = PollHumidityTempature(tcs.Token);

            Console.ReadKey(true);
            tcs.Cancel();
            touchSensorTask.Wait();
            humidityTempatureTask.Wait();
        }

        static async Task PollTouchSensor(CancellationToken token)
        {
            Console.WriteLine("Starting PollTouchSensor");

            var sensorPin = Pi.Gpio[P1.Pin11];
            sensorPin.PinMode = GpioPinDriveMode.Input;

            var blinkingPin = Pi.Gpio[P1.Pin16];
            blinkingPin.PinMode = GpioPinDriveMode.Output;

            while (!token.IsCancellationRequested)
            {
                bool isOn = sensorPin.Read();
                blinkingPin.Write(isOn);
                await Task.Delay(250, token);
            }

            Console.WriteLine("Ending PollTouchSensor");
        }

        static async Task PollHumidityTempature(CancellationToken token)
        {
            Console.WriteLine("Starting PollHumidityTempature");

            var dataPin = Pi.Gpio[P1.Pin13];

            dataPin.PinMode = GpioPinDriveMode.Output;
            dataPin.Write(GpioPinValue.High);

            while (!token.IsCancellationRequested)
            {
                dataPin.PinMode = GpioPinDriveMode.Output;
                dataPin.Write(GpioPinValue.High);

                await Task.Delay(3000, token);

                (var humidity, var tempature) = await ReadSensorData(dataPin, token);

                if (humidity == default && tempature == default)
                {
                    Console.WriteLine("RH: ----: TMP: ---");
                }
                else
                {
                    Console.WriteLine($"RH: {humidity}: TMP: {tempature}");
                }
            }

            Console.WriteLine("Ending PollHumidityTempature");
        }

        private static async Task<(double humidity, double tempature)> ReadSensorData(IGpioPin dataPin, CancellationToken token)
        {
            dataPin.PinMode = GpioPinDriveMode.Output;
            dataPin.Write(GpioPinValue.High);
            await Task.Delay(25, token);
            dataPin.Write(GpioPinValue.Low);
            dataPin.PinMode = GpioPinDriveMode.Input;
            dataPin.InputPullMode = GpioPinResistorPullMode.PullUp;
            await Task.Delay(27, token);

            if (dataPin.Read() == false)    // make sure the sensor is there
            {
                while (!dataPin.Read()) // Wait for data high
                {
                }

                uint data = 0;
                byte crc = 0;

                for (int i = 0; i < 32; i++)
                {
                    while (dataPin.Read()) // Data Clock Start
                    {
                    }

                    while (!dataPin.Read()) // Data Start
                    {
                    }

                    await Task.Delay(32, token);

                    data *= 2;

                    if (dataPin.Read())
                    {
                        data++;
                    }
                }

                for (int i = 0; i < 8; i++)
                {
                    while (dataPin.Read()) // Data Clock Start
                    {
                    }

                    while (!dataPin.Read()) // Data Start
                    {
                    }

                    await Task.Delay(32, token);

                    crc *= 2;

                    if (dataPin.Read())
                    {
                        crc++;
                    }
                }

                return ((double)(data >> 16) / 256, (double)(data & 0xffff) / 256);
            }
            else
            {
                return (default, default);
            }
        }
    }
}
