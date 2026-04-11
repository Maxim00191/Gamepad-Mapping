using System;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Core.Emulation;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core.Emulation.Noise;

/// <summary>Perlin-driven multipliers for delays and small mouse deltas; parameters resolved per call.</summary>
public sealed class HumanInputNoiseController : IHumanInputNoiseController
{
    /// <summary>Maps scheduled delay duration (ms) to seconds for phase integration.</summary>
    private const float DelayPhaseSecondsPerMillisecond = 0.001f;

    /// <summary>Time scaling: base term in delay phase rate from <see cref="Models.HumanInputNoiseParameters.Frequency"/>.</summary>
    private const float DelayFrequencyScaleMin = 0.15f;

    /// <summary>Time scaling: slope for Frequency on delay phase rate.</summary>
    private const float DelayFrequencyScaleSlope = 2.85f;

    /// <summary>Axial decoupling: fixed second coordinate for delay noise vs other Perlin samples.</summary>
    private const float DelayNoisePerlinSecondaryOffset = 0.31f;

    private const float DelaySmoothnessResponseFactor = 0.92f;
    private const float DelayMultiplierMin = 0.35f;
    private const float DelayMultiplierMax = 2.2f;
    private const float DelaySmoothingAlphaFloor = 0.04f;

    /// <summary>
    /// Time scaling: mouse tremor phase rate (Perlin domain units per second). Chosen so at ~60 move events/s
    /// the step matches the former per-call increment of <c>0.08f + Amplitude * 0.02f</c> (poll-rate independent).
    /// </summary>
    private const float MousePhaseUnitsPerSecondBase = 4.8f;

    /// <summary>Time scaling: extra phase rate per unit of <see cref="Models.HumanInputNoiseParameters.Amplitude"/>.</summary>
    private const float MousePhaseUnitsPerSecondPerAmplitude = 1.2f;

    /// <summary>Time scaling: maps Frequency slider to spatial scale along the tremor phase in Perlin space.</summary>
    private const float MouseFrequencyScaleMin = 0.2f;

    /// <summary>Time scaling: slope for Frequency on that spatial scale.</summary>
    private const float MouseFrequencyScaleSlope = 3f;

    /// <summary>Axial decoupling: second Perlin coordinate for horizontal jitter sample.</summary>
    private const float MousePerlinSecondaryX = 1.1f;

    /// <summary>Axial decoupling: phase offset so X/Y jitter streams stay uncorrelated.</summary>
    private const float MousePerlinPrimaryOffsetForY = 13.7f;

    /// <summary>Axial decoupling: second Perlin coordinate for vertical jitter sample.</summary>
    private const float MousePerlinSecondaryY = 2.3f;

    private const float MouseJitterLinearBase = 2f;
    private const float MouseJitterLinearPerMagnitude = 0.04f;
    private const float MouseJitterPixelScale = 4f;

    /// <summary>
    /// Vertical anisotropy: physiological jitter is typically slightly weaker on the Y-axis than X-axis.
    /// </summary>
    private const float MouseJitterVerticalScale = 0.85f;

    /// <summary>Bootstrap phase advance when no prior tick exists (≈ one 60Hz frame).</summary>
    private const float MousePhaseDefaultDeltaSeconds = 1f / 60f;

    /// <summary>Minimum advance when <see cref="ITimeProvider.GetTickCount64"/> does not change between moves.</summary>
    private const float MousePhaseMinDeltaSeconds = 1f / 2000f;

    private const float MousePhaseMaxDeltaSeconds = 0.25f;

    private readonly INoiseGenerator _noise;
    private readonly Func<HumanInputNoiseParameters> _getParameters;
    private readonly ITimeProvider _time;
    private readonly object _sync = new();

    private double _phase;
    private float _mousePhase;
    private float _smoothedDelayMultiplier = 1f;

    private long _lastMouseTickCount;
    private bool _hasLastMouseTick;

    private float _residualJitterX;
    private float _residualJitterY;

    public HumanInputNoiseController(INoiseGenerator noise, Func<HumanInputNoiseParameters> getParameters, ITimeProvider timeProvider)
    {
        _noise = noise;
        _getParameters = getParameters;
        _time = timeProvider;
    }

    public int AdjustDelayMs(int baseDelayMs)
    {
        if (baseDelayMs <= 0) return baseDelayMs;

        lock (_sync)
        {
            var p = _getParameters();
            if (!p.Enabled) return baseDelayMs;

            float freqScale = DelayFrequencyScaleMin + p.Frequency * DelayFrequencyScaleSlope;
            _phase += baseDelayMs * DelayPhaseSecondsPerMillisecond * freqScale;

            float n = _noise.Sample2D((float)_phase, DelayNoisePerlinSecondaryOffset);
            float targetMult = 1f + p.Amplitude * n;
            targetMult = Math.Clamp(targetMult, DelayMultiplierMin, DelayMultiplierMax);

            float alpha = Math.Clamp(1f - p.Smoothness * DelaySmoothnessResponseFactor, DelaySmoothingAlphaFloor, 1f);
            _smoothedDelayMultiplier = _smoothedDelayMultiplier + (targetMult - _smoothedDelayMultiplier) * alpha;

            int noisy = (int)Math.Round(baseDelayMs * _smoothedDelayMultiplier);
            return Math.Max(0, noisy);
        }
    }

    public (int Dx, int Dy) AdjustMouseMove(int deltaX, int deltaY)
    {
        if (deltaX == 0 && deltaY == 0)
            return (deltaX, deltaY);

        lock (_sync)
        {
            var p = _getParameters();
            if (!p.Enabled)
            {
                _residualJitterX = 0;
                _residualJitterY = 0;
                return (deltaX, deltaY);
            }

            // No scaled noise at zero amplitude; drop residuals so the result matches the user delta exactly.
            if (p.Amplitude <= 0f)
            {
                _residualJitterX = 0f;
                _residualJitterY = 0f;
                return (deltaX, deltaY);
            }

            AdvanceMousePhase(p);

            float freqScale = MouseFrequencyScaleMin + p.Frequency * MouseFrequencyScaleSlope;
            float phaseScaled = _mousePhase * freqScale;
            float nx = _noise.Sample2D(phaseScaled, MousePerlinSecondaryX);
            float ny = _noise.Sample2D(phaseScaled + MousePerlinPrimaryOffsetForY, MousePerlinSecondaryY);

            float mag = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
            float scale = p.Amplitude * (MouseJitterLinearBase + mag * MouseJitterLinearPerMagnitude);

            float rawX = nx * scale * MouseJitterPixelScale;
            float rawY = ny * scale * MouseJitterPixelScale * MouseJitterVerticalScale;

            _residualJitterX += rawX;
            _residualJitterY += rawY;

            int jx = (int)Math.Round(_residualJitterX);
            int jy = (int)Math.Round(_residualJitterY);
            _residualJitterX -= jx;
            _residualJitterY -= jy;

            return (deltaX + jx, deltaY + jy);
        }
    }

    private void AdvanceMousePhase(HumanInputNoiseParameters p)
    {
        long now = _time.GetTickCount64();
        float dtSec;
        if (!_hasLastMouseTick)
        {
            _hasLastMouseTick = true;
            _lastMouseTickCount = now;
            dtSec = MousePhaseDefaultDeltaSeconds;
        }
        else
        {
            long deltaMs = now - _lastMouseTickCount;
            _lastMouseTickCount = now;
            if (deltaMs <= 0)
                dtSec = MousePhaseMinDeltaSeconds;
            else
                dtSec = Math.Clamp(deltaMs / 1000f, MousePhaseMinDeltaSeconds, MousePhaseMaxDeltaSeconds);
        }

        float rate = MousePhaseUnitsPerSecondBase + p.Amplitude * MousePhaseUnitsPerSecondPerAmplitude;
        _mousePhase += rate * dtSec;
    }
}
