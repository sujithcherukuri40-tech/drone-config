using System.IO.Ports;

namespace PavamanDroneConfigurator.Infrastructure.Helpers;

public static class SerialPortHelper
{
    public static string[] GetAvailablePorts()
    {
        return SerialPort.GetPortNames();
    }
    
    public static int[] GetStandardBaudRates()
    {
        return new[] { 9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600 };
    }
}
