// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Buffers.Binary;
using System.Text.Json;
using SoundVisualizer.Ai;
using Xunit.Abstractions;

namespace SoundVisualizer.Tests.Ai;

/// <summary>
/// 옮긴 Qualcomm 원본 전처리를, 따로 만든 Python 기준 데이터(<c>qualcomm_source_frontend_meta.json</c> 의 모든 경우)와
/// 비교한다. 모바일 판 <c>QualcommSourceAudioPreprocessorGoldenTest</c> 와 같은 한도(최대 1e-5, 평균 1e-7)를 쓴다.
/// </summary>
public class QualcommSourceAudioPreprocessorGoldenTests
{
    private const string MetaFile = "qualcomm_source_frontend_meta.json";
    private static readonly string ReferenceDirectory = Path.Combine(AppContext.BaseDirectory, "Ai", "Reference");

    private readonly ITestOutputHelper _output;

    public QualcommSourceAudioPreprocessorGoldenTests(ITestOutputHelper output) => _output = output;

    /// <summary>메타 파일에 적힌 경우를 모두 돈다. 기준 데이터에 경우가 늘면 따로 고치지 않아도 함께 본다.</summary>
    public static TheoryData<string> Cases()
    {
        using JsonDocument meta = ReadMeta();
        var cases = new TheoryData<string>();
        foreach (JsonProperty entry in meta.RootElement.GetProperty("cases").EnumerateObject())
        {
            cases.Add(entry.Name);
        }
        return cases;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void 로그멜이_고정한_원본_전처리의_기준_데이터와_맞는다(string name)
    {
        using JsonDocument meta = ReadMeta();
        JsonElement entry = meta.RootElement.GetProperty("cases").GetProperty(name);
        JsonElement output = entry.GetProperty("output");
        float[] input = LoadFloat32(entry.GetProperty("input").GetString()!);
        float[] expected = LoadFloat32(output.GetProperty("path").GetString()!);

        float[] actual = new QualcommSourceAudioPreprocessor().ComputeLogMelSpectrogram(input);

        Assert.Equal(QualcommSourceAudioPreprocessor.LogMelSize, actual.Length);
        Assert.Equal(output.GetProperty("count").GetInt32(), expected.Length);
        Assert.Equal(expected.Length, actual.Length);
        Assert.All(actual, value => Assert.True(float.IsFinite(value)));

        double maximumAbsoluteError = 0.0;
        double absoluteErrorSum = 0.0;
        for (int index = 0; index < actual.Length; index++)
        {
            double error = Math.Abs((double)actual[index] - expected[index]);
            maximumAbsoluteError = Math.Max(maximumAbsoluteError, error);
            absoluteErrorSum += error;
        }
        double meanAbsoluteError = absoluteErrorSum / actual.Length;
        _output.WriteLine($"{name} Qualcomm source parity: maxAbs={maximumAbsoluteError:R} meanAbs={meanAbsoluteError:R}");

        Assert.True(maximumAbsoluteError < 1e-5, $"{name} maxAbs={maximumAbsoluteError:R} must stay below 1e-5");
        Assert.True(meanAbsoluteError < 1e-7, $"{name} meanAbs={meanAbsoluteError:R} must stay below 1e-7");
    }

    private static JsonDocument ReadMeta() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(ReferenceDirectory, MetaFile)));

    /// <summary>기준 데이터는 float32 리틀엔디언 배열이다.</summary>
    private static float[] LoadFloat32(string fileName)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(ReferenceDirectory, fileName));
        Assert.True(bytes.Length % sizeof(float) == 0, $"{fileName} is not a float32 array");
        var values = new float[bytes.Length / sizeof(float)];
        for (int index = 0; index < values.Length; index++)
        {
            values[index] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(index * sizeof(float)));
        }
        return values;
    }
}
