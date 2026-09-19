using System;
using UnityEngine;

public sealed class VanillaABotAnalyzer
{
    private const int Size = 256;
    private const int BandCount = 7;
    private const double TwoPi = 6.2831853;
    private readonly double[] window = new double[Size];
    private readonly double[] hammingWindow = new double[Size];
    private readonly int[] reverse = new int[Size];
    private readonly double[] rootReal = new double[Size / 2];
    private readonly double[] rootImaginary = new double[Size / 2];
    private readonly double[] real = new double[Size];
    private readonly double[] imaginary = new double[Size];
    private readonly double[] frequencies = new double[Size / 2];
    private readonly double[] edges = new double[BandCount + 1];
    private readonly double[] history = new double[BandCount];
    private float[] pcm;
    public float[] Levels { get; } = new float[BandCount];

    public VanillaABotAnalyzer()
    {
        for (int sample = 0; sample < Size; sample++)
        {
            double phase = 2 * Math.PI * sample / (Size - 1);
            double blackman = .42 - .5 * Math.Cos(phase) + .08 * Math.Cos(2 * phase);
            double hamming = 1 - .85 * Math.Cos(sample * (TwoPi / Size));
            window[sample] = blackman;
            hammingWindow[sample] = hamming;
            int value = sample;
            for (int bit = 0; bit < 8; bit++)
            {
                reverse[sample] = reverse[sample] * 2 + (value & 1);
                value >>= 1;
            }
        }
        for (int root = 0; root < rootReal.Length; root++)
        {
            rootReal[root] = Math.Cos(root * (TwoPi / Size));
            rootImaginary[root] = Math.Sin(root * (TwoPi / Size));
        }
        for (int edge = 0; edge < edges.Length; edge++) edges[edge] = Math.Pow(256, edge / 8.0) - .5;
    }

    public void Reset()
    {
        Array.Clear(history, 0, history.Length);
        Array.Clear(Levels, 0, Levels.Length);
    }

    public bool Read(AudioSource source)
    {
        AudioClip clip = source == null ? null : source.clip;
        if (clip == null || clip.loadState != AudioDataLoadState.Loaded || clip.loadType != AudioClipLoadType.DecompressOnLoad)
            return false;
        int channels = clip.channels;
        if (channels <= 0 || clip.samples <= 0) return false;
        int needed = Size * channels;
        if (pcm == null || pcm.Length != needed) pcm = new float[needed];
        int position = Math.Max(0, Math.Min(source.timeSamples, clip.samples - 1));
        if (!clip.GetData(pcm, position)) return false;
        int available = Math.Min(Size, clip.samples - position) * channels;
        if (available < pcm.Length) Array.Clear(pcm, available, pcm.Length - available);
        Analyze(pcm, channels);
        return true;
    }

    public void Analyze(float[] interleaved, int channels)
    {
        if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
        if (interleaved == null || interleaved.Length < Size * channels)
            throw new ArgumentException("The sample buffer must contain 256 complete frames.", nameof(interleaved));
        for (int sample = 0; sample < Size; sample++)
        {
            double mixed = 0;
            for (int channel = 0; channel < channels; channel++) mixed += .7 * interleaved[sample * channels + channel];
            real[reverse[sample]] = mixed * window[sample] * hammingWindow[sample];
            imaginary[reverse[sample]] = 0;
        }
        for (int half = 1, stride = Size / 2; stride > 0; half *= 2, stride /= 2)
        {
            for (int group = 0; group < Size; group += half * 2)
            {
                for (int butterfly = 0; butterfly < half; butterfly++)
                {
                    int even = group + butterfly;
                    int odd = even + half;
                    int root = butterfly * stride;
                    double oddReal = real[odd] * rootReal[root] - imaginary[odd] * rootImaginary[root];
                    double oddImaginary = real[odd] * rootImaginary[root] + imaginary[odd] * rootReal[root];
                    double evenReal = real[even];
                    double evenImaginary = imaginary[even];
                    real[even] = evenReal + oddReal;
                    imaginary[even] = evenImaginary + oddImaginary;
                    real[odd] = evenReal - oddReal;
                    imaginary[odd] = evenImaginary - oddImaginary;
                }
            }
        }
        for (int bin = 0; bin < frequencies.Length; bin++)
        {
            int index = bin + 1;
            double magnitude = Math.Sqrt(real[index] * real[index] + imaginary[index] * imaginary[index]);
            frequencies[bin] = magnitude * (index == Size / 2 ? 1.0 : 2.0) / Size;
        }
        for (int band = 0; band < BandCount; band++)
        {
            int start = (int)Math.Ceiling(edges[band]);
            int end = (int)Math.Floor(edges[band + 1]);
            double sum = 0;
            if (end < start) sum += frequencies[end] * (edges[band + 1] - edges[band]);
            else
            {
                if (start > 0) sum += frequencies[start - 1] * (start - edges[band]);
                for (int bin = start; bin < end; bin++) sum += frequencies[bin];
                sum += frequencies[end] * (edges[band + 1] - end);
            }
            sum *= 8.0 / 12;
            double scaled = sum <= 0 ? 0 : (1 + 20 * Math.Log(sum) / Math.Log(10) / 40) * 16;
            int graph = (int)Math.Max(0, Math.Min(16, scaled));
            double target = graph / 16.0;
            history[band] += Math.Max(-.1, Math.Min(.1, target - history[band]));
            Levels[band] = (float)history[band];
        }
    }
}
