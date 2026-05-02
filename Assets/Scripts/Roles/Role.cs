using System;
using System.Runtime.CompilerServices;
using UnityEngine;

public class Role
{
   public RoleType RoleType;
}

public enum RoleType
{
    None,
    Catcher,
    Switcher
}
