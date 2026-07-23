using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BardCycler
{
    public class CyclerEngine : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYUP = 0x0105;
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT { public uint type; public INPUTUNION u; }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT ki; }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        public enum CycleState { Idle, Cycling }

        private CyclerConfig _config;
        private IntPtr _hookId = IntPtr.Zero;
        private readonly LowLevelKeyboardProc _hookProc;
        private CycleState _state = CycleState.Idle;
        private int _cycleIndex = 0;
        private int _originalSlot = 1;
        private int _lastKnownSlot = 1;
        private System.Windows.Forms.Timer _repeatTimer;
        private DateTime _keyDownTime = DateTime.MinValue;
        private bool _hotkeyHeld = false;
        private bool _repeatEngaged = false;

        public event Action<CycleState, int, int> StateChanged;
        public event Action<string> LogMessage;
        public bool IsRunning => _hookId != IntPtr.Zero;
        public CycleState CurrentState => _state;
        public int CurrentSlot => _state == CycleState.Cycling
            ? _config.TargetSlots[_cycleIndex]
            : _lastKnownSlot;

        public CyclerEngine(CyclerConfig config)
        {
            _config = config;
            _hookProc = HookCallback;
            _repeatTimer = new System.Windows.Forms.Timer { Interval = 10 };
            _repeatTimer.Tick += RepeatTick;
        }

        public void UpdateConfig(CyclerConfig c) => _config = c;

        public void Start()
        {
            if (_hookId != IntPtr.Zero) return;
            using (var cur = Process.GetCurrentProcess())
            using (var mod = cur.MainModule)
                _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc,
                    GetModuleHandle(mod.ModuleName), 0);
            if (_hookId == IntPtr.Zero)
                throw new InvalidOperationException($"Hook failed: {Marshal.GetLastWin32Error()}");
            _repeatTimer.Start();
            Log("engine started.");
        }

        public void Stop()
        {
            _repeatTimer.Stop();
            _hotkeyHeld = false;
            _repeatEngaged = false;
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
            _state = CycleState.Idle;
            _cycleIndex = 0;
            Log("engine stopped.");
        }

        public void Dispose() => Stop();

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _config.Enabled)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;

                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                    if (vkCode >= 0x31 && vkCode <= 0x39)
                        _lastKnownSlot = vkCode - 0x30;

                if (!IsGameFocused())
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);

                if (key == _config.CycleHotkey)
                {
                    if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                    {
                        if (!_hotkeyHeld)
                        {
                            _hotkeyHeld = true;
                            _keyDownTime = DateTime.UtcNow;
                            _repeatEngaged = false;
                            AdvanceCycle();
                        }
                        if (_config.ConsumeHotkey) return (IntPtr)1;
                    }
                    else if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                    {
                        _hotkeyHeld = false;
                        _repeatEngaged = false;
                        if (_config.ConsumeHotkey) return (IntPtr)1;
                    }
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void RepeatTick(object sender, EventArgs e)
        {
            if (!_config.AutoRepeatOnHold || !_hotkeyHeld || !_config.Enabled) return;
            var elapsed = (DateTime.UtcNow - _keyDownTime).TotalMilliseconds;
            if (!_repeatEngaged && elapsed >= _config.RepeatDelayMs)
            {
                _repeatEngaged = true;
                _keyDownTime = DateTime.UtcNow;
                AdvanceCycle();
            }
            else if (_repeatEngaged && elapsed >= _config.RepeatRateMs)
            {
                _keyDownTime = DateTime.UtcNow;
                AdvanceCycle();
            }
        }

        private void AdvanceCycle()
        {
            if (_state == CycleState.Idle)
            {
                _originalSlot = _lastKnownSlot;
                _state = CycleState.Cycling;
                _cycleIndex = 0;
                SendSlotKey(_config.TargetSlots[0]);
                Log($"cycle start. original={_originalSlot} -> slot {_config.TargetSlots[0]}");
            }
            else
            {
                _cycleIndex++;
                if (_cycleIndex >= _config.TargetSlots.Count)
                {
                    SendSlotKey(_originalSlot);
                    Log($"cycle complete. -> slot {_originalSlot}. idle.");
                    _state = CycleState.Idle;
                    _cycleIndex = 0;
                }
                else
                {
                    SendSlotKey(_config.TargetSlots[_cycleIndex]);
                    Log($"cycle -> slot {_config.TargetSlots[_cycleIndex]}");
                }
            }
            StateChanged?.Invoke(_state, CurrentSlot, _originalSlot);
        }

        private void SendSlotKey(int slot)
        {
            if (slot < 1 || slot > 9) return;
            ushort vk = (ushort)(0x30 + slot);
            INPUT[] inputs = new INPUT[2];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = vk;
            inputs[0].u.ki.dwFlags = 0;
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].u.ki.wVk = vk;
            inputs[1].u.ki.dwFlags = KEYEVENTF_KEYUP;
            uint sent = SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
            if (sent != 2) Log($"SendInput partial: {sent}/2");
        }

        private bool IsGameFocused()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;
            GetWindowThreadProcessId(hwnd, out uint pid);
            try
            {
                using (var p = Process.GetProcessById((int)pid))
                {
                    string n = p.ProcessName.ToLowerInvariant();
                    return n.Contains("javaw") || n.Contains("java")
                        || n.Contains("minecraft") || n.Contains("lunar");
                }
            }
            catch { return false; }
        }

        private void Log(string msg) => LogMessage?.Invoke(msg);
    }
}
