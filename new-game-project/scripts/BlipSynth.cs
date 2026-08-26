using Godot;
using System.Collections.Generic;

/// <summary>
/// Tiny procedural synth (port of game.ts BlipSynth). AudioStreamGenerator
/// push-mode: square/saw/triangle blips with exponential decay envelopes.
/// No audio assets required.
/// </summary>
public partial class BlipSynth : Node
{
    public enum OscType { Square, Sawtooth, Triangle }

    private sealed class Tone
    {
        public float Freq;
        public float Dur;
        public float Vol;
        public OscType Type;
        public long StartSample; // absolute sample index when the tone begins
    }

    private AudioStreamPlayer? _player;
    private AudioStreamGeneratorPlayback? _playback;
    private readonly List<Tone> _pending = new();
    private const int MixRate = 22050;
    private long _sampleClock;

    public override void _Ready()
    {
        var gen = new AudioStreamGenerator { MixRate = MixRate };
        _player = new AudioStreamPlayer { Stream = gen, Bus = "Master", VolumeDb = 0f };
        AddChild(_player);
        _player.Play();
    }

    public override void _Process(double delta)
    {
        if (_player == null || _player.Stream is not AudioStreamGenerator gen) return;
        if (_playback == null)
        {
            _playback = _player.GetStreamPlayback() as AudioStreamGeneratorPlayback;
            if (_playback == null) return;
        }

        int frames = _playback.GetFramesAvailable();
        if (frames <= 0) return;

        var buffer = new Vector2[frames];
        for (int i = 0; i < frames; i++)
        {
            float sample = 0f;
            long now = _sampleClock + i;

            for (int t = _pending.Count - 1; t >= 0; t--)
            {
                var tone = _pending[t];
                long local = now - tone.StartSample;
                if (local < 0) continue;
                float tSec = local / (float)MixRate;
                if (tSec > tone.Dur)
                {
                    _pending.RemoveAt(t);
                    continue;
                }
                float phase = local / (float)MixRate * tone.Freq;
                float wave = tone.Type switch
                {
                    OscType.Square => System.MathF.Sign(System.MathF.Sin(phase * System.MathF.Tau)),
                    OscType.Sawtooth => 2f * (phase % 1f) - 1f,
                    _ => 2f * System.MathF.Abs(2f * (phase % 1f) - 1f) - 1f,
                };
                float env = (float)System.Math.Pow(0.0001, tSec / tone.Dur);
                sample += wave * env * tone.Vol;
            }

            buffer[i] = new Vector2(Util.Clamp(sample, -1f, 1f), 0f);
        }

        foreach (var frame in buffer) _playback.PushFrame(frame);
        _sampleClock += frames;
    }

    /// <summary>Enqueue a blip; delay in seconds before it starts.</summary>
    private void Enqueue(float freq, float dur, OscType type = OscType.Square, float vol = 0.06f, double delay = 0.0)
    {
        if (_player == null) return;
        _pending.Add(new Tone
        {
            Freq = freq,
            Dur = dur,
            Vol = vol,
            Type = type,
            StartSample = _sampleClock + (long)(delay * MixRate),
        });
    }

    // ---------- presets (port of BlipSynth methods) ----------

    public void Bonk()
    {
        Enqueue(180f, 0.15f, OscType.Square, 0.09f);
        Enqueue(90f, 0.25f, OscType.Sawtooth, 0.05f);
    }

    public void Alarm()
    {
        Enqueue(660f, 0.3f, OscType.Square, 0.05f);
        Enqueue(520f, 0.3f, OscType.Square, 0.05f, 0.2);
    }

    public void Success()
    {
        Enqueue(523f, 0.18f, OscType.Triangle, 0.07f);
        Enqueue(659f, 0.18f, OscType.Triangle, 0.07f, 0.11);
        Enqueue(784f, 0.18f, OscType.Triangle, 0.07f, 0.22);
    }

    public void Pickup() => Enqueue(440f, 0.08f, OscType.Triangle, 0.06f);
}

