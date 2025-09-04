using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Pipes;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace spuhudmod
{
    public static class IC10MemoryPatch
    {
        // Storage for temporary device substitutions
        private static Dictionary<int, ILogicable> _deviceSubstitutions = new Dictionary<int, ILogicable>();
        
        // Flag to indicate when substitution is active
        public static bool _substitutionActive = false;
        
        // Helper method for both PUT and GET operations
        public static bool HandleMemoryOperationPrefix(object instance, int index, ref int result, System.Type operationType)
        {
            try
            {
                var type = instance.GetType();
                
                // Get the chip and device index
                var chipField = type.BaseType.GetField("_Chip", BindingFlags.NonPublic | BindingFlags.Instance);
                var deviceIndexField = type.GetField("_DeviceIndex", BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (chipField == null || deviceIndexField == null)
                    return true;
                
                var chip = (ProgrammableChip)chipField.GetValue(instance);
                var deviceIndexVar = deviceIndexField.GetValue(instance);
                
                // Get device index using alias
                var aliasField = deviceIndexVar.GetType().GetField("_Alias", BindingFlags.NonPublic | BindingFlags.Instance);
                var deviceIndexField2 = deviceIndexVar.GetType().GetField("_DeviceIndex", BindingFlags.NonPublic | BindingFlags.Instance);
                
                string alias = aliasField?.GetValue(deviceIndexVar) as string;
                int directIndex = (int)(deviceIndexField2?.GetValue(deviceIndexVar) ?? -1);
                
                int devIndex;
                if (!string.IsNullOrEmpty(alias))
                {
                    // Check if it's a direct device reference like d0, d1, d5 etc.
                    if (alias.StartsWith("d") && directIndex >= 0)
                    {
                        devIndex = directIndex;
                    }
                    else
                    {
                        // Find device by alias
                        var aliasesField = typeof(ProgrammableChip).GetField("_Aliases", BindingFlags.NonPublic | BindingFlags.Instance);
                        var aliases = aliasesField?.GetValue(chip) as System.Collections.IDictionary;
                        
                        if (aliases != null && aliases.Contains(alias))
                        {
                            var aliasValue = aliases[alias];
                            var targetField = aliasValue.GetType().GetField("Target", BindingFlags.Public | BindingFlags.Instance);
                            var indexField = aliasValue.GetType().GetField("Index", BindingFlags.Public | BindingFlags.Instance);
                            
                            if (targetField != null && indexField != null)
                            {
                                var target = targetField.GetValue(aliasValue);
                                devIndex = (int)indexField.GetValue(aliasValue);
                            }
                            else
                            {
                                return true;
                            }
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
                else if (directIndex >= 0)
                {
                    devIndex = directIndex;
                }
                else
                {
                    return true;
                }
                
                // Get CircuitHousing and device
                var circuitHousing = chip.ParentSlot?.Parent as CircuitHousing;
                if (circuitHousing == null)
                    return true;
                
                var device = circuitHousing.GetLogicableFromIndex(devIndex);                
                
                // Check if it's a LogicTransmitter in passive mode
                if (device is LogicTransmitter transmitter && !transmitter.IsActiveTransmitter)
                {
                    var currentDevice = transmitter.CurrentDevice;
                    bool isMemoryOperation = (operationType.Name.Contains("PUT") && currentDevice is IMemoryWritable) ||
                                           (operationType.Name.Contains("GET") && currentDevice is IMemoryReadable);
                    
                    if (currentDevice != null && isMemoryOperation)
                    {
                        // Activate substitution system
                        _substitutionActive = true;
                        SetDeviceSubstitution(devIndex, currentDevice);
                        
                        // Let original method run with substitution in place
                        // The CircuitHousing patch will handle the substitution
                        return true; 
                    }
                }
                
                return true; // Use original method for normal devices
            }
            catch (Exception ex)
            {
                return true;
            }
        }
        
        // Public methods to manage device substitutions
        public static void SetDeviceSubstitution(int deviceIndex, ILogicable device)
        {
            _deviceSubstitutions[deviceIndex] = device;
        }
        
        public static void RemoveDeviceSubstitution(int deviceIndex)
        {
            _deviceSubstitutions.Remove(deviceIndex);
        }
        
        public static ILogicable GetDeviceSubstitution(int deviceIndex)
        {
            return _deviceSubstitutions.ContainsKey(deviceIndex) ? _deviceSubstitutions[deviceIndex] : null;
        }
        
        public static void ClearAllSubstitutions()
        {
            _deviceSubstitutions.Clear();
            _substitutionActive = false;
        }
    }
    
    [HarmonyPatch(typeof(CircuitHousing), "GetLogicableFromIndex", new Type[] { typeof(int), typeof(int) })]
    public class CircuitHousingPatch
    {
        [HarmonyPrefix]
        public static bool GetLogicableFromIndexPrefix(CircuitHousing __instance, int deviceIndex, int networkIndex, ref ILogicable __result)
        {
            // Only check substitutions when substitution system is active
            if (IC10MemoryPatch._substitutionActive)
            {
                var substitution = IC10MemoryPatch.GetDeviceSubstitution(deviceIndex);
                if (substitution != null)
                {
                    __result = substitution;
                    return false; // Skip original method
                }
            }

            return true; // Use original method
        }
    }
    
    [HarmonyPatch]
    public class IC10PutMemoryPatch
    {
        [HarmonyPatch]
        [HarmonyPrefix] 
        public static bool PutOperationExecutePrefix(object __instance, int index, ref int __result)
        {
            var putType = typeof(ProgrammableChip).GetNestedType("_PUT_Operation", BindingFlags.NonPublic);
            return IC10MemoryPatch.HandleMemoryOperationPrefix(__instance, index, ref __result, putType);
        }
        
        [HarmonyPatch]
        [HarmonyPostfix]
        public static void PutOperationExecutePostfix(object __instance)
        {
            IC10MemoryPatch.ClearAllSubstitutions();
        }
        
        static MethodBase TargetMethod()
        {
            var putOperationType = typeof(ProgrammableChip).GetNestedType("_PUT_Operation", BindingFlags.NonPublic);
            return putOperationType?.GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance);
        }
    }
    
    [HarmonyPatch]
    public class IC10GetMemoryPatch
    {
        [HarmonyPatch]
        [HarmonyPrefix]
        public static bool GetOperationExecutePrefix(object __instance, int index, ref int __result)
        {
            var getType = typeof(ProgrammableChip).GetNestedType("_GET_Operation", BindingFlags.NonPublic);
            return IC10MemoryPatch.HandleMemoryOperationPrefix(__instance, index, ref __result, getType);
        }
        
        [HarmonyPatch]
        [HarmonyPostfix]
        public static void GetOperationExecutePostfix(object __instance)
        {
            IC10MemoryPatch.ClearAllSubstitutions();
        }
        
        static MethodBase TargetMethod()
        {
            var getOperationType = typeof(ProgrammableChip).GetNestedType("_GET_Operation", BindingFlags.NonPublic);
            return getOperationType?.GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance);
        }
    }
}

