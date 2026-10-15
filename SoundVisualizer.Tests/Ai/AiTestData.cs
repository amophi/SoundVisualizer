// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using SoundVisualizer.Ai;

namespace SoundVisualizer.Tests.Ai;

/// <summary>
/// AI 테스트가 같이 쓰는 기준 데이터. 테스트 출력 폴더의 Ai/Reference(모바일 app/src/test/resources/ai_reference 와 같은 파일)와
/// Models/yamnet_class_map.csv(앱이 쓰는 파일, 모바일 assets 와 같은 내용)를 읽는다.
/// </summary>
internal static class AiTestData
{
    private static readonly Lazy<YamnetClassMap> RealClassMap = new(() => YamnetClassMap.Load(ClassMapPath));

    public static string ClassMapPath => Path.Combine(AppContext.BaseDirectory, "Models", "yamnet_class_map.csv");

    /// <summary>앱이 쓰는 yamnet.onnx·yamnet.data 가 있는 폴더(모바일 assets/ai 와 같은 파일).</summary>
    public static string ModelDirectory => Path.Combine(AppContext.BaseDirectory, "Models");

    /// <summary>실제 521개 클래스 목록. 불변이라 테스트끼리 나눠 쓴다.</summary>
    public static YamnetClassMap ClassMap => RealClassMap.Value;

    public static string ReferencePath(string fileName) => Path.Combine(AppContext.BaseDirectory, "Ai", "Reference", fileName);

    /// <summary>리틀엔디언 float32 배열 파일.</summary>
    public static float[] LoadFloats(string fileName, int expectedCount)
    {
        byte[] bytes = File.ReadAllBytes(ReferencePath(fileName));
        Assert.Equal(expectedCount * sizeof(float), bytes.Length);
        var values = new float[expectedCount];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i * sizeof(float)));
        }
        return values;
    }

    /// <summary>CP4 골든의 YAMNet 확률 521개(cp4_{name}_probs.bin).</summary>
    public static float[] LoadCp4Probs(string name) => LoadFloats($"cp4_{name}_probs.bin", YamnetCoarseClassifier.NumClasses);

    public static JsonElement LoadJson(string fileName)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ReferencePath(fileName)));
        return document.RootElement.Clone();
    }

    /// <summary>
    /// 521개 이름. 앞쪽은 <paramref name="head"/> 이고 나머지는 <paramref name="prefix"/>+번호다
    /// (모바일 테스트의 List(521) { i -> head.getOrNull(i) ?: "prefix$i" }).
    /// </summary>
    public static string[] Names(string prefix, params string[] head)
    {
        var names = new string[YamnetCoarseClassifier.NumClasses];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = i < head.Length ? head[i] : prefix + i.ToString(CultureInfo.InvariantCulture);
        }
        return names;
    }

    /// <summary>모두 0 이고 지정한 번호만 값이 있는 확률 521개.</summary>
    public static float[] Probabilities(params (int Index, float Value)[] values)
    {
        var probabilities = new float[YamnetCoarseClassifier.NumClasses];
        foreach (var (index, value) in values) probabilities[index] = value;
        return probabilities;
    }
}
