using System;
using System.Linq;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace SoundVisualizer.CoreAudio
{
    public class AudioDataAvailableEventArgs : EventArgs
    {
        public byte[] Buffer { get; set; }
        public int Channels { get; set; }

        public AudioDataAvailableEventArgs(byte[] buffer, int channels)
        {
            Buffer = buffer;
            Channels = channels;
        }
    }

    public class AudioCaptureEngine
    {
        private WasapiLoopbackCapture _captureDevice;

        public event EventHandler<AudioDataAvailableEventArgs> OnAudioDataAvailable;

        public WaveFormat? CaptureFormat => _captureDevice?.WaveFormat;

        public void StartCapture()
        {
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            var targetDevice = devices.FirstOrDefault(d => d.FriendlyName.Contains("CABLE Input"));

            if (targetDevice == null)
            {
                Console.WriteLine("⚠ 가상 케이블이 없습니다. 기본 출력 장치로 대체합니다.");
                targetDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }

            _captureDevice = new WasapiLoopbackCapture(targetDevice);

            _captureDevice.DataAvailable += (sender, args) =>
            {
                if (args.BytesRecorded == 0) return;

                byte[] validData = new byte[args.BytesRecorded];
                Array.Copy(args.Buffer, validData, args.BytesRecorded);

                var eventArgs = new AudioDataAvailableEventArgs(validData, _captureDevice.WaveFormat.Channels);
                OnAudioDataAvailable?.Invoke(this, eventArgs);
            };

            _captureDevice.StartRecording();
            Console.WriteLine($"🔥 오디오 후킹 시작: {targetDevice.FriendlyName}");
            Console.WriteLine($"📊 감지된 채널 수: {_captureDevice.WaveFormat.Channels}채널");
        }

        public void StopCapture()
        {
            if (_captureDevice != null)
            {
                _captureDevice.StopRecording();
                _captureDevice.Dispose();
                Console.WriteLine("🛑 오디오 후킹 중지됨.");
            }
        }
    }
}