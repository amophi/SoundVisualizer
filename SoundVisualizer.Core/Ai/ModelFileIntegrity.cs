// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Security.Cryptography;

namespace SoundVisualizer.Ai
{
    /// <summary>
    /// 앱과 함께 배포한 모델 파일이 고정해 둔 바로 그 파일인지 확인한다. Android 의 ModelCacheIntegrity.kt 에서 검사 부분만 옮겼다.
    ///
    /// Android 는 assets 를 앱 저장소로 복사하면서 크기와 SHA-256 을 맞춰 보고, 어긋난 사본은 지우고 다시 복사한다.
    /// PC 는 실행 파일 옆의 파일을 그대로 쓰므로 다시 복사할 원본이 없다. 그래서 불러오기 전에 확인만 하고, 어긋나면
    /// 예외를 던져 앱이 AI 없이 화면만 돌게 한다. 잘린 파일, 크기는 같은데 깨진 파일, 다른 버전의 모델이 모두 여기서 걸린다.
    /// </summary>
    internal static class ModelFileIntegrity
    {
        /// <summary>파일 하나의 기대값. <see cref="Sha256"/> 은 소문자 16진수 64자다.</summary>
        internal sealed record ExpectedFile(string Name, long ByteCount, string Sha256);

        /// <summary>
        /// 파일이 있고 크기와 SHA-256 이 모두 맞는지. 크기가 다르면 해시를 계산하지 않는다.
        /// 읽다가 난 I/O 오류는 false 로 바꾸지 않고 그대로 던진다(Android 와 같다).
        /// </summary>
        public static bool IsValid(string path, ExpectedFile expected)
        {
            ArgumentNullException.ThrowIfNull(expected);
            var file = new FileInfo(path);
            return file.Exists && file.Length == expected.ByteCount && Sha256Hex(path) == expected.Sha256;
        }

        /// <summary>
        /// <paramref name="directory"/> 의 파일을 모두 확인한다. 무엇이 어떻게 다른지를 메시지에 담는다.
        /// </summary>
        /// <exception cref="FileNotFoundException">파일이 없다.</exception>
        /// <exception cref="InvalidDataException">크기나 SHA-256 이 배포한 파일과 다르다.</exception>
        public static void Verify(string directory, IReadOnlyList<ExpectedFile> files)
        {
            ArgumentNullException.ThrowIfNull(directory);
            ArgumentNullException.ThrowIfNull(files);
            foreach (var expected in files)
            {
                string path = Path.Combine(directory, expected.Name);
                var file = new FileInfo(path);
                if (!file.Exists)
                    throw new FileNotFoundException($"Model file '{expected.Name}' is missing", path);
                if (file.Length != expected.ByteCount)
                    throw new InvalidDataException(
                        $"Model file '{expected.Name}' is {file.Length} bytes, expected {expected.ByteCount} (truncated or a different model): {path}");
                string actual = Sha256Hex(path);
                if (actual != expected.Sha256)
                    throw new InvalidDataException(
                        $"Model file '{expected.Name}' has SHA-256 {actual}, expected {expected.Sha256} (damaged or a different model): {path}");
            }
        }

        internal static string Sha256Hex(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexStringLower(SHA256.HashData(stream));
        }
    }
}
