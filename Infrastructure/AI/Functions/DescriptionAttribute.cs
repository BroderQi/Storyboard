using System.ComponentModel;

namespace Storyboard.AI.Functions;

/// <summary>
/// DescriptionAttribute - 用于标记函数和参数的描述
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter)]
public class DescriptionAttribute : Attribute
{
    public string Description { get; }

    public DescriptionAttribute(string description)
    {
        Description = description;
    }
}
