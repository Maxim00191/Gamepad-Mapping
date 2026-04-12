using System;
using System.Diagnostics;
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
    private const float DelayFrequencyScaleMin = 0.2f;

    /// <summary>Time scaling: slope for Frequency on delay phase rate.</summary>
    private const float DelayFrequencyScaleSlope = 4.5f;

    /// <summary>Axial decoupling: fixed second coordinate for delay noise vs other Perlin samples.</summary>
    private const float DelayNoisePerlinSecondaryOffset = 0.31f;

    /// <summary>Axial decoupling: second coordinate for tap-hold jitter vs delay/mouse streams.</summary>
    private const float TapHoldNoisePerlinSecondaryOffset = 0.47f;

    private const int TapHoldEnvelopeMinMs = 20;
    private const int TapHoldEnvelopeMaxMs = 100;

    private const float DelaySmoothnessResponseFactor = 0.92f;
    private const float DelayMultiplierMin = 0.25f;
    private const float DelayMultiplierMax = 3.5f;
    private const float DelaySmoothingAlphaFloor = 0.04f;

    /// <summary>Extra gain on <see cref="HumanInputNoiseParameters.Amplitude"/> for delay and mouse (UI slider range unchanged).</summary>
    private const float AmplitudeResponseGain = 2.5f;

    /// <summary>
    /// Mouse tremor: base phase rate (Perlin domain units per second). Independent of <see cref="Models.HumanInputNoiseParameters.Amplitude"/>.
    /// </summary>
    private const float MousePhaseUnitsPerSecondBase = 4.0f;

    /// <summary>Extra mouse phase rate per unit of <see cref="Models.HumanInputNoiseParameters.Frequency"/> (not Amplitude).</summary>
    private const float MousePhaseUnitsPerSecondPerFrequency = 4.0f;

    /// <summary>Time scaling: maps Frequency slider to spatial scale along the tremor phase in Perlin space.</summary>
    private const float MouseFrequencyScaleMin = 0.08f;

    /// <summary>Time scaling: slope for Frequency on that spatial scale.</summary>
    private const float MouseFrequencyScaleSlope = 2.0f;

    /// <summary>Axial decoupling: second Perlin coordinate for horizontal jitter sample.</summary>
    private const float MousePerlinSecondaryX = 1.1f;

    /// <summary>Axial decoupling: phase offset so X/Y jitter streams stay uncorrelated.</summary>
    private const float MousePerlinPrimaryOffsetForY = 13.7f;

    /// <summary>Axial decoupling: second Perlin coordinate for vertical jitter sample.</summary>
    private const float MousePerlinSecondaryY = 2.3f;

    private const float MouseJitterLinearBase = 6.0f;
    private const float MouseJitterPixelScale = 4.5f;

    /// <summary>
    /// Vertical anisotropy: physiological jitter is typically slightly weaker on the Y-axis than X-axis.
    /// </summary>
    private const float MouseJitterVerticalScale = 0.85f;

    /// <summary>Bootstrap phase advance when no prior sample exists (≈ one 60Hz frame).</summary>
    private const float MousePhaseDefaultDeltaSeconds = 1f / 60f;

    /// <summary>Minimum advance when QPC returns an identical timestamp between moves (ultra-fast back-to-back calls).</summary>
    private const float MousePhaseMinDeltaSeconds = 1f / 2000f;

    private const float MousePhaseMaxDeltaSeconds = 0.25f;

    /// <summary>
    /// Stick magnitude M in [0,1]: phase rate scales by max(floor, 1 - c·M) so high deflection lowers tremor frequency.
    /// </summary>
    private const float MouseStickInertialC = 0.85f;

    private const float MouseStickInertialFrequencyFloor = 0.12f;

    private readonly INoiseGenerator _noise;
    private readonly Func<HumanInputNoiseParameters> _getParameters;
    private readonly ITimeProvider _time;
    private readonly object _sync = new();

    private double _delayPhase;
    private double _tapHoldPhase;
    private float _mousePhase;
    private float _smoothedDelayMultiplier = 1f;

    private long _lastMousePerfTimestamp;
    private bool _hasLastMousePerfSample;

    private float _smoothedMouseRawX;
    private float _smoothedMouseRawY;

    private float _prevSmoothedMouseRawX;
    private float _prevSmoothedMouseRawY;

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
            _delayPhase += baseDelayMs * DelayPhaseSecondsPerMillisecond * freqScale;

            float n = _noise.Sample2D((float)_delayPhase, DelayNoisePerlinSecondaryOffset);
            float targetMult = 1f + p.Amplitude * AmplitudeResponseGain * n;
            targetMult = Math.Clamp(targetMult, DelayMultiplierMin, DelayMultiplierMax);

            float alpha = Math.Clamp(1f - p.Smoothness * DelaySmoothnessResponseFactor, DelaySmoothingAlphaFloor, 1f);
            _smoothedDelayMultiplier = _smoothedDelayMultiplier + (targetMult - _smoothedDelayMultiplier) * alpha;

            int noisy = (int)Math.Round(baseDelayMs * _smoothedDelayMultiplier);
            return Math.Max(0, noisy);
        }
    }

    public int AdjustTapHoldMs(int nominalMs, int maxDeviationMs)
    {
        lock (_sync)
        {
            var p = _getParameters();
            if (!p.Enabled || maxDeviationMs <= 0)
                return nominalMs;

            float freqScale = DelayFrequencyScaleMin + p.Frequency * DelayFrequencyScaleSlope;
            _tapHoldPhase += nominalMs * DelayPhaseSecondsPerMillisecond * freqScale;

            float n = _noise.Sample2D((float)_tapHoldPhase, TapHoldNoisePerlinSecondaryOffset);
            int delta = (int)Math.Round(n * maxDeviationMs);
            return Math.Clamp(nominalMs + delta, TapHoldEnvelopeMinMs, TapHoldEnvelopeMaxMs);
        }
    }

    public (int Dx, int Dy) AdjustMouseMove(int deltaX, int deltaY, float stickMagnitude = 1.0f)
    {
        lock (_sync)
        {
            var p = _getParameters();
            if (!p.Enabled || p.Amplitude <= 0f || stickMagnitude <= 0.01f)
            {
                _residualJitterX = 0;
                _residualJitterY = 0;
                _smoothedMouseRawX = 0;
                _smoothedMouseRawY = 0;
                _prevSmoothedMouseRawX = 0;
                _prevSmoothedMouseRawY = 0;
                return (deltaX, deltaY);
            }

            float mStick = Math.Clamp(stickMagnitude, 0f, 1f);
            float dtSec = AdvanceMousePhase(p, mStick);

            float freqScale = MouseFrequencyScaleMin + p.Frequency * MouseFrequencyScaleSlope;
            float phaseScaled = _mousePhase * freqScale;
            float nx = _noise.Sample2D(phaseScaled, MousePerlinSecondaryX);
            float ny = _noise.Sample2D(phaseScaled + MousePerlinPrimaryOffsetForY, MousePerlinSecondaryY);

            // Amplitude vs stick: sublinear sqrt(M) — stronger micro-jitter at low M, bounded growth at full deflection.
            float amplitudeStickFactor = MathF.Sqrt(mStick);
            float scale = p.Amplitude * AmplitudeResponseGain * MouseJitterLinearBase * amplitudeStickFactor;

            float targetRawX = nx * scale * MouseJitterPixelScale;
            float targetRawY = ny * scale * MouseJitterPixelScale * MouseJitterVerticalScale;

            // Use a time-dependent smoothing factor (alpha) to ensure consistent behavior across different frame rates.
            // p.Smoothness [0..1] maps to a response time. 
            // We use a simple EMA: next = current + (target - current) * (1 - exp(-dt / tau))
            // Here we simplify it to a dt-weighted alpha for the requested smoothness.
            float baseAlpha = Math.Clamp(MathF.Pow(1f - p.Smoothness, 3f), 0.01f, 1f);
            float dtWeightedAlpha = Math.Clamp(baseAlpha * (dtSec / MousePhaseDefaultDeltaSeconds), 0.001f, 1f);

            _smoothedMouseRawX = _smoothedMouseRawX + (targetRawX - _smoothedMouseRawX) * dtWeightedAlpha;
            _smoothedMouseRawY = _smoothedMouseRawY + (targetRawY - _smoothedMouseRawY) * dtWeightedAlpha;

            // Calculate the delta movement from the position change.
            // This prevents "position as velocity" drift and ensures the cursor correctly follows the tremor offset.
            float deltaJitterX = _smoothedMouseRawX - _prevSmoothedMouseRawX;
            float deltaJitterY = _smoothedMouseRawY - _prevSmoothedMouseRawY;

            _prevSmoothedMouseRawX = _smoothedMouseRawX;
            _prevSmoothedMouseRawY = _smoothedMouseRawY;

            // Accumulate the delta into the residual buffer.
            _residualJitterX += deltaJitterX;
            _residualJitterY += deltaJitterY;

            int jx = (int)Math.Truncate(_residualJitterX);
            int jy = (int)Math.Truncate(_residualJitterY);
            _residualJitterX -= jx;
            _residualJitterY -= jy;

            return (deltaX + jx, deltaY + jy);
        }
    }

    private float AdvanceMousePhase(HumanInputNoiseParameters p, float stickMagnitude01)
    {
        long now = _time.GetPerformanceTimestamp();
        float dtSec;
        if (!_hasLastMousePerfSample)
        {
            _hasLastMousePerfSample = true;
            _lastMousePerfTimestamp = now;
            dtSec = MousePhaseDefaultDeltaSeconds;
        }
        else
        {
            long deltaTicks = now - _lastMousePerfTimestamp;
            _lastMousePerfTimestamp = now;
            if (deltaTicks <= 0)
                dtSec = MousePhaseMinDeltaSeconds;
            else
                dtSec = (float)Math.Clamp(
                    deltaTicks / (double)Stopwatch.Frequency,
                    MousePhaseMinDeltaSeconds,
                    MousePhaseMaxDeltaSeconds);
        }

        // f_dynamic = f_base * max(ε, 1 - c·M_stick); applied to phase rate only (single domain traversal).
        float fDynamic = Math.Max(MouseStickInertialFrequencyFloor, 1f - MouseStickInertialC * stickMagnitude01);
        float rate = (MousePhaseUnitsPerSecondBase + p.Frequency * MousePhaseUnitsPerSecondPerFrequency) * fDynamic;
        _mousePhase += rate * dtSec;
        return dtSec;
    }
}
