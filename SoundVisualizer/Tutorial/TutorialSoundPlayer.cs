// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SoundVisualizer.Tutorial
{
    /// <summary>
    /// 튜토리얼 소리를 내는 곳(Android TutorialSoundPlayer, #327). 소리 켜기를 누르면 <see cref="TryOpen"/> 으로 만들고, 끄거나
    /// 튜토리얼을 닫으면 Dispose 한다.
    ///
    /// - Windows 기본 출력 장치로 공유 모드로 낸다. 시스템 볼륨을 따르고, 장치의 샘플레이트·채널 수로 바꾸는 것은 Windows 가 한다
    ///   (NAudio WasapiOut 이 공유 모드에서 AUTOCONVERTPCM 을 켠다).
    /// - 조각은 처음 켤 때 한 번 읽어 둔다(16비트 모노 WAV 넷, 합쳐 약 470KB).
    /// - 출력 장치가 없거나 열지 못하면 만들지 않는다(null). 튜토리얼은 그림만으로 그대로 돈다.
    ///
    /// Play·StopAll·Dispose 는 UI 스레드에서 부르고, 섞기는 NAudio 의 출력 스레드가 한다(MixingSampleProvider 가 입력 목록을 잠근다).
    /// </summary>
    internal sealed class TutorialSoundPlayer : IDisposable
    {
        /// <summary>조각의 샘플레이트. 안드로이드 원본 그대로다.</summary>
        private const int SampleRate = 44100;

        /// <summary>한꺼번에 나는 소리 수. 경적 세 번이 겹쳐도 넉넉하다(안드로이드와 같다).</summary>
        private const int MaxStreams = 4;

        /// <summary>출력 지연(밀리초). 그림의 박자와 어긋나 보이지 않을 만큼 짧게 둔다.</summary>
        private const int LatencyMs = 80;

        private static readonly WaveFormat MixFormat = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 2);

        // 조각은 앱이 도는 동안 한 번만 읽는다. UI 스레드에서만 읽고 쓴다.
        private static Dictionary<TutorialClip, float[]>? _clips;

        private readonly WasapiOut _output;
        private readonly MixingSampleProvider _mixer;
        private readonly Dictionary<TutorialClip, float[]> _clipSamples;
        private readonly Queue<ISampleProvider> _streams = new();
        private bool _disposed;

        private TutorialSoundPlayer(WasapiOut output, MixingSampleProvider mixer, Dictionary<TutorialClip, float[]> clips)
        {
            _output = output;
            _mixer = mixer;
            _clipSamples = clips;
            _output.PlaybackStopped += (s, e) =>
            {
                // 쓰던 장치를 뽑는 등으로 멈췄다. 튜토리얼은 소리 없이 계속 보인다.
                if (e.Exception != null) AppLog.Warn("tutorial", "튜토리얼 소리를 내던 출력 장치가 멈췄습니다.", e.Exception);
            };
        }

        /// <summary>
        /// 기본 출력 장치를 열어 소리를 낼 준비를 한다. 출력 장치가 없거나(이 PC 처럼) 열지 못하면 이유를 남기고 null 을 돌려준다.
        /// </summary>
        public static TutorialSoundPlayer? TryOpen()
        {
            WasapiOut? output = null;
            try
            {
                var clips = _clips ??= LoadClips();
                // 아무것도 나지 않는 동안에도 조용한 소리를 내며 돈다. 소리마다 장치를 새로 열면 첫 소리가 늦는다.
                var mixer = new MixingSampleProvider(MixFormat) { ReadFully = true };
                output = new WasapiOut(AudioClientShareMode.Shared, useEventSync: true, LatencyMs);
                output.Init(mixer);
                output.Play();
                return new TutorialSoundPlayer(output, mixer, clips);
            }
            catch (Exception ex)
            {
                output?.Dispose();
                AppLog.Warn("tutorial", "튜토리얼 소리를 낼 출력 장치를 열지 못해 소리 없이 보입니다.", ex);
                return null;
            }
        }

        /// <summary>소리 하나를 왼쪽·오른쪽 크기대로 낸다. 너무 많이 겹치면 가장 오래된 것을 끊는다.</summary>
        public void Play(TutorialCue cue)
        {
            if (_disposed) return;
            var stream = new ClipStream(_clipSamples[cue.Clip], cue.Left, cue.Right);
            _mixer.AddMixerInput(stream);
            _streams.Enqueue(stream);
            // 이미 끝나 섞기에서 빠진 것도 세지만, 빼는 것은 해가 없다.
            while (_streams.Count > MaxStreams) _mixer.RemoveMixerInput(_streams.Dequeue());
        }

        /// <summary>나던 소리를 모두 멈춘다. 장치는 열어 둔다.</summary>
        public void StopAll()
        {
            if (_disposed) return;
            _mixer.RemoveAllMixerInputs();
            _streams.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            StopAll();
            _disposed = true;
            try
            {
                _output.Stop();
                _output.Dispose();
            }
            catch (Exception ex)
            {
                AppLog.Warn("tutorial", "튜토리얼 소리 장치를 닫지 못했습니다.", ex);
            }
        }

        /// <summary>앱에 넣어 둔 조각 넷을 읽는다. 16비트 모노 44.1kHz 가 아니면 잘못 넣은 파일이다.</summary>
        private static Dictionary<TutorialClip, float[]> LoadClips()
        {
            var clips = new Dictionary<TutorialClip, float[]>();
            foreach (var clip in Enum.GetValues<TutorialClip>())
            {
                var uri = new Uri("/Tutorial/Sounds/" + FileName(clip), UriKind.Relative);
                var resource = Application.GetResourceStream(uri) ?? throw new FileNotFoundException("Tutorial clip is missing.", uri.OriginalString);
                using var stream = resource.Stream;
                using var reader = new WaveFileReader(stream);
                var format = reader.WaveFormat;
                if (format.Encoding != WaveFormatEncoding.Pcm || format.BitsPerSample != 16 || format.Channels != 1 || format.SampleRate != SampleRate)
                {
                    throw new InvalidDataException("Tutorial clip is not 16-bit mono 44.1 kHz PCM: " + uri.OriginalString);
                }
                var samples = new float[reader.SampleCount];
                int read = reader.ToSampleProvider().Read(samples, 0, samples.Length);
                clips[clip] = read == samples.Length ? samples : samples[..read];
            }
            return clips;
        }

        private static string FileName(TutorialClip clip) => clip switch
        {
            TutorialClip.Chirp => "tutorial_chirp.wav",
            TutorialClip.Birds => "tutorial_birds.wav",
            TutorialClip.Talk => "tutorial_talk.wav",
            _ => "tutorial_honk.wav",
        };

        /// <summary>모노 조각 하나를 왼쪽·오른쪽 크기로 나눠 스테레오로 낸다(안드로이드 SoundPool.play 의 좌우 볼륨). 끝나면 섞기에서 빠진다.</summary>
        private sealed class ClipStream : ISampleProvider
        {
            private readonly float[] _samples;
            private readonly float _left;
            private readonly float _right;
            private int _position;

            public ClipStream(float[] samples, float left, float right)
            {
                _samples = samples;
                _left = left;
                _right = right;
            }

            public WaveFormat WaveFormat => MixFormat;

            public int Read(float[] buffer, int offset, int count)
            {
                int frames = Math.Min(count / 2, _samples.Length - _position);
                for (int i = 0; i < frames; i++)
                {
                    float sample = _samples[_position + i];
                    buffer[offset + 2 * i] = sample * _left;
                    buffer[offset + 2 * i + 1] = sample * _right;
                }
                _position += frames;
                return frames * 2;
            }
        }
    }
}
