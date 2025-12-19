using CESDK;
using CheatEngineAdditions.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CheatEngineAdditions
{
    public class CheatEngineAdditions : CESDKPluginClass
    {
        public override string GetPluginName()
        {
            return "Cheat Engine Additions";
        }

        private string? tempPath;

        public string TempPath
        {
            get
            {
                if (string.IsNullOrEmpty(tempPath)) tempPath = Path.Combine(Path.GetTempPath(), "CheatEngineAdditions");
                if (!Directory.Exists(tempPath)) Directory.CreateDirectory(tempPath);
                return tempPath ?? string.Empty;
            }
        }

        public string Version = "1.0.1";

        public override bool EnablePlugin()
        {
            try
            {
                sdk.lua.Register("RegisterKeybind", (IntPtr luaState) =>
                {
                    string keybindName = sdk.lua.ToString(luaState, 1);
                    if (string.IsNullOrEmpty(keybindName))
                    {
                        sdk.lua.DoString(@"showMessage('Keybind name is null! Failed to register keybind')");
                        return 0;
                    }
                    List<int> keys = new List<int>();
                    for (int i = 1; i <= sdk.lua.ObjLen(luaState, 2); i++)
                    {
                        sdk.lua.PushInteger(luaState, i);
                        sdk.lua.GetTable(luaState, 2);
                        keys.Add((int)sdk.lua.ToInteger(luaState, -1));
                        sdk.lua.Pop(luaState, 1);
                    }
                    if (keys.Count == 0)
                    {
                        sdk.lua.DoString(@"showMessage('No keys provided! Failed to register keybind')");
                        return 0;
                    }
                    if (!sdk.lua.IsFunction(luaState, 3))
                    {
                        sdk.lua.DoString(@"showMessage('No function provided! Failed to register keybind')");
                        return 0;
                    }
                    sdk.lua.PushValue(luaState, 3);
                    sdk.lua.SetGlobal(keybindName);
                    Action callback = () =>
                    {
                        sdk.lua.GetGlobal(keybindName);
                        sdk.lua.Call(0, 0);
                    };
                    string focusedTabCondition = sdk.lua.ToString(luaState, 4) ?? "";
                    KeyboardUtil.RegisterKeybind(keybindName, keys, callback, focusedTabCondition);
                    return 1;
                });
                sdk.lua.Register("UnregisterKeybind", (IntPtr luaState) =>
                {
                    string keybindName = sdk.lua.ToString(luaState, 1);
                    if (string.IsNullOrEmpty(keybindName))
                    {
                        sdk.lua.DoString(@"showMessage('Keybind name is null! Failed to unregister keybind')");
                        return 0;
                    }
                    sdk.lua.PushNil(luaState);
                    sdk.lua.SetGlobal(keybindName);
                    KeyboardUtil.UnregisterKeybind(keybindName);
                    return 1;
                });
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
                        sdk.lua.DoString(@"showMessage('Not attached to a process! Failed to generate signature')");
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
                sdk.lua.Register("SaveProcessIcon", (IntPtr luaState) =>
                {
                    int width = (int)sdk.lua.ToInteger(luaState, 1);
                    int height = (int)sdk.lua.ToInteger(luaState, 2);
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
                        sdk.lua.PushString(luaState, "");
                        sdk.lua.DoString(@"showMessage('Process is null! Failed to get process icon')");
                        return 0;
                    }
                    string? exePath = process.MainModule?.FileName ?? string.Empty;
                    if (string.IsNullOrEmpty(exePath))
                    {
                        sdk.lua.PushString(luaState, "");
                        sdk.lua.DoString(@"showMessage('Process path is null! Failed to get process icon')");
                        return 0;
                    }
                    Icon icon = Icon.ExtractAssociatedIcon(exePath);
                    if (icon == null)
                    {
                        sdk.lua.PushString(luaState, "");
                        sdk.lua.DoString(@"showMessage('Icon is null! Failed to get process icon')");
                        return 0;
                    }
                    Bitmap bitMap = icon.ToBitmap();
                    int targetWidth = Math.Min(width > 0 ? width : bitMap.Width, 23150);
                    int targetHeight = Math.Min(height > 0 ? height : bitMap.Height, 23150);
                    if (bitMap.Width != targetWidth || bitMap.Height != targetHeight) bitMap = new Bitmap(bitMap, new Size(targetWidth, targetHeight));
                    string imagePath = Path.Combine(TempPath, $"{process.ProcessName}.png");
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        bitMap.Save(memoryStream, ImageFormat.Png);
                        File.WriteAllBytes(imagePath, memoryStream.ToArray());
                        sdk.lua.PushString(luaState, imagePath);
                    }
                    return 1;
                });
                sdk.lua.Register("OpenTempFolder", (IntPtr luaState) =>
                {
                    Process.Start("explorer.exe", TempPath);
                    return 1;
                });
                sdk.lua.Register("GetVersion", (IntPtr luaState) =>
                {
                    sdk.lua.PushString(luaState, Version);
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

                    RegisterKeybind(""CopyRelativeAddressKeybind"", { VK_CONTROL, VK_Q }, function()
                        copyRelativeAddress()  
                    end, ""Memory Viewer"")

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

                    RegisterKeybind(""GenerateSignatureKeybind"", { VK_CONTROL, VK_E }, function()
                        generateSignature()  
                    end, ""Memory Viewer"")

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

                    RegisterKeybind(""RefineSignatureKeybind"", { VK_CONTROL, VK_F }, function()
                        refineSignature()  
                    end, ""Memory Viewer"")

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

                    local versionMenuItem = createMenuItem(popup)
                    versionMenuItem.Caption = ""Version "" .. GetVersion()
                    versionMenuItem.Enabled = false
                    cheatEngineAdditionsTab.add(versionMenuItem)
                
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

                    local saveProcessIconMenuItem = createMenuItem(menu)
                    saveProcessIconMenuItem.Caption='Save Process Icon';
                    saveProcessIconMenuItem.OnClick=function(menuItem)
                        if not IsAttached() then
                            showMessage('Not attached to a process! Failed to get process icon')
                            return
                        end
                        local width = 0
                        local height = 0    
                        local response = inputQuery(""Question"", ""Do you want to change the default icon size? Click OK or (y/n) "", """")
                        if response and (response == """" or response == ""y"" or response == ""yes"") then
                            width = inputQuery(""Width"", ""Please enter the width you want the icon to be"", """")
                            height = inputQuery(""Height"", ""Please enter the height you want the icon to be"", """")
                        end
                        SaveProcessIcon(width, height)
                        OpenTempFolder()
                    end
                    cheatEngineAdditionsTab.add(saveProcessIconMenuItem)
                ");
            }
            catch (Exception ex)
            {
                MessageBox.Show($@"
                    Exception Type:
                    {ex.GetType().FullName}
                    Message: 
                    {ex.Message}
                    StackTrace:
                    {ex.StackTrace}
                ", "Cheat Engine Additions Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }

        public override bool DisablePlugin()
        {
            return true;
        }
    }
}