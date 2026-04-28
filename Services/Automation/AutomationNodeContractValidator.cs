#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Utils;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationNodeContractValidator : IAutomationNodeContractValidator
{
    public bool TryValidate(AutomationGraphDocument document, IAutomationExecutionGraphIndex index, out string? detail)
    {
        foreach (var node in document.Nodes)
        {
            switch (node.NodeTypeId)
            {
                case "perception.capture_screen":
                    var captureMode = AutomationNodePropertyReader.ReadString(
                        node.Properties,
                        AutomationNodePropertyKeys.CaptureMode);
                    if (string.Equals(captureMode, "roi", StringComparison.OrdinalIgnoreCase) &&
                        (!AutomationNodePropertyReader.TryReadRoiCapture(node.Properties, out var roi) || roi.IsEmpty))
                    {
                        detail = "capture_screen:roi_missing";
                        return true;
                    }

                    break;
                case "perception.find_image":
                    if (index.GetDataSource(node.Id, "haystack.image") is null)
                    {
                        detail = "find_image:haystack_input_missing";
                        return true;
                    }

                    var algorithmText = AutomationNodePropertyReader.ReadString(
                        node.Properties,
                        AutomationNodePropertyKeys.FindImageAlgorithm);
                    var algorithm = AutomationVisionAlgorithmStorage.ParseFindImageAlgorithmKind(algorithmText);
                    var needlePath = AutomationNodePropertyReader.ReadString(
                        node.Properties,
                        AutomationNodePropertyKeys.FindImageNeedlePath);
                    if (AutomationVisionAlgorithmRequirements.RequiresNeedleImage(algorithm) &&
                        string.IsNullOrWhiteSpace(needlePath))
                    {
                        detail = "find_image:needle_missing";
                        return true;
                    }

                    if (AutomationVisionAlgorithmRequirements.RequiresYoloOnnxModel(algorithm))
                    {
                        var onnxPathRaw = AutomationNodePropertyReader.ReadString(
                            node.Properties,
                            AutomationNodePropertyKeys.FindImageYoloOnnxPath);
                        if (!AutomationYoloOnnxPaths.TryResolveEffectiveModelPath(onnxPathRaw, out _))
                        {
                            detail = "find_image:yolo_model_missing";
                            return true;
                        }
                    }

                    break;
                case "logic.branch_image":
                    if (index.GetDataSource(node.Id, "probe.image") is null)
                    {
                        detail = "branch_image:probe_input_missing";
                        return true;
                    }

                    break;
                case "output.keyboard_key":
                    var keyText = AutomationNodePropertyReader.ReadString(
                        node.Properties,
                        AutomationNodePropertyKeys.KeyboardKey);
                    if (!AutomationKeyboardKeyParser.TryParse(keyText, out _))
                    {
                        detail = "keyboard_key:key_invalid";
                        return true;
                    }

                    break;
                case "output.mouse_click":
                    var useMatch = AutomationNodePropertyReader.ReadBool(
                        node.Properties,
                        AutomationNodePropertyKeys.MouseUseMatchPosition);
                    if (useMatch && index.GetDataSource(node.Id, "probe.image") is null)
                    {
                        detail = "mouse_click:probe_input_missing";
                        return true;
                    }

                    break;
                case "logic.branch_bool":
                    if (index.GetDataSource(node.Id, "condition") is null)
                    {
                        detail = "branch_bool:condition_missing";
                        return true;
                    }

                    break;
                case "variables.set":
                    var variableName = AutomationNodePropertyReader.ReadString(
                        node.Properties,
                        AutomationNodePropertyKeys.VariableName);
                    if (string.IsNullOrWhiteSpace(variableName))
                    {
                        detail = "variables_set:name_missing";
                        return true;
                    }

                    break;
            }
        }

        detail = null;
        return false;
    }
}
