namespace lingualink_client.Models
{
    public sealed class PeerAudioCaptureTarget
    {
        public int? ProcessId { get; }
        public string DisplayName { get; }

        public bool IsSystemAudio => !ProcessId.HasValue;

        public PeerAudioCaptureTarget(int? processId, string displayName)
        {
            ProcessId = processId;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }
}
