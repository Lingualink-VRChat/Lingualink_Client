using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using lingualink_client.Models; // Assuming Models.cs is in Models folder

namespace lingualink_client.Services
{
    public class MicrophoneManager
    {
        public List<MMDeviceWrapper> GetAvailableMicrophones(out MMDeviceWrapper? defaultDeviceWrapper)
        {
            var wrappers = new List<MMDeviceWrapper>();
            defaultDeviceWrapper = null;

            using (var enumerator = new MMDeviceEnumerator())
            {
                var mmDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
                if (mmDevices.Count == 0)
                {
                    return wrappers;
                }

                for (int i = 0; i < mmDevices.Count; i++)
                {
                    var mmDevice = mmDevices[i];
                    int waveInIndex = -1;
                    for (int n = 0; n < WaveIn.DeviceCount; n++)
                    {
                        var caps = WaveIn.GetCapabilities(n);
                        if (mmDevice.FriendlyName.Contains(caps.ProductName) || caps.ProductName.Contains(mmDevice.FriendlyName.Substring(0, Math.Min(mmDevice.FriendlyName.Length, 15))))
                        {
                            waveInIndex = n;
                            break;
                        }
                    }
                    if (waveInIndex == -1 && i < WaveIn.DeviceCount)
                    {
                        waveInIndex = i;
                    }
                    var wrapper = new MMDeviceWrapper(mmDevice, waveInIndex);
                    wrappers.Add(wrapper);
                    mmDevice.Dispose(); // Dispose immediately after use
                }

                MMDevice? defaultMmDevice = null;
                try
                {
                    defaultMmDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                    if (defaultMmDevice != null)
                    {
                        defaultDeviceWrapper = wrappers.FirstOrDefault(w => w.ID == defaultMmDevice.ID);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting default mic: {ex.Message}");
                }
                finally
                {
                    defaultMmDevice?.Dispose();
                }
            }
            return wrappers;
        }
    }
}