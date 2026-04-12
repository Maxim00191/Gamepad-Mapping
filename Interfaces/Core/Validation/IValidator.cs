namespace GamepadMapperGUI.Interfaces.Core;

public interface IValidator<in T>
{
    IValidationResult Validate(T instance);
}
