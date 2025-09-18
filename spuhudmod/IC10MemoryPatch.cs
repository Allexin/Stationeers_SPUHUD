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
                
                // Get the chip and device reference (new structure in updated game)
                var chipField = type.GetField("_Chip", BindingFlags.NonPublic | BindingFlags.Instance);
                if (chipField == null)
                {
                    // Try in base type if not found in current type
                    chipField = type.BaseType?.GetField("_Chip", BindingFlags.NonPublic | BindingFlags.Instance);
                }
                
                var deviceRefField = type.GetField("_DeviceRef", BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (chipField == null || deviceRefField == null)
                    return true;
                
                var chip = (ProgrammableChip)chipField.GetValue(instance);
                var deviceRef = deviceRefField.GetValue(instance);
                
                // Get CircuitHousing
                var circuitHousing = chip.ParentSlot?.Parent as CircuitHousing;
                if (circuitHousing == null)
                    return true;
                
                // Get device using the new IDeviceVariable interface
                var getDeviceMethod = deviceRef.GetType().GetMethod("GetDevice", BindingFlags.Public | BindingFlags.Instance);
                if (getDeviceMethod == null)
                    return true;
                
                var device = getDeviceMethod.Invoke(deviceRef, new object[] { circuitHousing }) as ILogicable;
                if (device == null)
                    return true;
                
                // Get device index for substitution system
                int devIndex = GetDeviceIndex(deviceRef, chip, circuitHousing, device);
                
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
        
        // Helper method to get device index from various device variable types
        private static int GetDeviceIndex(object deviceRef, ProgrammableChip chip, CircuitHousing circuitHousing, ILogicable device)
        {
            try
            {
                var deviceRefType = deviceRef.GetType();
                
                // For DirectDeviceVariable
                if (deviceRefType.Name == "DirectDeviceVariable")
                {
                    var getVariableValueMethod = deviceRefType.GetMethod("GetVariableValue", BindingFlags.Public | BindingFlags.Instance);
                    if (getVariableValueMethod != null)
                    {
                        // Get the AliasTarget.Register enum value
                        var aliasTargetType = typeof(ProgrammableChip).GetNestedType("_AliasTarget", BindingFlags.NonPublic);
                        if (aliasTargetType != null)
                        {
                            var registerValue = Enum.Parse(aliasTargetType, "Register");
                            var deviceId = (int)getVariableValueMethod.Invoke(deviceRef, new object[] { registerValue });
                            // Find device index by ID
                            return FindDeviceIndexById(circuitHousing, deviceId);
                        }
                    }
                }
                
                // For DeviceAliasVariable or DeviceIndexVariable
                var getVariableIndexMethod = deviceRefType.GetMethod("GetVariableIndex", BindingFlags.Public | BindingFlags.Instance);
                if (getVariableIndexMethod != null)
                {
                    var aliasTargetType = typeof(ProgrammableChip).GetNestedType("_AliasTarget", BindingFlags.NonPublic);
                    if (aliasTargetType != null)
                    {
                        var deviceValue = Enum.Parse(aliasTargetType, "Device");
                        var deviceIndex = (int)getVariableIndexMethod.Invoke(deviceRef, new object[] { deviceValue, false });
                        return deviceIndex;
                    }
                }
                
                // Fallback: search by device reference
                return FindDeviceIndexByReference(circuitHousing, device);
            }
            catch
            {
                // Fallback: search by device reference
                return FindDeviceIndexByReference(circuitHousing, device);
            }
        }
        
        // Helper method to find device index by ID
        private static int FindDeviceIndexById(CircuitHousing circuitHousing, int deviceId)
        {
            try
            {
                var device = circuitHousing.GetLogicableFromId(deviceId);
                if (device != null)
                {
                    // Find which slot this device is in
                    for (int slot = 0; slot < 6; slot++)
                    {
                        var slotDevice = circuitHousing.GetLogicableFromIndex(slot);
                        if (slotDevice == device)
                            return slot;
                    }
                }
            }
            catch { }
            return 0; // Default to slot 0
        }
        
        // Helper method to find device index by reference
        private static int FindDeviceIndexByReference(CircuitHousing circuitHousing, ILogicable device)
        {
            try
            {
                for (int i = 0; i < 6; i++) // Stationeers has max 6 device slots
                {
                    var slotDevice = circuitHousing.GetLogicableFromIndex(i);
                    if (slotDevice == device)
                        return i;
                }
            }
            catch { }
            return 0; // Default to slot 0
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

