using System;

namespace IthraaSoft.EasyMapper.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class MapToAttribute(params Type[] targetTypes) : Attribute
{
    public Type[] TargetType { get; set; } = targetTypes;
}
