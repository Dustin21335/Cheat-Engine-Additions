using CESDK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CheatEngineAdditions
{
    public class CheatEngineAdditions : CESDKPluginClass
    {
        public override string GetPluginName()
        {
            return "Cheat Engine Additions";
        }

        public override bool EnablePlugin()
        {
            sdk.lua.Register("GetRelativeAddress", (IntPtr luaState) =>
            {
                sdk.lua.GetGlobal("getOpenedProcessID");
                sdk.lua.Call(luaState, 0, 1);
                int pid = (int)sdk.lua.ToInteger(luaState, -1);
                sdk.lua.Pop(luaState, 1);
                if (pid == 0)
                {
                    sdk.lua.PushString(luaState, "0x0");
                    sdk.lua.DoString(@"showMessage('Not attached to a process! Failed to get relative address')");
                    return 0;
                }
                Process process = Process.GetProcessById(pid);
                if (process == null)
                {
                    sdk.lua.PushString(luaState, "0x0");
                    sdk.lua.DoString(@"showMessage('Process is null! Failed to get relative address')");
                    return 0;
                }
                long address = sdk.lua.ToInteger(luaState, 1);
                ProcessModule processModule = process.Modules.Cast<ProcessModule>().FirstOrDefault(m => address >= m.BaseAddress.ToInt64() && address < m.BaseAddress.ToInt64() + m.ModuleMemorySize);
                if (processModule == null)
                {
                    sdk.lua.PushString(luaState, "0x0");
                    sdk.lua.DoString(@"showMessage('Process Module is null! Failed to get relative address')");
                    return 0;
                }
                sdk.lua.PushString(luaState, $"0x{(address - processModule.BaseAddress.ToInt64()).ToString("X")}");
                return 1;
            });
            sdk.lua.Register("GenerateSignature", (IntPtr luaState) =>
            {
                sdk.lua.GetGlobal("getOpenedProcessID");
                sdk.lua.Call(luaState, 0, 1);
                int pid = (int)sdk.lua.ToInteger(luaState, -1);
                sdk.lua.Pop(luaState, 1);
                if (pid == 0)
                {
                    sdk.lua.PushString(luaState, "");
                    sdk.lua.DoString(@"showMessage('Not attached to a process! Failed to get relative address')");
                    return 0;
                }
                long address = sdk.lua.ToInteger(luaState, 1);
                long length = sdk.lua.ToInteger(luaState, 2);
                if (length <= 0)
                {
                    sdk.lua.PushString(luaState, "");
                    sdk.lua.DoString(@"showMessage('Invalid length for a signature!')");
                    return 0;
                }
                int intLength = (int)length;
                sdk.lua.GetGlobal("ReadBytes");
                sdk.lua.PushInteger(luaState, address);
                sdk.lua.PushInteger(luaState, length);
                sdk.lua.Call(luaState, 2, intLength);
                byte[] bytes = new byte[length];
                for (int i = 0; i < length; i++) bytes[i] = (byte)sdk.lua.ToInteger(luaState, -(intLength) + i);
                sdk.lua.Pop(luaState, (int)length);
                List<string> signature = new List<string>();
                for (int i = 0; i < bytes.Length; i++)
                {
                    byte b = bytes[i];
                    if (b == 0x00 || b == 0x90)
                    {
                        signature.Add("??");
                        continue;
                    }
                    if ((b == 0xE8 || b == 0xE9) && i + 4 < bytes.Length)
                    {
                        signature.Add(b.ToString("X2"));
                        signature.Add("??");
                        signature.Add("??");
                        signature.Add("??");
                        signature.Add("??");
                        i += 4;
                        continue;
                    }
                    signature.Add(b.ToString("X2"));
                }
                sdk.lua.PushString(luaState, string.Join(" ", signature));
                return 1;
            });
            sdk.lua.Register("RefineSignature", (IntPtr luaState) =>
            {
                long address = sdk.lua.ToInteger(luaState, 1);
                string oldSignature = sdk.lua.ToString(luaState, 2);
                if (string.IsNullOrEmpty(oldSignature))
                {
                    sdk.lua.PushString(luaState, "");
                    sdk.lua.DoString(@"showMessage('Old Signature is empty or null!')");
                    return 0;
                }
                string[] oldSignatureBytes = oldSignature.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
                sdk.lua.GetGlobal("GenerateSignature");
                sdk.lua.PushInteger(luaState, address);
                sdk.lua.PushInteger(luaState, oldSignatureBytes.Length);
                sdk.lua.Call(luaState, 2, 1);
                string newSignature = sdk.lua.ToString(luaState, -1);
                sdk.lua.Pop(luaState, 1);
                if (string.IsNullOrEmpty(newSignature))
                {
                    sdk.lua.PushString(luaState, "");
                    sdk.lua.DoString(@"showMessage('New Signature is empty or null!')");
                    return 0;
                }
                string[] newSignatureBytes = newSignature.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
                string[] refinedSignature = oldSignatureBytes.Zip(newSignatureBytes, (oldByte, newByte) => oldByte == "??" || newByte == "??" || !oldByte.Equals(newByte, StringComparison.OrdinalIgnoreCase) ? "??" : oldByte).ToArray();
                sdk.lua.PushString(luaState, string.Join(" ", refinedSignature));
                return 1;
            });
            sdk.lua.Register("ClearAddressesByFilter", (IntPtr luaState) =>
            {
                string type = sdk.lua.ToString(luaState, 1);
                sdk.lua.DoString($@"
                    local type = '{type}'
                    local foundList = getMainForm().FoundList3
                    local menuItemRemove
                    for i = 0, foundList.PopupMenu.Items.Count - 1 do
                        if foundList.PopupMenu.Items[i].Name == ""Removeselectedaddresses1"" then
                            menuItemRemove = foundList.PopupMenu.Items[i]
                            break
                        end
                    end
                    local addressesToRemove = {{}}
                    for i = foundList.Items.Count - 1, 0, -1 do
                        local item = foundList.Items[i]   
                        if item then
                            local value = tonumber(item.SubItems[0])
                            local select = false
                            if type == ""invalid"" and (not value or value ~= value or string.match(item.SubItems[0], ""[eE]"")) then
                                select = true
                            elseif value then
                                if type == ""positive"" and value > 0 then
                                    select = true
                                elseif type == ""negative"" and value < 0 then
                                    select = true
                                elseif type == ""zero"" and value == 0 then
                                    select = true
                                elseif filterType == 'decimals' and value % 1 ~= 0 then
                                    select = true
                                elseif filterType == 'nodecimals' and value % 1 == 0 then
                                    select = true
                                end
                            end
                            if select then
                                table.insert(addressesToRemove, i)
                            end
                        end
                    end
                    for _, i in ipairs(addressesToRemove) do
                        foundList.Items[i].Selected = true
                    end
                    menuItemRemove.DoClick()
                ");
                return 1;
            });
            sdk.lua.DoString(@"
                local menu = MainForm.Menu
                local cheatEngineAdditionsTab = createMenuItem(menu)
                cheatEngineAdditionsTab.Caption='Cheat Engine Additions'
                menu.Items.insert(MainForm.miHelp.MenuIndex + 1, cheatEngineAdditionsTab)
                local memoryView = getMemoryViewForm()
                local disassemblerView = memoryView.DisassemblerView
                local popupMenu = disassemblerView.PopupMenu

                function copyRelativeAddress()
                    local relativeAddress = GetRelativeAddress(disassemblerView.SelectedAddress)
                    if relativeAddress and relativeAddress ~= ""0x0"" then
                        writeToClipboard(relativeAddress)
                        showMessage(""Relative Address copied to clipboard "" .. relativeAddress)
                    end
                end

                local copyRelativeAddressMenuItem = createMenuItem(popup)
                copyRelativeAddressMenuItem.Caption = ""Copy Relative Address (Ctrl + Q)""
                copyRelativeAddressMenuItem.OnClick = function(menuItem)
                    copyRelativeAddress()
                end

                createHotkey(function()
                    copyRelativeAddress()  
                end, { VK_CONTROL, VK_Q })

                function generateSignature()
                    local signatureLength = inputQuery(""Signature Length"", ""What do you want the length of the signature to be?"", ""10"") 
                    if signatureLength then
                        local signature = GenerateSignature(disassemblerView.SelectedAddress, signatureLength)
                        if signature ~= nil and signature ~= """" then
                            writeToClipboard(signature)
                            showMessage(""Signature copied to clipboard "" .. signature)
                        end
                    end
                end 

                local generateSignatureMenuItem = createMenuItem(popup)
                generateSignatureMenuItem.Caption = ""Generate Signature (Ctrl + E)""
                generateSignatureMenuItem.OnClick = function(menuItem)
                    generateSignature()
                end

                createHotkey(function()
                    generateSignature()  
                end, { VK_CONTROL, VK_E })

                function refineSignature()
                    local oldSignature = inputQuery(""Old Signature"", ""Please enter the old signature to refine"", """")
                    if oldSignature then
                        local refinedSignature = RefineSignature(disassemblerView.SelectedAddress, oldSignature)
                        if refinedSignature ~= nil and refinedSignature ~= """" then
                            writeToClipboard(refinedSignature)
                            showMessage(""Refined Signature copied to clipboard "" .. refinedSignature)
                        end
                    end
                end

                local refineSignatureMenuItem = createMenuItem(popup)
                refineSignatureMenuItem.Caption = ""Refine Signature (Ctrl + F)""
                refineSignatureMenuItem.OnClick = function(menuItem)
                    refineSignature()
                end

                createHotkey(function()
                    refineSignature()  
                end, { VK_CONTROL, VK_F })

                local insertIndex = 0
                for i = 0, popupMenu.Items.Count - 1 do
                    if popupMenu.Items[i].Caption == ""Copy to clipboard"" then
                        insertIndex = i
                        break
                    end
                end

                popupMenu.Items.insert(insertIndex, copyRelativeAddressMenuItem) 
                popupMenu.Items.insert(insertIndex, refineSignatureMenuItem)
                popupMenu.Items.insert(insertIndex, generateSignatureMenuItem)
                
                local clearPositiveNumbersMenuItem = createMenuItem(menu)
                clearPositiveNumbersMenuItem.Caption='Clear Positive Numbers';
                clearPositiveNumbersMenuItem.OnClick=function(menuItem)
                    ClearAddressesByFilter(""positive"")
                end
                cheatEngineAdditionsTab.add(clearPositiveNumbersMenuItem)

                local clearNegativeNumbersMenuItem = createMenuItem(menu)
                clearNegativeNumbersMenuItem.Caption='Clear Negative Numbers';
                clearNegativeNumbersMenuItem.OnClick=function(menuItem)
                    ClearAddressesByFilter(""negative"")
                end
                cheatEngineAdditionsTab.add(clearNegativeNumbersMenuItem)

                local clearZerosMenuItem = createMenuItem(menu)
                clearZerosMenuItem.Caption='Clear Zeros';
                clearZerosMenuItem.OnClick=function(menuItem)
                    ClearAddressesByFilter(""zero"")
                end
                cheatEngineAdditionsTab.add(clearZerosMenuItem)

                local clearInvalidsMenuItem = createMenuItem(menu)
                clearInvalidsMenuItem.Caption='Clear Invalid Numbers';
                clearInvalidsMenuItem.OnClick=function(menuItem)
                    ClearAddressesByFilter(""invalid"")
                end
                cheatEngineAdditionsTab.add(clearInvalidsMenuItem)

                local clearDeciminalNumbersMenuItem = createMenuItem(menu)
                clearDeciminalNumbersMenuItem.Caption='Clear Deciminal Numbers';
                clearDeciminalNumbersMenuItem.OnClick=function(menuItem)
                    ClearAddressesByFilter(""nodeciminal"")
                end
                cheatEngineAdditionsTab.add(clearDeciminalNumbersMenuItem)

                local clearNonDeciminalNumbersMenuItem = createMenuItem(menu)
                clearNonDeciminalNumbersMenuItem.Caption='Clear Non Deciminal Numbers';
                clearNonDeciminalNumbersMenuItem.OnClick=function(menuItem)
                    ClearAddressesByFilter(""deciminal"")
                end
                cheatEngineAdditionsTab.add(clearNonDeciminalNumbersMenuItem)
            ");
            return true;
        }

        public override bool DisablePlugin()
        {

            return true;
        }
    }
}
