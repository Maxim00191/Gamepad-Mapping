# Automation Node Catalog

This document lists all currently registered automation nodes, their purpose, and their input/output ports.

## Node Reference

| Node ID | Display Name | Function | Inputs | Outputs |
| --- | --- | --- | --- | --- |
| `automation.loop` | Loop | Repeats the connected execution branch. | `flow.in (Execution, triggers loop)` | `loop.body (Execution, repeated body branch)`, `flow.out (Execution, exits loop)` |
| `perception.capture_screen` | Capture screen | Grabs pixels for downstream vision checks. | `flow.in (Execution, triggers capture)` | `flow.out (Execution, continues pipeline)`, `screen.image (ImageOrCoordinates, captured frame)` |
| `automation.delay` | Delay | Pauses execution for a fixed duration. | `flow.in (Execution, starts timer)` | `flow.out (Execution, emitted after delay)` |
| `perception.find_image` | Find image | Looks for a template inside the haystack bitmap. | `flow.in (Execution, starts search)`, `haystack.image (ImageOrCoordinates, source image to inspect)` | `flow.out (Execution, continues after search)`, `probe.image (ImageOrCoordinates, matched region preview)`, `result.found (Boolean, whether match exists)`, `result.x (Number, match X coordinate)`, `result.y (Number, match Y coordinate)`, `result.count (Integer, number of matches)` |
| `logic.branch_image` | Branch on image | Routes execution depending on visual match logic. | `flow.in (Execution, starts branching)`, `probe.image (ImageOrCoordinates, image payload to evaluate)`, `coord.x (Number, optional X override)`, `coord.y (Number, optional Y override)` | `branch.match (Execution, path when image condition passes)`, `branch.miss (Execution, path when image condition fails)` |
| `output.keyboard_key` | Keyboard key | Sends a keystroke via the emulation stack (supports per-node input mode override selected from picker options). | `flow.in (Execution, triggers key action)`, `probe.image (ImageOrCoordinates, optional context image)` | `flow.out (Execution, emitted after key action)` |
| `output.mouse_click` | Mouse click | Injects a mouse button click (supports per-node input mode override selected from picker options). | `flow.in (Execution, triggers mouse click)`, `probe.image (ImageOrCoordinates, optional context image)`, `coord.x (Number, click target X)`, `coord.y (Number, click target Y)` | `flow.out (Execution, emitted after click)` |
| `math.add` | Add | Calculates a value from two numeric inputs. | `left (Number, first operand)`, `right (Number, second operand)` | `value (Number, sum result)` |
| `math.subtract` | Subtract | Calculates a value from two numeric inputs. | `left (Number, minuend)`, `right (Number, subtrahend)` | `value (Number, subtraction result)` |
| `math.multiply` | Multiply | Calculates a value from two numeric inputs. | `left (Number, first factor)`, `right (Number, second factor)` | `value (Number, product result)` |
| `math.divide` | Divide | Calculates a value from two numeric inputs. | `left (Number, dividend)`, `right (Number, divisor)` | `value (Number, quotient result)` |
| `logic.gt` | Greater Than | Compares two numeric values and outputs a boolean. | `left (Number, left operand)`, `right (Number, right operand)` | `value (Boolean, true when left > right)` |
| `logic.lt` | Less Than | Compares two numeric values and outputs a boolean. | `left (Number, left operand)`, `right (Number, right operand)` | `value (Boolean, true when left < right)` |
| `logic.eq` | Equals | Compares two numeric values and outputs a boolean. | `left (Number, left operand)`, `right (Number, right operand)` | `value (Boolean, true when operands are equal)` |
| `logic.and` | AND | Combines two booleans with a logical operation. | `left (Boolean, first condition)`, `right (Boolean, second condition)` | `value (Boolean, logical AND result)` |
| `logic.or` | OR | Combines two booleans with a logical operation. | `left (Boolean, first condition)`, `right (Boolean, second condition)` | `value (Boolean, logical OR result)` |
| `logic.not` | NOT | Transforms one boolean input. | `input (Boolean, source condition)` | `value (Boolean, inverted condition)` |
| `math.random` | Random | Outputs a random integer in configured range. | *(none)* | `value (Integer, sampled random value)` |
| `variables.get` | Get Variable | Reads a value from the automation blackboard. | *(none)* | `value (Any, retrieved variable value)` |
| `variables.set` | Set Variable | Writes a value to the automation blackboard. | `flow.in (Execution, triggers write)`, `value.number (Number, numeric candidate value)`, `value.bool (Boolean, boolean candidate value)`, `value.string (String, string candidate value)` | `flow.out (Execution, emitted after write)` |
| `logic.branch_bool` | Branch (Bool) | Routes execution by boolean condition. | `flow.in (Execution, starts branching)`, `condition (Boolean, branch condition)` | `branch.true (Execution, path when condition is true)`, `branch.false (Execution, path when condition is false)` |
| `logic.branch_compare` | Compare Branch | Routes execution by comparing two numeric values. | `flow.in (Execution, starts comparison)`, `left (Number, left operand)`, `right (Number, right operand)` | `branch.true (Execution, path when comparison is true)`, `branch.false (Execution, path when comparison is false)` |
| `logic.switch` | Switch | Routes execution by matching an input value. | `flow.in (Execution, starts switch evaluation)`, `value (String, value matched against cases)` | `case.match (Execution, path when a case matches)`, `case.default (Execution, fallback path)` |
| `logic.loop_control` | Loop Control | Requests break or continue for active loop. | `flow.in (Execution, sends loop-control request)` | `flow.out (Execution, continues after handling request)` |
| `logic.loop_jump` | Loop Jump | Jumps execution back to a named loop start (optional alternative to wiring the loop body back to the loop’s `flow.in`). | `flow.in (Execution, performs jump)` | *(none — continues at the target loop node)* |
| `debug.log` | Log | Writes a message to run log. | `flow.in (Execution, triggers log write)`, `message (String, log text)` | `flow.out (Execution, emitted after logging)` |
| `control.pid_controller` | PID Controller | Computes a continuous control signal from current and target values. | `current.value (Number, measured process value)`, `target.value (Number, desired setpoint)` | `control.signal (Number, computed control output)` |
| `output.key_state` | Key State | Holds, releases, or inspects keyboard key state. | `flow.in (Execution, triggers key-state action)` | `flow.out (Execution, emitted after action)`, `result.pressed (Boolean, current pressed state)` |
| `output.human_noise` | Human Noise | Applies human-like noise to a configured mouse delta and emits movement. | `flow.in (Execution, triggers noise/movement output)` | `flow.out (Execution, emitted after movement)` |
| `automation.macro` | Macro | Executes a referenced sub-graph and then resumes outer flow. | `flow.in (Execution, enters macro)` | `flow.out (Execution, resumes parent graph)` |
| `event.listener` | Event Listener | Trigger node that starts a flow branch when a matching signal is published. | *(none)* | `flow.out (Execution, starts listener branch)` |
| `event.emit` | Emit Event | Publishes a named signal on the automation event bus. | `flow.in (Execution, publishes signal)` | `flow.out (Execution, continues after publish)` |

## Type Legend

- `Execution`: control-flow connection between runnable nodes.
- `Number`, `Integer`, `Boolean`, `String`, `Any`: data values.
- `ImageOrCoordinates`: image or coordinate payload used by vision/action nodes.
