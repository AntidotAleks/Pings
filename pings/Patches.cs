using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
// ReSharper disable InconsistentNaming

namespace pings
{
    public static class Patches
    { internal static readonly string[] names = {"Pings.colorDefault", "Pings.colorOwn"}; }
    
    [HarmonyPatch(typeof(InputField), "SetText")]
    public static class InputField_SetText_Patch
    {
        public static void Prefix(InputField __instance, ref InputField.OnValidateInput __state)
        {
            if (!Patches.names.Contains(__instance.transform.parent.name)) return;
            __state = __instance.onValidateInput;
            __instance.onValidateInput = null;
        }
        
        public static void Postfix(InputField __instance, InputField.OnValidateInput __state)
        {
            if (__state == null) return;
            __instance.onValidateInput = __state;
        }
    }

    // // Save the input on Esc
    // [HarmonyPatch(typeof(InputField), nameof(InputField.DeactivateInputField))]
    // public static class InputField_DeactivateInputField_Patch
    // {
    //     public static void Prefix(InputField __instance)
    //     {
    //         if (!Patches.names.Contains(__instance.transform.parent.name)) return;
    //         var wasCanceledField = AccessTools.Field(typeof(InputField), "m_WasCanceled");
    //         if (wasCanceledField != null) wasCanceledField.SetValue(__instance, false);
    //
    //     }
    // }
}