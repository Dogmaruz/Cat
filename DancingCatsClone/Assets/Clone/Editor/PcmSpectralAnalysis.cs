using System;
using System.Collections.Generic;
using System.Linq;

namespace DancingCats.Clone.Editor
{
    /// <summary>Offline, identity-free spectral attacks and conservative harmonic note evidence.</summary>
    public static class PcmSpectralAnalysis
    {
        public sealed class Result
        {
            public double[] accents = Array.Empty<double>();
            public MusicalNote[] sustained = Array.Empty<MusicalNote>();
            public MusicalCue[] follows = Array.Empty<MusicalCue>();
            public double highBandFraction;
        }

        public static Result Analyze(float[] pcm, int rate, int channels)
        {
            const int size = 1024;
            int stride = Math.Max(1, rate / 11025), frames = pcm.Length / channels;
            double analysisRate = (double)rate / stride;
            int hop = Math.Max(1, (int)Math.Round(analysisRate * .01));
            int downFrames = (frames + stride - 1) / stride;
            int count = Math.Max(0, (downFrames - size) / hop + 1);
            if (count < 5) return new Result();
            var data = new double[channels][];
            int radius = stride;
            for (int channel = 0; channel < channels; channel++)
            {
                data[channel] = new double[downFrames];
                for (int sample = 0; sample < downFrames; sample++)
                {
                    double sum = 0; int from = Math.Max(0, sample * stride - radius), to = Math.Min(frames - 1, sample * stride + radius);
                    for (int at = from; at <= to; at++) sum += pcm[at * channels + channel];
                    data[channel][sample] = sum / (to - from + 1);
                }
            }
            var hann = new double[size];
            for (int sample = 0; sample < size; sample++) hann[sample] = .5 - .5 * Math.Cos(2 * Math.PI * sample / (size - 1));
            var real = new double[size]; var imaginary = new double[size];
            var magnitude = new double[size / 2 + 1]; var previous = new double[magnitude.Length];
            var whitened = new double[magnitude.Length];
            var fullFlux = new double[count]; var highFlux = new double[count];
            var times = new double[count]; var pitch = new double[count]; var confidence = new double[count]; var partials = new int[count];
            // Offline phrase comparison owns the complete spectra; runtime playback never allocates these.
            var phraseSpectra = new double[count][];
            double totalEnergy = 0, highEnergy = 0;
            int highFirst = Math.Max(1, (int)Math.Ceiling(2500 / analysisRate * size));
            int highLast = Math.Min(magnitude.Length - 1, (int)Math.Floor(5000 / analysisRate * size));
            for (int frame = 0; frame < count; frame++)
            {
                Array.Clear(magnitude, 0, magnitude.Length);
                for (int channel = 0; channel < channels; channel++)
                {
                    for (int sample = 0; sample < size; sample++) { real[sample] = data[channel][frame * hop + sample] * hann[sample]; imaginary[sample] = 0; }
                    Fourier(real, imaginary);
                    for (int bin = 0; bin < magnitude.Length; bin++)
                        magnitude[bin] += Math.Sqrt(real[bin] * real[bin] + imaginary[bin] * imaginary[bin]) / channels;
                }
                // Combine channel magnitudes, not waveform sums: antiphase stereo stays audible to analysis.
                times[frame] = (frame * hop + size / 2.0) / analysisRate;
                phraseSpectra[frame] = (double[])magnitude.Clone();
                for (int bin = 0; bin < magnitude.Length; bin++)
                {
                    double value = Math.Log(1 + magnitude[bin] * 12);
                    double rise = frame == 0 ? 0 : Math.Max(0, value - previous[bin]);
                    fullFlux[frame] += rise / magnitude.Length;
                    totalEnergy += magnitude[bin] * magnitude[bin];
                    if (bin >= highFirst && bin <= highLast)
                    {
                        highFlux[frame] += rise / Math.Max(1, highLast - highFirst + 1);
                        highEnergy += magnitude[bin] * magnitude[bin];
                    }
                    previous[bin] = value;
                    double localMean = 0; int left = Math.Max(0, bin - 5), right = Math.Min(magnitude.Length - 1, bin + 5);
                    for (int near = left; near <= right; near++) localMean += magnitude[near];
                    whitened[bin] = Math.Min(8, magnitude[bin] / (localMean / (right - left + 1) + .03));
                }
                EstimatePitch(whitened, analysisRate, size, out pitch[frame], out confidence[frame], out partials[frame]);
            }
            double fraction = highEnergy / Math.Max(1e-20, totalEnergy);
            // A narrow-band instrument has no usable bright attack band; retain its full spectrum.
            bool brightAttacks = fraction > .0004;
            double[] accents = Peaks(brightAttacks ? highFlux : fullFlux, times, brightAttacks ? 1.6 : 1.2, .16);
            var filteredPitch = (double[])pitch.Clone();
            var median = new double[5];
            for (int frame = 2; frame + 2 < count; frame++)
            {
                for (int offset = 0; offset < 5; offset++) median[offset] = pitch[frame + offset - 2];
                Array.Sort(median); filteredPitch[frame] = median[2];
            }
            var notes = new List<MusicalNote>(); int start = 0;
            for (int frame = 1; frame <= count; frame++)
            {
                bool breakNote = frame == count || Math.Abs(filteredPitch[frame] - filteredPitch[start]) > 1
                    || confidence[frame] < 7.2 || partials[frame] < 3;
                if (!breakNote) continue;
                double end = frame == count ? times[count - 1] : times[frame];
                if (end - times[start] >= .55)
                {
                    double strength = 0; for (int at = start; at < frame; at++) strength += confidence[at];
                    notes.Add(new MusicalNote { start = times[start], end = end, midi = filteredPitch[start], confidence = Math.Min(1, strength / (frame - start) / 15) });
                }
                start = frame;
            }
            return new Result { accents = accents, sustained = notes.ToArray(), highBandFraction = fraction,
                follows = PcmPhraseAnalysis.Analyze(accents, times, phraseSpectra, brightAttacks ? highFlux : fullFlux, analysisRate / size) };
        }

        private static double[] Peaks(double[] novelty, double[] times, double threshold, double gap)
        {
            double maximum = novelty.Max();
            var peaks = new List<double>(); var strengths = new List<double>();
            for (int frame = 1; frame + 1 < novelty.Length; frame++)
            {
                double average = 0; int left = Math.Max(0, frame - 40), right = Math.Min(novelty.Length - 1, frame + 40);
                for (int at = left; at <= right; at++) average += novelty[at];
                average /= right - left + 1;
                if (novelty[frame] >= novelty[frame - 1] && novelty[frame] > novelty[frame + 1]
                    && novelty[frame] > Math.Max(maximum * .02, average * threshold))
                { peaks.Add(times[frame]); strengths.Add(novelty[frame]); }
            }
            return AudioChartGenerator.SelectAccentTimes(peaks.ToArray(), strengths.ToArray(), gap);
        }

        private static void EstimatePitch(double[] white, double rate, int size, out double midi, out double confidence, out int partials)
        {
            midi = 0; confidence = 0; partials = 0;
            for (double candidate = 55; candidate < 82; candidate += .25)
            {
                double fundamental = 440 * Math.Pow(2, (candidate - 69) / 12), score = 0; int covered = 0, available = 0;
                for (int harmonic = 1; harmonic <= 6; harmonic++)
                {
                    int bin = (int)Math.Round(fundamental * harmonic / rate * size);
                    if (bin >= white.Length) break;
                    double value = white[bin];
                    score += value * (harmonic == 1 ? 1 : harmonic == 2 ? .9 : harmonic == 3 ? .8 : harmonic == 4 ? .65 : harmonic == 5 ? .55 : .45);
                    if (value > 1.35) covered++;
                    available++;
                }
                if (available >= 4 && score > confidence) { confidence = score; midi = candidate; partials = covered; }
            }
        }

        private static void Fourier(double[] real, double[] imaginary)
        {
            int length = real.Length;
            for (int sample = 1, reversed = 0; sample < length; sample++)
            {
                int bit = length >> 1; for (; (reversed & bit) != 0; bit >>= 1) reversed ^= bit; reversed ^= bit;
                if (sample >= reversed) continue;
                double swap = real[sample]; real[sample] = real[reversed]; real[reversed] = swap;
                swap = imaginary[sample]; imaginary[sample] = imaginary[reversed]; imaginary[reversed] = swap;
            }
            for (int size = 2; size <= length; size <<= 1)
            {
                double angle = -2 * Math.PI / size, stepReal = Math.Cos(angle), stepImaginary = Math.Sin(angle);
                for (int offset = 0; offset < length; offset += size)
                {
                    double rotationReal = 1, rotationImaginary = 0;
                    for (int sample = 0; sample < size / 2; sample++)
                    {
                        int even = offset + sample, odd = even + size / 2;
                        double oddReal = real[odd] * rotationReal - imaginary[odd] * rotationImaginary;
                        double oddImaginary = real[odd] * rotationImaginary + imaginary[odd] * rotationReal;
                        real[odd] = real[even] - oddReal; imaginary[odd] = imaginary[even] - oddImaginary;
                        real[even] += oddReal; imaginary[even] += oddImaginary;
                        double nextReal = rotationReal * stepReal - rotationImaginary * stepImaginary;
                        rotationImaginary = rotationReal * stepImaginary + rotationImaginary * stepReal; rotationReal = nextReal;
                    }
                }
            }
        }
    }
}
