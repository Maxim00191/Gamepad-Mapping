using System;
using System.Collections.Generic;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Models;
using Vortice.XInput;
using Xunit;

namespace GamepadMapping.Tests.Core.Processing
{
    public class ChordResolverTests
    {
        [Theory]
        [InlineData("A", new[] { GamepadButtons.A }, false, false, "A")]
        [InlineData("A+B", new[] { GamepadButtons.A, GamepadButtons.B }, false, false, "A + B")]
        [InlineData("B+A", new[] { GamepadButtons.A, GamepadButtons.B }, false, false, "A + B")] // normalized ordering
        [InlineData("RT+A", new[] { GamepadButtons.A }, true, false, "RightTrigger + A")]
        [InlineData("LT+B", new[] { GamepadButtons.B }, false, true, "LeftTrigger + B")]
        [InlineData("LT+RT+X", new[] { GamepadButtons.X }, true, true, "LeftTrigger + RightTrigger + X")]
        [InlineData("A,B;X", new[] { GamepadButtons.A, GamepadButtons.B, GamepadButtons.X }, false, false, "A + B + X")] // mixed separators
        [InlineData(" A + B ", new[] { GamepadButtons.A, GamepadButtons.B }, false, false, "A + B")] // spaces
        [InlineData("A++B", new[] { GamepadButtons.A, GamepadButtons.B }, false, false, "A + B")] // duplicate separators
        [InlineData("a+B", new[] { GamepadButtons.A, GamepadButtons.B }, false, false, "A + B")] // casing
        [InlineData("RT+RT+A", new[] { GamepadButtons.A }, true, false, "RightTrigger + A")] // duplicate triggers
        public void TryParseButtonChord_ValidInputs_ReturnsTrueAndNormalizes(
            string source,
            GamepadButtons[] expectedButtons,
            bool expectedReqRt,
            bool expectedReqLt,
            string expectedNormalized)
        {
            // act
            var ok = ChordResolver.TryParseButtonChord(
                source,
                out var buttons,
                out var reqRt,
                out var reqLt,
                out var normalized);

            // assert
            Assert.True(ok);
            Assert.Equal(expectedButtons.Length, buttons.Count);
            foreach (var b in expectedButtons)
                Assert.Contains(b, buttons);

            Assert.Equal(expectedReqRt, reqRt);
            Assert.Equal(expectedReqLt, reqLt);
            Assert.Equal(expectedNormalized, normalized);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("UnknownButton")]
        [InlineData("A+UnknownButton")]
        [InlineData("DPadUp+DPadDown")] // Semantic error: physically impossible
        [InlineData("DPadLeft+DPadRight")] // Semantic error: physically impossible
        [InlineData("LeftThumbUp+LeftThumbDown")] // Semantic error: physically impossible
        [InlineData("RightThumbLeft+RightThumbRight")] // Semantic error: physically impossible
        public void TryParseButtonChord_InvalidInputs_ReturnsFalse(string? source)
        {
            var ok = ChordResolver.TryParseButtonChord(
                source,
                out var buttons,
                out var reqRt,
                out var reqLt,
                out var normalized);

            Assert.False(ok);
            Assert.Empty(buttons);
            Assert.False(reqRt);
            Assert.False(reqLt);
            Assert.Equal(string.Empty, normalized);
        }

        [Fact]
        public void TryParseButtonChord_NoButtonsOnlyTriggers_ReturnsFalse()
        {
            var ok = ChordResolver.TryParseButtonChord(
                "LT+RT",
                out var buttons,
                out _,
                out _,
                out _);

            Assert.False(ok);
            Assert.Empty(buttons);
        }

        [Fact]
        public void DoesChordMatchEvent_AllConditionsMet_ReturnsTrue()
        {
            var chord = new List<GamepadButtons> { GamepadButtons.A, GamepadButtons.B };
            var active = new List<GamepadButtons> { GamepadButtons.A, GamepadButtons.B, GamepadButtons.X };

            var matched = ChordResolver.DoesChordMatchEvent(
                chordButtons: chord,
                requiresRightTrigger: true,
                requiresLeftTrigger: true,
                leftTriggerValue: 1.0f,
                rightTriggerValue: 1.0f,
                triggerMatchThreshold: 0.5f,
                changedButton: GamepadButtons.A,
                activeButtons: active);

            Assert.True(matched);
        }

        [Fact]
        public void DoesChordMatchEvent_ChangedButtonNotInChord_ReturnsFalse()
        {
            var chord = new List<GamepadButtons> { GamepadButtons.A };
            var active = new List<GamepadButtons> { GamepadButtons.A };

            var matched = ChordResolver.DoesChordMatchEvent(
                chordButtons: chord,
                requiresRightTrigger: false,
                requiresLeftTrigger: false,
                leftTriggerValue: 0.0f,
                rightTriggerValue: 0.0f,
                triggerMatchThreshold: 0.5f,
                changedButton: GamepadButtons.B,
                activeButtons: active);

            Assert.False(matched);
        }

        [Fact]
        public void DoesChordMatchEvent_MissingActiveButton_ReturnsFalse()
        {
            var chord = new List<GamepadButtons> { GamepadButtons.A, GamepadButtons.B };
            var active = new List<GamepadButtons> { GamepadButtons.A };

            var matched = ChordResolver.DoesChordMatchEvent(
                chordButtons: chord,
                requiresRightTrigger: false,
                requiresLeftTrigger: false,
                leftTriggerValue: 0.0f,
                rightTriggerValue: 0.0f,
                triggerMatchThreshold: 0.5f,
                changedButton: GamepadButtons.A,
                activeButtons: active);

            Assert.False(matched);
        }

        [Fact]
        public void DoesChordMatchEvent_TriggerRequirementNotMet_ReturnsFalse()
        {
            var chord = new List<GamepadButtons> { GamepadButtons.A };
            var active = new List<GamepadButtons> { GamepadButtons.A };

            var matched = ChordResolver.DoesChordMatchEvent(
                chordButtons: chord,
                requiresRightTrigger: true,
                requiresLeftTrigger: false,
                leftTriggerValue: 0.0f,
                rightTriggerValue: 0.2f,
                triggerMatchThreshold: 0.5f,
                changedButton: GamepadButtons.A,
                activeButtons: active);

            Assert.False(matched);
        }

        [Theory]
        [InlineData(GamepadButtons.A, true)]
        [InlineData(GamepadButtons.B, true)]
        [InlineData(GamepadButtons.X, true)]
        [InlineData(GamepadButtons.Y, true)]
        [InlineData(GamepadButtons.LeftShoulder, false)]
        [InlineData(GamepadButtons.RightShoulder, false)]
        public void IsFaceActionButton_WorksAsExpected(GamepadButtons button, bool expected)
        {
            Assert.Equal(expected, ChordResolver.IsFaceActionButton(button));
        }

        [Fact]
        public void ChordSpecificity_CountsButtonsAndTriggers()
        {
            var chord = new List<GamepadButtons> { GamepadButtons.A, GamepadButtons.B };

            var specificity = ChordResolver.ChordSpecificity(chord, requiresRightTrigger: true, requiresLeftTrigger: false);

            Assert.Equal(3, specificity); // 2 buttons + 1 trigger
        }

        [Fact]
        public void IsOtherChordStrictlyMoreSpecific_ReturnsTrueForProperSupersetWithStrongerTriggerRequirements()
        {
            var candidate = new List<GamepadButtons> { GamepadButtons.A };
            var other = new List<GamepadButtons> { GamepadButtons.A, GamepadButtons.B };

            var result = ChordResolver.IsOtherChordStrictlyMoreSpecific(
                candidateChord: candidate,
                candidateReqRt: false,
                candidateReqLt: false,
                otherChord: other,
                otherReqRt: true,
                otherReqLt: false);

            Assert.True(result);
        }

        [Fact]
        public void IsOtherChordStrictlyMoreSpecific_FalseWhenOtherNotSuperset()
        {
            var candidate = new List<GamepadButtons> { GamepadButtons.A, GamepadButtons.B };
            var other = new List<GamepadButtons> { GamepadButtons.A };

            var result = ChordResolver.IsOtherChordStrictlyMoreSpecific(
                candidateChord: candidate,
                candidateReqRt: false,
                candidateReqLt: false,
                otherChord: other,
                otherReqRt: false,
                otherReqLt: false);

            Assert.False(result);
        }

        [Fact]
        public void IsOtherChordStrictlyMoreSpecific_FalseWhenCandidateRequiresTriggerThatOtherDoesNot()
        {
            var candidate = new List<GamepadButtons> { GamepadButtons.A };
            var other = new List<GamepadButtons> { GamepadButtons.A, GamepadButtons.B };

            var result = ChordResolver.IsOtherChordStrictlyMoreSpecific(
                candidateChord: candidate,
                candidateReqRt: true,
                candidateReqLt: false,
                otherChord: other,
                otherReqRt: false,
                otherReqLt: false);

            Assert.False(result);
        }
    }
}