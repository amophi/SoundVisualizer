// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using SoundVisualizer.Ai;

namespace SoundVisualizer.Tests.Ai;

/// <summary>
/// 모델 파일 확인. 모바일 ModelCacheIntegrityTest 에서 검사 부분을 옮기고, 깨진 파일로 세션을 만들지 않는지를 더했다.
/// PC 는 assets 를 앱 저장소로 복사하지 않으므로 복사·복구(ensureFiles, copyIfInvalid) 테스트는 옮기지 않았다.
/// </summary>
public class ModelFileIntegrityTests
{
    [Fact] // validity_rejectsMissingEmptyTruncatedAndSameSizeCorruptFiles
    public void 없거나_비었거나_잘렸거나_크기는_같은데_깨진_파일은_통과하지_못한다()
    {
        using var dir = new TempDirectory();
        byte[] source = { 1, 2, 3, 4, 5, 6 };
        var expected = Expected("model.data", source);
        string cached = Path.Combine(dir.Path, expected.Name);

        Assert.False(ModelFileIntegrity.IsValid(cached, expected));
        File.WriteAllBytes(cached, Array.Empty<byte>());
        Assert.False(ModelFileIntegrity.IsValid(cached, expected));
        File.WriteAllBytes(cached, source[..^1]);
        Assert.False(ModelFileIntegrity.IsValid(cached, expected));
        File.WriteAllBytes(cached, new byte[] { 6, 5, 4, 3, 2, 1 });
        Assert.False(ModelFileIntegrity.IsValid(cached, expected));
        File.WriteAllBytes(cached, source);
        Assert.True(ModelFileIntegrity.IsValid(cached, expected));
    }

    [Fact] // shippedYamnetAssets_matchTheCacheIntegritySpecifications
    public void 배포하는_모델_파일은_고정한_크기와_SHA256과_같다()
    {
        foreach (var expected in YamnetModelFiles.All)
        {
            string path = Path.Combine(AiTestData.ModelDirectory, expected.Name);
            Assert.True(File.Exists(path), $"Missing shipped model file: {expected.Name}");
            Assert.Equal(expected.ByteCount, new FileInfo(path).Length);
            Assert.Equal(expected.Sha256, ModelFileIntegrity.Sha256Hex(path));
            Assert.Matches(new Regex("^[0-9a-f]{64}$"), expected.Sha256);
        }
        ModelFileIntegrity.Verify(AiTestData.ModelDirectory, YamnetModelFiles.All);
    }

    [Fact]
    public void 고정값은_Android_의_YamnetModelFiles_와_같다()
    {
        // 두 앱이 같은 모델을 쓴다는 약속이다. 모델을 바꾸면 YamnetInference.kt 의 값과 함께 여기도 고친다.
        Assert.Equal(
            new[]
            {
                ("yamnet.onnx", 25_592L, "290369c40886a4ae948f77671fff901023eec81484ac393465dfb1b342b1ca85"),
                ("yamnet.data", 14_915_108L, "aa05b5b196bdfd74fb59ae4cbba22578c5ab25b9e792e52867678844bcecc839"),
            },
            YamnetModelFiles.All.Select(file => (file.Name, file.ByteCount, file.Sha256)));
    }

    [Fact]
    public void 다른_폴더로_옮긴_모델도_그_폴더의_가중치로_불러온다()
    {
        // 가중치(yamnet.data)는 작업 폴더가 아니라 yamnet.onnx 옆에서 찾는다. 실행 파일 위치와 상관없이 불러와야 한다.
        using var dir = CopyOfShippedModel();
        float[] logMel = AiTestData.LoadFloats("logmel_golden.bin", YamnetInference.LogMelSize);
        float[] expected = AiTestData.LoadFloats("yamnet_logits_golden.bin", YamnetInference.NumClasses);
        var logits = new float[YamnetInference.NumClasses];

        using (var yamnet = YamnetInference.Create(dir.Path))
        {
            yamnet.Infer(logMel, logits, new float[YamnetInference.NumClasses]);
        }

        Assert.True(logits.Zip(expected, (a, b) => Math.Abs(a - b)).Max() < 1e-3f);
    }

    [Theory]
    [InlineData("yamnet.data")]
    [InlineData("yamnet.onnx")]
    public void 한_바이트만_바뀐_모델_파일로는_세션을_만들지_않는다(string fileName)
    {
        using var dir = CopyOfShippedModel();
        FlipMiddleByte(Path.Combine(dir.Path, fileName));

        var error = Assert.Throws<InvalidDataException>(() => YamnetInference.Create(dir.Path));

        Assert.Contains(fileName, error.Message);
        Assert.Contains("SHA-256", error.Message);
    }

    [Fact]
    public void 잘린_가중치는_크기에서_걸린다()
    {
        using var dir = CopyOfShippedModel();
        string data = Path.Combine(dir.Path, "yamnet.data");
        using (var stream = new FileStream(data, FileMode.Open, FileAccess.Write))
        {
            stream.SetLength(stream.Length - 1);
        }

        var error = Assert.Throws<InvalidDataException>(() => YamnetInference.Create(dir.Path));

        Assert.Contains("yamnet.data", error.Message);
        Assert.Contains("14915107 bytes", error.Message);
    }

    [Fact]
    public void 가중치가_없으면_파일을_찾지_못했다고_알린다()
    {
        using var dir = CopyOfShippedModel();
        string data = Path.Combine(dir.Path, "yamnet.data");
        File.Delete(data);

        var error = Assert.Throws<FileNotFoundException>(() => YamnetInference.Create(dir.Path));

        Assert.Equal(data, error.FileName);
    }

    private static ModelFileIntegrity.ExpectedFile Expected(string name, byte[] bytes) =>
        new(name, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)));

    private static TempDirectory CopyOfShippedModel()
    {
        var dir = new TempDirectory();
        foreach (var file in YamnetModelFiles.All)
        {
            File.Copy(Path.Combine(AiTestData.ModelDirectory, file.Name), Path.Combine(dir.Path, file.Name));
        }
        return dir;
    }

    private static void FlipMiddleByte(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
        stream.Position = stream.Length / 2;
        int value = stream.ReadByte();
        stream.Position--;
        stream.WriteByte((byte)(value ^ 0x01));
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = Directory.CreateTempSubdirectory("sv-yamnet-").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // 임시 폴더 정리는 테스트 결과와 상관없다.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
