using System;

namespace IthraaSoft.EasyMapper.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class MappingIgnoreAttribute : Attribute
{
}
