// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Diagnostics;
using SoundVisualizer.Ai;
using Xunit.Abstractions;

namespace SoundVisualizer.Tests.Ai;

/// <summary>
/// YAMNet 실행을 Python 기준(onnxruntime 으로 만든 CP3 골든)과 맞춘다. 모바일 계측 테스트 YamnetInferenceInstrumentedTest 를 옮겼다.
/// 모바일은 기기에서만 돌지만 PC 의 ONNX Runtime 은 데스크톱에서 돌므로 단위 테스트로 둔다. 오차 기준도 모바일과 같다.
/// </summary>
public class YamnetInferenceTests(ITestOutputHelper output)
{
    private const int Classes = YamnetInference.NumClasses;

    [Fact] // yamnet_matchesPythonCp3Golden
    public void Python_기준과_로짓은_1e3_확률은_1e4_안이고_1위와_상위_5개가_같다()
    {
        float[] logMel = AiTestData.LoadFloats("logmel_golden.bin", YamnetInference.LogMelSize);
        float[] expectedLogits = AiTestData.LoadFloats("yamnet_logits_golden.bin", Classes);
        float[] expectedProbs = AiTestData.LoadFloats("yamnet_probs_golden.bin", Classes);
        var meta = AiTestData.LoadJson("yamnet_cp3_meta.json");

        using var yamnet = YamnetInference.Create(AiTestData.ModelDirectory);
        var logits1 = new float[Classes];
        var probs1 = new float[Classes];
        var logits2 = new float[Classes];
        var probs2 = new float[Classes];
        long started = Stopwatch.GetTimestamp();
        yamnet.Infer(logMel, logits1, probs1);
        double firstMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        yamnet.Infer(logMel, logits2, probs2);

        double sum = probs1.Sum(p => (double)p);
        var logitError = Errors(logits1, expectedLogits);
        var probError = Errors(probs1, expectedProbs);
        output.WriteLine($"ORT session: {yamnet.InputOutputSummary()}, first infer {firstMs:F2} ms");
        output.WriteLine($"softmax sum={sum:R}");
        output.WriteLine($"logits maxAbs={logitError.MaxAbs:E3} MAE={logitError.Mae:E3} RMSE={logitError.Rmse:E3}");
        output.WriteLine($"probs  maxAbs={probError.MaxAbs:E3} MAE={probError.Mae:E3} RMSE={probError.Rmse:E3}");

        Assert.True(Math.Abs(sum - 1.0) < 1e-5, $"softmax sum≈1 (got {sum})");
        // 같은 입력이면 비트까지 같다.
        Assert.Equal(Bits(logits1), Bits(logits2));
        Assert.Equal(Bits(probs1), Bits(probs2));
        // 같은 ONNX 모델이라 ONNX Runtime 판이 달라도 로짓이 아주 가깝다.
        Assert.True(logitError.MaxAbs < 1e-3, $"logits maxAbs too large: {logitError.MaxAbs}");
        Assert.True(probError.MaxAbs < 1e-4, $"probs maxAbs too large: {probError.MaxAbs}");
        Assert.Equal(ArgMax(expectedProbs), ArgMax(probs1));
        Assert.Equal(TopK(expectedProbs, 5), TopK(probs1, 5));
        // 메타에 적힌 Python 결과(1위 507, 상위 5개)와도 같다.
        Assert.Equal(meta.GetProperty("probs").GetProperty("argmax").GetInt32(), ArgMax(probs1));
        Assert.Equal(
            meta.GetProperty("top5").EnumerateArray().Select(hit => hit.GetProperty("index").GetInt32()),
            TopK(probs1, 5));
    }

    [Fact]
    public void 입력_버퍼를_다시_써도_추론마다_그때의_입력을_본다()
    {
        // 입력은 IoBinding 으로 묶은 버퍼를 주소로 읽는다. 앞 입력이 남거나 출력이 섞이면 아래 두 기준과 어긋난다.
        float[] golden = AiTestData.LoadFloats("logmel_golden.bin", YamnetInference.LogMelSize);
        float[] silence = AiTestData.LoadFloats("qualcomm_source_silence_logmel.bin", YamnetInference.LogMelSize);
        // 무음 로그멜에 대한 Python 확률. 무음의 로그멜은 옛 전처리와 Qualcomm 전처리에서 똑같으므로 지금도 유효한 기준이다.
        float[] silenceExpected = AiTestData.LoadFloats("e2e_silence_probs.bin", Classes);

        using var yamnet = YamnetInference.Create(AiTestData.ModelDirectory);
        var first = new float[Classes];
        var silenceProbs = new float[Classes];
        var again = new float[Classes];
        var logits = new float[Classes];
        yamnet.Infer(golden, logits, first);
        yamnet.Infer(silence, logits, silenceProbs);
        yamnet.Infer(golden, logits, again);

        Assert.True(Errors(silenceProbs, silenceExpected).MaxAbs < 1e-4);
        Assert.Equal("Silence", AiTestData.ClassMap[ArgMax(silenceProbs)]);
        Assert.Equal(Bits(first), Bits(again));
    }

    [Fact]
    public void 추론은_관리_힙에_할당하지_않는다()
    {
        float[] logMel = AiTestData.LoadFloats("logmel_golden.bin", YamnetInference.LogMelSize);
        var logits = new float[Classes];
        var probs = new float[Classes];
        using var yamnet = YamnetInference.Create(AiTestData.ModelDirectory);
        for (int i = 0; i < 5; i++) yamnet.Infer(logMel, logits, probs);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) yamnet.Infer(logMel, logits, probs);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // 추론 100번에 걸친 값이다. 추론마다 텐서나 결과 배열을 만들면 수십 KB 가 된다.
        Assert.True(allocated <= 64, $"{allocated} bytes allocated by 100 inferences");
    }

    [Fact]
    public void softmax_는_Python_기준_확률과_float_마지막_자리_안에서_같다()
    {
        float[] logits = AiTestData.LoadFloats("yamnet_logits_golden.bin", Classes);
        float[] expected = AiTestData.LoadFloats("yamnet_probs_golden.bin", Classes);
        var probs = new float[Classes];

        YamnetInference.Softmax(logits, probs);

        // Kotlin 은 최댓값과의 차를 float 로, Python 은 double 로 구한다. 그 차이는 float 반올림 한 번 수준이다.
        double maxAbs = Errors(probs, expected).MaxAbs;
        output.WriteLine($"softmax vs Python maxAbs={maxAbs:E3}");
        Assert.True(maxAbs < 1e-7, $"softmax maxAbs {maxAbs}");
        Assert.True(Math.Abs(probs.Sum(p => (double)p) - 1.0) < 1e-6);
    }

    [Fact]
    public void softmax_는_큰_로짓에서도_넘치지_않는다()
    {
        var probs = new float[3];
        YamnetInference.Softmax(new[] { 1000f, 0f, -1000f }, probs);
        Assert.Equal(new[] { 1f, 0f, 0f }, probs);
    }

    [Theory]
    [InlineData(float.NaN, 0f, 1f)]
    [InlineData(0f, float.NaN, 1f)]
    [InlineData(0f, 1f, float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity)]
    public void softmax_는_합이_유한하지_않으면_모두_같은_확률을_준다(float a, float b, float c)
    {
        var probs = new float[3];
        YamnetInference.Softmax(new[] { a, b, c }, probs);
        Assert.All(probs, p => Assert.Equal(1f / 3, p));
    }

    [Fact]
    public void 길이가_다른_입력이나_출력은_거절한다()
    {
        using var yamnet = YamnetInference.Create(AiTestData.ModelDirectory);
        Assert.Throws<ArgumentException>(() => yamnet.Infer(new float[YamnetInference.LogMelSize - 1], new float[Classes], new float[Classes]));
        Assert.Throws<ArgumentException>(() => yamnet.Infer(new float[YamnetInference.LogMelSize], new float[Classes - 1], new float[Classes]));
        Assert.Throws<ArgumentException>(() => yamnet.Infer(new float[YamnetInference.LogMelSize], new float[Classes], new float[Classes + 1]));
    }

    [Fact]
    public void 세션은_audio_입력과_class_scores_출력을_가진다()
    {
        using var yamnet = YamnetInference.Create(AiTestData.ModelDirectory);
        Assert.Equal("in=audio[1,1,96,64] out=class_scores[1,521]", yamnet.InputOutputSummary());
    }

    [Fact]
    public void 닫은_뒤에는_네이티브_세션에_들어가지_않고_예외를_던진다()
    {
        var yamnet = YamnetInference.Create(AiTestData.ModelDirectory);
        yamnet.Dispose();
        yamnet.Dispose(); // 두 번 닫아도 된다.

        // 모바일의 "Trying to score a closed OrtSession." 처럼 해제한 네이티브 세션에 들어가기 전에 막는다.
        Assert.Throws<ObjectDisposedException>(() =>
            yamnet.Infer(new float[YamnetInference.LogMelSize], new float[Classes], new float[Classes]));
    }

    private readonly record struct ErrorSummary(double MaxAbs, double Mae, double Rmse);

    private static ErrorSummary Errors(float[] actual, float[] expected)
    {
        Assert.Equal(expected.Length, actual.Length);
        double maxAbs = 0, sumAbs = 0, sumSq = 0;
        for (int i = 0; i < actual.Length; i++)
        {
            double d = Math.Abs((double)actual[i] - expected[i]);
            maxAbs = Math.Max(maxAbs, d);
            sumAbs += d;
            sumSq += d * d;
        }
        return new ErrorSummary(maxAbs, sumAbs / actual.Length, Math.Sqrt(sumSq / actual.Length));
    }

    private static int[] Bits(float[] values) => Array.ConvertAll(values, BitConverter.SingleToInt32Bits);

    private static int ArgMax(float[] values) => TopK(values, 1)[0];

    /// <summary>모바일 테스트의 sortedByDescending.take(k) 와 같다. 정렬이 안정적이라 확률이 같으면 앞 번호가 먼저다.</summary>
    private static int[] TopK(float[] values, int k) =>
        Enumerable.Range(0, values.Length).OrderByDescending(i => values[i]).Take(k).ToArray();
}
