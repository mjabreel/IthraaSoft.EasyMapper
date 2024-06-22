using System;

namespace IthraaSoft.EasyMapper.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class PropertyNameAttribute (string name) : Attribute
{
    public string Name { get; set; } = name;
}
