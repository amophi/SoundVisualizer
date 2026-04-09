using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

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

    public class AudioCaptureEngine : IMMNotificationClient
    {
        private WasapiLoopbackCapture? _captureDevice;
        private MMDeviceEnumerator? _notificationEnumerator;
        private CancellationTokenSource? _restartCts;
        private bool _isCapturing;
        private bool _firstDataLogged;

        // Latency 측정용
        private readonly Stopwatch _latencyWatch = new();
        private double _totalLatencyMs;
        private long _latencySampleCount;
        private int _logInterval = 100; // N회마다 콘솔 출력

        public event EventHandler<AudioDataAvailableEventArgs>? OnAudioDataAvailable;
        public event EventHandler<string>? OnCaptureError;

        public WaveFormat? CaptureFormat => _captureDevice?.WaveFormat;
        public bool IsCapturing => _isCapturing;
        public double AverageLatencyMs => _latencySampleCount > 0 ? _totalLatencyMs / _latencySampleCount : 0;
        public double LastLatencyMs { get; private set; }

        public void StartCapture()
        {
            // 장치 변경 알림 등록 (최초 1회)
            if (_notificationEnumerator == null)
            {
                _notificationEnumerator = new MMDeviceEnumerator();
                _notificationEnumerator.RegisterEndpointNotificationCallback(this);
            }
            StartCaptureDevice();
        }

        private void StartCaptureDevice()
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                if (devices.Count == 0)
                {
                    string msg = "🚨 출력 장치를 찾을 수 없습니다.";
                    Console.WriteLine(msg);
                    OnCaptureError?.Invoke(this, msg);
                    return;
                }

                // 실제 소리가 나오는 기본 출력 장치에서 캡처
                // CABLE Input 등 가상 장치를 우선하면 해당 장치가 기본 출력이 아닐 때 소리가 안 잡힘
                var targetDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                Console.WriteLine($"🎯 캡처 대상: {targetDevice.FriendlyName}");

                _captureDevice = new WasapiLoopbackCapture(targetDevice);

                _captureDevice.DataAvailable += (sender, args) =>
                {
                    if (args.BytesRecorded == 0) return;

                    if (!_firstDataLogged)
                    {
                        Console.WriteLine($"✅ 오디오 데이터 수신 확인 — bytes: {args.BytesRecorded}, 채널: {_captureDevice.WaveFormat.Channels}");
                        _firstDataLogged = true;
                    }

                    _latencyWatch.Restart();

                    byte[] validData = new byte[args.BytesRecorded];
                    Array.Copy(args.Buffer, validData, args.BytesRecorded);

                    var eventArgs = new AudioDataAvailableEventArgs(validData, _captureDevice.WaveFormat.Channels);
                    OnAudioDataAvailable?.Invoke(this, eventArgs);

                    _latencyWatch.Stop();
                    LastLatencyMs = _latencyWatch.Elapsed.TotalMilliseconds;
                    _totalLatencyMs += LastLatencyMs;
                    _latencySampleCount++;

                    if (_latencySampleCount % _logInterval == 0)
                    {
                        Console.WriteLine($"⏱ Latency: {LastLatencyMs:F3}ms (평균: {AverageLatencyMs:F3}ms, 샘플: {_latencySampleCount})");
                    }
                };

                _captureDevice.RecordingStopped += (sender, args) =>
                {
                    _isCapturing = false;

                    if (args.Exception != null)
                    {
                        string msg = $"🚨 캡처 비정상 종료: {args.Exception.Message}";
                        Console.WriteLine(msg);
                        OnCaptureError?.Invoke(this, msg);
                    }
                    else
                    {
                        Console.WriteLine("🛑 오디오 캡처 정상 종료.");
                    }
                };

                _captureDevice.StartRecording();
                _isCapturing = true;
                Console.WriteLine($"🔥 오디오 후킹 시작: {targetDevice.FriendlyName}");
                Console.WriteLine($"📊 감지된 채널 수: {_captureDevice.WaveFormat.Channels}채널");
            }
            catch (Exception ex)
            {
                _isCapturing = false;
                string msg = $"🚨 캡처 엔진 시작 실패: {ex.Message}";
                Console.WriteLine(msg);
                OnCaptureError?.Invoke(this, msg);
            }
        }

        public void StopCapture()
        {
            _restartCts?.Cancel();
            if (_notificationEnumerator != null)
            {
                try { _notificationEnumerator.UnregisterEndpointNotificationCallback(this); }
                catch { }
            }
            StopCaptureDevice();
        }

        private void StopCaptureDevice()
        {
            try
            {
                if (_captureDevice != null)
                {
                    _captureDevice.StopRecording();
                    _captureDevice.Dispose();
                    _captureDevice = null;
                    Console.WriteLine("🛑 오디오 후킹 중지됨.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ 캡처 중지 중 오류: {ex.Message}");
            }
            finally
            {
                _isCapturing = false;
            }
        }

        // 기본 출력 장치 변경 감지 → 300ms 디바운스 후 캡처 장치 자동 재시작
        void IMMNotificationClient.OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow != DataFlow.Render || role != Role.Multimedia) return;

            Console.WriteLine("🔄 기본 출력 장치 변경 감지 — 캡처 재시작 예약...");

            _restartCts?.Cancel();
            _restartCts = new CancellationTokenSource();
            var token = _restartCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300, token); // 장치 초기화 안정화 대기
                    if (token.IsCancellationRequested) return;

                    Console.WriteLine("🔄 새 기본 출력 장치로 캡처 재시작...");
                    StopCaptureDevice();
                    _firstDataLogged = false;
                    StartCaptureDevice();
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Console.WriteLine($"🚨 캡처 재시작 실패: {ex.Message}");
                }
            });
        }

        void IMMNotificationClient.OnDeviceAdded(string pwstrDeviceId) { }
        void IMMNotificationClient.OnDeviceRemoved(string pwstrDeviceId) { }
        void IMMNotificationClient.OnDeviceStateChanged(string deviceId, DeviceState newState) { }
        void IMMNotificationClient.OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}