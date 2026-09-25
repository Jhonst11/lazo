using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Lazo;

internal static class NetworkSmoke
{
    private static void Main(string[] args)
    {
        Run(args[0]).GetAwaiter().GetResult();
    }

    private static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        string input = Path.Combine(directory, "origen.bin");
        string inbox = Path.Combine(directory, "recibidos");
        byte[] data = new byte[1024 * 1024 + 57];
        new Random(42).NextBytes(data);
        File.WriteAllBytes(input, data);
        IPAddress local = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => a.Address)
            .First(a => { byte[] b = a.GetAddressBytes(); return b[0] == 10 || b[0] == 192 && b[1] == 168 || b[0] == 172 && b[1] >= 16 && b[1] <= 31; });

        int discoveryPort;
        using (UdpClient probe = new UdpClient(0))
            discoveryPort = ((IPEndPoint)probe.Client.LocalEndPoint).Port;
        int transferPort;
        TcpListener listenerProbe = new TcpListener(IPAddress.Loopback, 0);
        listenerProbe.Start();
        transferPort = ((IPEndPoint)listenerProbe.LocalEndpoint).Port;
        listenerProbe.Stop();

        using (NetworkEngine engine = new NetworkEngine(inbox, discoveryPort, transferPort))
        {
            TaskCompletionSource<string> finished = new TaskCompletionSource<string>();
            engine.OfferReceived += offer => Task.FromResult(true);
            engine.ReceiveFinished += (id, message) => finished.TrySetResult(message);
            engine.Start();
            Peer self = new Peer { Id = Guid.NewGuid(), Name = "prueba", Address = local, Port = transferPort };
            await engine.SendAsync(self, input, null);
            if (await Task.WhenAny(finished.Task, Task.Delay(10000)) != finished.Task)
                throw new Exception("No llegó confirmación de recepción.");
            if (finished.Task.Result.StartsWith("Error:")) throw new Exception(finished.Task.Result);
            string received = Path.Combine(inbox, "origen.bin");
            if (!File.Exists(received) || !File.ReadAllBytes(received).SequenceEqual(data))
                throw new Exception("El archivo recibido difiere del original.");
            Console.WriteLine("OK: aceptación, transferencia, SHA-256 y archivo recibido (" + data.Length + " bytes)");
        }
    }
}
