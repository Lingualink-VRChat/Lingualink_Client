// File: Services/OscService.cs
using System;
using System.Threading.Tasks;
// Add this using statement after installing OscCore NuGet package:
// using OscCore; 

namespace lingualink_client.Services
{
    // Placeholder for OscCore.OscSender and OscCore.OscMessage
    // In a real scenario, these would come from the OscCore library.
    namespace OscCore // Mocking OscCore for demonstration if not available
    {
        public class OscMessage
        {
            public string Address { get; }
            public object[] Arguments { get; }

            public OscMessage(string address, params object[] args)
            {
                Address = address;
                Arguments = args;
            }
        }

        public class OscSender // : IDisposable // Actual OscSender might not be IDisposable
        {
            private readonly string _ipAddress;
            private readonly int _port;
            private System.Net.Sockets.UdpClient? _udpClient;


            public OscSender(string ipAddress, int port)
            {
                _ipAddress = ipAddress;
                _port = port;
                // Actual OscCore.OscSender constructor might throw on invalid IP/port.
                // This is a simplified mock.
                try
                {
                    _udpClient = new System.Net.Sockets.UdpClient();
                    _udpClient.Connect(ipAddress, port); // For UDP, Connect just sets default remote host
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"OscSender: Error initializing UdpClient for {_ipAddress}:{_port} - {ex.Message}");
                    throw; // Re-throw to be caught by OscService constructor caller
                }
            }
            
            // Actual Send method in OscCore takes an OscMessage.
            // public void Send(OscMessage message) { /* ... actual send logic ... */ }
            // Simplified for this example:
            public void SendRaw(OscCore.OscMessage message)
            {
                 // This is a conceptual representation. Actual OscCore handles serialization.
                var bytes = new byte[0]; // Placeholder for serialized OSC message
                
                // Simplified serialization for concept:
                using (var stream = new System.IO.MemoryStream())
                using (var writer = new System.IO.BinaryWriter(stream))
                {
                    // Super simplified path for /chatbox/input
                    // Actual OSC format is more complex (see http://opensoundcontrol.org/spec-1_0)
                    // Address (null-terminated string, padded to 4 bytes)
                    WriteString(writer, message.Address);
                    // Type tag string, e.g., ",sbb" (null-terminated, padded)
                    WriteString(writer, GetTypeTagString(message.Arguments));

                    // Arguments (padded to 4 bytes each based on type)
                    foreach (var arg in message.Arguments)
                    {
                        if (arg is string s) WriteString(writer, s);
                        else if (arg is bool b) writer.Write(b ? BitConverter.GetBytes(1) : BitConverter.GetBytes(0)); // OSC booleans are T/F atoms, not int
                        // OscCore handles proper serialization of T/F atoms or int based on spec.
                        // For simplicity, let's assume int32 1 or 0 for bool for this mock.
                        // Actual OscCore uses specific atoms for True/False.
                        // For this simplified mock, let's just send the string argument:
                        else if (arg is int i) writer.Write(System.Net.IPAddress.HostToNetworkOrder(i));
                        // ... other types ...
                    }
                    bytes = stream.ToArray();
                }


                if (_udpClient == null) throw new InvalidOperationException("UdpClient not initialized or disposed.");
                 _udpClient.Send(bytes, bytes.Length); // This is synchronous
                System.Diagnostics.Debug.WriteLine($"OscSender: Sent message to {message.Address} at {_ipAddress}:{_port}");
            }

            private string GetTypeTagString(object[] args)
            {
                var sb = new System.Text.StringBuilder(",");
                foreach(var arg in args)
                {
                    if (arg is string) sb.Append('s');
                    else if (arg is bool) sb.Append(arg.Equals(true) ? 'T' : 'F'); // OSC True/False atoms
                    else if (arg is int) sb.Append('i');
                    // ... other types
                }
                return sb.ToString();
            }

            private void WriteString(System.IO.BinaryWriter writer, string s)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(s);
                writer.Write(bytes);
                writer.Write((byte)0); // Null terminator
                PadToMultipleOfFour(writer, bytes.Length + 1);
            }
            private void PadToMultipleOfFour(System.IO.BinaryWriter writer, int currentLength)
            {
                int remainder = currentLength % 4;
                if (remainder > 0)
                {
                    for (int i = 0; i < 4 - remainder; i++)
                    {
                        writer.Write((byte)0);
                    }
                }
            }

            // If OscSender were IDisposable (it's not in OscCore standard lib AFAIK)
            public void Dispose()
            {
                _udpClient?.Dispose();
                _udpClient = null;
            }
        }
    }


    public class OscService : IDisposable
    {
        private OscCore.OscSender? _sender; // Use actual OscCore.OscSender
        private const int VRChatChatboxCharacterLimit = 144;

        public OscService(string ipAddress, int port)
        {
            try
            {
                // Ensure you have using OscCore; at the top
                _sender = new OscCore.OscSender(ipAddress, port);
                System.Diagnostics.Debug.WriteLine($"OscService initialized for {ipAddress}:{port}");
            }
            catch (Exception ex)
            {
                // Log or handle specific exceptions like SocketException, ArgumentNullException, etc.
                System.Diagnostics.Debug.WriteLine($"OscService: Failed to initialize OscSender for {ipAddress}:{port}. Error: {ex.Message}");
                throw new InvalidOperationException($"Failed to initialize OSC sender for {ipAddress}:{port}. Ensure IP and port are valid and VRChat is configured for OSC.", ex);
            }
        }

        public async Task SendChatboxMessageAsync(string text, bool sendImmediately, bool playNotificationSound)
        {
            if (_sender == null)
            {
                System.Diagnostics.Debug.WriteLine("OscService: Sender is not initialized. Cannot send message.");
                // Or throw new InvalidOperationException("OSC Sender is not initialized.");
                return; 
            }

            string truncatedText = TruncateString(text, VRChatChatboxCharacterLimit);

            // VRChat expects /chatbox/input with 3 arguments: string, bool, bool
            // The booleans are typically represented as OSC True/False atoms, or sometimes integers 1/0.
            // OscCore handles the correct representation.
            var oscMessage = new OscCore.OscMessage("/chatbox/input", truncatedText, sendImmediately, playNotificationSound);
            
            try
            {
                // OscCore's Send is synchronous. Wrap in Task.Run for async behavior.
                await Task.Run(() => _sender.SendRaw(oscMessage)); // Use _sender.Send(oscMessage) with actual OscCore
                System.Diagnostics.Debug.WriteLine($"OSC: Sent to /chatbox/input: '{truncatedText.Replace("\n", "\\n")}' (Imm: {sendImmediately}, Sound: {playNotificationSound})");
            }
            catch (Exception ex)
            {
                // Log the exception (e.g., System.Net.Sockets.SocketException if host is unreachable)
                System.Diagnostics.Debug.WriteLine($"OscService: Error sending OSC message: {ex.Message}");
                throw; // Re-throw to be handled by the caller (MainWindowViewModel)
            }
        }

        private string TruncateString(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            // Simple truncation. Consider grapheme clusters if dealing with complex scripts, but for now this is standard.
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        public void Dispose()
        {
            // If OscCore.OscSender implements IDisposable, call dispose here.
            // (_sender as IDisposable)?.Dispose();
            // As per OscCore source, OscSender itself is not IDisposable in the public API for .NET Standard
            // but it does hold a UdpClient. If this service is long-lived and re-created,
            // ensure the UdpClient resources are managed.
            // For now, if _sender has a Dispose method (e.g. in some builds/forks):
            // var disposableSender = _sender as IDisposable;
            // disposableSender?.Dispose();
            _sender?.Dispose(); // Call if the mock/actual OscSender has Dispose
            _sender = null!;
            System.Diagnostics.Debug.WriteLine("OscService disposed.");
        }
    }
}