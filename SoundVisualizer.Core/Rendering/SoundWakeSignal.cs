// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Rendering
{
    /// <summary>
    /// 쉬는(idle) 오버레이를 소리가 나는 순간 깨운다(Android SoundWakeSignal, #170).
    ///
    /// 쉬는 동안 오버레이는 33ms 마다 단축키와 소리를 확인한다. 캡처 스레드는 어차피 버퍼마다 소리 크기를 알고 있으므로,
    /// 조용히 쉬는 동안에는 큰 소리가 든 버퍼에서 바로 확인을 앞당기게 한다. 그러면 다음 33ms 를 기다리지 않고 그리기 시작한다.
    ///
    /// 순서는 이렇다. 오버레이(UI 스레드)가 확인하기 **직전에** <see cref="Arm"/> 을 부르고 소리를 확인한다. 그 뒤 캡처 스레드가
    /// <see cref="OnBuffer"/> 에서 큰 소리를 보면 한 번만 true 를 돌려주고, 캡처 스레드는 확인을 UI 스레드에 넘긴다.
    /// **<see cref="Arm"/> 뒤에 들어온 버퍼만 센다.** 마지막 소리 크기만 보고 깨우면, 캡처가 멈춰 그 값이 크게 남아 있을 때 쉬지 못한다.
    /// 표시를 꺼 둔 종류의 소리가 계속 나는 동안에는 오버레이가 <see cref="Disarm"/> 해 둔다. 그러지 않으면 버퍼마다(약 10ms) 깨어
    /// 33ms 확인보다 오히려 잦아진다.
    ///
    /// 캡처 스레드가 버퍼마다 더 하는 일은 원자 변수 읽기 한 번이다. 할당은 없다.
    /// </summary>
    public sealed class SoundWakeSignal
    {
        private readonly float _threshold;
        private int _armed;

        /// <param name="threshold">소리 크기가 이 값보다 크면 깨운다.</param>
        public SoundWakeSignal(float threshold)
        {
            _threshold = threshold;
        }

        /// <summary>지금 큰 소리를 기다리는지. 캡처 스레드가 소리 크기를 잴지 먼저 보는 데 쓴다.</summary>
        public bool IsArmed => Volatile.Read(ref _armed) != 0;

        /// <summary>지금부터 들어오는 큰 소리 한 번을 알린다. 오버레이가 소리를 확인하기 직전에 부른다.</summary>
        public void Arm() => Volatile.Write(ref _armed, 1);

        /// <summary>큰 소리를 기다리지 않는다. 깨어났거나 33ms 확인만 할 때 부른다.</summary>
        public void Disarm() => Volatile.Write(ref _armed, 0);

        /// <summary>
        /// 캡처 스레드가 버퍼를 넘긴 직후 그 버퍼의 소리 크기로 부른다. 기다리는 중이고 크기가 문턱을 넘으면 true 를 한 번만
        /// 돌려준다(여러 스레드가 동시에 불러도 하나만). 그때 부르는 쪽이 오버레이의 확인을 앞당긴다.
        /// </summary>
        public bool OnBuffer(float level)
        {
            if (Volatile.Read(ref _armed) == 0) return false; // 깨어 있거나 이미 알렸다. 그리는 동안은 대부분 여기서 끝난다.
            if (!(level > _threshold)) return false;
            return Interlocked.Exchange(ref _armed, 0) == 1;
        }
    }
}
