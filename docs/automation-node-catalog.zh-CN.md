# 自动化节点目录

本文档列出了当前已注册的全部自动化节点、用途，以及它们的输入/输出端口。

## 节点参考

| 节点 ID | 显示名称 | 功能 | 输入 | 输出 |
| --- | --- | --- | --- | --- |
| `automation.loop` | 循环 | 重复执行已连接的执行分支。 | `flow.in (Execution，触发循环执行)` | `loop.body (Execution，循环体分支)`，`flow.out (Execution，退出循环后继续)` |
| `perception.capture_screen` | 捕获屏幕 | 抓取像素供下游视觉检测使用。 | `flow.in (Execution，触发截图)` | `flow.out (Execution，继续后续流程)`，`screen.image (ImageOrCoordinates，捕获的画面)` |
| `automation.delay` | 延迟 | 将执行暂停固定时长。 | `flow.in (Execution，启动计时)` | `flow.out (Execution，延迟结束后输出)` |
| `perception.find_image` | 查找图像 | 在底图位图中搜索模板图像。 | `flow.in (Execution，开始查找)`, `haystack.image (ImageOrCoordinates，待搜索源图)` | `flow.out (Execution，查找结束后继续)`，`probe.image (ImageOrCoordinates，匹配区域预览)`，`result.found (Boolean，是否找到匹配)`，`result.x (Number，匹配 X 坐标)`，`result.y (Number，匹配 Y 坐标)`，`result.count (Integer，匹配数量)` |
| `logic.branch_image` | 图像分支 | 根据视觉匹配逻辑路由执行流。 | `flow.in (Execution，触发分支判断)`，`probe.image (ImageOrCoordinates，待判断图像数据)`，`coord.x (Number，可选 X 坐标覆盖)`，`coord.y (Number，可选 Y 坐标覆盖)` | `branch.match (Execution，条件匹配时分支)`，`branch.miss (Execution，条件未命中时分支)` |
| `output.keyboard_key` | 键盘按键 | 通过模拟栈发送一次按键输入。 | `flow.in (Execution，触发按键动作)`，`probe.image (ImageOrCoordinates，可选上下文图像)` | `flow.out (Execution，按键动作后继续)` |
| `output.mouse_click` | 鼠标点击 | 注入一次鼠标按键点击。 | `flow.in (Execution，触发点击动作)`，`probe.image (ImageOrCoordinates，可选上下文图像)`，`coord.x (Number，点击目标 X)`，`coord.y (Number，点击目标 Y)` | `flow.out (Execution，点击后继续)` |
| `math.add` | 加法 | 根据两个数值输入计算结果。 | `left (Number，第一个操作数)`，`right (Number，第二个操作数)` | `value (Number，加法结果)` |
| `math.subtract` | 减法 | 根据两个数值输入计算结果。 | `left (Number，被减数)`，`right (Number，减数)` | `value (Number，减法结果)` |
| `math.multiply` | 乘法 | 根据两个数值输入计算结果。 | `left (Number，第一个因子)`，`right (Number，第二个因子)` | `value (Number，乘法结果)` |
| `math.divide` | 除法 | 根据两个数值输入计算结果。 | `left (Number，被除数)`，`right (Number，除数)` | `value (Number，除法结果)` |
| `logic.gt` | 大于 | 比较两个数值并输出布尔值。 | `left (Number，左操作数)`，`right (Number，右操作数)` | `value (Boolean，left > right 时为 true)` |
| `logic.lt` | 小于 | 比较两个数值并输出布尔值。 | `left (Number，左操作数)`，`right (Number，右操作数)` | `value (Boolean，left < right 时为 true)` |
| `logic.eq` | 等于 | 比较两个数值并输出布尔值。 | `left (Number，左操作数)`，`right (Number，右操作数)` | `value (Boolean，两个操作数相等时为 true)` |
| `logic.and` | 与 (AND) | 对两个布尔值执行逻辑运算。 | `left (Boolean，第一个条件)`，`right (Boolean，第二个条件)` | `value (Boolean，逻辑与结果)` |
| `logic.or` | 或 (OR) | 对两个布尔值执行逻辑运算。 | `left (Boolean，第一个条件)`，`right (Boolean，第二个条件)` | `value (Boolean，逻辑或结果)` |
| `logic.not` | 非 (NOT) | 对单个布尔输入执行取反。 | `input (Boolean，输入条件)` | `value (Boolean，取反后的结果)` |
| `math.random` | 随机数 | 在配置范围内输出随机整数。 | *(无)* | `value (Integer，采样得到的随机值)` |
| `variables.get` | 读取变量 | 从自动化黑板读取一个值。 | *(无)* | `value (Any，读取到的变量值)` |
| `variables.set` | 写入变量 | 将值写入自动化黑板。 | `flow.in (Execution，触发写入)`，`value.number (Number，候选数值)`，`value.bool (Boolean，候选布尔值)`，`value.string (String，候选字符串)` | `flow.out (Execution，写入后继续)` |
| `logic.branch_bool` | 分支（布尔） | 根据布尔条件路由执行流。 | `flow.in (Execution，触发分支判断)`，`condition (Boolean，分支条件)` | `branch.true (Execution，条件为 true 时分支)`，`branch.false (Execution，条件为 false 时分支)` |
| `logic.switch` | 开关分支 | 通过匹配输入值路由执行流。 | `flow.in (Execution，触发开关判断)`，`value (String，用于匹配 case 的输入值)` | `case.match (Execution，命中 case 时分支)`，`case.default (Execution，默认分支)` |
| `logic.loop_control` | 循环控制 | 为当前活动循环请求中断或继续。 | `flow.in (Execution，发送循环控制请求)` | `flow.out (Execution，请求处理后继续)` |
| `debug.log` | 日志 | 将消息写入运行日志。 | `flow.in (Execution，触发日志写入)`，`message (String，日志文本)` | `flow.out (Execution，写入日志后继续)` |
| `control.pid_controller` | PID 控制器 | 根据当前值与目标值计算连续控制信号。 | `current.value (Number，当前测量值)`，`target.value (Number，目标设定值)` | `control.signal (Number，计算得到的控制输出)` |
| `output.key_state` | 按键状态 | 按住、释放或读取键盘按键状态。 | `flow.in (Execution，触发按键状态动作)` | `flow.out (Execution，动作执行后继续)`，`result.pressed (Boolean，当前是否处于按下状态)` |
| `output.human_noise` | 人类噪声 | 对配置的鼠标位移施加人类化噪声并输出移动。 | `flow.in (Execution，触发噪声/移动输出)` | `flow.out (Execution，移动输出后继续)` |

## 类型说明

- `Execution`：可执行节点之间的控制流连接。
- `Number`、`Integer`、`Boolean`、`String`、`Any`：数据值类型。
- `ImageOrCoordinates`：视觉/动作节点使用的图像或坐标载荷类型。
