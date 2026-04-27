using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace lingualink_client.Services.Audio
{
    /// <summary>
    /// Captures audio from a target process tree using Windows process loopback.
    /// Requires Windows support for AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK.
    /// </summary>
    public sealed class ProcessLoopbackCapture : IDisposable
    {
        private const string VirtualAudioDeviceProcessLoopback = "VIRTUAL_AUDIO_DEVICE_PROCESS_LOOPBACK";
        private const int S_OK = 0;
        private const int VT_BLOB = 65;
        private const int AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
        private const int AUDCLNT_STREAMFLAGS_EVENTCALLBACK = 0x00040000;
        private const long ReftimesPerSecond = 10_000_000;

        private static readonly Guid IAudioClientGuid = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
        private static readonly Guid IAudioCaptureClientGuid = new Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317");

        private readonly int _processId;
        private readonly object _lock = new object();

        private Thread? _captureThread;
        private ManualResetEvent? _audioSamplesReadyEvent;
        private TaskCompletionSource<bool>? _started;
        private volatile bool _stopRequested;
        private IAudioClient? _audioClient;
        private IAudioCaptureClient? _captureClient;
        private bool _disposed;

        public event EventHandler<WaveInEventArgs>? DataAvailable;
        public event EventHandler<StoppedEventArgs>? RecordingStopped;

        public WaveFormat WaveFormat { get; private set; } = new WaveFormat(48000, 32, 2);

        public ProcessLoopbackCapture(int processId)
        {
            if (processId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(processId));
            }

            _processId = processId;
        }

        public void StartRecording()
        {
            lock (_lock)
            {
                ThrowIfDisposed();

                if (_captureThread != null)
                {
                    return;
                }

                _stopRequested = false;
                _started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _captureThread = new Thread(CaptureThreadMain)
                {
                    IsBackground = true,
                    Name = "LinguaLink Process Loopback Capture"
                };
                _captureThread.SetApartmentState(ApartmentState.MTA);
                _captureThread.Start();
            }

            if (_started == null)
            {
                throw new InvalidOperationException("Process loopback capture did not initialize.");
            }

            _started.Task.GetAwaiter().GetResult();
        }

        public void StopRecording()
        {
            Thread? thread;
            lock (_lock)
            {
                thread = _captureThread;
                _stopRequested = true;
                _audioSamplesReadyEvent?.Set();
            }

            if (thread != null && thread != Thread.CurrentThread)
            {
                thread.Join(TimeSpan.FromSeconds(2));
            }
        }

        private void CaptureThreadMain()
        {
            Exception? stopException = null;

            try
            {
                InitializeCaptureAsync().GetAwaiter().GetResult();
                _started?.TrySetResult(true);
                CaptureLoop();
            }
            catch (Exception ex)
            {
                stopException = ex;
                _started?.TrySetException(ex);
            }
            finally
            {
                CleanupCaptureObjects();
                lock (_lock)
                {
                    _captureThread = null;
                }

                RecordingStopped?.Invoke(this, new StoppedEventArgs(stopException));
            }
        }

        private async Task InitializeCaptureAsync()
        {
            _audioClient = await ActivateAudioClientAsync(_processId);
            var mixFormatPtr = IntPtr.Zero;

            try
            {
                var hr = _audioClient.GetMixFormat(out mixFormatPtr);
                Marshal.ThrowExceptionForHR(hr);
                WaveFormat = WaveFormat.MarshalFromPtr(mixFormatPtr);

                _audioSamplesReadyEvent = new ManualResetEvent(false);
                var flags = AUDCLNT_STREAMFLAGS_LOOPBACK | AUDCLNT_STREAMFLAGS_EVENTCALLBACK;
                var audioSessionGuid = Guid.Empty;

                hr = _audioClient.Initialize(
                    AudioClientShareMode.Shared,
                    flags,
                    ReftimesPerSecond,
                    0,
                    mixFormatPtr,
                    ref audioSessionGuid);
                Marshal.ThrowExceptionForHR(hr);

                hr = _audioClient.SetEventHandle(_audioSamplesReadyEvent.SafeWaitHandle.DangerousGetHandle());
                Marshal.ThrowExceptionForHR(hr);

                var serviceGuid = IAudioCaptureClientGuid;
                hr = _audioClient.GetService(ref serviceGuid, out var captureClient);
                Marshal.ThrowExceptionForHR(hr);
                _captureClient = (IAudioCaptureClient)captureClient;

                hr = _audioClient.Start();
                Marshal.ThrowExceptionForHR(hr);
            }
            finally
            {
                if (mixFormatPtr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(mixFormatPtr);
                }
            }
        }

        private void CaptureLoop()
        {
            if (_audioSamplesReadyEvent == null || _captureClient == null)
            {
                throw new InvalidOperationException("Process loopback capture is not initialized.");
            }

            while (!_stopRequested)
            {
                _audioSamplesReadyEvent.WaitOne(500);
                ReadAvailablePackets();
            }
        }

        private void ReadAvailablePackets()
        {
            if (_captureClient == null)
            {
                return;
            }

            var hr = _captureClient.GetNextPacketSize(out var packetFrames);
            Marshal.ThrowExceptionForHR(hr);
            while (packetFrames > 0 && !_stopRequested)
            {
                hr = _captureClient.GetBuffer(
                    out var data,
                    out var framesAvailable,
                    out var flags,
                    out _,
                    out _);
                Marshal.ThrowExceptionForHR(hr);

                try
                {
                    var bytesRecorded = checked((int)(framesAvailable * WaveFormat.BlockAlign));
                    var buffer = new byte[bytesRecorded];

                    if ((flags & AudioClientBufferFlags.Silent) == 0 && data != IntPtr.Zero)
                    {
                        Marshal.Copy(data, buffer, 0, bytesRecorded);
                    }

                    DataAvailable?.Invoke(this, new WaveInEventArgs(buffer, bytesRecorded));
                }
                finally
                {
                    hr = _captureClient.ReleaseBuffer(framesAvailable);
                    Marshal.ThrowExceptionForHR(hr);
                }

                hr = _captureClient.GetNextPacketSize(out packetFrames);
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        private static async Task<IAudioClient> ActivateAudioClientAsync(int processId)
        {
            var activationParamsPtr = IntPtr.Zero;
            var blobPtr = IntPtr.Zero;

            try
            {
                var activationParams = new AudioClientActivationParams
                {
                    ActivationType = AudioClientActivationType.ProcessLoopback,
                    ProcessLoopbackParams = new AudioClientProcessLoopbackParams
                    {
                        TargetProcessId = (uint)processId,
                        ProcessLoopbackMode = ProcessLoopbackMode.IncludeTargetProcessTree
                    }
                };

                blobPtr = Marshal.AllocHGlobal(Marshal.SizeOf<AudioClientActivationParams>());
                Marshal.StructureToPtr(activationParams, blobPtr, false);

                var propVariant = new PropVariant
                {
                    vt = VT_BLOB,
                    blob = new Blob
                    {
                        cbSize = Marshal.SizeOf<AudioClientActivationParams>(),
                        pBlobData = blobPtr
                    }
                };

                activationParamsPtr = Marshal.AllocHGlobal(Marshal.SizeOf<PropVariant>());
                Marshal.StructureToPtr(propVariant, activationParamsPtr, false);

                var completionHandler = new ActivateAudioInterfaceCompletionHandler();
                var audioClientGuid = IAudioClientGuid;
                var hr = ActivateAudioInterfaceAsync(
                    VirtualAudioDeviceProcessLoopback,
                    ref audioClientGuid,
                    activationParamsPtr,
                    completionHandler,
                    out _);
                Marshal.ThrowExceptionForHR(hr);

                var activatedInterface = await completionHandler.Task.ConfigureAwait(false);
                return (IAudioClient)activatedInterface;
            }
            finally
            {
                if (activationParamsPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(activationParamsPtr);
                }

                if (blobPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(blobPtr);
                }
            }
        }

        private void CleanupCaptureObjects()
        {
            try
            {
                _audioClient?.Stop();
            }
            catch
            {
                // Ignore cleanup failures.
            }

            ReleaseComObject(_captureClient);
            ReleaseComObject(_audioClient);
            _captureClient = null;
            _audioClient = null;

            _audioSamplesReadyEvent?.Dispose();
            _audioSamplesReadyEvent = null;
        }

        private static void ReleaseComObject(object? value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                Marshal.ReleaseComObject(value);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ProcessLoopbackCapture));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StopRecording();
            GC.SuppressFinalize(this);
        }

        [DllImport("Mmdevapi.dll", ExactSpelling = true, PreserveSig = true)]
        private static extern int ActivateAudioInterfaceAsync(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
            ref Guid riid,
            IntPtr activationParams,
            IActivateAudioInterfaceCompletionHandler completionHandler,
            out IActivateAudioInterfaceAsyncOperation activationOperation);

        [ComImport]
        [Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioClient
        {
            [PreserveSig]
            int Initialize(
                AudioClientShareMode shareMode,
                int streamFlags,
                long bufferDuration,
                long periodicity,
                IntPtr format,
                ref Guid audioSessionGuid);

            [PreserveSig]
            int GetBufferSize(out uint bufferSize);

            [PreserveSig]
            int GetStreamLatency(out long latency);

            [PreserveSig]
            int GetCurrentPadding(out uint currentPadding);

            [PreserveSig]
            int IsFormatSupported(AudioClientShareMode shareMode, IntPtr format, out IntPtr closestMatch);

            [PreserveSig]
            int GetMixFormat(out IntPtr deviceFormat);

            [PreserveSig]
            int GetDevicePeriod(out long defaultDevicePeriod, out long minimumDevicePeriod);

            [PreserveSig]
            int Start();

            [PreserveSig]
            int Stop();

            [PreserveSig]
            int Reset();

            [PreserveSig]
            int SetEventHandle(IntPtr eventHandle);

            [PreserveSig]
            int GetService(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
        }

        [ComImport]
        [Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioCaptureClient
        {
            [PreserveSig]
            int GetBuffer(
                out IntPtr data,
                out uint numFramesToRead,
                out AudioClientBufferFlags bufferFlags,
                out long devicePosition,
                out long qpcPosition);

            [PreserveSig]
            int ReleaseBuffer(uint numFramesRead);

            [PreserveSig]
            int GetNextPacketSize(out uint numFramesInNextPacket);
        }

        [ComImport]
        [Guid("41D949AB-9862-444A-80F6-C261334DA5EB")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IActivateAudioInterfaceCompletionHandler
        {
            void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation);
        }

        [ComImport]
        [Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IActivateAudioInterfaceAsyncOperation
        {
            void GetActivateResult(
                out int activateResult,
                [MarshalAs(UnmanagedType.IUnknown)] out object activatedInterface);
        }

        [ComVisible(true)]
        [ClassInterface(ClassInterfaceType.None)]
        private sealed class ActivateAudioInterfaceCompletionHandler : IActivateAudioInterfaceCompletionHandler
        {
            private readonly TaskCompletionSource<object> _completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<object> Task => _completion.Task;

            public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation)
            {
                try
                {
                    activateOperation.GetActivateResult(out var activateResult, out var activatedInterface);
                    if (activateResult != S_OK)
                    {
                        _completion.TrySetException(Marshal.GetExceptionForHR(activateResult) ?? new IOException($"Process loopback activation failed: 0x{activateResult:X8}"));
                        return;
                    }

                    _completion.TrySetResult(activatedInterface);
                }
                catch (Exception ex)
                {
                    _completion.TrySetException(ex);
                }
            }
        }

        private enum AudioClientActivationType
        {
            Default = 0,
            ProcessLoopback = 1
        }

        private enum ProcessLoopbackMode
        {
            IncludeTargetProcessTree = 0,
            ExcludeTargetProcessTree = 1
        }

        private enum AudioClientShareMode
        {
            Shared = 0,
            Exclusive = 1
        }

        [Flags]
        private enum AudioClientBufferFlags
        {
            None = 0,
            DataDiscontinuity = 1,
            Silent = 2,
            TimestampError = 4
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioClientActivationParams
        {
            public AudioClientActivationType ActivationType;
            public AudioClientProcessLoopbackParams ProcessLoopbackParams;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioClientProcessLoopbackParams
        {
            public uint TargetProcessId;
            public ProcessLoopbackMode ProcessLoopbackMode;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct PropVariant
        {
            [FieldOffset(0)]
            public ushort vt;

            [FieldOffset(8)]
            public Blob blob;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Blob
        {
            public int cbSize;
            public IntPtr pBlobData;
        }
    }
}
