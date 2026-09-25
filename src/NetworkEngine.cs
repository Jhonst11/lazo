using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lazo
{
    internal sealed class Peer
    {
        public Guid Id;
        public string Name;
        public IPAddress Address;
        public int Port;
        public DateTime SeenUtc;
        public override string ToString() { return Name + "  /  " + Address; }
    }

    internal sealed class Offer
    {
        public Guid Id;
        public string Sender;
        public IPAddress Address;
        public string FileName;
        public long Size;
    }

    internal sealed class NetworkEngine : IDisposable
    {
        public const int DiscoveryPort = 48351;
        public const int TransferPort = 48352;
        private const long MaxFileSize = 20L * 1024 * 1024 * 1024;
        private readonly Guid _id = Guid.NewGuid();
        private readonly string _receiveDirectory;
        private readonly object _peerLock = new object();
        private readonly Dictionary<Guid, Peer> _peers = new Dictionary<Guid, Peer>();
        private UdpClient _udp;
        private TcpListener _listener;
        private volatile bool _running;
        private int _receiving;

        public event Action<List<Peer>> PeersChanged;
        public event Func<Offer, Task<bool>> OfferReceived;
        public event Action<Guid, double> ReceiveProgress;
        public event Action<Guid, string> ReceiveFinished;

        public NetworkEngine(string receiveDirectory = null)
        {
            _receiveDirectory = receiveDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Lazo");
        }

        public void Start()
        {
            _udp = new UdpClient(AddressFamily.InterNetwork);
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
            _udp.EnableBroadcast = true;
            _listener = new TcpListener(IPAddress.Any, TransferPort);
            _listener.Start(8);
            _running = true;
            Task.Run((Action)ListenDiscovery);
            Task.Run((Action)BroadcastLoop);
            Task.Run((Action)ListenTransfers);
        }

        private void ListenDiscovery()
        {
            while (_running)
            {
                try
                {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udp.Receive(ref remote);
                    if (data.Length > 512 || !IsLocalSubnet(remote.Address)) continue;
                    string[] fields = Encoding.UTF8.GetString(data).Split('|');
                    Guid id;
                    int port;
                    if (fields.Length != 4 || fields[0] != "LAZO1" ||
                        !Guid.TryParse(fields[1], out id) || id == _id ||
                        !int.TryParse(fields[3], out port) || port != TransferPort) continue;
                    string name = CleanLabel(fields[2]);
                    if (name.Length == 0) continue;
                    lock (_peerLock)
                    {
                        Peer previous;
                        bool changed = !_peers.TryGetValue(id, out previous) || previous.Name != name ||
                                       !previous.Address.Equals(remote.Address) || previous.Port != port;
                        _peers[id] = new Peer { Id = id, Name = name, Address = remote.Address, Port = port, SeenUtc = DateTime.UtcNow };
                        if (changed) PublishPeers();
                    }
                }
                catch (ObjectDisposedException) { return; }
                catch (SocketException) { if (!_running) return; }
                catch { /* Invalid LAN packet: ignore it. */ }
            }
        }

        private void BroadcastLoop()
        {
            byte[] payload = Encoding.UTF8.GetBytes("LAZO1|" + _id + "|" + CleanLabel(Environment.MachineName) + "|" + TransferPort);
            while (_running)
            {
                try
                {
                    foreach (IPAddress address in BroadcastAddresses())
                        _udp.Send(payload, payload.Length, new IPEndPoint(address, DiscoveryPort));
                    lock (_peerLock)
                    {
                        Guid[] stale = _peers.Where(p => (DateTime.UtcNow - p.Value.SeenUtc).TotalSeconds > 9)
                            .Select(p => p.Key).ToArray();
                        foreach (Guid key in stale) _peers.Remove(key);
                        if (stale.Length > 0) PublishPeers();
                    }
                }
                catch (ObjectDisposedException) { return; }
                catch (SocketException) { /* Firewall or disconnected adapter. Retry. */ }
                for (int i = 0; i < 25 && _running; i++) Thread.Sleep(100);
            }
        }

        private void PublishPeers()
        {
            Action<List<Peer>> handler = PeersChanged;
            if (handler != null) handler(_peers.Values.OrderBy(p => p.Name).ToList());
        }

        private static IEnumerable<IPAddress> BroadcastAddresses()
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (UnicastIPAddressInformation entry in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (entry.Address.AddressFamily != AddressFamily.InterNetwork || entry.IPv4Mask == null ||
                        !IsPrivate(entry.Address)) continue;
                    byte[] ip = entry.Address.GetAddressBytes();
                    byte[] mask = entry.IPv4Mask.GetAddressBytes();
                    byte[] broadcast = new byte[4];
                    for (int i = 0; i < 4; i++) broadcast[i] = (byte)(ip[i] | ~mask[i]);
                    IPAddress result = new IPAddress(broadcast);
                    if (seen.Add(result.ToString())) yield return result;
                }
            }
        }

        private static bool IsPrivate(IPAddress address)
        {
            byte[] b = address.GetAddressBytes();
            return b.Length == 4 && (b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
                   (b[0] == 192 && b[1] == 168));
        }

        private static bool IsLocalSubnet(IPAddress address)
        {
            if (!IsPrivate(address)) return false;
            byte[] target = address.GetAddressBytes();
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                foreach (UnicastIPAddressInformation entry in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (entry.Address.AddressFamily != AddressFamily.InterNetwork || entry.IPv4Mask == null) continue;
                    byte[] local = entry.Address.GetAddressBytes();
                    byte[] mask = entry.IPv4Mask.GetAddressBytes();
                    bool match = true;
                    for (int i = 0; i < 4; i++) if ((target[i] & mask[i]) != (local[i] & mask[i])) match = false;
                    if (match) return true;
                }
            }
            return false;
        }

        private void ListenTransfers()
        {
            while (_running)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    Task.Run(() => HandleTransfer(client));
                }
                catch (SocketException) { if (!_running) return; }
                catch (ObjectDisposedException) { return; }
            }
        }

        private async Task HandleTransfer(TcpClient client)
        {
            Guid offerId = Guid.Empty;
            string temp = null;
            bool accepted = false;
            bool ownsReceive = false;
            try
            {
                using (client)
                {
                    client.ReceiveTimeout = 120000;
                    client.SendTimeout = 30000;
                    IPAddress address = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
                    if (!IsLocalSubnet(address)) return;
                    using (NetworkStream stream = client.GetStream())
                    using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true))
                    using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
                    {
                        byte[] magic = reader.ReadBytes(5);
                        if (Encoding.ASCII.GetString(magic) != "LAZO1") return;
                        string sender = CleanLabel(ReadText(reader, 80));
                        string name = SafeFileName(ReadText(reader, 255));
                        long size = reader.ReadInt64();
                        if (sender.Length == 0 || name.Length == 0 || size < 0 || size > MaxFileSize) return;
                        offerId = Guid.NewGuid();
                        if (Interlocked.CompareExchange(ref _receiving, 1, 0) != 0)
                        {
                            writer.Write((byte)0); writer.Flush(); return;
                        }
                        ownsReceive = true;
                        Offer offer = new Offer { Id = offerId, Sender = sender, Address = address, FileName = name, Size = size };
                        Func<Offer, Task<bool>> handler = OfferReceived;
                        bool allow = handler != null && await handler(offer);
                        if (!allow) { writer.Write((byte)0); writer.Flush(); return; }
                        accepted = true;
                        writer.Write((byte)1); writer.Flush();
                        client.ReceiveTimeout = 30000;
                        string folder = _receiveDirectory;
                        Directory.CreateDirectory(folder);
                        temp = Path.Combine(folder, "." + offerId.ToString("N") + ".part");
                        byte[] buffer = new byte[64 * 1024];
                        long received = 0;
                        byte[] actual;
                        using (FileStream file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        using (SHA256 sha = SHA256.Create())
                        {
                            while (received < size)
                            {
                                int count = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, size - received));
                                if (count <= 0) throw new EndOfStreamException();
                                file.Write(buffer, 0, count);
                                sha.TransformBlock(buffer, 0, count, null, 0);
                                received += count;
                                Action<Guid, double> progress = ReceiveProgress;
                                if (progress != null) progress(offerId, size == 0 ? 1 : (double)received / size);
                            }
                            sha.TransformFinalBlock(new byte[0], 0, 0);
                            actual = sha.Hash;
                        }
                        byte[] expected = reader.ReadBytes(32);
                        if (expected.Length != 32 || !actual.SequenceEqual(expected)) throw new InvalidDataException("El archivo no superó la verificación de integridad.");
                        string destination = UniqueDestination(folder, name);
                        File.Move(temp, destination);
                        temp = null;
                        writer.Write((byte)1); writer.Flush();
                        Action<Guid, string> finished = ReceiveFinished;
                        if (finished != null) finished(offerId, "Guardado en Descargas\\Lazo");
                    }
                }
            }
            catch (Exception ex)
            {
                if (accepted)
                {
                    Action<Guid, string> finished = ReceiveFinished;
                    if (finished != null) finished(offerId, "Error: " + ex.Message);
                }
            }
            finally
            {
                if (temp != null) try { File.Delete(temp); } catch { }
                if (ownsReceive) Interlocked.Exchange(ref _receiving, 0);
            }
        }

        public async Task SendAsync(Peer peer, string path, Action<double> progress)
        {
            FileInfo info = new FileInfo(path);
            if (!info.Exists) throw new FileNotFoundException("El archivo ya no existe.");
            if (info.Length > MaxFileSize) throw new InvalidOperationException("Límite: 20 GB por archivo.");
            if (!IsLocalSubnet(peer.Address)) throw new InvalidOperationException("El equipo ya no está en la misma subred.");
            using (TcpClient client = new TcpClient(AddressFamily.InterNetwork))
            {
                Task connect = client.ConnectAsync(peer.Address, peer.Port);
                if (await Task.WhenAny(connect, Task.Delay(7000)) != connect)
                    throw new TimeoutException("El equipo no respondió.");
                await connect;
                client.ReceiveTimeout = 120000;
                client.SendTimeout = 30000;
                using (NetworkStream stream = client.GetStream())
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true))
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(Encoding.ASCII.GetBytes("LAZO1"));
                    WriteText(writer, CleanLabel(Environment.MachineName));
                    WriteText(writer, info.Name);
                    writer.Write(info.Length);
                    writer.Flush();
                    byte reply = reader.ReadByte();
                    if (reply != 1) throw new InvalidOperationException("El destinatario rechazó la transferencia o está ocupado.");
                    byte[] buffer = new byte[64 * 1024];
                    long sent = 0;
                    byte[] hash;
                    using (FileStream file = info.OpenRead())
                    using (SHA256 sha = SHA256.Create())
                    {
                        int count;
                        while ((count = await file.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await stream.WriteAsync(buffer, 0, count);
                            sha.TransformBlock(buffer, 0, count, null, 0);
                            sent += count;
                            if (progress != null) progress(info.Length == 0 ? 1 : (double)sent / info.Length);
                        }
                        sha.TransformFinalBlock(new byte[0], 0, 0);
                        hash = sha.Hash;
                    }
                    await stream.WriteAsync(hash, 0, hash.Length);
                    await stream.FlushAsync();
                    if (reader.ReadByte() != 1) throw new IOException("El destinatario no pudo guardar el archivo.");
                }
            }
        }

        private static string ReadText(BinaryReader reader, int maxBytes)
        {
            int length = reader.ReadUInt16();
            if (length > maxBytes) throw new InvalidDataException("Campo demasiado largo.");
            byte[] value = reader.ReadBytes(length);
            if (value.Length != length) throw new EndOfStreamException();
            return new UTF8Encoding(false, true).GetString(value);
        }

        private static void WriteText(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length > 255) throw new InvalidOperationException("Nombre demasiado largo.");
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        private static string CleanLabel(string value)
        {
            return new string(value.Where(c => !char.IsControl(c) && c != '|').Take(60).ToArray()).Trim();
        }

        private static string SafeFileName(string name)
        {
            if (name != Path.GetFileName(name) || name == "." || name == ".." || name.Length == 0) return "";
            if (name.Any(c => char.IsControl(c) || Path.GetInvalidFileNameChars().Contains(c))) return "";
            return name;
        }

        private static string UniqueDestination(string folder, string name)
        {
            string stem = Path.GetFileNameWithoutExtension(name);
            string ext = Path.GetExtension(name);
            string result = Path.Combine(folder, name);
            for (int i = 1; File.Exists(result); i++) result = Path.Combine(folder, stem + " (" + i + ")" + ext);
            return result;
        }

        public void Dispose()
        {
            _running = false;
            if (_udp != null) _udp.Close();
            if (_listener != null) _listener.Stop();
        }
    }
}
