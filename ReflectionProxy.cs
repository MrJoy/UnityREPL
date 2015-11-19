//-----------------------------------------------------------------
// Base class for utility classes to make reflection less cumbersome to use.
//-----------------------------------------------------------------
using System;
using System.Reflection;

internal class ReflectionProxy {
  internal const BindingFlags PUBLIC_STATIC = BindingFlags.Public | BindingFlags.Static;
  internal const BindingFlags NONPUBLIC_STATIC = BindingFlags.NonPublic | BindingFlags.Static;

  // Turn one or more `Type`s into an array.
  protected static Type[] Signature(params Type[] sig) {
    return sig;
  }
}
