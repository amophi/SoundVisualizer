// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
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
        public int BytesRecorded { get; set; }
        public int Channels { get; set; }

        public AudioDataAvailableEventArgs(byte[] buffer, int bytesRecorded, int channels)
        {
            Buffer = buffer;
            BytesRecorded = bytesRecorded;
            Channels = channels;
        }
    }

    public class AudioCaptureEngine : IMMNotificationClient, IDisposable
    {
        private WasapiLoopbackCapture? _captureDevice;
        private MMDeviceEnumerator? _notificationEnumerator;
        private CancellationTokenSource? _restartCts;
        private bool _isCapturing;
        private bool _firstDataLogged;
        private bool _isDisposed;

        // Latency 측정용
        private readonly Stopwatch _latencyWatch = new();
        private double _totalLatencyMs;
        private long _latencySampleCount;
        private int _logInterval = 100; // N회마다 콘솔 출력

        public event EventHandler<AudioDataAvailableEventArgs>? OnAudioDataAvailable;
        public event EventHandler<string>? OnCaptureError;
        public event EventHandler<int>? OnChannelsChanged;

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
            if (_isDisposed) return;

            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                if (devices.Count == 0)
                {
                    string msg = "출력 장치를 찾을 수 없습니다.";
                    OnCaptureError?.Invoke(this, msg);
                    return;
                }

                // 실제 소리가 나오는 기본 출력 장치에서 캡처
                // CABLE Input 등 가상 장치를 우선하면 해당 장치가 기본 출력이 아닐 때 소리가 안 잡힘
                var targetDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                int currentChannels = targetDevice.AudioClient.MixFormat.Channels;
                OnChannelsChanged?.Invoke(this, currentChannels);


                _captureDevice = new WasapiLoopbackCapture(targetDevice);
                var format = _captureDevice.WaveFormat;
                int channels = format.Channels;

                _captureDevice.DataAvailable += (sender, args) =>
                {
                    if (args.BytesRecorded == 0) return;

                    if (!_firstDataLogged)
                    {
                        _firstDataLogged = true;
                    }

                    _latencyWatch.Restart();

                    var eventArgs = new AudioDataAvailableEventArgs(args.Buffer, args.BytesRecorded, channels);
                    OnAudioDataAvailable?.Invoke(this, eventArgs);

                    _latencyWatch.Stop();
                    LastLatencyMs = _latencyWatch.Elapsed.TotalMilliseconds;
                    _totalLatencyMs += LastLatencyMs;
                    _latencySampleCount++;

                    if (_latencySampleCount % _logInterval == 0)
                    {
                    }
                };

                _captureDevice.RecordingStopped += (sender, args) =>
                {
                    _isCapturing = false;

                    if (args.Exception != null)
                    {
                        string msg = $"캡처 비정상 종료: {args.Exception.Message}";
                        OnCaptureError?.Invoke(this, msg);
                    }
                    else
                    {
                    }
                };

                _captureDevice.StartRecording();
                _isCapturing = true;
            }
            catch (Exception ex)
            {
                _isCapturing = false;
                string msg = $"캡처 엔진 시작 실패: {ex.Message}";
                OnCaptureError?.Invoke(this, msg);
            }
        }

        public void StopCapture()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _restartCts?.Cancel();
            _restartCts?.Dispose();
            _restartCts = null;

            if (_notificationEnumerator != null)
            {
                try 
                { 
                    _notificationEnumerator.UnregisterEndpointNotificationCallback(this);
                    _notificationEnumerator.Dispose();
                }
                catch { }
                _notificationEnumerator = null;
            }
            StopCaptureDevice();
        }

        public void Dispose()
        {
            StopCapture();
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
                }
            }
            catch (Exception ex)
            {
            }
            finally
            {
                _isCapturing = false;
            }
        }

        // 장치 변경 또는 속성 변경(스피커 구성 등) 감지 시 캡처 장치 재시작
        private void TriggerRestart()
        {
            if (_isDisposed) return;

            _restartCts?.Cancel();
            _restartCts = new CancellationTokenSource();
            var token = _restartCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(500, token); // 장치 초기화 안정화 대기
                    if (token.IsCancellationRequested || _isDisposed) return;

                    StopCaptureDevice();
                    if (_isDisposed) return;
                    _firstDataLogged = false;
                    StartCaptureDevice();
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                }
            });
        }

        void IMMNotificationClient.OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow != DataFlow.Render || role != Role.Multimedia) return;
            TriggerRestart();
        }

        void IMMNotificationClient.OnDeviceAdded(string pwstrDeviceId) { }
        void IMMNotificationClient.OnDeviceRemoved(string pwstrDeviceId) { }
        
        void IMMNotificationClient.OnDeviceStateChanged(string deviceId, DeviceState newState) 
        {
            TriggerRestart();
        }
        
        void IMMNotificationClient.OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) 
        {
            TriggerRestart();
        }
    }
}
