using System;
using System.Device.Gpio;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    private static GpioController gpio;
    private static int dataPin = 18;    // Red wire (DT/DOUT)
    private static int clockPin = 23;   // Orange wire (SCK)
    private static long offset = 0;
    private static double referenceUnit = 1.0; // Will be calculated

    static async Task Main(string[] args)
    {
        Console.WriteLine("HX711 Weight Sensor - Raspberry Pi 5");
        Console.WriteLine("====================================");

        // Initialize GPIO
        gpio = new GpioController();
        gpio.OpenPin(dataPin, PinMode.Input);
        gpio.OpenPin(clockPin, PinMode.Output);
        gpio.Write(clockPin, PinValue.Low);

        try
        {
            // Calibrate the sensor
            await CalibrateWithYourData();
            
            Console.WriteLine("\nStarting weight readings...");
            Console.WriteLine("Press Ctrl+C to stop\n");

            while (true)
            {
                try
                {
                    long rawValue = await ReadRaw();
                    double weightGrams = (rawValue - offset) / referenceUnit;
                    double weightKg = weightGrams / 1000.0;
                    
                    Console.WriteLine($"Raw: {rawValue:D8} | Weight: {weightGrams:F1}g ({weightKg:F3}kg) | Time: {DateTime.Now:HH:mm:ss}");
                    
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Reading error: {ex.Message}");
                    await Task.Delay(100);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            Console.WriteLine("\nCleaning up...");
            gpio?.Dispose();
        }
    }

    static async Task CalibrateWithYourData()
    {
        Console.WriteLine("Calibrating based on your test data...");
        
        // Based on your readings:
        // No load: ~-37,000 raw
        // 1kg load: ~168,000 raw
        // 1.25kg load: ~221,000 raw
        
        // Calculate from your 1kg test:
        long noLoadRaw = -37000;     // Average from your no-load readings
        long oneKgRaw = 168500;      // Average from your 1kg readings
        
        offset = noLoadRaw;
        referenceUnit = (oneKgRaw - noLoadRaw) / 1000.0; // 1000 grams
        
        Console.WriteLine($"Calibration complete!");
        Console.WriteLine($"Offset (no load): {offset}");
        Console.WriteLine($"Reference unit: {referenceUnit:F2} units per gram");
        
        // Verify with your 1.25kg reading
        long oneKgQuarterRaw = 221200; // Average from your 1.25kg readings
        double verifyWeight = (oneKgQuarterRaw - offset) / referenceUnit;
        Console.WriteLine($"Verification: 1.25kg reading should be ~1250g, calculated: {verifyWeight:F1}g");
    }

    static async Task<long> ReadRaw()
    {
        // Wait for chip to be ready (data pin goes low)
        int timeout = 1000;
        while (gpio.Read(dataPin) == PinValue.High && timeout > 0)
        {
            await Task.Delay(1);
            timeout--;
        }

        if (timeout == 0)
        {
            throw new TimeoutException("HX711 not ready - check connections");
        }

        long value = 0;

        // Read 24 bits of data
        for (int i = 0; i < 24; i++)
        {
            gpio.Write(clockPin, PinValue.High);
            await Task.Delay(TimeSpan.FromMicroseconds(1));

            value = value << 1;
            if (gpio.Read(dataPin) == PinValue.High)
            {
                value++;
            }

            gpio.Write(clockPin, PinValue.Low);
            await Task.Delay(TimeSpan.FromMicroseconds(1));
        }

        // 25th pulse for gain setting
        gpio.Write(clockPin, PinValue.High);
        await Task.Delay(TimeSpan.FromMicroseconds(1));
        gpio.Write(clockPin, PinValue.Low);
        await Task.Delay(TimeSpan.FromMicroseconds(1));

        // Convert to signed 24-bit value
        if ((value & 0x800000) != 0)
        {
            value -= 0x1000000;
        }

        return value;
    }

    static async Task InteractiveCalibrate()
    {
        Console.WriteLine("\nInteractive Calibration");
        Console.WriteLine("======================");
        Console.WriteLine("1. Remove all weight from scale");
        Console.Write("Press Enter when ready...");
        Console.ReadLine();

        Console.WriteLine("Taking no-load readings...");
        long total = 0;
        for (int i = 0; i < 10; i++)
        {
            long reading = await ReadRaw();
            total += reading;
            Console.WriteLine($"No-load reading {i + 1}: {reading}");
            await Task.Delay(200);
        }
        offset = total / 10;
        Console.WriteLine($"No-load average (offset): {offset}");

        Console.Write("\n2. Place a known weight on scale and enter weight in grams: ");
        if (double.TryParse(Console.ReadLine(), out double knownWeight) && knownWeight > 0)
        {
            Console.WriteLine("Taking loaded readings...");
            total = 0;
            for (int i = 0; i < 10; i++)
            {
                long reading = await ReadRaw();
                total += reading;
                Console.WriteLine($"Loaded reading {i + 1}: {reading}");
                await Task.Delay(200);
            }
            long loadedAverage = total / 10;
            
            referenceUnit = (loadedAverage - offset) / knownWeight;
            Console.WriteLine($"Loaded average: {loadedAverage}");
            Console.WriteLine($"Reference unit: {referenceUnit:F6} units per gram");
        }
    }
}