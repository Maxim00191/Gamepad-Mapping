# Emulation Backend Parity Checklist

This document tracks the expected behavior and known differences between the Win32 (`SendInput`) and Injection (`InputInjector`) backends.

## Overview

| Feature | Win32 (SendInput) | Injection (InputInjector) |
|---------|-------------------|---------------------------|
| **Primary Mechanism** | ScanCodes (mapped from VK) | VirtualKeys (UWP/WinRT API) |
| **Compatibility** | High (works with most games/apps) | Medium (may be blocked by anti-cheats) |
| **Privileges** | Standard User (usually) | Requires UI Access / Trusted Process |
| **Unicode Support** | Native (`KEYEVENTF_UNICODE`) | Limited (requires manual mapping) |

## Keyboard Parity

| Operation | Win32 Behavior | Injection Behavior |
|-----------|----------------|--------------------|
| **Key Down/Up** | Sends ScanCode by default. | Sends VirtualKey. |
| **Modifiers** | Handled via sequential VK/ScanCode sends. | Handled via sequential VK sends. |
| **Chords** | Synchronized via `_chordSequenceGate`. | Sequential sends (no explicit gate yet). |
| **Extended Keys** | Uses `KEYEVENTF_EXTENDEDKEY` flag. | Uses `InjectedInputKeyOptions.ExtendedKey`. |
| **Tap Timing** | Clamped 20ms-50ms (default 30ms). | Default 30ms (not clamped). |

## Mouse Parity

| Operation | Win32 Behavior | Injection Behavior |
|-----------|----------------|--------------------|
| **Buttons (L/R/M)** | `MOUSEEVENTF_*DOWN/UP` | `InjectedInputMouseOptions.*Down/Up` |
| **X1/X2 Buttons** | `MOUSEEVENTF_XDOWN/UP` with data. | `InjectedInputMouseOptions.XDown/Up` with data. |
| **Wheel** | `MOUSEEVENTF_WHEEL` (Delta 120). | `InjectedInputMouseOptions.Wheel` (Delta 120). |
| **Movement** | `MOUSEEVENTF_MOVE` (Relative). | `InjectedInputMouseOptions.Move` (Relative). |
| **Click Timing** | 30ms hold. | 30ms hold. |

## Known Inconsistencies

1. **Unicode/Text:** Win32 supports full Unicode text injection. Injection backend currently has a `no-op` for `SendText`.
2. **Timing:** Win32 `TapKey` clamps the hold duration to a safe range (20-50ms) to ensure games register the hit. Injection does not yet enforce this clamping.
3. **Error Handling:** Both backends now log warnings and `no-op` on invalid keys/state instead of throwing exceptions.
